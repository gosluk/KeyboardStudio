using KeyboardStudio.Build;
using Xunit;

namespace KeyboardStudio.Windows.Tests;

public sealed class ProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_CapturesRequestAndOutput()
    {
        var request = CreateEchoRequest();

        var result = await new ProcessRunner().RunAsync(request);

        Assert.Equal(request.Executable, result.Executable);
        Assert.Equal(request.Arguments, result.Arguments);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("KeyboardStudio process runner", result.StandardOutput, StringComparison.Ordinal);
        Assert.True(result.Duration >= TimeSpan.Zero);
    }

    [Fact]
    public async Task RunAsync_CancellationTerminatesProcess()
    {
        var request = CreateLongRunningRequest();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new ProcessRunner().RunAsync(request, cancellation.Token));
    }

    private static ProcessRequest CreateEchoRequest() => OperatingSystem.IsWindows()
        ? new ProcessRequest(
            "cmd.exe",
            ["/d", "/c", "echo", "KeyboardStudio process runner"],
            Environment.CurrentDirectory,
            new Dictionary<string, string?>())
        : new ProcessRequest(
            "/bin/echo",
            ["KeyboardStudio process runner"],
            Environment.CurrentDirectory,
            new Dictionary<string, string?>());

    private static ProcessRequest CreateLongRunningRequest() => OperatingSystem.IsWindows()
        ? new ProcessRequest(
            "ping.exe",
            ["-n", "30", "127.0.0.1"],
            Environment.CurrentDirectory,
            new Dictionary<string, string?>())
        : new ProcessRequest(
            "/bin/sleep",
            ["30"],
            Environment.CurrentDirectory,
            new Dictionary<string, string?>());
}
