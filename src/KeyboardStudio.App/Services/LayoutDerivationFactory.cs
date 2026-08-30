using KeyboardStudio.Core;
using KeyboardStudio.Persistence;

namespace KeyboardStudio.App;

/// <summary>Captures an installable baseline only for a new project imported from the system.</summary>
public static class LayoutDerivationFactory
{
    private static readonly HashSet<string> UnsafeDiagnosticCodes =
    [
        LayoutImportDiagnosticCodes.AlternateGroupsIgnored,
        LayoutImportDiagnosticCodes.UnsupportedConstructIgnored,
        LayoutImportDiagnosticCodes.UnrecognizedStatementSkipped,
        LayoutImportDiagnosticCodes.MergeModeApproximated,
        LayoutImportDiagnosticCodes.CompositionTargetUnavailable,
        LayoutImportDiagnosticCodes.LayerBeyondModelDropped,
        LayoutImportDiagnosticCodes.DeadKeyDropped,
        LayoutImportDiagnosticCodes.OutputNotRepresentable
    ];

    public static LayoutDerivation? Create(
        ImportableLayoutDescriptor descriptor,
        LayoutImportResult result,
        DateTimeOffset importedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(result);

        if (descriptor.Origin != LayoutSourceOrigin.System ||
            result is not { Success: true, Project: { } project } ||
            string.IsNullOrWhiteSpace(result.ResolvedSectionId))
        {
            return null;
        }

        var unsafeDiagnostics = result.Report.Diagnostics
            .Where(diagnostic => UnsafeDiagnosticCodes.Contains(diagnostic.Code))
            .ToArray();
        var hasLayoutWideLoss = unsafeDiagnostics.Any(diagnostic =>
            diagnostic.KeyId is null && diagnostic.SourceKeyName is null);

        var baseline = project.Layout.Mappings
            .Select(mapping => KeyMappingSnapshot.From(
                mapping,
                !hasLayoutWideLoss && !unsafeDiagnostics.Any(diagnostic =>
                    string.Equals(diagnostic.KeyId, mapping.KeyId, StringComparison.Ordinal))))
            .ToArray();

        return new LayoutDerivation(
            Guid.NewGuid().ToString("N"),
            descriptor.SourceId,
            descriptor.Origin,
            descriptor.LayoutId,
            descriptor.VariantId,
            result.ResolvedSectionId,
            importedAtUtc,
            result.Report.Fidelity,
            baseline);
    }
}
