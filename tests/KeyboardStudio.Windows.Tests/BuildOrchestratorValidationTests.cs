using KeyboardStudio.Build;
using KeyboardStudio.Core;
using Xunit;

namespace KeyboardStudio.Windows.Tests;

public sealed class BuildOrchestratorValidationTests
{
    [Fact]
    public async Task BuildAsync_WhenValidationHasError_DoesNotGenerateOrCompile()
    {
        var generator = new TrackingArtifactGenerator();
        var compiler = new TrackingNativeCompiler();
        var orchestrator = CreateOrchestrator(ValidationSeverity.Error, generator, compiler);

        var result = await orchestrator.BuildAsync(
            DemoProjectFactory.Create(),
            new BuildOptions(BuildTarget.WindowsX64, "out"));

        Assert.False(result.Success);
        Assert.False(generator.WasCalled);
        Assert.False(compiler.WasCalled);
        Assert.Contains(result.ValidationIssues, issue => issue.Severity == ValidationSeverity.Error);
    }

    [Theory]
    [InlineData(ValidationSeverity.Info)]
    [InlineData(ValidationSeverity.Warning)]
    public async Task BuildAsync_WhenValidationIsNonBlocking_ContinuesBuild(
        ValidationSeverity severity)
    {
        var generator = new TrackingArtifactGenerator();
        var compiler = new TrackingNativeCompiler();
        var orchestrator = CreateOrchestrator(severity, generator, compiler);

        var result = await orchestrator.BuildAsync(
            DemoProjectFactory.Create(),
            new BuildOptions(BuildTarget.WindowsX64, "out"));

        Assert.True(result.Success);
        Assert.True(generator.WasCalled);
        Assert.True(compiler.WasCalled);
        Assert.Contains(result.ValidationIssues, issue => issue.Severity == severity);
    }

    private static BuildOrchestrator CreateOrchestrator(
        ValidationSeverity severity,
        TrackingArtifactGenerator generator,
        TrackingNativeCompiler compiler) =>
        new(
            new KeyboardProjectValidator([new StaticValidationRule(severity)]),
            generator,
            new AvailableBuildEnvironment(),
            compiler);

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
    }

    private sealed class TrackingNativeCompiler : INativeCompiler
    {
        public bool WasCalled { get; private set; }

        public Task<CompilationResult> CompileAsync(
            GeneratedSource source,
            BuildTarget target,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(new CompilationResult(true, "keyboard.dll", []));
        }
    }
}
