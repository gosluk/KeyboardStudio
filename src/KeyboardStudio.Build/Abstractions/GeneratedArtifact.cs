namespace KeyboardStudio.Build;

public sealed record GeneratedArtifact(
    GeneratedSource Source,
    string OutputFileName = "keyboard.dll");
