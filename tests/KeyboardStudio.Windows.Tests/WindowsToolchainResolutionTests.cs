using KeyboardStudio.Build;
using Xunit;

namespace KeyboardStudio.Windows.Tests;

public sealed class WindowsToolchainResolutionTests
{
    [Fact]
    public void ResolvedEnvironment_ExposesToolchainContract()
    {
        var environment = new ResolvedBuildEnvironment(
            BuildTarget.WindowsX64,
            @"C:\VC\bin\cl.exe",
            @"C:\VC\bin\link.exe",
            @"C:\SDK\bin\rc.exe",
            [@"C:\VC\include", @"C:\SDK\Include\um"],
            [@"C:\VC\lib\x64", @"C:\SDK\Lib\um\x64"],
            "14.50.35717",
            "10.0.26100.0");

        Assert.Equal(BuildTarget.WindowsX64, environment.Target);
        Assert.EndsWith("cl.exe", environment.CompilerPath, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("link.exe", environment.LinkerPath, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("rc.exe", environment.ResourceCompilerPath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("14.50.35717", environment.ToolVersion);
        Assert.Equal("10.0.26100.0", environment.SdkVersion);
        Assert.NotEmpty(environment.IncludePaths);
        Assert.NotEmpty(environment.LibraryPaths);
    }
}
