using KeyboardStudio.Build;
using KeyboardStudio.Core;
using Xunit;

namespace KeyboardStudio.Windows.Tests;

public sealed class WindowsNativeCompilationIntegrationTests
{
    [Fact]
    [Trait("Category", "WindowsIntegration")]
    public async Task CompileAsync_WhenToolchainIsAvailable_ProducesDll()
    {
        var environment = new WindowsBuildEnvironment();
        if (!environment.CanBuild(BuildTarget.WindowsX64))
        {
            return;
        }

        var buildRoot = Path.Combine(Path.GetTempPath(), $"KeyboardStudio-{Guid.NewGuid():N}");
        try
        {
            var generator = new WindowsArtifactGenerator(new WindowsLayoutMetadata(
                "kbd-phase7-test",
                "KeyboardStudio Phase 7 integration test"));
            var options = new BuildOptions(BuildTarget.WindowsX64, buildRoot);
            var artifact = await generator.GenerateAsync(DemoProjectFactory.Create(), options);

            var result = await new MsvcKeyboardCompiler(environment, new ProcessRunner())
                .CompileAsync(artifact, options);

            Assert.True(result.Success, result.RawLog);
            Assert.True(File.Exists(result.ArtifactPath), result.RawLog);
            var verification = Assert.IsType<ArtifactVerificationResult>(result.Verification);
            Assert.True(verification.ExpectedExportFound, result.RawLog);
            Assert.Equal(ArtifactLoadTestStatus.Passed, verification.LoadTest.Status);
        }
        finally
        {
            if (Directory.Exists(buildRoot))
            {
                Directory.Delete(buildRoot, recursive: true);
            }
        }
    }
}
