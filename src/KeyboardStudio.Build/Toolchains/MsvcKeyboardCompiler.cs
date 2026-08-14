namespace KeyboardStudio.Build;

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
