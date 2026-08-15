using KeyboardStudio.Build;
using KeyboardStudio.Core;
using Xunit;

namespace KeyboardStudio.Windows.Tests;

public sealed class BuildOrchestratorValidationTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task BuildAsync_WhenValidationHasError_DoesNotGenerateOrCompile()
    {
        var generator = new TrackingArtifactGenerator();
        var compiler = new TrackingNativeCompiler();
        var manifestWriter = new TrackingBuildManifestWriter();
        var orchestrator = CreateOrchestrator(
            ValidationSeverity.Error,
            generator,
            compiler,
            manifestWriter);

        var result = await orchestrator.BuildAsync(
            DemoProjectFactory.Create(),
            new BuildOptions(BuildTarget.WindowsX64, "out"));

        Assert.False(result.Success);
        Assert.False(generator.WasCalled);
        Assert.False(compiler.WasCalled);
        Assert.False(manifestWriter.WasCalled);
        Assert.Contains(result.ValidationIssues, issue => issue.Severity == ValidationSeverity.Error);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(ValidationSeverity.Info)]
    [InlineData(ValidationSeverity.Warning)]
    public async Task BuildAsync_WhenValidationIsNonBlocking_ContinuesBuild(
        ValidationSeverity severity)
    {
        var generator = new TrackingArtifactGenerator();
        var compiler = new TrackingNativeCompiler();
        var manifestWriter = new TrackingBuildManifestWriter();
        var orchestrator = CreateOrchestrator(severity, generator, compiler, manifestWriter);

        var result = await orchestrator.BuildAsync(
            DemoProjectFactory.Create(),
            new BuildOptions(BuildTarget.WindowsX64, "out"));

        Assert.True(result.Success);
        Assert.True(generator.WasCalled);
        Assert.True(compiler.WasCalled);
        Assert.True(manifestWriter.WasCalled);
        Assert.NotNull(result.Compilation?.Manifest);
        Assert.Contains(result.Artifact?.GeneratedFiles ?? [], file => file.Name == "keyboard.c");
        Assert.Contains(result.ValidationIssues, issue => issue.Severity == severity);
    }

    private static BuildOrchestrator CreateOrchestrator(
        ValidationSeverity severity,
        TrackingArtifactGenerator generator,
        TrackingNativeCompiler compiler,
        TrackingBuildManifestWriter manifestWriter) =>
        new(
            new KeyboardProjectValidator([new StaticValidationRule(severity)]),
            new BuildBackendResolver([
                new WindowsBuildBackend(
                    generator,
                    new AvailableBuildEnvironment(),
                    compiler,
                    manifestWriter)
            ]));

    private sealed class StaticValidationRule(ValidationSeverity severity) : IKeyboardProjectValidationRule
    {
        public IReadOnlyList<ValidationIssue> Validate(KeyboardProject project) =>
            [new ValidationIssue(severity, "TEST001", "Test diagnostic")];
    }

    private sealed class TrackingArtifactGenerator : IArtifactGenerator
    {
        public bool WasCalled { get; private set; }

        public Task<GeneratedArtifact> GenerateAsync(
            KeyboardProject project,
            BuildOptions options,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(new GeneratedArtifact(new GeneratedSource(
                new Dictionary<string, string> { ["keyboard.c"] = string.Empty })));
        }
    }

    private sealed class AvailableBuildEnvironment : IBuildEnvironment
    {
        public bool CanBuild(BuildTarget target) => true;

        public BuildEnvironmentStatus GetStatus(BuildTarget target) =>
            new(true, "Available", [], [BuildTarget.WindowsX64]);

        public ResolvedBuildEnvironment? Resolve(BuildTarget target) => null;
    }

    private sealed class TrackingNativeCompiler : INativeCompiler
    {
        public bool WasCalled { get; private set; }

        public Task<CompilationResult> CompileAsync(
            GeneratedArtifact artifact,
            BuildOptions options,
            IProgress<BuildStageProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(new CompilationResult(true, "keyboard.dll", []));
        }
    }

    private sealed class TrackingBuildManifestWriter : IBuildManifestWriter
    {
        public bool WasCalled { get; private set; }

        public Task<BuildManifestWriteResult> WriteAsync(
            KeyboardProject project,
            GeneratedArtifact generatedArtifact,
            BuildOptions options,
            CompilationResult compilation,
            BuildReproducibilityResult? reproducibility,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
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
                    : new BuildReproducibilityManifest(true, true, true, "hash", "hash"),
                DateTimeOffset.UnixEpoch);
            return Task.FromResult(new BuildManifestWriteResult(manifest, "build-manifest.json"));
        }
    }
}
