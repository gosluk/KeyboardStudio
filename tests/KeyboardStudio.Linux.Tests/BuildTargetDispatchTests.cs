using KeyboardStudio.Build;
using KeyboardStudio.Core;
using Xunit;

namespace KeyboardStudio.Linux.Tests;

public sealed class BuildTargetDispatchTests
{
    [Fact]
    public async Task BuildAsync_WindowsAndLinuxTargets_InvokeDifferentBackends()
    {
        var windows = new TrackingBackend(BuildTarget.WindowsX64, BuildTarget.WindowsArm64);
        var linux = new TrackingBackend(BuildTarget.LinuxXkb);
        var orchestrator = new BuildOrchestrator(
            new KeyboardProjectValidator([]),
            new BuildBackendResolver([windows, linux]));

        await orchestrator.BuildAsync(
            DemoProjectFactory.Create(),
            new BuildOptions(BuildTarget.WindowsArm64, "out"));
        await orchestrator.BuildAsync(
            DemoProjectFactory.Create(),
            new BuildOptions(BuildTarget.LinuxXkb, "out"));

        Assert.Equal([BuildTarget.WindowsArm64], windows.Calls);
        Assert.Equal([BuildTarget.LinuxXkb], linux.Calls);
    }

    private sealed class TrackingBackend(params BuildTarget[] supportedTargets) : IBuildBackend
    {
        public List<BuildTarget> Calls { get; } = [];

        public IReadOnlySet<BuildTarget> SupportedTargets { get; } =
            new HashSet<BuildTarget>(supportedTargets);

        public BuildEnvironmentStatus GetStatus(BuildTarget target) =>
            new(true, "Available", [], supportedTargets);

        public Task<KeyboardBuildResult> BuildAsync(
            KeyboardProject project,
            BuildOptions options,
            CancellationToken cancellationToken = default)
        {
            Calls.Add(options.Target);
            return Task.FromResult(new KeyboardBuildResult(
                true,
                [],
                new ArtifactBuildResult(true, $"{options.Target}.artifact", [])));
        }
    }
}
