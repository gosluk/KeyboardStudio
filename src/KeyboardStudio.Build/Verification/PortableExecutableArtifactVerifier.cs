using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace KeyboardStudio.Build;

public sealed class PortableExecutableArtifactVerifier : IArtifactVerifier
{
    public const string RequiredExportName = "KbdLayerDescriptor";
    private readonly IArtifactLoadTester _loadTester;

    public PortableExecutableArtifactVerifier()
        : this(new WindowsArtifactLoadTester())
    {
    }

    public PortableExecutableArtifactVerifier(IArtifactLoadTester loadTester)
    {
        ArgumentNullException.ThrowIfNull(loadTester);
        _loadTester = loadTester;
    }

    public async Task<ArtifactVerificationResult> VerifyAsync(
        string artifactPath,
        BuildTarget target,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactPath);
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(artifactPath))
        {
            return Failure(
                target,
                null,
                false,
                false,
                "PE_FILE",
                $"The linked artifact does not exist: {artifactPath}");
        }

        try
        {
            using var stream = new FileStream(
                artifactPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.SequentialScan);
            using var peReader = new PEReader(stream, PEStreamOptions.LeaveOpen);
            var headers = peReader.PEHeaders;
            if (headers.PEHeader is null)
            {
                return Failure(
                    target,
                    headers.CoffHeader.Machine.ToString(),
                    false,
                    false,
                    "PE_HEADER",
                    "The linked artifact does not contain a PE optional header.");
            }

            var actualMachine = headers.CoffHeader.Machine;
            var expectedMachine = GetExpectedMachine(target);
            if (expectedMachine is null)
            {
                return Failure(
                    target,
                    actualMachine.ToString(),
                    false,
                    false,
                    "PE_TARGET",
                    $"PE verification does not support build target '{target}'.");
            }

            var isDll = headers.CoffHeader.Characteristics.HasFlag(Characteristics.Dll);
            var messages = new List<CompilerMessage>();
            if (actualMachine != expectedMachine)
            {
                messages.Add(new CompilerMessage(
                    "PE_ARCH",
                    $"The PE machine '{actualMachine}' does not match target '{target}' ({expectedMachine})."));
            }

            if (!isDll)
            {
                messages.Add(new CompilerMessage(
                    "PE_DLL",
                    "The PE image does not have the DLL characteristic."));
            }

            var expectedExportFound = PortableExecutableExportReader
                .ReadNames(peReader)
                .Contains(RequiredExportName);
            if (!expectedExportFound)
            {
                messages.Add(new CompilerMessage(
                    "PE_EXPORT",
                    $"The PE image does not export '{RequiredExportName}' under the expected name."));
            }

            var loadTest = new ArtifactLoadTestResult(
                ArtifactLoadTestStatus.NotRun,
                "Structural verification failed before the load-level smoke test.");
            if (messages.Count == 0)
            {
                loadTest = await _loadTester.TestAsync(
                    artifactPath,
                    target,
                    RequiredExportName,
                    cancellationToken);
                if (loadTest.Status == ArtifactLoadTestStatus.Failed)
                {
                    messages.Add(new CompilerMessage("PE_LOAD", loadTest.Message));
                }
            }

            return new ArtifactVerificationResult(
                messages.Count == 0,
                target,
                actualMachine.ToString(),
                isDll,
                expectedExportFound,
                loadTest,
                messages);
        }
        catch (Exception exception) when (
            exception is BadImageFormatException or
                IOException or
                UnauthorizedAccessException or
                InvalidOperationException or
                ArgumentOutOfRangeException or
                OverflowException)
        {
            return Failure(
                target,
                null,
                false,
                false,
                "PE_INVALID",
                $"The linked artifact is not a readable PE image: {exception.Message}");
        }
    }

    private static Machine? GetExpectedMachine(BuildTarget target) => target switch
    {
        BuildTarget.WindowsX64 => Machine.Amd64,
        _ => null
    };

    private static ArtifactVerificationResult Failure(
        BuildTarget target,
        string? machine,
        bool isDll,
        bool expectedExportFound,
        string code,
        string message) =>
        new(
            false,
            target,
            machine,
            isDll,
            expectedExportFound,
            new ArtifactLoadTestResult(
                ArtifactLoadTestStatus.NotRun,
                "Structural verification failed before the load-level smoke test."),
            [new CompilerMessage(code, message)]);
}
