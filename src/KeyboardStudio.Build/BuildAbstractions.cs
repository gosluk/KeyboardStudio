using KeyboardStudio.Core;

namespace KeyboardStudio.Build;

public enum BuildTarget
{
    WindowsX64,
    WindowsArm64
}

public sealed record BuildOptions(BuildTarget Target, string OutputDirectory);

public sealed record GeneratedSource(IReadOnlyDictionary<string, string> Files);

public sealed record GeneratedArtifact(GeneratedSource Source);

public sealed record CompilerMessage(string Code, string Message);

public sealed record CompilationResult(
    bool Success,
    string? ArtifactPath,
    IReadOnlyList<CompilerMessage> Messages);

public sealed record BuildEnvironmentStatus(bool Available, string Message);

public interface IBuildEnvironment
{
    bool CanBuild(BuildTarget target);
    BuildEnvironmentStatus GetStatus(BuildTarget target);
}

public interface IArtifactGenerator
{
    Task<GeneratedArtifact> GenerateAsync(
        KeyboardProject project,
        BuildOptions options,
        CancellationToken cancellationToken = default);
}

public interface INativeCompiler
{
    Task<CompilationResult> CompileAsync(
        GeneratedSource source,
        BuildTarget target,
        CancellationToken cancellationToken = default);
}

public sealed class WindowsBuildEnvironment : IBuildEnvironment
{
    public bool CanBuild(BuildTarget target) => OperatingSystem.IsWindows();

    public BuildEnvironmentStatus GetStatus(BuildTarget target) => OperatingSystem.IsWindows()
        ? new BuildEnvironmentStatus(true, "Windows host detected. MSVC/WDK discovery will be implemented next.")
        : new BuildEnvironmentStatus(false, "Native Windows keyboard DLL compilation requires a Windows build host with MSVC/WDK.");
}

public sealed class MsvcKeyboardCompiler : INativeCompiler
{
    public Task<CompilationResult> CompileAsync(
        GeneratedSource source,
        BuildTarget target,
        CancellationToken cancellationToken = default)
    {
        var result = new CompilationResult(
            false,
            null,
            [new CompilerMessage("MSVC000", "MSVC/WDK invocation is intentionally not implemented in the source skeleton yet.")]);
        return Task.FromResult(result);
    }
}
