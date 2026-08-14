using System.Runtime.InteropServices;

namespace KeyboardStudio.Build;

public sealed class WindowsArtifactLoadTester : IArtifactLoadTester
{
    public Task<ArtifactLoadTestResult> TestAsync(
        string artifactPath,
        BuildTarget target,
        string exportName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(exportName);
        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult(new ArtifactLoadTestResult(
                ArtifactLoadTestStatus.NotRun,
                "The load-level smoke test only runs on Windows."));
        }

        if (!CanLoadInCurrentProcess(target))
        {
            return Task.FromResult(new ArtifactLoadTestResult(
                ArtifactLoadTestStatus.NotRun,
                $"The {target} artifact cannot be loaded by this {RuntimeInformation.ProcessArchitecture} process."));
        }

        nint handle = 0;
        try
        {
            handle = NativeLibrary.Load(Path.GetFullPath(artifactPath));
            if (!NativeLibrary.TryGetExport(handle, exportName, out var address) || address == 0)
            {
                return Task.FromResult(new ArtifactLoadTestResult(
                    ArtifactLoadTestStatus.Failed,
                    $"The Windows loader could not resolve '{exportName}'."));
            }

            return Task.FromResult(new ArtifactLoadTestResult(
                ArtifactLoadTestStatus.Passed,
                $"The Windows loader resolved '{exportName}'."));
        }
        catch (Exception exception) when (
            exception is BadImageFormatException or DllNotFoundException or EntryPointNotFoundException)
        {
            return Task.FromResult(new ArtifactLoadTestResult(
                ArtifactLoadTestStatus.Failed,
                $"The Windows loader rejected the artifact: {exception.Message}"));
        }
        finally
        {
            if (handle != 0)
            {
                NativeLibrary.Free(handle);
            }
        }
    }

    private static bool CanLoadInCurrentProcess(BuildTarget target) => target switch
    {
        BuildTarget.WindowsX64 => RuntimeInformation.ProcessArchitecture == Architecture.X64,
        BuildTarget.WindowsArm64 => RuntimeInformation.ProcessArchitecture == Architecture.Arm64,
        _ => false
    };
}
