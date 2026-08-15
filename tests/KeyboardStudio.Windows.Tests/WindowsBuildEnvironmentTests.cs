using KeyboardStudio.Build;
using Xunit;

namespace KeyboardStudio.Windows.Tests;

public sealed class WindowsBuildEnvironmentTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void GetStatus_ReportsStructuredHostDiagnostic()
    {
        var environment = new WindowsBuildEnvironment(new StaticProbe(new BuildEnvironmentStatus(
            false,
            "Windows is required.",
            [new BuildEnvironmentDiagnostic("ENV_HOST", "Windows is required.")],
            [])));

        var status = environment.GetStatus(BuildTarget.WindowsX64);

        Assert.False(environment.CanBuild(BuildTarget.WindowsX64));
        Assert.False(status.Available);
        var diagnostic = Assert.Single(status.Diagnostics);
        Assert.Equal("ENV_HOST", diagnostic.Code);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void CanBuild_WhenX64IsDetected_SupportsOnlyX64()
    {
        var environment = new WindowsBuildEnvironment(new StaticProbe(new BuildEnvironmentStatus(
            true,
            "Available",
            [],
            [BuildTarget.WindowsX64])));

        Assert.True(environment.CanBuild(BuildTarget.WindowsX64));
        Assert.False(environment.CanBuild(BuildTarget.LinuxXkb));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Constructor_WhenRunningOnWindows_ResolvesOnlyWindowsTargets()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var resolver = new RecordingResolver();

        _ = new WindowsBuildEnvironmentProbe(resolver);

        Assert.Equal(
            [BuildTarget.WindowsX64],
            resolver.RequestedTargets);
    }

    private sealed class StaticProbe(BuildEnvironmentStatus status) : IWindowsBuildEnvironmentProbe
    {
        public BuildEnvironmentStatus Probe() => status;

        public ResolvedBuildEnvironment? Resolve(BuildTarget target) => null;
    }

    private sealed class RecordingResolver : IWindowsToolchainResolver
    {
        public List<BuildTarget> RequestedTargets { get; } = [];

        public ResolvedBuildEnvironment? Resolve(BuildTarget target)
        {
            RequestedTargets.Add(target);
            return null;
        }
    }
}
