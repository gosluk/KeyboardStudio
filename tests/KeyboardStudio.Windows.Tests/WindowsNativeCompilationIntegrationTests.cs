using KeyboardStudio.Build;
using KeyboardStudio.Core;
using Xunit;

namespace KeyboardStudio.Windows.Tests;

public sealed class WindowsNativeCompilationIntegrationTests
{
    [Fact]
    [Trait("Category", "WindowsIntegration")]
    public async Task BuildAsync_WhenToolchainIsAvailable_ProducesVerifiedReproducibleDll()
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
            var options = new BuildOptions(
                BuildTarget.WindowsX64,
                buildRoot,
                VerifyReproducibility: true);
            var orchestrator = new BuildOrchestrator(
                new KeyboardProjectValidator(),
                new BuildBackendResolver([
                    new WindowsBuildBackend(
                        generator,
                        environment,
                        new MsvcKeyboardCompiler(environment, new ProcessRunner()))
                ]));

            var result = await orchestrator.BuildAsync(DemoProjectFactory.Create(), options);

            var compilation = Assert.IsType<CompilationResult>(result.Compilation);
            Assert.True(result.Success, compilation.RawLog);
            Assert.True(File.Exists(compilation.ArtifactPath), compilation.RawLog);
            Assert.True(File.Exists(compilation.ManifestPath), compilation.RawLog);
            var verification = Assert.IsType<ArtifactVerificationResult>(compilation.Verification);
            Assert.True(verification.ExpectedExportFound, compilation.RawLog);
            Assert.Equal(ArtifactLoadTestStatus.Passed, verification.LoadTest.Status);
            Assert.True(result.Reproducibility?.Success is true, compilation.RawLog);
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
