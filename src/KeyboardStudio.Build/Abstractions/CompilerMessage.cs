namespace KeyboardStudio.Build;

public sealed record CompilerMessage(
    string Code,
    string Message,
    CompilerMessageSeverity Severity = CompilerMessageSeverity.Error,
    string? FilePath = null,
    int? Line = null,
    int? Column = null);
