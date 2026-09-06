using KeyboardStudio.Core;
using KeyboardStudio.Persistence;

namespace KeyboardStudio.App;

/// <summary>Captures an installable baseline only for a new project imported from the system.</summary>
public static class LayoutDerivationFactory
{
    /// <summary>
    /// The only loss an override can still erase: the levels of the key being overridden.
    ///
    /// An override does not replace a key. XKB merges a key definition field by field, and a
    /// derived variant writes exactly two of them — the group-1 symbols and the group-1 type — so
    /// everything else the key carries survives it. That was confirmed against the compiler:
    /// overriding group 1 of a key leaves its <c>actions</c> and its second group exactly as the
    /// base defined them. Alternate groups and unsupported key constructs are therefore not
    /// reasons to refuse an override, and neither is an inexactly composed layout: a statement the
    /// reader skipped, a merge mode it approximated, or an include it could not resolve may leave
    /// the baseline an imperfect picture of the host, but overriding one key cannot erase another
    /// key that was never written.
    ///
    /// What an override does replace is the levels. A level the model could not hold is carried
    /// verbatim in <see cref="LayoutImportKeySource.Levels"/> and written back around the user's
    /// change — so a key whose source came with the import stays safe. A key whose source did not
    /// (a derivation saved before they were kept) has nothing to write those levels from, and only
    /// that key is left unsafe to override.
    /// </summary>
    private static readonly HashSet<string> LevelDiagnosticCodes =
    [
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

        var lossyDiagnostics = result.Report.Diagnostics
            .Where(diagnostic => LevelDiagnosticCodes.Contains(diagnostic.Code))
            .ToArray();

        // Level loss that names no key cannot be attributed to one, so it costs every key its
        // override. Nothing else is layout-wide any more: loss that is not a level does not travel
        // to a key an override never touches.
        var hasLayoutWideLoss = lossyDiagnostics.Any(diagnostic =>
            diagnostic.KeyId is null && diagnostic.SourceKeyName is null);
        var sources = result.KeySources.ToDictionary(
            source => source.KeyId,
            StringComparer.Ordinal);

        var baseline = project.Layout.Mappings
            .Select(mapping =>
            {
                sources.TryGetValue(mapping.KeyId, out var source);
                return KeyMappingSnapshot.From(
                    mapping,
                    !hasLayoutWideLoss && IsSafeToOverride(mapping.KeyId, lossyDiagnostics, source),
                    source);
            })
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

    /// <summary>
    /// Whether this key's levels can be written back without erasing one the import could not hold.
    /// A key that lost a level keeps its override only if the source that describes that level
    /// came with the import.
    /// </summary>
    private static bool IsSafeToOverride(
        string keyId,
        IReadOnlyList<LayoutImportDiagnostic> lossyDiagnostics,
        LayoutImportKeySource? source) =>
        source is { Levels.Count: > 0 } ||
        !lossyDiagnostics.Any(diagnostic =>
            string.Equals(diagnostic.KeyId, keyId, StringComparison.Ordinal));
}
