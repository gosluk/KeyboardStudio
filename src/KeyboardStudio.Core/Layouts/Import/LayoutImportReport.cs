namespace KeyboardStudio.Core;

/// <summary>
/// What an import actually managed to do.
///
/// Import is lossy by design, so every import returns a report whether it succeeded or not. The
/// editor shows it before the import is committed: the user needs to see which keys were dropped
/// while they can still cancel, not afterwards.
/// </summary>
/// <param name="Fidelity">How completely the layout survived. See <see cref="Classify"/>.</param>
/// <param name="KeysImported">Number of physical keys that received at least one output.</param>
/// <param name="KeysSkipped">Number of keys the source defined that could not be imported at all.</param>
/// <param name="ResolvedIncludeChain">
/// The definitions that were composed to produce this layout, in resolution order. Retained so an
/// import can be explained and reproduced; a source that composes nothing returns an empty list.
/// </param>
/// <param name="Diagnostics">Findings raised during the import, in the order they were raised.</param>
public sealed record LayoutImportReport(
    LayoutImportFidelity Fidelity,
    int KeysImported,
    int KeysSkipped,
    IReadOnlyList<string> ResolvedIncludeChain,
    IReadOnlyList<LayoutImportDiagnostic> Diagnostics)
{
    /// <summary>
    /// Derives the fidelity of an import from what it dropped.
    ///
    /// The rule lives here, next to the enum it produces, so that every source grades itself the
    /// same way. Were each source to decide for itself, "Reduced" would mean something different
    /// per platform and the badge in the import dialog would stop meaning anything.
    /// </summary>
    /// <param name="keysSkipped">Keys that could not be imported at all.</param>
    /// <param name="diagnostics">Findings raised during the import.</param>
    /// <returns>
    /// <see cref="LayoutImportFidelity.Partial"/> if any key was skipped,
    /// <see cref="LayoutImportFidelity.Reduced"/> if every key was imported but something above
    /// <see cref="ValidationSeverity.Info"/> was reported, and
    /// <see cref="LayoutImportFidelity.Exact"/> otherwise.
    /// </returns>
    public static LayoutImportFidelity Classify(
        int keysSkipped,
        IReadOnlyList<LayoutImportDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentOutOfRangeException.ThrowIfNegative(keysSkipped);

        if (keysSkipped > 0)
        {
            return LayoutImportFidelity.Partial;
        }

        foreach (var diagnostic in diagnostics)
        {
            if (diagnostic.Severity > ValidationSeverity.Info)
            {
                return LayoutImportFidelity.Reduced;
            }
        }

        return LayoutImportFidelity.Exact;
    }
}
