using KeyboardStudio.Build;
using KeyboardStudio.Core;
using KeyboardStudio.Testing;
using Xunit;

namespace KeyboardStudio.Build.Tests;

/// <summary>
/// The contract <see cref="BuildOrchestrator"/> guarantees every backend, proven against a stub so
/// it stays independent of what any one target does after validation passes.
/// </summary>
public sealed class BuildOrchestratorTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task BuildAsync_WhenValidationHasError_DoesNotReachTheBackend()
    {
        var backend = new TrackingBackend();
        var orchestrator = CreateOrchestrator(ValidationSeverity.Error, backend);

        var result = await orchestrator.BuildAsync(
            TestProjectFactory.Create(),
            new BuildOptions(BuildTarget.LinuxXkb, "out"));

        Assert.False(result.Success);
        Assert.False(backend.WasCalled);
        Assert.Contains(result.ValidationIssues, issue => issue.Severity == ValidationSeverity.Error);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(ValidationSeverity.Info)]
    [InlineData(ValidationSeverity.Warning)]
    public async Task BuildAsync_WhenValidationIsNonBlocking_ContinuesBuild(
        ValidationSeverity severity)
    {
        var backend = new TrackingBackend();
        var orchestrator = CreateOrchestrator(severity, backend);

        var result = await orchestrator.BuildAsync(
            TestProjectFactory.Create(),
            new BuildOptions(BuildTarget.LinuxXkb, "out"));

        Assert.True(result.Success);
        Assert.True(backend.WasCalled);
        Assert.Contains(result.ValidationIssues, issue => issue.Severity == severity);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task BuildAsync_ReportsTerminalStages()
    {
        var stages = new List<BuildStageProgress>();
        var orchestrator = CreateOrchestrator(ValidationSeverity.Info, new TrackingBackend());

        await orchestrator.BuildAsync(
            TestProjectFactory.Create(),
            new BuildOptions(BuildTarget.LinuxXkb, "out"),
            new DelegateProgress(stages.Add));

        Assert.Contains(
            stages,
            stage => stage.Name == BuildStageNames.Validating &&
                     stage.State == BuildStageState.Completed);
        Assert.Contains(
            stages,
            stage => stage.Name == BuildStageNames.Completed &&
                     stage.State == BuildStageState.Completed);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public async Task BuildAsync_WhenBackendCancels_ReportsCancelledAndRethrows()
    {
        var stages = new List<BuildStageProgress>();
        var orchestrator = CreateOrchestrator(ValidationSeverity.Info, new CancellingBackend());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => orchestrator.BuildAsync(
            TestProjectFactory.Create(),
            new BuildOptions(BuildTarget.LinuxXkb, "out"),
            new DelegateProgress(stages.Add)));

        Assert.Contains(
            stages,
            stage => stage.Name == BuildStageNames.Cancelled &&
                     stage.State == BuildStageState.Cancelled);
    }

    private static BuildOrchestrator CreateOrchestrator(
        ValidationSeverity severity,
        IBuildBackend backend) =>
        new(
            new KeyboardProjectValidator([new StaticValidationRule(severity)]),
            new BuildBackendResolver([backend]));

    private sealed class StaticValidationRule(ValidationSeverity severity) : IKeyboardProjectValidationRule
    {
        public IReadOnlyList<ValidationIssue> Validate(KeyboardProject project) =>
            [new ValidationIssue(severity, "TEST001", "Test diagnostic")];
    }

    private sealed class TrackingBackend : IBuildBackend
    {
        public bool WasCalled { get; private set; }

        public IReadOnlySet<BuildTarget> SupportedTargets { get; } =
            new HashSet<BuildTarget> { BuildTarget.LinuxXkb };

        public BuildEnvironmentStatus GetStatus(BuildTarget target) =>
            new(true, "Available", [], [BuildTarget.LinuxXkb]);

        public Task<KeyboardBuildResult> BuildAsync(
            KeyboardProject project,
            BuildOptions options,
            IProgress<BuildStageProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(new KeyboardBuildResult(
                true,
                [],
                new ArtifactBuildResult(true, "symbols/keyboardstudio", [])));
        }
    }

    private sealed class CancellingBackend : IBuildBackend
    {
        public IReadOnlySet<BuildTarget> SupportedTargets { get; } =
            new HashSet<BuildTarget> { BuildTarget.LinuxXkb };

        public BuildEnvironmentStatus GetStatus(BuildTarget target) =>
            new(true, "Available", [], [BuildTarget.LinuxXkb]);

        public Task<KeyboardBuildResult> BuildAsync(
            KeyboardProject project,
            BuildOptions options,
            IProgress<BuildStageProgress>? progress = null,
            CancellationToken cancellationToken = default) =>
            throw new OperationCanceledException(cancellationToken);
    }

    private sealed class DelegateProgress(Action<BuildStageProgress> report)
        : IProgress<BuildStageProgress>
    {
        public void Report(BuildStageProgress value) => report(value);
    }
}
