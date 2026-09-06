namespace KeyboardStudio.App;

/// <summary>
/// One entry in a layer row's kind picker, pairing the value with the word shown for it.
/// </summary>
public sealed class KeyOutputKindOptionViewModel
{
    public KeyOutputKindOptionViewModel(KeyOutputKind kind, string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        Kind = kind;
        Label = label;
    }

    public KeyOutputKind Kind { get; }

    public string Label { get; }

    public override string ToString() => Label;
}
