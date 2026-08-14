using System.Text;

namespace KeyboardStudio.Core;

public sealed record CharacterOutput : KeyOutput
{
    public CharacterOutput(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.Length == 0 ||
            !Rune.TryGetRuneAt(value, 0, out var rune) ||
            rune.Utf16SequenceLength != value.Length)
        {
            throw new ArgumentException(
                "A character output must contain exactly one Unicode scalar value.",
                nameof(value));
        }

        Value = value;
    }

    public string Value { get; }
}
