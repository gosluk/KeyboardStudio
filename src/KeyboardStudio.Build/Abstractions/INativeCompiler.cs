namespace KeyboardStudio.Build;

public interface INativeCompiler
{
    Task<CompilationResult> CompileAsync(
        GeneratedArtifact artifact,
        BuildOptions options,
        CancellationToken cancellationToken = default);
}
