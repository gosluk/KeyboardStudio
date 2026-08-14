using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace KeyboardStudio.Build;

public sealed class PortableExecutableArtifactVerifier : IArtifactVerifier
{
    public Task<ArtifactVerificationResult> VerifyAsync(
        string artifactPath,
        BuildTarget target,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactPath);
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(artifactPath))
        {
            return Task.FromResult(Failure(
                target,
                null,
                false,
                "PE_FILE",
                $"The linked artifact does not exist: {artifactPath}"));
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
                return Task.FromResult(Failure(
                    target,
                    headers.CoffHeader.Machine.ToString(),
                    false,
                    "PE_HEADER",
                    "The linked artifact does not contain a PE optional header."));
            }

            var actualMachine = headers.CoffHeader.Machine;
            var expectedMachine = GetExpectedMachine(target);
            if (expectedMachine is null)
            {
                return Task.FromResult(Failure(
                    target,
                    actualMachine.ToString(),
                    false,
                    "PE_TARGET",
                    $"PE verification does not support build target '{target}'."));
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

            return Task.FromResult(new ArtifactVerificationResult(
                messages.Count == 0,
                target,
                actualMachine.ToString(),
                isDll,
                messages));
        }
        catch (Exception exception) when (
            exception is BadImageFormatException or IOException or UnauthorizedAccessException)
        {
            return Task.FromResult(Failure(
                target,
                null,
                false,
                "PE_INVALID",
                $"The linked artifact is not a readable PE image: {exception.Message}"));
        }
    }

    private static Machine? GetExpectedMachine(BuildTarget target) => target switch
    {
        BuildTarget.WindowsX64 => Machine.Amd64,
        BuildTarget.WindowsArm64 => Machine.Arm64,
        _ => null
    };

    private static ArtifactVerificationResult Failure(
        BuildTarget target,
        string? machine,
        bool isDll,
        string code,
        string message) =>
        new(false, target, machine, isDll, [new CompilerMessage(code, message)]);
}
