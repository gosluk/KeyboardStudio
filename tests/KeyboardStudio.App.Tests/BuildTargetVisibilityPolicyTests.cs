using KeyboardStudio.App;
using KeyboardStudio.Build;
using Xunit;

namespace KeyboardStudio.App.Tests;

public sealed class BuildTargetVisibilityPolicyTests
{
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("linux")]
    [InlineData("windowsX64")]
    public void IsVisible_WhenOverrideIsAbsentOrUnrecognised_OffersOnlyTheLinuxTarget(string? overrideValue)
    {
        var policy = new EnvironmentBuildTargetVisibilityPolicy(overrideValue);

        Assert.True(policy.IsVisible(BuildTarget.LinuxXkb));
        Assert.False(policy.IsVisible(BuildTarget.WindowsX64));
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("all")]
    [InlineData("ALL")]
    [InlineData("  all  ")]
    public void IsVisible_WhenOverrideRequestsEveryTarget_OffersThemAll(string overrideValue)
    {
        var policy = new EnvironmentBuildTargetVisibilityPolicy(overrideValue);

        Assert.True(policy.IsVisible(BuildTarget.LinuxXkb));
        Assert.True(policy.IsVisible(BuildTarget.WindowsX64));
    }
}
