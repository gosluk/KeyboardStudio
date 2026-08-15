namespace KeyboardStudio.Linux;

public sealed record XkbDiagnostic(string Code, string Message, string? KeyId = null);
