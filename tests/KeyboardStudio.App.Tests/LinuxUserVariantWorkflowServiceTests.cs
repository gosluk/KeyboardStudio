using KeyboardStudio.App;
using KeyboardStudio.Core;
using KeyboardStudio.Linux;
using KeyboardStudio.Persistence;
using Xunit;

namespace KeyboardStudio.App.Tests;

public sealed class LinuxUserVariantWorkflowServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task InspectAsync_SystemDerivedEdit_PreparesChangedKeysOnlyBundleAndManagedInstall()
    {
        using var scope = new TemporaryScope();
        var (project, derivation) = DerivedProject();
        var service = CreateService(scope, ManagedCapability(scope));

        var result = await service.InspectAsync(
            project,
            derivation,
            "keyboardstudio_programmer",
            "Polish - KeyboardStudio");

        Assert.Equal(LinuxUserVariantStatus.NotInstalled, result.Status);
        Assert.True(result.CanGenerate);
        Assert.True(result.CanManage);
        Assert.Equal("pl", result.Metadata!.BaseLayoutId);
        Assert.Equal("qwertz", result.Metadata.BaseVariantId);
        var central = result.Bundle!.Find("symbols/keyboardstudio")!.Content;
        Assert.Contains("include \"%S/pl(qwertz)\"", central, StringComparison.Ordinal);
        Assert.Contains("symbols[Group1] = [ x, X ]", central, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task InspectAsync_InvalidIdentifier_ReturnsValidationWithoutBundle()
    {
        using var scope = new TemporaryScope();
        var (project, derivation) = DerivedProject();

        var result = await CreateService(scope, ManagedCapability(scope)).InspectAsync(
            project,
            derivation,
            "Bad Variant",
            "Polish - KeyboardStudio");

        Assert.Equal(LinuxUserVariantStatus.Unavailable, result.Status);
        Assert.Null(result.Bundle);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "KSW002");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task InspectAsync_X11Capability_KeepsBundleExportAndDisablesLiveManagement()
    {
        using var scope = new TemporaryScope();
        var (project, derivation) = DerivedProject();
        var capability = ManagedCapability(scope) with
        {
            Mode = XkbUserInstallMode.ExportOnly,
            SessionType = XkbSessionType.X11
        };

        var result = await CreateService(scope, capability).InspectAsync(
            project, derivation, null, null);

        Assert.Equal(LinuxUserVariantStatus.ExportOnly, result.Status);
        Assert.True(result.CanGenerate);
        Assert.False(result.CanManage);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task InspectAsync_MissingSystemBase_ReportsBaseUnavailableButKeepsExport()
    {
        using var scope = new TemporaryScope();
        var (project, derivation) = DerivedProject();
        var capability = ManagedCapability(scope) with
        {
            Mode = XkbUserInstallMode.ExportOnly,
            CanonicalSystemRoot = null
        };

        var result = await CreateService(scope, capability).InspectAsync(
            project, derivation, null, null);

        Assert.Equal(LinuxUserVariantStatus.BaseUnavailable, result.Status);
        Assert.True(result.CanGenerate);
        Assert.False(result.CanManage);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public async Task InspectAsync_MalformedHostManifest_ReportsBrokenWithoutOverwritingIt()
    {
        using var scope = new TemporaryScope();
        var (project, derivation) = DerivedProject();
        Directory.CreateDirectory(scope.Paths.KeyboardStudioStateRoot);
        var manifestPath = Path.Combine(scope.Paths.KeyboardStudioStateRoot, "installations.json");
        await File.WriteAllTextAsync(manifestPath, "not json");

        var result = await CreateService(scope, ManagedCapability(scope)).InspectAsync(
            project, derivation, null, null);

        Assert.Equal(LinuxUserVariantStatus.Broken, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "KSW004");
        Assert.Equal("not json", await File.ReadAllTextAsync(manifestPath));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public async Task InspectAsync_PublicVariantOwnedByAnotherProject_ReportsCollision()
    {
        using var scope = new TemporaryScope();
        var (project, derivation) = DerivedProject();
        Directory.CreateDirectory(scope.Paths.KeyboardStudioStateRoot);
        var hash = new string('a', 64);
        var manifest = new XkbInstallationManifest(
            XkbInstallationManifest.CurrentSchemaVersion,
            [new XkbInstalledVariant(
                "8d42e6a3b20f41b5c1f075a12b306246",
                "pl",
                "dvorak",
                "dvorak",
                "keyboardstudio_programmer",
                "ks_8d42e6a3b20f",
                "Other project",
                hash,
                hash,
                hash,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                "1.13.1")],
            []);
        await File.WriteAllTextAsync(
            Path.Combine(scope.Paths.KeyboardStudioStateRoot, "installations.json"),
            XkbInstallationManifestSerializer.Serialize(manifest));

        var result = await CreateService(scope, ManagedCapability(scope)).InspectAsync(
            project,
            derivation,
            "keyboardstudio_programmer",
            "Polish - KeyboardStudio");

        Assert.Equal(LinuxUserVariantStatus.Unavailable, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "KSW005");
    }

    private static LinuxUserVariantWorkflowService CreateService(
        TemporaryScope scope,
        XkbUserInstallCapability capability)
    {
        var environment = new StaticEnvironment(new Dictionary<string, string?>
        {
            ["HOME"] = scope.Root,
            ["XDG_CONFIG_HOME"] = scope.Paths.ConfigHome,
            ["XDG_STATE_HOME"] = scope.Paths.StateHome
        });
        return new LinuxUserVariantWorkflowService(
            new StaticCapabilityProbe(capability),
            new XdgDirectoryResolver(environment),
            new XkbUserVariantTranslator(),
            new XkbUserBundleWriter(),
            new NoOpInstallService());
    }

    private static XkbUserInstallCapability ManagedCapability(TemporaryScope scope) => new(
        XkbUserInstallMode.ManagedInstallation,
        XkbSessionType.Wayland,
        scope.Paths.UserXkbRoot,
        scope.Paths.KeyboardStudioStateRoot,
        PathsAreSafe: true,
        "/usr/bin/xkbcli",
        "xkbcli 1.13.1",
        new Version(1, 13, 1),
        MeetsRecommendedVersion: true,
        "/usr/share/X11/xkb",
        XkbRegistryDiscoverySupport.Available,
        []);

    private static (KeyboardProject Project, LayoutDerivation Derivation) DerivedProject()
    {
        var baselineMapping = Mapping("a", "A");
        var project = new KeyboardProject
        {
            Metadata = new ProjectMetadata { Name = "Polish" },
            Keyboard = new PhysicalKeyboard
            {
                Id = "iso-105",
                Keys = [new PhysicalKey { Id = "KeyA", ScanCode = 0x1E }]
            },
            Layout = new KeyboardLayout { Mappings = [Mapping("x", "X")] }
        };
        var derivation = new LayoutDerivation(
            "7c31d5f2a19e40a4b0ef64f01a295135",
            "system",
            LayoutSourceOrigin.System,
            "pl",
            "qwertz",
            "qwertz",
            DateTimeOffset.UtcNow,
            LayoutImportFidelity.Exact,
            [KeyMappingSnapshot.From(baselineMapping, true)]);
        return (project, derivation);
    }

    private static KeyMapping Mapping(string normal, string shifted) => new()
    {
        KeyId = "KeyA",
        LogicalKey = LogicalKey.A,
        Outputs =
        {
            [ModifierLayer.Default] = new CharacterOutput(normal),
            [ModifierLayer.Shift] = new CharacterOutput(shifted)
        }
    };

    private sealed class TemporaryScope : IDisposable
    {
        public TemporaryScope()
        {
            Root = Path.Combine(Path.GetTempPath(), $"keyboardstudio-workflow-{Guid.NewGuid():N}");
            Paths = new XdgDirectoryPaths(
                Path.Combine(Root, "config"),
                Path.Combine(Root, "state"),
                Path.Combine(Root, "config", "xkb"),
                Path.Combine(Root, "state", "keyboardstudio", "xkb"));
        }

        public string Root { get; }

        public XdgDirectoryPaths Paths { get; }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private sealed class StaticEnvironment(IReadOnlyDictionary<string, string?> values)
        : IXkbEnvironment
    {
        public string? GetVariable(string name) =>
            values.TryGetValue(name, out var value) ? value : null;
    }

    private sealed class StaticCapabilityProbe(XkbUserInstallCapability capability)
        : IXkbUserInstallCapabilityProbe
    {
        public Task<XkbUserInstallCapability> ProbeAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(capability);
    }

    private sealed class NoOpInstallService : IXkbUserInstallService
    {
        public Task<XkbUserInstallResult> InstallOrUpdateAsync(
            XkbGeneratedUserBundle bundle,
            XkbUserVariantMetadata metadata,
            XdgDirectoryPaths paths,
            XkbUserInstallCapability capability,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<XkbUserInstallResult> VerifyInstalledAsync(
            string projectInstallationId,
            XdgDirectoryPaths paths,
            XkbUserInstallCapability capability,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<XkbUserInstallResult> UninstallAsync(
            string projectInstallationId,
            XdgDirectoryPaths paths,
            XkbUserInstallCapability capability,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<XkbUserRecoveryResult> RecoverAsync(
            XdgDirectoryPaths paths,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
