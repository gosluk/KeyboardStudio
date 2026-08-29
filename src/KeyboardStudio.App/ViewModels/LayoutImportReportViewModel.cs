using KeyboardStudio.Core;

namespace KeyboardStudio.App;

/// <summary>
/// What an import cost, shown while the user can still cancel it.
///
/// Import is lossy by design, so the report is not an error screen: it is the difference between a
/// layout the user can trust and one they have to check key by key after the fact.
/// </summary>
public sealed class LayoutImportReportViewModel
{
    public LayoutImportReportViewModel(LayoutImportReport report, string? geometryName = null)
    {
        ArgumentNullException.ThrowIfNull(report);

        Fidelity = report.Fidelity;
        FidelityLabel = report.Fidelity switch
        {
            LayoutImportFidelity.Exact => "Exact",
            LayoutImportFidelity.Reduced => "Reduced",
            _ => "Partial"
        };
        FidelityDescription = report.Fidelity switch
        {
            LayoutImportFidelity.Exact => "Every key and output was represented.",
            LayoutImportFidelity.Reduced => "Every key was imported; some outputs were dropped.",
            _ => "Some keys could not be imported at all."
        };

        Summary = report.KeysSkipped == 0
            ? $"{report.KeysImported} keys imported"
            : $"{report.KeysImported} keys imported, {report.KeysSkipped} skipped";
        if (geometryName is not null)
        {
            Summary = $"{Summary}, on {geometryName}";
        }

        // The chain is what a layout is actually made of: pl(basic) is latin plus its own changes,
        // and latin is us plus its own. Showing it is what lets a user explain a key they did not
        // expect to see.
        IncludeChain = report.ResolvedIncludeChain.Count == 0
            ? string.Empty
            : string.Join(" ← ", report.ResolvedIncludeChain);

        Diagnostics = report.Diagnostics
            .OrderByDescending(diagnostic => diagnostic.Severity)
            .ThenBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
            .Select(ToDiagnosticViewModel)
            .ToArray();
    }

    public LayoutImportFidelity Fidelity { get; }

    public string FidelityLabel { get; }

    public string FidelityDescription { get; }

    public string Summary { get; }

    public string IncludeChain { get; }

    public bool HasIncludeChain => IncludeChain.Length > 0;

    public IReadOnlyList<DiagnosticViewModel> Diagnostics { get; }

    public bool HasDiagnostics => Diagnostics.Count > 0;

    /// <summary>
    /// Import findings render through the same row the editor's validation findings use.
    /// <see cref="LayoutImportDiagnostic"/> was shaped to allow exactly this; the one field it
    /// adds, the modifier layer, is folded into the message, since a report shown before the
    /// project exists has no key to jump to anyway.
    /// </summary>
    private static DiagnosticViewModel ToDiagnosticViewModel(LayoutImportDiagnostic diagnostic) =>
        new(new ValidationIssue(
                diagnostic.Severity,
                diagnostic.Code,
                diagnostic.Layer is { } layer
                    ? $"{diagnostic.Message} ({layer})"
                    : diagnostic.Message,
                diagnostic.KeyId),
            static _ => { });
}
