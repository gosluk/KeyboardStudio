using KeyboardStudio.Build;
using KeyboardStudio.Linux;
using Xunit;

namespace KeyboardStudio.Linux.Tests;

public sealed class XkbUserBundleVerifierTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task VerifyAsync_CompilesCustomBaseAndUnrelatedVariantAndChecksRegistry()
    {
        var (output, root, metadata) = await StageAsync();
        try
        {
            var runner = new RecordingRunner(request => request.Arguments[0] == "list"
                ? Result(request, "layouts:\n- layout: pl\n  variant: keyboardstudio_programmer\n", "", 0)
                : Result(request, "", "", 0));
            var verifier = new XkbUserBundleVerifier(runner, PolishRegistry());

            var result = await verifier.VerifyAsync(root, [metadata], Capability());

            Assert.Equal(XkbUserBundleVerificationStatus.Verified, result.Status);
            Assert.Equal(4, result.Checks.Count);
            Assert.Contains(result.Checks, check =>
                check.Kind == XkbUserBundleVerificationCheckKind.CustomVariant &&
                check.LayoutId == "pl" && check.VariantId == "keyboardstudio_programmer");
            Assert.Contains(result.Checks, check =>
                check.Kind == XkbUserBundleVerificationCheckKind.BaseVariant &&
                check.VariantId == "qwertz");
            Assert.Contains(result.Checks, check =>
                check.Kind == XkbUserBundleVerificationCheckKind.UnrelatedVariant &&
                check.VariantId == "dvorak");
            Assert.Contains(result.Checks, check =>
                check.Kind == XkbUserBundleVerificationCheckKind.RegistryDiscovery && check.Success);
            var compile = runner.Requests.First(request => request.Arguments[0] == "compile-keymap");
            Assert.Equal(
                ["compile-keymap", "--include", root, "--include-defaults", "--test"],
                compile.Arguments.Take(5));
            var registry = runner.Requests.Single(request => request.Arguments[0] == "list");
            Assert.Equal(
                ["list", "--ruleset=evdev", "--skip-default-paths", root, "/usr/share/X11/xkb"],
                registry.Arguments);
            Assert.Empty(result.Diagnostics);
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public async Task VerifyAsync_WhenCustomVariantFails_ReturnsStructuredFailure()
    {
        var (output, root, metadata) = await StageAsync();
        try
        {
            var runner = new RecordingRunner(request =>
            {
                var custom = request.Arguments.Contains("keyboardstudio_programmer");
                return Result(request, "", custom ? "syntax error" : "", custom ? 1 : 0);
            });
            var verifier = new XkbUserBundleVerifier(runner, PolishRegistry());

            var result = await verifier.VerifyAsync(
                root,
                [metadata],
                Capability(registry: XkbRegistryDiscoverySupport.Unavailable));

            Assert.Equal(XkbUserBundleVerificationStatus.Failed, result.Status);
            var failed = Assert.Single(result.Checks, check => !check.Success);
            Assert.Equal(XkbUserBundleVerificationCheckKind.CustomVariant, failed.Kind);
            Assert.Equal("syntax error", failed.StandardError);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "KSV005");
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public async Task VerifyAsync_WhenBaseSectionDisappeared_ReturnsStructuredFailure()
    {
        var (output, root, metadata) = await StageAsync();
        try
        {
            var runner = new RecordingRunner(request =>
            {
                var baseVariant = request.Arguments.Contains("qwertz") &&
                    !request.Arguments.Contains("keyboardstudio_programmer");
                return Result(request, "", baseVariant ? "base section missing" : "", baseVariant ? 1 : 0);
            });
            var verifier = new XkbUserBundleVerifier(runner, PolishRegistry());

            var result = await verifier.VerifyAsync(
                root,
                [metadata],
                Capability(registry: XkbRegistryDiscoverySupport.Unavailable));

            Assert.Equal(XkbUserBundleVerificationStatus.Failed, result.Status);
            var failed = Assert.Single(result.Checks, check => !check.Success);
            Assert.Equal(XkbUserBundleVerificationCheckKind.BaseVariant, failed.Kind);
            Assert.Equal("base section missing", failed.StandardError);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "KSV005");
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task VerifyAsync_WithoutRegistryTooling_VerifiesCompilationWithWarning()
    {
        var (output, root, metadata) = await StageAsync();
        try
        {
            var runner = new RecordingRunner(request => Result(request, "", "", 0));
            var verifier = new XkbUserBundleVerifier(runner, PolishRegistry());

            var result = await verifier.VerifyAsync(
                root,
                [metadata],
                Capability(registry: XkbRegistryDiscoverySupport.Unavailable));

            Assert.Equal(XkbUserBundleVerificationStatus.VerifiedWithWarnings, result.Status);
            Assert.DoesNotContain(result.Checks, check =>
                check.Kind == XkbUserBundleVerificationCheckKind.RegistryDiscovery);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "KSV006");
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public async Task VerifyAsync_WhenRequiredBundleFileIsMissing_FailsBeforeStartingXkbCli()
    {
        var (output, root, metadata) = await StageAsync();
        try
        {
            File.Delete(Path.Combine(root, "symbols", "pl"));
            var runner = new RecordingRunner(request => Result(request, "", "", 0));
            var verifier = new XkbUserBundleVerifier(runner, PolishRegistry());

            var result = await verifier.VerifyAsync(root, [metadata], Capability());

            Assert.Equal(XkbUserBundleVerificationStatus.Failed, result.Status);
            Assert.Empty(runner.Requests);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "KSV001");
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public async Task VerifyAsync_WhenNoUnrelatedVariantExists_FailsTheShadowingProof()
    {
        var (output, root, metadata) = await StageAsync();
        try
        {
            var registry = new StaticRegistryReader(
            [
                new XkbRegistryEntry("pl", "qwertz", "Polish QWERTZ", null, [], [])
            ]);
            var runner = new RecordingRunner(request => Result(request, "", "", 0));

            var result = await new XkbUserBundleVerifier(runner, registry)
                .VerifyAsync(
                    root,
                    [metadata],
                    Capability(registry: XkbRegistryDiscoverySupport.Unavailable));

            Assert.Equal(XkbUserBundleVerificationStatus.Failed, result.Status);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "KSV004");
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task VerifyBaseAsync_AfterUninstall_CompilesBaseAndUnrelatedWithoutCustomRegistryCheck()
    {
        var (output, root, metadata) = await StageAsync();
        try
        {
            File.Delete(Path.Combine(root, "symbols", "keyboardstudio"));
            File.Delete(Path.Combine(root, "symbols", "pl"));
            File.Delete(Path.Combine(root, "rules", "evdev.xml"));
            var runner = new RecordingRunner(request => Result(request, "", "", 0));

            var result = await new XkbUserBundleVerifier(runner, PolishRegistry())
                .VerifyBaseAsync(root, metadata, Capability());

            Assert.Equal(XkbUserBundleVerificationStatus.Verified, result.Status);
            Assert.Equal(2, result.Checks.Count);
            Assert.Contains(result.Checks, check =>
                check.Kind == XkbUserBundleVerificationCheckKind.BaseVariant &&
                check.VariantId == "qwertz");
            Assert.Contains(result.Checks, check =>
                check.Kind == XkbUserBundleVerificationCheckKind.UnrelatedVariant &&
                check.VariantId == "dvorak");
            Assert.DoesNotContain(result.Checks, check =>
                check.Kind is XkbUserBundleVerificationCheckKind.CustomVariant or
                    XkbUserBundleVerificationCheckKind.RegistryDiscovery);
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }

    private static async Task<(string Output, string Root, XkbUserVariantMetadata Metadata)> StageAsync()
    {
        var output = Path.Combine(Path.GetTempPath(), $"keyboardstudio-verify-{Guid.NewGuid():N}");
        var metadata = new XkbUserVariantMetadata(
            "7c31d5f2a19e40a4b0ef64f01a295135",
            "pl",
            "qwertz",
            "qwertz",
            "keyboardstudio_programmer",
            "Polish - KeyboardStudio");
        var layout = new XkbUserVariantLayout(
            metadata,
            [
                new XkbUserVariantKeyMapping(
                    "KeyA",
                    "<AC01>",
                    XkbKeyType.Alphabetic,
                    ["x", "X"])
            ],
            UsesLevelThree: false);
        var bundle = XkbUserBundleGenerator.Generate([layout]).Bundle!;
        var write = await new XkbUserBundleWriter().WriteAsync(bundle, output);
        return (output, write.BundleRoot, metadata);
    }

    private static XkbUserInstallCapability Capability(
        XkbRegistryDiscoverySupport registry = XkbRegistryDiscoverySupport.Available) =>
        new(
            XkbUserInstallMode.ManagedInstallation,
            XkbSessionType.Wayland,
            "/home/test/.config/xkb",
            "/home/test/.local/state/keyboardstudio/xkb",
            PathsAreSafe: true,
            "/usr/bin/xkbcli",
            "xkbcli 1.13.1",
            new Version(1, 13, 1),
            MeetsRecommendedVersion: true,
            "/usr/share/X11/xkb",
            registry,
            []);

    private static StaticRegistryReader PolishRegistry() =>
        new(
        [
            new XkbRegistryEntry("pl", null, "Polish", null, [], []),
            new XkbRegistryEntry("pl", "qwertz", "Polish QWERTZ", null, [], []),
            new XkbRegistryEntry("pl", "dvorak", "Polish Dvorak", null, [], [])
        ]);

    private static ProcessResult Result(
        ProcessRequest request,
        string stdout,
        string stderr,
        int exitCode) =>
        new(
            request.Executable,
            request.Arguments,
            stdout,
            stderr,
            exitCode,
            TimeSpan.FromMilliseconds(2),
            request.WorkingDirectory,
            request.Environment);

    private sealed class StaticRegistryReader(IReadOnlyList<XkbRegistryEntry> entries)
        : IXkbLayoutRegistryReader
    {
        public IReadOnlyList<XkbRegistryEntry> Read(XkbDataRoot root) => entries;
    }

    private sealed class RecordingRunner(Func<ProcessRequest, ProcessResult> handler) : IProcessRunner
    {
        public List<ProcessRequest> Requests { get; } = [];

        public Task<ProcessResult> RunAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(handler(request));
        }
    }
}
