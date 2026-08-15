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

    private static BuildViewModel CreateViewModel(ITargetBuildService service) =>
        new(CreateProject, service, new KeyboardProjectValidator());

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

        public BuildEnvironmentStatus GetEnvironmentStatus(BuildTarget target) =>
            new(true, $"{(target == BuildTarget.LinuxXkb ? "Linux XKB" : target)} is available.", [], [target]);

        public Task<KeyboardBuildResult> BuildAsync(
            KeyboardProject project,
            BuildOptions options,
            IReadOnlyDictionary<string, string> profileSettings,
            CancellationToken cancellationToken = default)
        {
            LastOptions = options;
            LastSettings = profileSettings;
            var path = Path.Combine(options.OutputDirectory, "xkb", "symbols", profileSettings[BuildProfileKeys.LayoutId]);
            return Task.FromResult(new KeyboardBuildResult(
                true,
                [],
                new ArtifactBuildResult(true, path, [])));
        }
    }
}
