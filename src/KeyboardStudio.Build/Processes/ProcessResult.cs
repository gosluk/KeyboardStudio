namespace KeyboardStudio.Build;

public sealed record ProcessResult(
    string Executable,
    IReadOnlyList<string> Arguments,
    string StandardOutput,
    string StandardError,
    int ExitCode,
    TimeSpan Duration,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string?> Environment);
