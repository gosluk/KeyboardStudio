namespace KeyboardStudio.Linux;

/// <summary>
/// The layout a Linux host is configured to type with, and where that was read from.
///
/// One layout, not the list the host may hold: a host configured with <c>us,pl</c> has two layouts
/// it switches between, while a project has exactly one. The first is the one the session starts
/// in, and importing that beats refusing to choose.
/// </summary>
/// <param name="LayoutId">The XKB layout name, such as <c>pl</c>. Never empty.</param>
/// <param name="VariantId">The XKB variant name, or <see langword="null"/> for the layout's default.</param>
/// <param name="Origin">Where in the fallback chain this was found.</param>
public sealed record XkbActiveLayout(
    string LayoutId,
    string? VariantId,
    XkbActiveLayoutOrigin Origin)
{
    /// <summary>
    /// What libxkbcommon itself assumes when nothing is configured. Having the last step of the
    /// chain be a real answer rather than a null is what lets the caller treat detection as total.
    /// </summary>
    public static XkbActiveLayout Fallback { get; } = new("us", null, XkbActiveLayoutOrigin.Fallback);

    /// <summary>The layout as XKB names it: <c>pl</c>, or <c>pl(dvorak)</c> when a variant is set.</summary>
    public string Describe() => VariantId is null ? LayoutId : $"{LayoutId}({VariantId})";
}
