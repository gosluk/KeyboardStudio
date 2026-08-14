using System.Globalization;
using System.Text;
using KeyboardStudio.Build;

namespace KeyboardStudio.Linux;

public sealed class XkbArtifactVerifier : IXkbArtifactVerifier
{
    private readonly IXkbManagedValidator _managedValidator;
    private readonly IXkbCliLocator _locator;
    private readonly IProcessRunner _processRunner;

    public XkbArtifactVerifier()
        : this(new XkbManagedValidator(), new PathXkbCliLocator(), new ProcessRunner())
    {
    }

    public XkbArtifactVerifier(
        IXkbManagedValidator managedValidator,
        IXkbCliLocator locator,
        IProcessRunner processRunner)
    {
        _managedValidator = managedValidator ?? throw new ArgumentNullException(nameof(managedValidator));
        _locator = locator ?? throw new ArgumentNullException(nameof(locator));
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
    }

    public async Task<XkbVerificationResult> VerifyAsync(
        XkbKeyboardLayout layout,
        XkbGeneratedSymbols generated,
        string xkbRoot,
        bool requireExternalVerification,
        CancellationToken cancellationToken = default)
    {
        var managedDiagnostics = _managedValidator.Validate(layout, generated);
        if (managedDiagnostics.Count > 0)
        {
            return new XkbVerificationResult(
                XkbVerificationStatus.Failed,
                false,
                null,
                null,
                [],
                "",
                "",
                null,
                TimeSpan.Zero,
                null,
                managedDiagnostics.Select(ToError).ToArray());
        }

        var executable = _locator.Find();
        if (executable is null)
        {
            var severity = requireExternalVerification
                ? BuildDiagnosticSeverity.Error
                : BuildDiagnosticSeverity.Warning;
            return new XkbVerificationResult(
                requireExternalVerification ? XkbVerificationStatus.Failed : XkbVerificationStatus.Unverified,
                true,
                null,
                null,
                [],
                "",
                "",
                null,
                TimeSpan.Zero,
                null,
                [new BuildArtifactDiagnostic(
                    severity,
                    "KSL004",
                    "xkbcli was not found; the symbols file passed managed validation but was not externally compiled.")]);
        }

        var versionResult = await _processRunner.RunAsync(
            new ProcessRequest(
                executable,
                ["--version"],
                xkbRoot,
                new Dictionary<string, string?>()),
            cancellationToken);
        var version = SelectVersion(versionResult);
        var arguments = new[]
        {
            "compile-keymap",
            "--include", xkbRoot,
            "--include-defaults",
            "--test",
            "--layout", layout.Metadata.LayoutId,
            "--variant", layout.Metadata.SectionId
        };
        var result = await _processRunner.RunAsync(
            new ProcessRequest(
                executable,
                arguments,
                xkbRoot,
                new Dictionary<string, string?>()),
            cancellationToken);
        var logPath = await WriteLogAsync(xkbRoot, versionResult, result, cancellationToken);
        var success = result.ExitCode == 0;
        return new XkbVerificationResult(
            success ? XkbVerificationStatus.Verified : XkbVerificationStatus.Failed,
            true,
            executable,
            version,
            arguments,
            result.StandardOutput,
            result.StandardError,
            result.ExitCode,
            result.Duration,
            logPath,
            success
                ? []
                : [new BuildArtifactDiagnostic(
                    BuildDiagnosticSeverity.Error,
                    "KSL005",
                    $"xkbcli rejected the generated symbols component with exit code {result.ExitCode}.")]);
    }

    private static BuildArtifactDiagnostic ToError(XkbDiagnostic diagnostic) =>
        new(BuildDiagnosticSeverity.Error, diagnostic.Code, diagnostic.Message, diagnostic.KeyId);

    private static string SelectVersion(ProcessResult result)
    {
        var value = string.IsNullOrWhiteSpace(result.StandardOutput)
            ? result.StandardError
            : result.StandardOutput;
        return value.Trim();
    }

    private static async Task<string> WriteLogAsync(
        string xkbRoot,
        ProcessResult version,
        ProcessResult verification,
        CancellationToken cancellationToken)
    {
        var logsDirectory = Path.Combine(xkbRoot, "logs");
        Directory.CreateDirectory(logsDirectory);
        var logPath = Path.Combine(logsDirectory, "xkbcli.log");
        var builder = new StringBuilder()
            .Append("$ ").Append(version.Executable).AppendLine(" --version")
            .AppendLine(version.StandardOutput.TrimEnd())
            .AppendLine(version.StandardError.TrimEnd())
            .Append("$ ").Append(verification.Executable).Append(' ')
            .AppendJoin(" ", verification.Arguments).AppendLine()
            .AppendLine("[stdout]").AppendLine(verification.StandardOutput.TrimEnd())
            .AppendLine("[stderr]").AppendLine(verification.StandardError.TrimEnd())
            .Append("[exit ").Append(verification.ExitCode.ToString(CultureInfo.InvariantCulture))
            .Append(", duration ")
            .Append(verification.Duration.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture))
            .AppendLine(" ms]");
        await File.WriteAllTextAsync(logPath, builder.ToString(), cancellationToken);
        return logPath;
    }
}
