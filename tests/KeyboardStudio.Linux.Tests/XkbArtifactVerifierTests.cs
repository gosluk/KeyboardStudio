using KeyboardStudio.Build;
using KeyboardStudio.Linux;
using Xunit;

namespace KeyboardStudio.Linux.Tests;

public sealed class XkbArtifactVerifierTests
{
    [Fact]
    public async Task VerifyAsync_WhenXkbCliIsMissing_ReturnsUnverifiedWarning()
    {
        var verifier = new XkbArtifactVerifier(
            new XkbManagedValidator(),
            new StaticLocator(null),
            new QueueProcessRunner([]));
        var (layout, generated) = CreateArtifact();

        var result = await verifier.VerifyAsync(layout, generated, "/tmp/xkb", false);

        Assert.Equal(XkbVerificationStatus.Unverified, result.Status);
        Assert.True(result.ManagedValidationPassed);
        Assert.Equal(BuildDiagnosticSeverity.Warning, Assert.Single(result.Diagnostics).Severity);
    }

    [Fact]
    public async Task VerifyAsync_WhenXkbCliSucceeds_CapturesInvocationAndVersion()
    {
        var root = Path.Combine(Path.GetTempPath(), $"KeyboardStudio-Verify-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var runner = new QueueProcessRunner([
                CreateProcessResult(["--version"], "xkbcli 1.9.0\n", "", 0),
                CreateProcessResult(["compile-keymap"], "", "", 0)
            ]);
            var verifier = new XkbArtifactVerifier(
                new XkbManagedValidator(),
                new StaticLocator("/usr/bin/xkbcli"),
                runner);
            var (layout, generated) = CreateArtifact();

            var result = await verifier.VerifyAsync(layout, generated, root, true);

            Assert.Equal(XkbVerificationStatus.Verified, result.Status);
            Assert.Equal("xkbcli 1.9.0", result.ToolVersion);
            Assert.Contains("--include-defaults", result.Arguments);
            Assert.Contains("--test", result.Arguments);
            Assert.True(File.Exists(result.LogPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task VerifyAsync_WhenXkbCliPredatesTestFlag_CompilesWithoutTestArgument()
    {
        var root = Path.Combine(Path.GetTempPath(), $"KeyboardStudio-Verify-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var runner = new QueueProcessRunner([
                CreateProcessResult(["--version"], "1.6.0\n", "", 0),
                CreateProcessResult(["compile-keymap"], "compiled keymap", "", 0)
            ]);
            var verifier = new XkbArtifactVerifier(
                new XkbManagedValidator(),
                new StaticLocator("/usr/bin/xkbcli"),
                runner);
            var (layout, generated) = CreateArtifact();

            var result = await verifier.VerifyAsync(layout, generated, root, true);

            Assert.Equal(XkbVerificationStatus.Verified, result.Status);
            Assert.DoesNotContain("--test", result.Arguments);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static (XkbKeyboardLayout Layout, XkbGeneratedSymbols Generated) CreateArtifact()
    {
        var layout = new XkbKeyboardLayout(
            new XkbLayoutMetadata("demo", "basic", "Demo"),
            [new XkbKeyMapping("KeyA", "<AC01>", XkbKeyType.Alphabetic, ["a", "A"])],
            false);
        return (layout, new XkbSymbolsGenerator().Generate(layout));
    }

    private static ProcessResult CreateProcessResult(
        IReadOnlyList<string> arguments,
        string stdout,
        string stderr,
        int exitCode) =>
        new(
            "/usr/bin/xkbcli",
            arguments,
            stdout,
            stderr,
            exitCode,
            TimeSpan.FromMilliseconds(5),
            "/tmp/xkb",
            new Dictionary<string, string?>());

    private sealed class StaticLocator(string? path) : IXkbCliLocator
    {
        public string? Find() => path;
    }

    private sealed class QueueProcessRunner(IEnumerable<ProcessResult> results) : IProcessRunner
    {
        private readonly Queue<ProcessResult> _results = new(results);

        public Task<ProcessResult> RunAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_results.Dequeue() with
            {
                Executable = request.Executable,
                Arguments = request.Arguments,
                WorkingDirectory = request.WorkingDirectory
            });
    }
}
