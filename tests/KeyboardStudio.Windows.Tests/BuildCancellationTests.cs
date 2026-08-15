using KeyboardStudio.Build;
using Xunit;

namespace KeyboardStudio.Windows.Tests;

public sealed class BuildCancellationTests
{
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public async Task CompileAsync_WhenCancelled_PreservesDiagnosticWorkspaceByDefault()
    {
        var buildRoot = Path.Combine(Path.GetTempPath(), $"KeyboardStudio-{Guid.NewGuid():N}");
        try
        {
            var compiler = new MsvcKeyboardCompiler(new AvailableEnvironment(), new CancellingRunner());

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => compiler.CompileAsync(
                CreateArtifact(),
                new BuildOptions(BuildTarget.WindowsX64, buildRoot)));

            var workspace = Assert.Single(Directory.EnumerateDirectories(buildRoot));
            Assert.True(File.Exists(Path.Combine(workspace, "logs", "cancellation.log")));
            Assert.True(Directory.Exists(Path.Combine(workspace, "generated")));
        }
        finally
        {
            if (Directory.Exists(buildRoot))
            {
                Directory.Delete(buildRoot, recursive: true);
            }
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CompileAsync_WhenCancelledAndDeletionRequested_RemovesWorkspace()
    {
        var buildRoot = Path.Combine(Path.GetTempPath(), $"KeyboardStudio-{Guid.NewGuid():N}");
        try
        {
            var compiler = new MsvcKeyboardCompiler(new AvailableEnvironment(), new CancellingRunner());

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => compiler.CompileAsync(
                CreateArtifact(),
                new BuildOptions(
                    BuildTarget.WindowsX64,
                    buildRoot,
                    BuildCleanupPolicy.DeleteFailedBuild)));

            Assert.Empty(Directory.EnumerateDirectories(buildRoot));
        }
        finally
        {
            if (Directory.Exists(buildRoot))
            {
                Directory.Delete(buildRoot, recursive: true);
            }
        }
    }

    private static GeneratedArtifact CreateArtifact() =>
        new(new GeneratedSource(new Dictionary<string, string>
        {
            ["keyboard.c"] = "/* source */\n",
            ["keyboard.def"] = "EXPORTS\n",
            ["keyboard.rc"] = "1 VERSIONINFO\n"
        }));

    private sealed class AvailableEnvironment : IBuildEnvironment
    {
        public bool CanBuild(BuildTarget target) => target == BuildTarget.WindowsX64;

        public BuildEnvironmentStatus GetStatus(BuildTarget target) =>
            new(true, "Available", [], [BuildTarget.WindowsX64]);

        public ResolvedBuildEnvironment? Resolve(BuildTarget target) =>
            target == BuildTarget.WindowsX64
                ? new ResolvedBuildEnvironment(
                    target,
                    "cl.exe",
                    "link.exe",
                    "rc.exe",
                    ["include"],
                    ["lib"],
                    "14.50",
                    "10.0")
                : null;
    }

    private sealed class CancellingRunner : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default) =>
            throw new OperationCanceledException(cancellationToken);
    }
}
