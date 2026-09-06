using KeyboardStudio.Core;

namespace KeyboardStudio.Linux;

/// <summary>Translates only user changes from an import baseline into complete XKB key overrides.</summary>
public sealed class XkbUserVariantTranslator
{
    public const string UnsafeSourceBehaviorCode = "KSU001";
    public const string UnsupportedOutputCode = "KSU002";
    public const string UnwritableSourceLevelCode = "KSU003";

    /// <summary>A key that was written anyway, and what writing it cost.</summary>
    public const string AcceptedIncompleteKeyCode = "KSU004";

    /// <summary>
    /// What may be written back verbatim into a generated symbols file. Source levels and key types
    /// reach here having been read out of the host's own XKB data, and they leave here inside a
    /// file that gets compiled — so they are checked against the notation they claim to be in
    /// rather than trusted for having come from a file that parsed.
    /// </summary>
    private static readonly System.Buffers.SearchValues<char> KeysymCharacters =
        System.Buffers.SearchValues.Create(
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_");

    /// <summary>Type names additionally carry '+', as in <c>CTRL+ALT</c>.</summary>
    private static readonly System.Buffers.SearchValues<char> KeyTypeCharacters =
        System.Buffers.SearchValues.Create(
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_+");

    private static readonly ModifierLayer[] Layers =
    [
        ModifierLayer.Default,
        ModifierLayer.Shift,
        ModifierLayer.AltGr,
        ModifierLayer.ShiftAltGr
    ];

    private readonly KeyboardLayoutDiffer _differ;
    private readonly IXkbKeyNameMapper _keyNameMapper;
    private readonly IXkbKeysymMapper _keysymMapper;

    public XkbUserVariantTranslator()
        : this(new KeyboardLayoutDiffer(), new XkbKeyNameMapper(), new XkbKeysymMapper())
    {
    }

    public XkbUserVariantTranslator(
        KeyboardLayoutDiffer differ,
        IXkbKeyNameMapper keyNameMapper,
        IXkbKeysymMapper keysymMapper)
    {
        _differ = differ ?? throw new ArgumentNullException(nameof(differ));
        _keyNameMapper = keyNameMapper ?? throw new ArgumentNullException(nameof(keyNameMapper));
        _keysymMapper = keysymMapper ?? throw new ArgumentNullException(nameof(keysymMapper));
    }

    /// <param name="acceptIncompleteKeys">
    /// Whether a key whose levels cannot all be carried may be written anyway, with the levels
    /// that cannot be carried left out. Off by default: dropping part of a key is the user's call,
    /// not the translator's, and every such key comes back in
    /// <see cref="XkbUserVariantTranslationResult.AcceptedLoss"/> for them to be shown before it
    /// is written anywhere.
    /// </param>
    public XkbUserVariantTranslationResult Translate(
        KeyboardProject project,
        IReadOnlyList<KeyMappingSnapshot> baseline,
        XkbUserVariantMetadata metadata,
        bool acceptIncompleteKeys = false)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(metadata);

        var difference = _differ.Compare(project.Layout, baseline);
        var scope = new TranslationScope(acceptIncompleteKeys, [], []);
        var mappings = new List<XkbUserVariantKeyMapping>();

        foreach (var change in difference.Changes)
        {
            // Levels the model could not hold are no longer a reason to be here: those come back
            // from the source. What is left is a key whose source never came with the import, so
            // its unrepresented levels can only be dropped.
            if (!change.IsSafeToOverride && !scope.Allow(
                    UnsafeSourceBehaviorCode,
                    $"Physical key '{change.KeyId}' cannot be overridden: its import dropped levels " +
                    "the model does not hold, and this project has no record of what they were.",
                    $"Physical key '{change.KeyId}' is written with the levels the editor holds; " +
                    "whatever its import dropped is not written back.",
                    change.KeyId))
            {
                continue;
            }

            var keyNameResult = _keyNameMapper.Map(project.Keyboard.Id, change.KeyId);
            if (!keyNameResult.Success)
            {
                scope.Blocking.AddRange(keyNameResult.Diagnostics);
                continue;
            }

            var keysyms = TranslateKeysyms(change, scope);
            if (keysyms is null)
            {
                continue;
            }

            var sourceTypeName = SelectSourceTypeName(change, keysyms.Length, scope);
            if (keysyms.Length > Layers.Length && sourceTypeName is null)
            {
                if (!scope.AcceptIncompleteKeys)
                {
                    continue;
                }

                // Accepted: without the type that reaches them, the extra levels cannot be written.
                keysyms = keysyms[..Layers.Length];
            }

            var logicalKey = change.Current?.LogicalKey ?? change.Baseline!.LogicalKey;
            mappings.Add(new XkbUserVariantKeyMapping(
                change.KeyId,
                keyNameResult.KeyName!,
                SelectType(change.KeyId, logicalKey, Math.Min(keysyms.Length, Layers.Length)),
                keysyms,
                sourceTypeName));
        }

        if (scope.Blocking.Count > 0)
        {
            return new XkbUserVariantTranslationResult(false, null, scope.Blocking.AsReadOnly());
        }

        var ordered = mappings.OrderBy(mapping => mapping.KeyName, StringComparer.Ordinal).ToArray();
        return new XkbUserVariantTranslationResult(
            true,
            new XkbUserVariantLayout(
                metadata,
                ordered,
                ordered.Any(mapping => mapping.Keysyms
                    .Skip(2)
                    .Any(keysym => !string.Equals(keysym, "NoSymbol", StringComparison.Ordinal)))),
            [.. scope.Accepted])
        {
            AcceptedLoss = scope.Accepted.AsReadOnly()
        };
    }

    /// <summary>
    /// Builds the complete level list this key will be written with.
    ///
    /// An override replaces a key entirely, so every level it has must be named here — including
    /// the ones the model never held. Those come back verbatim from the source the import kept
    /// beside the mapping: a dead key on the third level, a fifth level reached through Lock. What
    /// the user changed is theirs; what they never saw stays what it was.
    /// </summary>
    private string[]? TranslateKeysyms(
        KeyboardKeyDifference change,
        TranslationScope scope)
    {
        var source = change.Baseline?.SourceLevels ?? [];
        var modelLevels = Math.Max(
            1,
            Math.Max(
                HighestRelevantLevel(change),
                change.Current is null ? 0 : HighestCurrentLevel(change.Current)));
        var keysyms = new string[Math.Max(modelLevels, source.Count)];

        for (var index = 0; index < keysyms.Length; index++)
        {
            if (index >= Layers.Length)
            {
                // Past the model entirely. Only the source ever described this level, so only the
                // source can write it.
                if (!TryTakeSourceLevel(source, index, change.KeyId, scope, out keysyms[index]))
                {
                    return null;
                }

                continue;
            }

            var layer = Layers[index];
            if (change.Current?.Outputs.TryGetValue(layer, out var output) == true)
            {
                if (!_keysymMapper.TryMap(output, out keysyms[index]))
                {
                    // Nothing to write and nothing to drop instead: this one cannot be accepted.
                    scope.Blocking.Add(new XkbDiagnostic(
                        UnsupportedOutputCode,
                        $"Output on layer '{layer}' cannot be represented as an XKB keysym.",
                        change.KeyId));
                    return null;
                }

                continue;
            }

            // The editor shows nothing on this layer, which means one of two different things: the
            // import could not represent what the source had and the user never saw it, or the
            // user cleared an output the import did hold. Only the second is a change to write.
            if (change.Baseline?.Outputs.ContainsKey(layer) != true && index < source.Count)
            {
                if (!TryTakeSourceLevel(source, index, change.KeyId, scope, out keysyms[index]))
                {
                    return null;
                }

                continue;
            }

            keysyms[index] = "NoSymbol";
        }

        // A key left with a logical identity but no outputs of its own types what it is named
        // after, which is what the editor shows for it.
        if (change.Current is { Outputs.Count: 0 })
        {
            if (!_keysymMapper.TryMap(change.Current.LogicalKey, out keysyms[0]))
            {
                scope.Blocking.Add(new XkbDiagnostic(
                    UnsupportedOutputCode,
                    $"Logical key '{change.Current.LogicalKey}' cannot be represented as an XKB keysym.",
                    change.KeyId));
                return null;
            }
        }

        return keysyms;
    }

    /// <summary>
    /// Reads one level back out of the source, refusing anything that would not be a keysym name.
    /// </summary>
    private static bool TryTakeSourceLevel(
        IReadOnlyList<string> source,
        int index,
        string keyId,
        TranslationScope scope,
        out string keysym)
    {
        keysym = index < source.Count ? source[index] : "NoSymbol";
        if (IsWritable(keysym, KeysymCharacters))
        {
            return true;
        }

        var unwritable = keysym;
        keysym = "NoSymbol";
        return scope.Allow(
            UnwritableSourceLevelCode,
            $"Level {index + 1} of physical key '{keyId}' reads '{unwritable}' in the imported " +
            "layout, which is not a keysym name that can be written back.",
            $"Level {index + 1} of physical key '{keyId}' is written empty: the imported layout " +
            $"reads '{unwritable}' there, which is not a keysym name that can be written back.",
            keyId);
    }

    /// <summary>
    /// The type to write, when the key needs the source's own.
    ///
    /// A key whose levels all fit the model is typed from the model, so that levels the user has
    /// just added stay reachable. A key with more levels than that can only reach them through the
    /// type the source declared, and without one there is no way to write the key without losing
    /// them — which is the refusal this method reports.
    /// </summary>
    private static string? SelectSourceTypeName(
        KeyboardKeyDifference change,
        int levelCount,
        TranslationScope scope)
    {
        if (levelCount <= Layers.Length)
        {
            return null;
        }

        var sourceType = change.Baseline?.SourceKeyType;
        if (sourceType is not null && IsWritable(sourceType, KeyTypeCharacters))
        {
            return sourceType;
        }

        var reason = sourceType is null
            ? "was not declared there"
            : $"reads '{sourceType}', which cannot be written back";
        scope.Allow(
            UnwritableSourceLevelCode,
            $"Physical key '{change.KeyId}' has {levelCount} levels in the imported layout, and the " +
            $"key type that reaches the ones beyond the fourth {reason}.",
            $"Physical key '{change.KeyId}' is written with four levels: it has {levelCount} in the " +
            $"imported layout, and the key type that reaches the rest {reason}.",
            change.KeyId);
        return null;
    }

    private static bool IsWritable(string value, System.Buffers.SearchValues<char> allowed) =>
        value.Length > 0 && !value.AsSpan().ContainsAnyExcept(allowed);

    /// <summary>
    /// What one translation is allowed to give up, and the record of what it gave up.
    /// </summary>
    private sealed record TranslationScope(
        bool AcceptIncompleteKeys,
        List<XkbDiagnostic> Blocking,
        List<XkbDiagnostic> Accepted)
    {
        /// <summary>
        /// Reports something that cannot be written. Without acceptance it stops the key and the
        /// whole variant with it; with acceptance it is recorded as loss the caller has agreed to
        /// and the key is written without it. Returns whether the key may still be written.
        /// </summary>
        public bool Allow(
            string code,
            string blockedMessage,
            string acceptedMessage,
            string keyId)
        {
            if (!AcceptIncompleteKeys)
            {
                Blocking.Add(new XkbDiagnostic(code, blockedMessage, keyId));
                return false;
            }

            Accepted.Add(new XkbDiagnostic(AcceptedIncompleteKeyCode, acceptedMessage, keyId));
            return true;
        }
    }

    private static int HighestRelevantLevel(KeyboardKeyDifference change)
    {
        var highest = change.ChangedLayers.Count == 0
            ? 0
            : change.ChangedLayers.Max(layer => Array.IndexOf(Layers, layer)) + 1;

        if (change.Current is null && change.Baseline is not null)
        {
            highest = Math.Max(highest, HighestCurrentLevel(change.Baseline));
        }

        return highest;
    }

    private static int HighestCurrentLevel(KeyMappingSnapshot mapping) =>
        mapping.Outputs.Count == 0
            ? 1
            : mapping.Outputs.Keys.Max(layer => Array.IndexOf(Layers, layer)) + 1;

    private static XkbKeyType SelectType(
        string physicalKeyId,
        LogicalKey logicalKey,
        int levelCount)
    {
        var keypad = physicalKeyId.StartsWith("Numpad", StringComparison.Ordinal);
        var alphabetic = logicalKey is >= LogicalKey.A and <= LogicalKey.Z;

        return levelCount switch
        {
            <= 1 => XkbKeyType.OneLevel,
            2 when keypad => XkbKeyType.Keypad,
            2 when alphabetic => XkbKeyType.Alphabetic,
            2 => XkbKeyType.TwoLevel,
            _ when keypad => XkbKeyType.FourLevelMixedKeypad,
            _ when alphabetic => XkbKeyType.FourLevelSemialphabetic,
            _ => XkbKeyType.FourLevel
        };
    }
}
