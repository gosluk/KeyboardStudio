using KeyboardStudio.App;
using KeyboardStudio.Build;
using KeyboardStudio.Core;
using Xunit;

namespace KeyboardStudio.App.Tests;

public sealed class BuildViewModelTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void TargetSelection_UpdatesProfileAndEnvironmentPresentation()
    {
        var service = new RecordingBuildService();
        var viewModel = CreateViewModel(service);

        viewModel.SelectedTarget = viewModel.Targets.Single(option => option.Target == BuildTarget.LinuxXkb);

        Assert.Equal("Linux XKB", viewModel.SelectedTarget.DisplayName);
        Assert.Contains(viewModel.ProfileSettings, setting => setting.Key == BuildProfileKeys.SectionId);
        Assert.DoesNotContain(viewModel.ProfileSettings, setting => setting.Key == BuildProfileKeys.FileVersion);
        Assert.Equal("Linux XKB is available.", viewModel.EnvironmentStatus);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task BuildCommand_UsesSelectedTargetProfileAndPresentsArtifact()
    {
        var service = new RecordingBuildService();
        var viewModel = CreateViewModel(service);
        viewModel.SelectedTarget = viewModel.Targets.Single(option => option.Target == BuildTarget.LinuxXkb);
        viewModel.ProfileSettings.Single(setting => setting.Key == BuildProfileKeys.LayoutId).Value = "custom";
        var outputDirectory = Path.Combine(Path.GetTempPath(), "keyboard-build");
        viewModel.OutputDirectory = outputDirectory;

        await viewModel.BuildCommand.ExecuteAsync(null);

        Assert.Equal(BuildTarget.LinuxXkb, service.LastOptions?.Target);
        Assert.Equal(outputDirectory, service.LastOptions?.OutputDirectory);
        Assert.Equal("custom", service.LastSettings?[BuildProfileKeys.LayoutId]);
        Assert.Equal(Path.Combine(outputDirectory, "xkb", "symbols", "custom"), viewModel.ArtifactPath);
        Assert.Equal("Build completed successfully.", viewModel.Status);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildCommand_WhenSelectedTargetHasErrors_DisablesOnlyThatTarget()
    {
        var service = new RecordingBuildService
        {
            ReadinessFactory = target => CreateReadiness(
                target,
                target == BuildTarget.WindowsX64
                    ? [new ValidationIssue(ValidationSeverity.Error, "KSW001", "Unsupported mapping.")]
                    : [])
        };
        var viewModel = CreateViewModel(service);

        Assert.False(viewModel.BuildCommand.CanExecute(null));
        Assert.Contains(viewModel.Problems, problem =>
            problem.Kind == BuildProblemKind.TargetCompatibility);

        viewModel.SelectedTarget = viewModel.Targets.Single(option => option.Target == BuildTarget.LinuxXkb);

        Assert.True(viewModel.BuildCommand.CanExecute(null));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildCommand_WhenRequiredWindowsToolsAreUnavailable_IsDisabled()
    {
        var service = new RecordingBuildService
        {
            ReadinessFactory = target => new BuildReadiness(
                new BuildEnvironmentStatus(false, "MSVC unavailable.", [], [target]),
                [],
                [])
        };
        var viewModel = CreateViewModel(service);

        Assert.False(viewModel.BuildCommand.CanExecute(null));
        Assert.Equal("MSVC unavailable.", viewModel.EnvironmentStatus);
        Assert.Contains(viewModel.Problems, problem =>
            problem.Kind == BuildProblemKind.MissingRequiredToolchain);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildCommand_WhenOptionalXkbVerifierIsUnavailable_RemainsEnabledWithWarning()
    {
        var service = new RecordingBuildService
        {
            ReadinessFactory = target => new BuildReadiness(
                new BuildEnvironmentStatus(
                    true,
                    "Generation available; optional verifier unavailable.",
                    [new BuildEnvironmentDiagnostic("KSL004", "xkbcli was not found.")],
                    [target]),
                [],
                [])
        };
        var viewModel = CreateViewModel(service);
        viewModel.SelectedTarget = viewModel.Targets.Single(option => option.Target == BuildTarget.LinuxXkb);

        Assert.True(viewModel.BuildCommand.CanExecute(null));
        Assert.Contains("optional verifier unavailable", viewModel.EnvironmentStatus, StringComparison.Ordinal);
        Assert.Contains(viewModel.Problems, problem =>
            problem.Kind == BuildProblemKind.OptionalVerifierUnavailable);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task BuildCommand_WhenCommonValidationFails_DoesNotInvokeAnyBackend()
    {
        var service = new RecordingBuildService
        {
            ReadinessFactory = target => new BuildReadiness(
                new BuildEnvironmentStatus(true, "Available", [], [target]),
                [new ValidationIssue(ValidationSeverity.Error, "KSP101", "Project name is required.")],
                [])
        };
        var viewModel = CreateViewModel(service);

        await viewModel.BuildCommand.ExecuteAsync(null);
        viewModel.SelectedTarget = viewModel.Targets.Single(option => option.Target == BuildTarget.LinuxXkb);
        await viewModel.BuildCommand.ExecuteAsync(null);

        Assert.Equal(0, service.BuildCallCount);
        Assert.Contains(viewModel.Problems, problem =>
            problem.Kind == BuildProblemKind.ProjectValidation);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task BuildCommand_WhileBuildIsRunning_IsDisabled()
    {
        var completion = new TaskCompletionSource<KeyboardBuildResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new RecordingBuildService { PendingResult = completion.Task };
        var viewModel = CreateViewModel(service);

        var execution = viewModel.BuildCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsBuilding);
        Assert.False(viewModel.BuildCommand.CanExecute(null));

        completion.SetResult(CreateSuccessfulResult("/tmp/layout"));
        await execution;
        Assert.True(viewModel.BuildCommand.CanExecute(null));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task BuildCommand_WhenLinuxSelected_ShowsOnlyReportedLinuxStages()
    {
        var service = new RecordingBuildService
        {
            ReportedStages =
            [
                BuildStageNames.Validating,
                BuildStageNames.GeneratingXkb,
                BuildStageNames.WritingArtifact,
                BuildStageNames.Verifying,
                BuildStageNames.Completed
            ]
        };
        var viewModel = CreateViewModel(service);
        viewModel.SelectedTarget = viewModel.Targets.Single(option => option.Target == BuildTarget.LinuxXkb);

        await viewModel.BuildCommand.ExecuteAsync(null);

        Assert.Equal(service.ReportedStages, viewModel.Stages.Select(stage => stage.Name));
        Assert.DoesNotContain(viewModel.Stages, stage => stage.Name == BuildStageNames.Compiling);
        Assert.All(viewModel.Stages, stage => Assert.Equal(BuildStageState.Completed, stage.State));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CancelCommand_WhenBuildIsRunning_CancelsBackendAndPresentsCancellation()
    {
        var service = new RecordingBuildService { WaitForCancellation = true };
        var viewModel = CreateViewModel(service);
        var execution = viewModel.BuildCommand.ExecuteAsync(null);

        viewModel.CancelBuildCommand.Execute(null);
        await execution;

        Assert.Equal("Build cancelled.", viewModel.Status);
        Assert.Contains(viewModel.Stages, stage =>
            stage.Name == BuildStageNames.Cancelled && stage.State == BuildStageState.Cancelled);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ResultActions_OpenInspectAndCopyBuildOutputs()
    {
        var service = new RecordingBuildService();
        var interaction = new RecordingBuildInteractionService();
        var viewModel = CreateViewModel(service, interaction);
        viewModel.SelectedTarget = viewModel.Targets.Single(option => option.Target == BuildTarget.LinuxXkb);
        viewModel.OutputDirectory = "/tmp/keyboard-output";

        await viewModel.BuildCommand.ExecuteAsync(null);
        await viewModel.OpenOutputDirectoryCommand.ExecuteAsync(null);
        await viewModel.InspectGeneratedFileCommand.ExecuteAsync(null);
        await viewModel.CopyBuildLogCommand.ExecuteAsync(null);
        await viewModel.CopyArtifactPathCommand.ExecuteAsync(null);

        Assert.Equal(["/tmp/keyboard-output"], interaction.OpenedDirectories);
        var inspected = Assert.Single(interaction.InspectedFiles);
        Assert.EndsWith("keyboardstudio", inspected.Title, StringComparison.Ordinal);
        Assert.Equal("xkb symbols", inspected.Content);
        Assert.Equal(2, interaction.CopiedTexts.Count);
        Assert.Contains("build log", interaction.CopiedTexts[0], StringComparison.Ordinal);
        Assert.Equal(viewModel.ArtifactPath, interaction.CopiedTexts[1]);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("GEN_SOURCE", BuildProblemKind.SourceGeneration, "Source generation error")]
    [InlineData("C1000", BuildProblemKind.CompilerOrLinker, "Compiler or linker error")]
    [InlineData("PE_EXPORT", BuildProblemKind.ArtifactVerification, "Artifact verification error")]
    public async Task BuildResult_WhenBackendFails_PresentsCategorizedProblem(
        string code,
        BuildProblemKind expectedKind,
        string expectedCategory)
    {
        var result = new KeyboardBuildResult(
            false,
            [],
            new ArtifactBuildResult(
                false,
                null,
                [new BuildArtifactDiagnostic(BuildDiagnosticSeverity.Error, code, "Failure")]));
        var service = new RecordingBuildService { PendingResult = Task.FromResult(result) };
        var viewModel = CreateViewModel(service);

        await viewModel.BuildCommand.ExecuteAsync(null);

        var problem = Assert.Single(viewModel.Problems);
        Assert.Equal(expectedKind, problem.Kind);
        Assert.Equal(expectedCategory, problem.Category);
        Assert.Contains(expectedCategory, viewModel.Status, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task BuildResult_WhenOptionalVerifierIsMissing_PresentsUnverifiedSuccess()
    {
        var result = new KeyboardBuildResult(
            true,
            [],
            new ArtifactBuildResult(
                true,
                "/tmp/layout",
                [new BuildArtifactDiagnostic(
                    BuildDiagnosticSeverity.Warning,
                    "KSL004",
                    "xkbcli was not found.")]));
        var service = new RecordingBuildService { PendingResult = Task.FromResult(result) };
        var viewModel = CreateViewModel(service);

        await viewModel.BuildCommand.ExecuteAsync(null);

        Assert.Contains(viewModel.Problems, problem =>
            problem.Kind == BuildProblemKind.OptionalVerifierUnavailable);
        Assert.Equal("Build completed without external XKB verification.", viewModel.Status);
    }

    private static BuildViewModel CreateViewModel(
        ITargetBuildService service,
        IBuildInteractionService? interactionService = null) =>
        new(CreateProject, service, interactionService);

    private static KeyboardProject CreateProject() => new()
    {
        Metadata = new ProjectMetadata
        {
            Name = "Test layout",
            Description = "Test",
            Version = "1.0.0",
            Language = "en"
        },
        Keyboard = new PhysicalKeyboard { Id = "test", Keys = [] },
        Layout = new KeyboardLayout()
    };

    private sealed class RecordingBuildService : ITargetBuildService
    {
        public BuildOptions? LastOptions { get; private set; }

        public IReadOnlyDictionary<string, string>? LastSettings { get; private set; }

        public Func<BuildTarget, BuildReadiness>? ReadinessFactory { get; init; }

        public Task<KeyboardBuildResult>? PendingResult { get; init; }

        public IReadOnlyList<string>? ReportedStages { get; init; }

        public bool WaitForCancellation { get; init; }

        public int BuildCallCount { get; private set; }

        public BuildEnvironmentStatus GetEnvironmentStatus(BuildTarget target) =>
            new(true, $"{(target == BuildTarget.LinuxXkb ? "Linux XKB" : target)} is available.", [], [target]);

        public BuildReadiness GetReadiness(
            KeyboardProject project,
            BuildTarget target,
            IReadOnlyDictionary<string, string> profileSettings,
            string outputDirectory) =>
            ReadinessFactory?.Invoke(target) ?? CreateReadiness(target, []);

        public Task<KeyboardBuildResult> BuildAsync(
            KeyboardProject project,
            BuildOptions options,
            IReadOnlyDictionary<string, string> profileSettings,
            IProgress<BuildStageProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            BuildCallCount++;
            LastOptions = options;
            LastSettings = profileSettings;
            if (WaitForCancellation)
            {
                return WaitForCancellationAsync(cancellationToken, progress);
            }

            if (ReportedStages is not null)
            {
                foreach (var stage in ReportedStages)
                {
                    progress?.Report(new BuildStageProgress(stage, BuildStageState.Completed));
                }
            }

            var path = Path.Combine(options.OutputDirectory, "xkb", "symbols", profileSettings[BuildProfileKeys.LayoutId]);
            return PendingResult ?? Task.FromResult(CreateSuccessfulResult(path));
        }

        private static async Task<KeyboardBuildResult> WaitForCancellationAsync(
            CancellationToken cancellationToken,
            IProgress<BuildStageProgress>? progress)
        {
            progress?.Report(new BuildStageProgress(BuildStageNames.Validating, BuildStageState.Running));
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                progress?.Report(new BuildStageProgress(BuildStageNames.Cancelled, BuildStageState.Cancelled));
                throw;
            }

            throw new InvalidOperationException("The cancellation test unexpectedly completed.");
        }
    }

    private sealed class RecordingBuildInteractionService : IBuildInteractionService
    {
        public List<string> OpenedDirectories { get; } = [];

        public List<(string Title, string Content)> InspectedFiles { get; } = [];

        public List<string> CopiedTexts { get; } = [];

        public Task OpenDirectoryAsync(string path)
        {
            OpenedDirectories.Add(path);
            return Task.CompletedTask;
        }

        public Task ShowGeneratedTextAsync(string title, string content)
        {
            InspectedFiles.Add((title, content));
            return Task.CompletedTask;
        }

        public Task CopyTextAsync(string text)
        {
            CopiedTexts.Add(text);
            return Task.CompletedTask;
        }
    }

    private static BuildReadiness CreateReadiness(
        BuildTarget target,
        IReadOnlyList<ValidationIssue> targetIssues) =>
        new(
            new BuildEnvironmentStatus(
                true,
                $"{(target == BuildTarget.LinuxXkb ? "Linux XKB" : target)} is available.",
                [],
                [target]),
            [],
            targetIssues);

    private static KeyboardBuildResult CreateSuccessfulResult(string path) =>
        new(
            true,
            [],
            new ArtifactBuildResult(
                true,
                path,
                [new BuildArtifactDiagnostic(BuildDiagnosticSeverity.Info, "TEST", "Build diagnostic")],
                "build log",
                GeneratedFiles: [new BuildTextFile(path, "xkb symbols")]));
}
