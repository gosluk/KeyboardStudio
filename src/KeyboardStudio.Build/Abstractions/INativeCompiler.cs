namespace KeyboardStudio.Build;

public interface INativeCompiler
{
    Task<CompilationResult> CompileAsync(
        GeneratedSource source,
        BuildTarget target,
        CancellationToken cancellationToken = default);
}
