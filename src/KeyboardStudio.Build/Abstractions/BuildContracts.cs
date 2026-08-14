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
