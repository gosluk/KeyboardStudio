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
    public void CanBuild_RequiresDetectedTarget()
    {
        var environment = new WindowsBuildEnvironment(new StaticProbe(new BuildEnvironmentStatus(
            true,
            "Available",
            [],
            [BuildTarget.WindowsX64])));

        Assert.True(environment.CanBuild(BuildTarget.WindowsX64));
        Assert.False(environment.CanBuild(BuildTarget.WindowsArm64));
        Assert.False(environment.GetStatus(BuildTarget.WindowsArm64).Available);
    }

    private sealed class StaticProbe(BuildEnvironmentStatus status) : IWindowsBuildEnvironmentProbe
    {
        public BuildEnvironmentStatus Probe() => status;

        public ResolvedBuildEnvironment? Resolve(BuildTarget target) => null;
    }
}
