using KeyboardStudio.App;
using KeyboardStudio.Build;
using KeyboardStudio.Core;
using Xunit;

namespace KeyboardStudio.App.Tests;

public sealed class BuildViewModelTests
{
    [Fact]
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
    public async Task BuildCommand_UsesSelectedTargetProfileAndPresentsArtifact()
    {
        var service = new RecordingBuildService();
        var viewModel = CreateViewModel(service);
        viewModel.SelectedTarget = viewModel.Targets.Single(option => option.Target == BuildTarget.LinuxXkb);
        viewModel.ProfileSettings.Single(setting => setting.Key == BuildProfileKeys.LayoutId).Value = "custom";
        viewModel.OutputDirectory = "/tmp/keyboard-build";

        await viewModel.BuildCommand.ExecuteAsync(null);

        Assert.Equal(BuildTarget.LinuxXkb, service.LastOptions?.Target);
        Assert.Equal("/tmp/keyboard-build", service.LastOptions?.OutputDirectory);
        Assert.Equal("custom", service.LastSettings?[BuildProfileKeys.LayoutId]);
        Assert.Equal("/tmp/keyboard-build/xkb/symbols/custom", viewModel.ArtifactPath);
        Assert.Equal("Build completed successfully.", viewModel.Status);
    }

    [Fact]
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

        viewModel.SelectedTarget = viewModel.Targets.Single(option => option.Target == BuildTarget.LinuxXkb);

        Assert.True(viewModel.BuildCommand.CanExecute(null));
    }

    [Fact]
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
    }

    [Fact]
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

    private static BuildViewModel CreateViewModel(ITargetBuildService service) =>
        new(CreateProject, service);

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
            CancellationToken cancellationToken = default)
        {
            LastOptions = options;
            LastSettings = profileSettings;
            var path = Path.Combine(options.OutputDirectory, "xkb", "symbols", profileSettings[BuildProfileKeys.LayoutId]);
            return PendingResult ?? Task.FromResult(CreateSuccessfulResult(path));
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
        new(true, [], new ArtifactBuildResult(true, path, []));
}
