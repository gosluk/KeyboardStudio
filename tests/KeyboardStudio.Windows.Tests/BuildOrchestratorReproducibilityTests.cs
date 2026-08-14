using KeyboardStudio.Build;
using KeyboardStudio.Core;
using Xunit;

namespace KeyboardStudio.Windows.Tests;

public sealed class BuildOrchestratorReproducibilityTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BuildAsync_WhenReproducibilityIsRequested_BuildsTwiceAndReportsResult(
        bool reproducible)
    {
        var generator = new CountingArtifactGenerator();
        var compiler = new CountingNativeCompiler();
        var checker = new StaticReproducibilityChecker(reproducible);
        var orchestrator = new BuildOrchestrator(
            new KeyboardProjectValidator([]),
            generator,
            new AvailableBuildEnvironment(),
            compiler,
            new StaticManifestWriter(),
            checker);

        var result = await orchestrator.BuildAsync(
            DemoProjectFactory.Create(),
            new BuildOptions(
                BuildTarget.WindowsX64,
                "out",
                VerifyReproducibility: true));

        Assert.Equal(2, generator.CallCount);
        Assert.Equal(2, compiler.CallCount);
        Assert.True(checker.WasCalled);
        Assert.Equal(reproducible, result.Success);
        Assert.Equal(reproducible, result.Reproducibility?.Success);
        Assert.Equal(!reproducible, result.Compilation?.Messages.Any(
            message => message.Code == "REPRO_BINARY"));
    }

    private sealed class CountingArtifactGenerator : IArtifactGenerator
    {
        public int CallCount { get; private set; }

        public Task<GeneratedArtifact> GenerateAsync(
            KeyboardProject project,
            BuildOptions options,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new GeneratedArtifact(new GeneratedSource(
                new Dictionary<string, string> { ["keyboard.c"] = "source" })));
        }
    }

    private sealed class CountingNativeCompiler : INativeCompiler
    {
        public int CallCount { get; private set; }

        public Task<CompilationResult> CompileAsync(
            GeneratedArtifact artifact,
            BuildOptions options,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new CompilationResult(
                true,
                $"keyboard-{CallCount}.dll",
                [],
                WorkspacePath: $"workspace-{CallCount}"));
        }
    }

    private sealed class AvailableBuildEnvironment : IBuildEnvironment
    {
        public bool CanBuild(BuildTarget target) => true;

        public BuildEnvironmentStatus GetStatus(BuildTarget target) =>
            new(true, "Available", [], [BuildTarget.WindowsX64]);

        public ResolvedBuildEnvironment? Resolve(BuildTarget target) => null;
    }

    private sealed class StaticManifestWriter : IBuildManifestWriter
    {
        public Task<BuildManifestWriteResult> WriteAsync(
            KeyboardProject project,
            GeneratedArtifact generatedArtifact,
            BuildOptions options,
            CompilationResult compilation,
            BuildReproducibilityResult? reproducibility,
            CancellationToken cancellationToken = default)
        {
            var manifest = new BuildManifest(
                1,
                project.Metadata.Name,
                options.Target,
                [],
                new BuildToolchainVersions("test", "test"),
                new BuildManifestFile("keyboard.dll", "hash"),
                new BuildVerificationManifest(
                    "Amd64",
                    true,
                    true,
                    ArtifactLoadTestStatus.NotRun),
                reproducibility is null
                    ? null
                    : new BuildReproducibilityManifest(
                        reproducibility.Success,
                        reproducibility.GeneratedSourcesMatch,
                        reproducibility.BinaryOutputsMatch,
                        reproducibility.FirstArtifactSha256,
                        reproducibility.SecondArtifactSha256),
                DateTimeOffset.UnixEpoch);
            return Task.FromResult(new BuildManifestWriteResult(manifest, "build-manifest.json"));
        }
    }

    private sealed class StaticReproducibilityChecker(bool reproducible)
        : IBuildReproducibilityChecker
    {
        public bool WasCalled { get; private set; }

        public Task<BuildReproducibilityResult> CompareAsync(
            GeneratedArtifact firstGeneratedArtifact,
            string firstArtifactPath,
            GeneratedArtifact secondGeneratedArtifact,
            string secondArtifactPath,
            string? comparisonWorkspacePath,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            IReadOnlyList<CompilerMessage> messages = reproducible
                ? []
                : [new CompilerMessage("REPRO_BINARY", "The DLL hashes differ.")];
            return Task.FromResult(new BuildReproducibilityResult(
                reproducible,
                true,
                reproducible,
                "first-hash",
                reproducible ? "first-hash" : "second-hash",
                comparisonWorkspacePath,
                messages));
        }
    }
}
