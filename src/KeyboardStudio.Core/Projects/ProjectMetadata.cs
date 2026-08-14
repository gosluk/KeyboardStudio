namespace KeyboardStudio.Core;

public sealed class ProjectMetadata
{
    /// <summary>
    /// User-facing display name of the keyboard project.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Human-readable project description.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// User-managed project version. This is independent of the .kbdproj schema version.
    /// </summary>
    public string Version { get; init; } = "0.1.0";

    /// <summary>
    /// BCP 47 language or locale tag describing the layout, or "und" when unspecified.
    /// </summary>
    public string Language { get; init; } = "und";
}
