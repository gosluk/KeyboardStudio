namespace KeyboardStudio.Build;

public interface INativeCompiler
{
    Task<CompilationResult> CompileAsync(
        GeneratedArtifact artifact,
        BuildOptions options,
        IProgress<BuildStageProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
