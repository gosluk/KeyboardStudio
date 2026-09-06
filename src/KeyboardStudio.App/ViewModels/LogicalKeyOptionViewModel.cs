using KeyboardStudio.Core;

namespace KeyboardStudio.App;

/// <summary>
/// One entry in the functional-key picker.
/// </summary>
/// <remarks>
/// The label comes from <see cref="LogicalKeyLegend"/> rather than from the enum name, so the picker
/// says "Num 8" exactly like the keycap and the tooltip do. A picker that said "Numpad8" while the
/// key it assigns reads "Num 8" would make the user translate between two vocabularies for one
/// thing. Where the legend is a bare glyph the enum name follows it, because a dropdown listing
/// "/", "8" and "\" with no further words names nothing and, for the two backslashes, does not even
/// distinguish its own entries.
/// </remarks>
public sealed class LogicalKeyOptionViewModel
{
    public LogicalKeyOptionViewModel(LogicalKey key)
    {
        Key = key;
        Label = Describe(key);
    }

    public LogicalKey Key { get; }

    public string Label { get; }

    public override string ToString() => Label;

    private static string Describe(LogicalKey key)
    {
        if (key == LogicalKey.None)
        {
            return "None";
        }

        var legend = LogicalKeyLegend.For(key).Replace("\n", " ", StringComparison.Ordinal).Trim();
        var name = key.ToString();
        if (legend.Length == 0 || string.Equals(legend, name, StringComparison.Ordinal))
        {
            return name;
        }

        // A legend of a word or two names the key on its own — "Num 8", "Caps Lock", "L Shift" — and
        // repeating the enum after it only costs width the panel does not have. A glyph does not.
        return legend.Length <= 2 ? $"{legend}  ({name})" : legend;
    }
}
