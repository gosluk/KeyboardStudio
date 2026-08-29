namespace KeyboardStudio.Core;

public sealed record KeyboardLayoutDifference(IReadOnlyList<KeyboardKeyDifference> Changes)
{
    public bool HasChanges => Changes.Count > 0;
}
