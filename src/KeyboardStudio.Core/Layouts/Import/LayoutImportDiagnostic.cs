namespace KeyboardStudio.Core;

/// <summary>
/// One finding raised while importing a layout.
///
/// Deliberately shaped like <see cref="ValidationIssue"/> and sharing its
/// <see cref="ValidationSeverity"/>, so import findings render through the editor's existing
/// diagnostics list with a working jump-to-key target. <see cref="Layer"/> is the one addition:
/// import loss is frequently confined to a single modifier layer of a single key, and saying which
/// is the difference between a report a user can act on and a report they can only read.
/// </summary>
/// <param name="Severity">How much the finding cost. Errors mean the import could not proceed.</param>
/// <param name="Code">A stable <c>KSI</c> code from <see cref="LayoutImportDiagnosticCodes"/>.</param>
/// <param name="Message">Human-readable explanation, naming the affected key where it helps.</param>
/// <param name="KeyId">Physical key the finding concerns, or <see langword="null"/> if it is layout-wide.</param>
/// <param name="Layer">Modifier layer the finding concerns, or <see langword="null"/> if it spans all of them.</param>
public sealed record LayoutImportDiagnostic(
    ValidationSeverity Severity,
    string Code,
    string Message,
    string? KeyId = null,
    ModifierLayer? Layer = null)
{
    /// <summary>
    /// Source-format key name before it can be resolved to an editor key ID. Importers use this to
    /// keep loss attached to the key that caused it, including keys the selected template omits.
    /// </summary>
    public string? SourceKeyName { get; init; }
}
