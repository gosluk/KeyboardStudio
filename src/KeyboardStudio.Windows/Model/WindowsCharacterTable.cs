namespace KeyboardStudio.Windows;

public sealed record WindowsCharacterTable(
    int Width,
    IReadOnlyList<WindowsCharacterMapping> Rows);
