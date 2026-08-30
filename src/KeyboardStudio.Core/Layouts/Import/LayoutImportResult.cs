namespace KeyboardStudio.Core;

/// <summary>
/// The outcome of an import: the project, when there is one, and an honest account of what it cost.
///
/// A failed import is an ordinary result rather than an exception. A host's layout data is not the
/// application's to trust, and a layout that cannot be read is a thing to report in the dialog, not
/// a fault to unwind the stack over.
/// </summary>
/// <param name="Success">Whether a project was produced. When true, <paramref name="Project"/> is not null.</param>
/// <param name="Project">The imported project, or <see langword="null"/> if the import failed.</param>
/// <param name="SuggestedTemplateId">
/// The physical keyboard template the source inferred, echoed back so the dialog can show what was
/// chosen even when the caller left <see cref="LayoutImportOptions.TemplateId"/> unset.
/// </param>
/// <param name="Report">What was imported, what was dropped, and why. Always present.</param>
/// <param name="ResolvedSectionId">
/// The concrete source section that was imported. It differs from a requested null/default
/// variant and must be retained before a derived layout can inherit it safely.
/// </param>
public sealed record LayoutImportResult(
    bool Success,
    KeyboardProject? Project,
    string? SuggestedTemplateId,
    LayoutImportReport Report,
    string? ResolvedSectionId)
{
    /// <summary>Creates the result of an import that produced a project.</summary>
    public static LayoutImportResult Succeeded(
        KeyboardProject project,
        string? suggestedTemplateId,
        LayoutImportReport report,
        string? resolvedSectionId = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(report);
        return new LayoutImportResult(true, project, suggestedTemplateId, report, resolvedSectionId);
    }

    /// <summary>
    /// Creates the result of an import that produced nothing. The report still carries the
    /// diagnostics explaining why, which is the whole reason a failure is a result and not a throw.
    /// </summary>
    public static LayoutImportResult Failed(LayoutImportReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return new LayoutImportResult(false, null, null, report, null);
    }
}
