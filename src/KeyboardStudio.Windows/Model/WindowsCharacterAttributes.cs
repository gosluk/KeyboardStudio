namespace KeyboardStudio.Windows;

[Flags]
public enum WindowsCharacterAttributes : byte
{
    None = 0,
    CapsLock = 1 << 0
}
