using System.Text;

namespace KeyboardStudio.Linux;

public sealed record XkbLayoutMetadata
{
    public XkbLayoutMetadata(string layoutId, string sectionId, string description)
    {
        LayoutId = SanitizeIdentifier(layoutId, "layout");
        SectionId = SanitizeIdentifier(sectionId, "basic");
        Description = string.IsNullOrWhiteSpace(description)
            ? "KeyboardStudio layout"
            : description.Trim();
    }

    public string LayoutId { get; }

    public string SectionId { get; }

    public string Description { get; }

    public static string SanitizeIdentifier(string? value, string fallback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fallback);

        var source = value?.Trim().ToLowerInvariant() ?? string.Empty;
        var builder = new StringBuilder(source.Length);
        foreach (var character in source)
        {
            if (character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-')
            {
                builder.Append(character);
            }
            else if (char.IsWhiteSpace(character) && builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var result = builder.ToString().Trim('-');
        if (result.Length == 0)
        {
            result = fallback;
        }

        if (char.IsDigit(result[0]))
        {
            result = $"layout-{result}";
        }

        return result;
    }
}
