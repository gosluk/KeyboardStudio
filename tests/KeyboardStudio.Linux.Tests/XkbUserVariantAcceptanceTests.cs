using System.Xml.Linq;
using KeyboardStudio.Build;
using KeyboardStudio.Core;
using KeyboardStudio.Linux;
using KeyboardStudio.Persistence;
using Xunit;

namespace KeyboardStudio.Linux.Tests;

public sealed class XkbUserVariantAcceptanceTests
{
    private const string InstallationId = "7c31d5f2a19e40a4b0ef64f01a295135";

    [Fact]
    [Trait("Category", "Integration")]
    public async Task PolishQwertz_EditSaveInstallVerifyUpdateAndUninstall_PreservesEverythingUnowned()
    {
        var source = VendoredXkbFixture.CreateSource();
        var descriptor = (await source.ListAsync()).Single(item =>
            item.LayoutId == "pl" && item.VariantId == "qwertz");
        var imported = await source.ImportAsync(descriptor.ToReference(), LayoutImportOptions.Default);
        Assert.True(imported.Success);

        var baseline = imported.Project!.Layout.Mappings
            .Select(mapping => KeyMappingSnapshot.From(
                mapping,
                !imported.Report.Diagnostics.Any(diagnostic =>
                    string.Equals(diagnostic.KeyId, mapping.KeyId, StringComparison.Ordinal))))
            .ToArray();
        Assert.True(baseline.Single(mapping => mapping.KeyId == "KeyA").IsSafeToOverride);
        imported.Project.Layout.Find("KeyA")!.Outputs[ModifierLayer.Default] = new CharacterOutput("x");

        var derivation = new LayoutDerivation(
            InstallationId,
            descriptor.SourceId,
            descriptor.Origin,
            descriptor.LayoutId,
            descriptor.VariantId,
            imported.ResolvedSectionId!,
            new DateTimeOffset(2026, 8, 30, 10, 0, 0, TimeSpan.Zero),
            imported.Report.Fidelity,
            baseline);
        var document = new KeyboardProjectDocument(
            imported.Project,
            new Dictionary<string, ProjectTargetProfile>(StringComparer.Ordinal),
            LayoutDerivation: derivation);
        await using var stream = new MemoryStream();
        var store = new JsonKeyboardProjectDocumentStore();
        await store.SaveAsync(document, stream);
        stream.Position = 0;
        var reopened = await store.LoadAsync(stream);

        var metadata = Metadata(reopened.LayoutDerivation!);
        var firstBundle = Generate(reopened, metadata, "x");
        var central = firstBundle.Files.Single(file => file.RelativePath == "symbols/keyboardstudio").Content;
        Assert.Contains("include \"%S/pl(qwertz)\"", central, StringComparison.Ordinal);
        Assert.Equal(1, Count(central, "key <AC01>"));

        using var scope = new TemporaryXdgScope();
        var originalBridge = "// user bridge\nxkb_symbols \"mine\" { key <AB01> { [ z ] }; };\n";
        var originalRegistry = """
            <?xml version="1.0" encoding="UTF-8"?>
            <xkbConfigRegistry>
              <layoutList />
              <unknown owner="user" />
            </xkbConfigRegistry>
            """ + "\n";
        await scope.SeedAsync(originalBridge, originalRegistry);
        var systemBytes = await File.ReadAllBytesAsync(
            Path.Combine(VendoredXkbFixture.Root, "symbols", "pl"));
        var verifier = new StructuralAcceptanceVerifier();
        var service = new XkbUserInstallService(verifier);

        var install = await service.InstallOrUpdateAsync(
            firstBundle,
            metadata,
            scope.Paths,
            Capability(scope.Paths));

        Assert.True(install.Success, Describe(install));
        Assert.Equal(XkbUserInstallCommand.Install, install.Command);
        AssertRegistryPlacement(scope.Paths, "pl", "keyboardstudio_programmer");
        AssertUnrelatedContent(scope.Paths, originalBridge, "owner=\"user\"");

        var verified = await service.VerifyInstalledAsync(
            InstallationId,
            scope.Paths,
            Capability(scope.Paths));
        Assert.True(verified.Success, Describe(verified));

        reopened.Project.Layout.Find("KeyA")!.Outputs[ModifierLayer.Default] = new CharacterOutput("y");
        var updateBundle = Generate(reopened, metadata, "y");
        var update = await service.InstallOrUpdateAsync(
            updateBundle,
            metadata,
            scope.Paths,
            Capability(scope.Paths));

        Assert.True(update.Success, Describe(update));
        Assert.Equal(XkbUserInstallCommand.Update, update.Command);
        Assert.Contains("symbols[Group1] = [ y, A, U00E6, U00C6 ]", await File.ReadAllTextAsync(
            Path.Combine(scope.Paths.UserXkbRoot, "symbols", "keyboardstudio")), StringComparison.Ordinal);
        AssertUnrelatedContent(scope.Paths, originalBridge, "owner=\"user\"");

        var uninstall = await service.UninstallAsync(
            InstallationId,
            scope.Paths,
            Capability(scope.Paths));

        Assert.True(uninstall.Success, Describe(uninstall));
        Assert.Empty(uninstall.Manifest!.Installations);
        Assert.False(File.Exists(Path.Combine(
            scope.Paths.UserXkbRoot,
            "symbols",
            "keyboardstudio")));
        Assert.Equal(originalBridge, await File.ReadAllTextAsync(
            Path.Combine(scope.Paths.UserXkbRoot, "symbols", "pl")));
        Assert.Equal(originalRegistry, await File.ReadAllTextAsync(
            Path.Combine(scope.Paths.UserXkbRoot, "rules", "evdev.xml")));
        Assert.Equal(systemBytes, await File.ReadAllBytesAsync(
            Path.Combine(VendoredXkbFixture.Root, "symbols", "pl")));
        Assert.Equal(5, verifier.VariantVerificationCount);
        Assert.Equal(2, verifier.BaseVerificationCount);
        Assert.All(verifier.Roots, root => Assert.StartsWith(scope.Root, root, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "XkbIntegration")]
    public async Task PinnedPolishAndAlbanianBundles_CompileCustomBaseAndUnrelatedVariants()
    {
        var executable = OperatingSystem.IsLinux() ? new PathXkbCliLocator().Find() : null;
        if (executable is null)
        {
            if (string.Equals(
                    Environment.GetEnvironmentVariable("CI"),
                    "true",
                    StringComparison.OrdinalIgnoreCase))
            {
                Assert.Fail("xkbcli is required for XkbIntegration tests in Linux CI.");
            }

            return;
        }

        var polish = await TranslateFixtureAsync(
            "pl",
            "qwertz",
            "41d39c0672d349fdbe43b197e1739f61",
            "keyboardstudio_programmer");
        var albanian = await TranslateFixtureAsync(
            "al",
            null,
            "ee4be3816eab4711a3df33c9d094933d",
            "keyboardstudio_programmer");
        var bundle = XkbUserBundleGenerator.Generate([polish, albanian]);
        Assert.True(bundle.Success);
        var output = Directory.CreateTempSubdirectory("keyboardstudio-user-xkb-compile");
        try
        {
            var write = await new XkbUserBundleWriter().WriteAsync(bundle.Bundle!, output.FullName);
            var capability = new XkbUserInstallCapability(
                XkbUserInstallMode.ManagedInstallation,
                XkbSessionType.Wayland,
                write.BundleRoot,
                Path.Combine(output.FullName, "state"),
                PathsAreSafe: true,
                executable,
                "xkbcli integration test",
                new Version(1, 13, 0),
                MeetsRecommendedVersion: true,
                VendoredXkbFixture.Root,
                XkbRegistryDiscoverySupport.Available,
                []);
            var verifier = new XkbUserBundleVerifier(
                new ProcessRunner(),
                new XkbRulesRegistryReader(new HostXkbFileSystem()));

            var result = await verifier.VerifyAsync(
                write.BundleRoot,
                [polish.Metadata, albanian.Metadata],
                capability);

            Assert.Equal(XkbUserBundleVerificationStatus.Verified, result.Status);
            Assert.Equal(8, result.Checks.Count);
            Assert.All(result.Checks, check => Assert.True(
                check.Success,
                $"{check.Kind} {check.LayoutId}({check.VariantId}): {check.StandardError}"));
        }
        finally
        {
            output.Delete(recursive: true);
        }
    }

    private static async Task<XkbUserVariantLayout> TranslateFixtureAsync(
        string layoutId,
        string? variantId,
        string installationId,
        string publicVariantId)
    {
        var source = VendoredXkbFixture.CreateSource();
        var descriptor = (await source.ListAsync()).Single(item =>
            item.LayoutId == layoutId && item.VariantId == variantId);
        var imported = await source.ImportAsync(descriptor.ToReference(), LayoutImportOptions.Default);
        Assert.True(imported.Success);
        var baseline = imported.Project!.Layout.Mappings
            .Select(mapping => KeyMappingSnapshot.From(
                mapping,
                !imported.Report.Diagnostics.Any(diagnostic =>
                    string.Equals(diagnostic.KeyId, mapping.KeyId, StringComparison.Ordinal))))
            .ToArray();
        imported.Project.Layout.Find("KeyA")!.Outputs[ModifierLayer.Default] = new CharacterOutput("x");
        var metadata = new XkbUserVariantMetadata(
            installationId,
            layoutId,
            variantId,
            imported.ResolvedSectionId!,
            publicVariantId,
            $"{descriptor.DisplayName} - KeyboardStudio");

        var translation = new XkbUserVariantTranslator().Translate(
            imported.Project,
            baseline,
            metadata);
        Assert.True(translation.Success);
        Assert.Single(translation.Layout!.Mappings);
        return translation.Layout;
    }

    private static XkbGeneratedUserBundle Generate(
        KeyboardProjectDocument document,
        XkbUserVariantMetadata metadata,
        string expectedDefault)
    {
        var translation = new XkbUserVariantTranslator().Translate(
            document.Project,
            document.LayoutDerivation!.BaselineMappings,
            metadata);
        Assert.True(translation.Success);
        var mapping = Assert.Single(translation.Layout!.Mappings);
        Assert.Equal("<AC01>", mapping.KeyName);
        Assert.Equal([expectedDefault, "A", "U00E6", "U00C6"], mapping.Keysyms);

        var generation = XkbUserBundleGenerator.Generate([translation.Layout]);
        Assert.True(generation.Success);
        return generation.Bundle!;
    }

    private static XkbUserVariantMetadata Metadata(LayoutDerivation derivation) => new(
        derivation.ProjectInstallationId,
        derivation.BaseLayoutId,
        derivation.BaseVariantId,
        derivation.ResolvedBaseSectionId,
        "keyboardstudio_programmer",
        "Polish - KeyboardStudio");

    private static XkbUserInstallCapability Capability(XdgDirectoryPaths paths) => new(
        XkbUserInstallMode.ManagedInstallation,
        XkbSessionType.Wayland,
        paths.UserXkbRoot,
        paths.KeyboardStudioStateRoot,
        PathsAreSafe: true,
        "/usr/bin/xkbcli",
        "xkbcli 1.13.1",
        new Version(1, 13, 1),
        MeetsRecommendedVersion: true,
        VendoredXkbFixture.Root,
        XkbRegistryDiscoverySupport.Available,
        []);

    private static void AssertRegistryPlacement(
        XdgDirectoryPaths paths,
        string layoutId,
        string variantId)
    {
        var document = XDocument.Load(Path.Combine(paths.UserXkbRoot, "rules", "evdev.xml"));
        var layout = document.Descendants("layout").Single(element =>
            string.Equals(
                element.Element("configItem")?.Element("name")?.Value,
                layoutId,
                StringComparison.Ordinal));
        Assert.Contains(layout.Descendants("variant"), variant =>
            string.Equals(
                variant.Element("configItem")?.Element("name")?.Value,
                variantId,
                StringComparison.Ordinal));
    }

    private static void AssertUnrelatedContent(
        XdgDirectoryPaths paths,
        string bridge,
        string registryFragment)
    {
        Assert.Contains(bridge, File.ReadAllText(
            Path.Combine(paths.UserXkbRoot, "symbols", "pl")), StringComparison.Ordinal);
        Assert.Contains(registryFragment, File.ReadAllText(
            Path.Combine(paths.UserXkbRoot, "rules", "evdev.xml")), StringComparison.Ordinal);
    }

    private static int Count(string value, string fragment) =>
        value.Split(fragment, StringSplitOptions.None).Length - 1;

    private static string Describe(XkbUserInstallResult result) =>
        string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic =>
            $"{diagnostic.Code}: {diagnostic.Message}"));

    private sealed class StructuralAcceptanceVerifier : IXkbUserBundleVerifier
    {
        public int VariantVerificationCount { get; private set; }

        public int BaseVerificationCount { get; private set; }

        public List<string> Roots { get; } = [];

        public Task<XkbUserBundleVerificationResult> VerifyAsync(
            string bundleRoot,
            IReadOnlyList<XkbUserVariantMetadata> variants,
            XkbUserInstallCapability capability,
            bool requireBundleManifest = true,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            VariantVerificationCount++;
            Roots.Add(bundleRoot);
            Assert.NotEmpty(variants);
            Assert.All(variants, variant =>
            {
                var central = File.ReadAllText(Path.Combine(bundleRoot, "symbols", "keyboardstudio"));
                Assert.Contains(
                    $"include \"%S/{variant.BaseLayoutId}({variant.ResolvedBaseSectionId})\"",
                    central,
                    StringComparison.Ordinal);
                Assert.True(File.Exists(Path.Combine(bundleRoot, "symbols", variant.BaseLayoutId)));
                AssertRegistryPlacement(
                    new XdgDirectoryPaths(string.Empty, string.Empty, bundleRoot, string.Empty),
                    variant.BaseLayoutId,
                    variant.PublicVariantId);
            });

            var checks = variants.SelectMany(variant => new[]
            {
                Check(XkbUserBundleVerificationCheckKind.CustomVariant, variant.BaseLayoutId, variant.PublicVariantId),
                Check(XkbUserBundleVerificationCheckKind.BaseVariant, variant.BaseLayoutId, variant.BaseVariantId),
                Check(XkbUserBundleVerificationCheckKind.UnrelatedVariant, variant.BaseLayoutId, "dvorak"),
                Check(XkbUserBundleVerificationCheckKind.RegistryDiscovery, variant.BaseLayoutId, variant.PublicVariantId)
            }).ToArray();
            return Task.FromResult(Success(checks));
        }

        public Task<XkbUserBundleVerificationResult> VerifyBaseAsync(
            string bundleRoot,
            XkbUserVariantMetadata removedVariant,
            XkbUserInstallCapability capability,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            BaseVerificationCount++;
            Roots.Add(bundleRoot);
            return Task.FromResult(Success(
            [
                Check(
                    XkbUserBundleVerificationCheckKind.BaseVariant,
                    removedVariant.BaseLayoutId,
                    removedVariant.BaseVariantId),
                Check(
                    XkbUserBundleVerificationCheckKind.UnrelatedVariant,
                    removedVariant.BaseLayoutId,
                    "dvorak")
            ]));
        }

        private static XkbUserBundleVerificationCheck Check(
            XkbUserBundleVerificationCheckKind kind,
            string layoutId,
            string? variantId) =>
            new(kind, layoutId, variantId, Success: true, [], 0, string.Empty, string.Empty);

        private static XkbUserBundleVerificationResult Success(
            IReadOnlyList<XkbUserBundleVerificationCheck> checks) =>
            new(
                XkbUserBundleVerificationStatus.Verified,
                "/usr/bin/xkbcli",
                "xkbcli 1.13.1",
                checks,
                []);
    }

    private sealed class TemporaryXdgScope : IDisposable
    {
        public TemporaryXdgScope()
        {
            Root = Path.Combine(Path.GetTempPath(), $"keyboardstudio-acceptance-{Guid.NewGuid():N}");
            Paths = new XdgDirectoryPaths(
                Path.Combine(Root, "config"),
                Path.Combine(Root, "state"),
                Path.Combine(Root, "config", "xkb"),
                Path.Combine(Root, "state", "keyboardstudio", "xkb"));
        }

        public string Root { get; }

        public XdgDirectoryPaths Paths { get; }

        public async Task SeedAsync(string bridge, string registry)
        {
            Directory.CreateDirectory(Path.Combine(Paths.UserXkbRoot, "symbols"));
            Directory.CreateDirectory(Path.Combine(Paths.UserXkbRoot, "rules"));
            await File.WriteAllTextAsync(Path.Combine(Paths.UserXkbRoot, "symbols", "pl"), bridge);
            await File.WriteAllTextAsync(
                Path.Combine(Paths.UserXkbRoot, "rules", "evdev.xml"), registry);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
