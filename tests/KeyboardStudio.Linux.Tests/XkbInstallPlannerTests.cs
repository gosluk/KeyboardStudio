using KeyboardStudio.Linux;
using Xunit;

namespace KeyboardStudio.Linux.Tests;

public sealed class XkbInstallPlannerTests
{
    private static readonly DateTimeOffset VerifiedAt =
        new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    [Trait("Category", "Unit")]
    public void PlanInstall_IntoAbsentRoot_CreatesOnlyTheThreeManagedDestinations()
    {
        var metadata = Polish();

        var result = XkbInstallPlanner.PlanInstall(
            Bundle(metadata),
            metadata,
            Paths(),
            XkbInstallationManifest.Empty,
            [],
            VerifiedAt,
            "xkbcli 1.13.1");

        Assert.True(result.Success);
        Assert.Equal(XkbInstallAction.Install, result.Plan!.Action);
        Assert.Equal(3, result.Plan.Operations.Count);
        Assert.All(result.Plan.Operations, operation =>
            Assert.Equal(XkbInstallOperationKind.Create, operation.Kind));
        Assert.Equal(
            ["rules/evdev.xml", "symbols/keyboardstudio", "symbols/pl"],
            result.Plan.Operations.Select(operation => operation.RelativePath).Order(StringComparer.Ordinal));
        Assert.Single(result.Plan.UpdatedManifest.Installations);
        Assert.Equal(3, result.Plan.UpdatedManifest.Files.Count);
        Assert.Equal(
            "/home/test/.local/state/keyboardstudio/xkb/installations.json",
            result.Plan.ManifestPath);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void PlanInstall_WithHandWrittenBridgeAndRegistry_PreservesUnrelatedContent()
    {
        var metadata = Polish();
        const string bridge = "// mine\nxkb_symbols \"personal\" { };\n";
        const string registry = """
            <xkbConfigRegistry version="1.1">
              <layoutList><layout><configItem><name>us</name></configItem></layout></layoutList>
              <unknown keep="yes" />
            </xkbConfigRegistry>
            """;

        var result = XkbInstallPlanner.PlanInstall(
            Bundle(metadata),
            metadata,
            Paths(),
            XkbInstallationManifest.Empty,
            [Snapshot("symbols/pl", bridge), Snapshot("rules/evdev.xml", registry)],
            VerifiedAt,
            "xkbcli 1.13.1");

        Assert.True(result.Success);
        var bridgeOperation = Assert.Single(result.Plan!.Operations, operation =>
            operation.RelativePath == "symbols/pl");
        Assert.Equal(XkbInstallOperationKind.Replace, bridgeOperation.Kind);
        Assert.StartsWith(bridge, bridgeOperation.Content);
        var registryOperation = Assert.Single(result.Plan.Operations, operation =>
            operation.RelativePath == "rules/evdev.xml");
        Assert.Contains("<unknown keep=\"yes\"", registryOperation.Content);
        Assert.False(result.Plan.UpdatedManifest.Files.Single(file =>
            file.RelativePath == "symbols/pl").WasCreatedByKeyboardStudio);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void PlanInstallAndUninstall_SeveralProjects_PreservesOtherProjectThenDeletesLastOwnedFiles()
    {
        var paths = Paths();
        var firstMetadata = Polish();
        var first = XkbInstallPlanner.PlanInstall(
            Bundle(firstMetadata),
            firstMetadata,
            paths,
            XkbInstallationManifest.Empty,
            [],
            VerifiedAt,
            "1.13.1").Plan!;
        var current = Apply([], first.Operations);

        var secondMetadata = PolishDvorak();
        var second = XkbInstallPlanner.PlanInstall(
            Bundle(secondMetadata),
            secondMetadata,
            paths,
            first.UpdatedManifest,
            current,
            VerifiedAt.AddMinutes(1),
            "1.13.1").Plan!;
        current = Apply(current, second.Operations);

        Assert.Equal(2, second.UpdatedManifest.Installations.Count);
        Assert.Contains(firstMetadata.ProjectInstallationId, Content(current, "symbols/keyboardstudio"));
        Assert.Contains(secondMetadata.ProjectInstallationId, Content(current, "symbols/keyboardstudio"));

        var removeFirst = XkbInstallPlanner.PlanUninstall(
            firstMetadata.ProjectInstallationId,
            paths,
            second.UpdatedManifest,
            current).Plan!;
        current = Apply(current, removeFirst.Operations);

        Assert.Single(removeFirst.UpdatedManifest.Installations);
        Assert.DoesNotContain(firstMetadata.ProjectInstallationId, Content(current, "symbols/pl"));
        Assert.Contains(secondMetadata.ProjectInstallationId, Content(current, "symbols/pl"));

        var removeLast = XkbInstallPlanner.PlanUninstall(
            secondMetadata.ProjectInstallationId,
            paths,
            removeFirst.UpdatedManifest,
            current).Plan!;

        Assert.Empty(removeLast.UpdatedManifest.Installations);
        Assert.Equal(3, removeLast.Operations.Count);
        Assert.All(removeLast.Operations, operation =>
            Assert.Equal(XkbInstallOperationKind.Delete, operation.Kind));
        Assert.Empty(removeLast.UpdatedManifest.Files);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void PlanUninstall_AfterUnrelatedSharedEdits_PreservesThoseEdits()
    {
        var metadata = Polish();
        var install = XkbInstallPlanner.PlanInstall(
            Bundle(metadata), metadata, Paths(), XkbInstallationManifest.Empty, [], VerifiedAt, "1.13.1").Plan!;
        var current = Apply([], install.Operations).ToList();
        ReplaceSnapshot(
            current,
            "symbols/pl",
            Content(current, "symbols/pl") + "\n// user addition\nxkb_symbols \"mine\" { };\n");
        ReplaceSnapshot(
            current,
            "rules/evdev.xml",
            Content(current, "rules/evdev.xml").Replace(
                "</xkbConfigRegistry>",
                "<unknown keep=\"yes\" /></xkbConfigRegistry>",
                StringComparison.Ordinal));

        var result = XkbInstallPlanner.PlanUninstall(
            metadata.ProjectInstallationId,
            Paths(),
            install.UpdatedManifest,
            current);

        Assert.True(result.Success);
        var bridge = Assert.Single(result.Plan!.Operations, operation =>
            operation.RelativePath == "symbols/pl");
        Assert.Equal(XkbInstallOperationKind.Replace, bridge.Kind);
        Assert.Contains("xkb_symbols \"mine\"", bridge.Content);
        var registry = Assert.Single(result.Plan.Operations, operation =>
            operation.RelativePath == "rules/evdev.xml");
        Assert.Contains("<unknown keep=\"yes\"", registry.Content);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public void PlanUpdate_WhenManagedBlockWasExternallyEdited_RefusesOverwrite()
    {
        var metadata = Polish();
        var install = XkbInstallPlanner.PlanInstall(
            Bundle(metadata), metadata, Paths(), XkbInstallationManifest.Empty, [], VerifiedAt, "1.13.1").Plan!;
        var current = Apply([], install.Operations).ToList();
        ReplaceSnapshot(
            current,
            "symbols/pl",
            Content(current, "symbols/pl").Replace("include", "// external\n    include", StringComparison.Ordinal));

        var result = XkbInstallPlanner.PlanInstall(
            Bundle(metadata), metadata, Paths(), install.UpdatedManifest, current, VerifiedAt, "1.13.1");

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "KSM003");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public void PlanInstall_WhenCentralFileIsUnowned_RefusesIt()
    {
        var metadata = Polish();

        var result = XkbInstallPlanner.PlanInstall(
            Bundle(metadata),
            metadata,
            Paths(),
            XkbInstallationManifest.Empty,
            [Snapshot("symbols/keyboardstudio", "// someone else's file\n")],
            VerifiedAt,
            "1.13.1");

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "KSP004");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public void PlanInstall_WhenDestinationIsSymlink_RefusesIt()
    {
        var metadata = Polish();

        var result = XkbInstallPlanner.PlanInstall(
            Bundle(metadata),
            metadata,
            Paths(),
            XkbInstallationManifest.Empty,
            [new XkbInstallFileSnapshot("symbols/pl", "content", IsSymbolicLink: true)],
            VerifiedAt,
            "1.13.1");

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "KSP003");
    }

    [Theory]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    [InlineData("/usr/share/X11/xkb", "/home/test/.local/state/keyboardstudio/xkb")]
    [InlineData("/home/test/elsewhere/xkb", "/home/test/.local/state/keyboardstudio/xkb")]
    [InlineData("/home/test/.config/xkb", "/etc/keyboardstudio/xkb")]
    public void PlanInstall_WhenResolvedPathsEscapeTheirUserHomes_RefusesThem(
        string xkbRoot,
        string stateRoot)
    {
        var metadata = Polish();
        var paths = Paths() with
        {
            UserXkbRoot = xkbRoot,
            KeyboardStudioStateRoot = stateRoot
        };

        var result = XkbInstallPlanner.PlanInstall(
            Bundle(metadata),
            metadata,
            paths,
            XkbInstallationManifest.Empty,
            [],
            VerifiedAt,
            "1.13.1");

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "KSP003");
    }

    private static XdgDirectoryPaths Paths() => new(
        "/home/test/.config",
        "/home/test/.local/state",
        "/home/test/.config/xkb",
        "/home/test/.local/state/keyboardstudio/xkb");

    private static XkbUserVariantMetadata Polish() => new(
        "7c31d5f2a19e40a4b0ef64f01a295135",
        "pl", "qwertz", "qwertz",
        "keyboardstudio_programmer",
        "Polish - KeyboardStudio");

    private static XkbUserVariantMetadata PolishDvorak() => new(
        "8d42e6a3b20f41b5c1f075a12b306246",
        "pl", "dvorak", "dvorak",
        "keyboardstudio_dvorak",
        "Polish Dvorak - KeyboardStudio");

    private static XkbGeneratedUserBundle Bundle(XkbUserVariantMetadata metadata) =>
        XkbUserBundleGenerator.Generate(
        [
            new XkbUserVariantLayout(
                metadata,
                [new XkbUserVariantKeyMapping("KeyA", "<AC01>", XkbKeyType.Alphabetic, ["x", "X"])],
                UsesLevelThree: false)
        ]).Bundle!;

    private static XkbInstallFileSnapshot Snapshot(string path, string content) =>
        new(path, content, IsSymbolicLink: false);

    private static XkbInstallFileSnapshot[] Apply(
        IReadOnlyList<XkbInstallFileSnapshot> current,
        IReadOnlyList<XkbInstallOperation> operations)
    {
        var files = current.ToDictionary(file => file.RelativePath, StringComparer.Ordinal);
        foreach (var operation in operations)
        {
            if (operation.Kind == XkbInstallOperationKind.Delete)
            {
                files.Remove(operation.RelativePath);
            }
            else
            {
                files[operation.RelativePath] = Snapshot(operation.RelativePath, operation.Content!);
            }
        }

        return files.Values.OrderBy(file => file.RelativePath, StringComparer.Ordinal).ToArray();
    }

    private static string Content(
        IReadOnlyList<XkbInstallFileSnapshot> files,
        string path) =>
        files.Single(file => file.RelativePath == path).Content!;

    private static void ReplaceSnapshot(
        List<XkbInstallFileSnapshot> files,
        string path,
        string content)
    {
        var index = files.FindIndex(file => file.RelativePath == path);
        files[index] = Snapshot(path, content);
    }
}
