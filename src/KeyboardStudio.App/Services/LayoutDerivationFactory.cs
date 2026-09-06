using KeyboardStudio.Core;
using KeyboardStudio.Persistence;

namespace KeyboardStudio.App;

/// <summary>Captures an installable baseline only for a new project imported from the system.</summary>
public static class LayoutDerivationFactory
{
    /// <summary>
    /// Loss that keeping the source's own levels cannot undo: another group, a key action, a
    /// construct the reader did not recognize, a composition that was approximated or could not be
    /// read. None of it is a level of one key, so writing that key back out restores nothing.
    /// </summary>
    private static readonly HashSet<string> UnrecoverableDiagnosticCodes =
    [
        LayoutImportDiagnosticCodes.AlternateGroupsIgnored,
        LayoutImportDiagnosticCodes.UnsupportedConstructIgnored,
        LayoutImportDiagnosticCodes.UnrecognizedStatementSkipped,
        LayoutImportDiagnosticCodes.MergeModeApproximated,
        LayoutImportDiagnosticCodes.CompositionTargetUnavailable
    ];

    /// <summary>
    /// Loss confined to the levels of a single key: a level past the four the model holds, a dead
    /// key, a keysym with no character behind it. <see cref="LayoutImportKeySource.Levels"/> holds
    /// every one of them verbatim, so a key whose source came with the import can be overridden
    /// without erasing anything — the backend writes the levels it never represented straight back.
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
            .Where(diagnostic => UnrecoverableDiagnosticCodes.Contains(diagnostic.Code) ||
                                 LevelDiagnosticCodes.Contains(diagnostic.Code))
            .ToArray();

        // Loss that names no key cannot be reasoned about per key, so it costs every key its
        // override — whichever kind it was.
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
    /// Whether one key can be replaced whole without erasing what the import could not hold.
    ///
    /// Loss of a level is answered by the source that recorded it; every other kind of loss still
    /// puts the key out of reach, because nothing kept beside the mapping describes it.
    /// </summary>
    private static bool IsSafeToOverride(
        string keyId,
        IReadOnlyList<LayoutImportDiagnostic> lossyDiagnostics,
        LayoutImportKeySource? source)
    {
        var recoverable = true;
        foreach (var diagnostic in lossyDiagnostics)
        {
            if (!string.Equals(diagnostic.KeyId, keyId, StringComparison.Ordinal))
            {
                continue;
            }

            if (UnrecoverableDiagnosticCodes.Contains(diagnostic.Code))
            {
                return false;
            }

            recoverable = false;
        }

        return recoverable || source is { Levels.Count: > 0 };
    }
}
