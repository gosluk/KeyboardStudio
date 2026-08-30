using KeyboardStudio.App;
using KeyboardStudio.Core;
using KeyboardStudio.Linux;
using KeyboardStudio.Persistence;
using Xunit;

namespace KeyboardStudio.App.Tests;

public sealed class LinuxUserVariantViewModelTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void WithoutSystemDerivation_ShowsReimportGuidanceAndDisablesActions()
    {
        var project = Project();
        var viewModel = new LinuxUserVariantViewModel(
            () => project,
            () => null,
            () => "/tmp/output",
            new FakeLinuxUserVariantWorkflowService());

        Assert.False(viewModel.IsVisible);
        Assert.Contains("Import a system layout", viewModel.StatusText, StringComparison.Ordinal);
        Assert.False(viewModel.RefreshCommand.CanExecute(null));
        Assert.False(viewModel.GenerateCommand.CanExecute(null));
        Assert.False(viewModel.InstallCommand.CanExecute(null));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Refresh_ExposesSourceCapabilityPathsWarningsAndInstallReadiness()
    {
        var project = Project();
        var derivation = Derivation(project);
        var workflow = new FakeLinuxUserVariantWorkflowService().AddInspection(
            Preparation(
                LinuxUserVariantStatus.NotInstalled,
                [new XkbDiagnostic("KSC006", "A newer libxkbcommon version is recommended.")]));
        var viewModel = Create(project, derivation, workflow);

        await viewModel.RefreshAsync();

        Assert.Equal("pl", viewModel.BaseLayout);
        Assert.Equal("qwertz", viewModel.BaseVariant);
        Assert.Equal("Exact", viewModel.SourceFidelity);
        Assert.Equal("keyboardstudio_qwertz", viewModel.VariantId);
        Assert.Equal("Polish - KeyboardStudio", viewModel.DisplayName);
        Assert.Contains("/home/test/.config/xkb", viewModel.PathsText, StringComparison.Ordinal);
        Assert.Contains("Wayland", viewModel.CapabilityText, StringComparison.Ordinal);
        Assert.Contains("KSC006", viewModel.DiagnosticsText, StringComparison.Ordinal);
        Assert.True(viewModel.GenerateCommand.CanExecute(null));
        Assert.True(viewModel.InstallCommand.CanExecute(null));
        Assert.False(viewModel.UpdateCommand.CanExecute(null));
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(LinuxUserVariantStatus.ExportOnly, true, false, false, false)]
    [InlineData(LinuxUserVariantStatus.Installed, true, false, false, true)]
    [InlineData(LinuxUserVariantStatus.UpdateAvailable, true, false, true, true)]
    [InlineData(LinuxUserVariantStatus.ExternallyModified, true, false, false, true)]
    [InlineData(LinuxUserVariantStatus.Broken, true, false, false, true)]
    [InlineData(LinuxUserVariantStatus.BaseUnavailable, true, false, false, false)]
    public async Task Refresh_StatusControlsExactlyTheSafeCommands(
        LinuxUserVariantStatus status,
        bool canGenerate,
        bool canInstall,
        bool canUpdate,
        bool canVerifyOrUninstall)
    {
        var project = Project();
        var workflow = new FakeLinuxUserVariantWorkflowService().AddInspection(Preparation(status));
        var viewModel = Create(project, Derivation(project), workflow);

        await viewModel.RefreshAsync();

        Assert.Equal(canGenerate, viewModel.GenerateCommand.CanExecute(null));
        Assert.Equal(canInstall, viewModel.InstallCommand.CanExecute(null));
        Assert.Equal(canUpdate, viewModel.UpdateCommand.CanExecute(null));
        Assert.Equal(canVerifyOrUninstall, viewModel.VerifyInstalledCommand.CanExecute(null));
        Assert.Equal(canVerifyOrUninstall, viewModel.UninstallCommand.CanExecute(null));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task LiveAction_WhenConfirmationIsDeclined_ShowsExactPathsAndDoesNotCallService()
    {
        var project = Project();
        var workflow = new FakeLinuxUserVariantWorkflowService().AddInspection(
            Preparation(LinuxUserVariantStatus.NotInstalled));
        var interaction = new FakeLinuxUserVariantInteractionService { Confirm = false };
        var viewModel = Create(project, Derivation(project), workflow, interaction);
        await viewModel.RefreshAsync();

        await viewModel.InstallCommand.ExecuteAsync(null);

        Assert.Equal("Install", interaction.LastAction);
        Assert.Equal(7, interaction.LastPaths.Count);
        Assert.Contains(interaction.LastPaths, path => path.EndsWith(
            "symbols/keyboardstudio", StringComparison.Ordinal));
        Assert.Contains(interaction.LastPaths, path => path.EndsWith(
            "symbols/pl", StringComparison.Ordinal));
        Assert.Contains(interaction.LastPaths, path => path.EndsWith(
            "rules/evdev.xml", StringComparison.Ordinal));
        Assert.Contains(interaction.LastPaths, path => path.EndsWith(
            "installations.json", StringComparison.Ordinal));
        Assert.Contains(interaction.LastPaths, path => path.EndsWith(
            "journal.json", StringComparison.Ordinal));
        Assert.Equal(0, workflow.InstallOrUpdateCount);
        Assert.Contains("cancelled", viewModel.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task EveryLiveOperation_RefreshesStatusAndCommandEnablementAfterCompletion()
    {
        var project = Project();
        var workflow = new FakeLinuxUserVariantWorkflowService()
            .AddInspection(Preparation(LinuxUserVariantStatus.NotInstalled))
            .AddInspection(Preparation(LinuxUserVariantStatus.Installed))
            .AddInspection(Preparation(LinuxUserVariantStatus.Installed))
            .AddInspection(Preparation(LinuxUserVariantStatus.UpdateAvailable))
            .AddInspection(Preparation(LinuxUserVariantStatus.Installed))
            .AddInspection(Preparation(LinuxUserVariantStatus.NotInstalled));
        var viewModel = Create(
            project,
            Derivation(project),
            workflow,
            new FakeLinuxUserVariantInteractionService());
        await viewModel.RefreshAsync();

        await viewModel.InstallCommand.ExecuteAsync(null);
        Assert.Equal(LinuxUserVariantStatus.Installed, viewModel.Status);
        Assert.True(viewModel.VerifyInstalledCommand.CanExecute(null));

        await viewModel.VerifyInstalledCommand.ExecuteAsync(null);
        Assert.Equal(LinuxUserVariantStatus.Installed, viewModel.Status);

        viewModel.DisplayName = "Polish Programmer - KeyboardStudio";
        await viewModel.RefreshAsync();
        Assert.Equal(LinuxUserVariantStatus.UpdateAvailable, viewModel.Status);
        await viewModel.UpdateCommand.ExecuteAsync(null);
        Assert.Equal(LinuxUserVariantStatus.Installed, viewModel.Status);

        await viewModel.UninstallCommand.ExecuteAsync(null);
        Assert.Equal(LinuxUserVariantStatus.NotInstalled, viewModel.Status);
        Assert.True(viewModel.InstallCommand.CanExecute(null));
        Assert.Equal(2, workflow.InstallOrUpdateCount);
        Assert.Equal(1, workflow.VerifyCount);
        Assert.Equal(1, workflow.UninstallCount);
        Assert.Equal(6, workflow.InspectCount);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Cancel_DuringLiveOperation_ReportsRollbackSafeCancellation()
    {
        var project = Project();
        var workflow = new FakeLinuxUserVariantWorkflowService
        {
            WaitForCancellation = true
        }.AddInspection(Preparation(LinuxUserVariantStatus.NotInstalled));
        var viewModel = Create(
            project,
            Derivation(project),
            workflow,
            new FakeLinuxUserVariantInteractionService());
        await viewModel.RefreshAsync();

        var running = viewModel.InstallCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => workflow.InstallOrUpdateCount == 1);
        viewModel.CancelCommand.Execute(null);
        await running;

        Assert.Contains("rolled back", viewModel.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GenerateAndOpenOutput_UseBuildOutputWithoutRequestingLiveConfirmation()
    {
        var project = Project();
        var workflow = new FakeLinuxUserVariantWorkflowService
        {
            OperationResult = new LinuxUserVariantOperationResult(
                true,
                "Generated.",
                "/tmp/output/xkb-user-bundle",
                [])
        }.AddInspection(Preparation(LinuxUserVariantStatus.ExportOnly));
        var interaction = new FakeLinuxUserVariantInteractionService();
        var viewModel = Create(project, Derivation(project), workflow, interaction);
        await viewModel.RefreshAsync();

        await viewModel.GenerateCommand.ExecuteAsync(null);
        await viewModel.OpenOutputCommand.ExecuteAsync(null);

        Assert.Equal(1, workflow.GenerateCount);
        Assert.Equal("/tmp/output/xkb-user-bundle", viewModel.GeneratedBundlePath);
        Assert.Equal(viewModel.GeneratedBundlePath, interaction.OpenedPath);
        Assert.Null(interaction.LastAction);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task InvalidVariantPreparation_PresentsValidationAndKeepsMutationCommandsDisabled()
    {
        var project = Project();
        var workflow = new FakeLinuxUserVariantWorkflowService().AddInspection(
            Preparation(
                LinuxUserVariantStatus.Unavailable,
                [new XkbDiagnostic("KSW002", "The custom variant ID is invalid.")],
                includeBundle: false));
        var viewModel = Create(project, Derivation(project), workflow);
        viewModel.VariantId = "Bad Variant";

        await viewModel.RefreshAsync();

        Assert.Equal("Bad Variant", workflow.LastVariantId);
        Assert.Contains("KSW002", viewModel.DiagnosticsText, StringComparison.Ordinal);
        Assert.False(viewModel.GenerateCommand.CanExecute(null));
        Assert.False(viewModel.InstallCommand.CanExecute(null));
    }

    private static LinuxUserVariantViewModel Create(
        KeyboardProject project,
        LayoutDerivation derivation,
        FakeLinuxUserVariantWorkflowService workflow,
        ILinuxUserVariantInteractionService? interaction = null) =>
        new(() => project, () => derivation, () => "/tmp/output", workflow, interaction);

    private static LinuxUserVariantPreparation Preparation(
        LinuxUserVariantStatus status,
        IReadOnlyList<XkbDiagnostic>? diagnostics = null,
        bool includeBundle = true)
    {
        var metadata = new XkbUserVariantMetadata(
            "7c31d5f2a19e40a4b0ef64f01a295135",
            "pl",
            "qwertz",
            "qwertz",
            "keyboardstudio_qwertz",
            "Polish - KeyboardStudio");
        var paths = new XdgDirectoryPaths(
            "/home/test/.config",
            "/home/test/.local/state",
            "/home/test/.config/xkb",
            "/home/test/.local/state/keyboardstudio/xkb");
        var managed = status != LinuxUserVariantStatus.ExportOnly &&
                      status != LinuxUserVariantStatus.BaseUnavailable;
        var capability = new XkbUserInstallCapability(
            managed ? XkbUserInstallMode.ManagedInstallation : XkbUserInstallMode.ExportOnly,
            XkbSessionType.Wayland,
            paths.UserXkbRoot,
            paths.KeyboardStudioStateRoot,
            PathsAreSafe: true,
            "/usr/bin/xkbcli",
            "xkbcli 1.13.1",
            new Version(1, 13, 1),
            MeetsRecommendedVersion: true,
            status == LinuxUserVariantStatus.BaseUnavailable ? null : "/usr/share/X11/xkb",
            XkbRegistryDiscoverySupport.Available,
            []);
        var bundle = includeBundle
            ? new XkbGeneratedUserBundle(
            [
                new XkbUserBundleFile("symbols/keyboardstudio", "content", new string('a', 64))
            ])
            : null;
        var manifest = status is LinuxUserVariantStatus.NotInstalled or
            LinuxUserVariantStatus.ExportOnly or
            LinuxUserVariantStatus.BaseUnavailable or
            LinuxUserVariantStatus.Unavailable
            ? XkbInstallationManifest.Empty
            : new XkbInstallationManifest(
                XkbInstallationManifest.CurrentSchemaVersion,
                [new XkbInstalledVariant(
                    metadata.ProjectInstallationId,
                    metadata.BaseLayoutId,
                    metadata.BaseVariantId,
                    metadata.ResolvedBaseSectionId,
                    metadata.PublicVariantId,
                    metadata.InternalSectionId,
                    metadata.Description,
                    new string('a', 64),
                    new string('b', 64),
                    new string('c', 64),
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow,
                    "1.13.1")],
                []);
        return new LinuxUserVariantPreparation(
            status,
            metadata,
            bundle,
            paths,
            capability,
            manifest,
            diagnostics ?? []);
    }

    private static KeyboardProject Project()
    {
        var mapping = new KeyMapping
        {
            KeyId = "KeyA",
            LogicalKey = LogicalKey.A,
            Outputs =
            {
                [ModifierLayer.Default] = new CharacterOutput("a"),
                [ModifierLayer.Shift] = new CharacterOutput("A")
            }
        };
        return new KeyboardProject
        {
            Metadata = new ProjectMetadata { Name = "Polish" },
            Keyboard = new PhysicalKeyboard
            {
                Id = "iso-105",
                Keys = [new PhysicalKey { Id = "KeyA", ScanCode = 0x1E }]
            },
            Layout = new KeyboardLayout { Mappings = [mapping] }
        };
    }

    private static LayoutDerivation Derivation(KeyboardProject project) => new(
        "7c31d5f2a19e40a4b0ef64f01a295135",
        "system",
        LayoutSourceOrigin.System,
        "pl",
        "qwertz",
        "qwertz",
        DateTimeOffset.UtcNow,
        LayoutImportFidelity.Exact,
        project.Layout.Mappings.Select(mapping => KeyMappingSnapshot.From(mapping, true)).ToArray());

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }
}
