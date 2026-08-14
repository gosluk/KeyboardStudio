namespace KeyboardStudio.Build;

public sealed record ProcessRequest(
    string Executable,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string?> Environment);
