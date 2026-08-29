namespace KeyboardStudio.Linux;

/// <summary>Names a derived variant without changing the identity of its system base layout.</summary>
public sealed record XkbUserVariantMetadata
{
    public XkbUserVariantMetadata(
        string projectInstallationId,
        string baseLayoutId,
        string? baseVariantId,
        string resolvedBaseSectionId,
        string publicVariantId,
        string description)
    {
        ProjectInstallationId = RequireInstallationId(projectInstallationId);
        BaseLayoutId = RequireSourceIdentifier(baseLayoutId, nameof(baseLayoutId));
        BaseVariantId = baseVariantId;
        ResolvedBaseSectionId = RequireSourceIdentifier(
            resolvedBaseSectionId,
            nameof(resolvedBaseSectionId));
        PublicVariantId = XkbLayoutMetadata.SanitizeIdentifier(
            publicVariantId,
            "keyboardstudio");
        Description = string.IsNullOrWhiteSpace(description)
            ? "KeyboardStudio variant"
            : description.Trim();
    }

    public string ProjectInstallationId { get; }

    public string BaseLayoutId { get; }

    public string? BaseVariantId { get; }

    public string ResolvedBaseSectionId { get; }

    public string PublicVariantId { get; }

    public string Description { get; }

    public string InternalSectionId => $"ks_{ProjectInstallationId[..12]}";

    private static string RequireInstallationId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length != 32 || value.Any(character => !char.IsAsciiHexDigit(character)))
        {
            throw new ArgumentException(
                "A project installation ID must be a 32-digit hexadecimal GUID representation.",
                nameof(value));
        }

        return value.ToLowerInvariant();
    }

    private static string RequireSourceIdentifier(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not '_' and not '-'))
        {
            throw new ArgumentException(
                "An inherited XKB identifier may use only ASCII letters, digits, '_' or '-'.",
                parameterName);
        }

        return value;
    }
}
