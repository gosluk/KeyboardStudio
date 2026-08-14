namespace KeyboardStudio.Windows;

/// <summary>
/// Windows-only identity used while generating a native keyboard layout artifact.
/// It intentionally lives outside KeyboardStudio.Core.
/// </summary>
public sealed record WindowsLayoutMetadata(
    string LayoutId,
    string LayoutName);
