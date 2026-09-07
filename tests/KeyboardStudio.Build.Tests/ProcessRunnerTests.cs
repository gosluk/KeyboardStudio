using KeyboardStudio.Build;
using Xunit;

namespace KeyboardStudio.Build.Tests;

public sealed class ProcessRunnerTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task RunAsync_CapturesRequestAndOutput()
    {
        var request = CreateEchoRequest();

        var result = await new ProcessRunner().RunAsync(request);

        Assert.Equal(request.Executable, result.Executable);
        Assert.Equal(request.Arguments, result.Arguments);
        Assert.Equal(request.WorkingDirectory, result.WorkingDirectory);
        Assert.Equal(request.Environment, result.Environment);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("KeyboardStudio process runner", result.StandardOutput, StringComparison.Ordinal);
        Assert.True(result.Duration >= TimeSpan.Zero);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task RunAsync_CancellationTerminatesProcess()
    {
        var request = CreateLongRunningRequest();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new ProcessRunner().RunAsync(request, cancellation.Token));
    }

    private static ProcessRequest CreateEchoRequest() =>
        new(
            "/bin/echo",
            ["KeyboardStudio process runner"],
            Environment.CurrentDirectory,
            new Dictionary<string, string?>());

    private static ProcessRequest CreateLongRunningRequest() =>
        new(
            "/bin/sleep",
            ["30"],
            Environment.CurrentDirectory,
            new Dictionary<string, string?>());
}
