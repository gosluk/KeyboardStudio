namespace KeyboardStudio.Core;

/// <summary>
/// Identifiers of the seed projects that can populate a new document.
/// </summary>
public static class SeedProjectId
{
    /// <summary>
    /// US layout on ISO 105-key hardware.
    /// </summary>
    public const string UsBasic = "us-basic";

    /// <summary>
    /// Seed used when the caller does not choose one.
    /// </summary>
    public const string Default = UsBasic;
}
