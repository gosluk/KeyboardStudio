namespace KeyboardStudio.Core;

public sealed record ValidationIssue(
    ValidationSeverity Severity,
    string Code,
    string Message,
    string? KeyId = null);
