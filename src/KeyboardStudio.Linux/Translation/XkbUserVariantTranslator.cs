using KeyboardStudio.Core;

namespace KeyboardStudio.Linux;

/// <summary>Translates only user changes from an import baseline into complete XKB key overrides.</summary>
public sealed class XkbUserVariantTranslator
{
    public const string UnsafeSourceBehaviorCode = "KSU001";
    public const string UnsupportedOutputCode = "KSU002";

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

    public XkbUserVariantTranslationResult Translate(
        KeyboardProject project,
        IReadOnlyList<KeyMappingSnapshot> baseline,
        XkbUserVariantMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(metadata);

        var difference = _differ.Compare(project.Layout, baseline);
        var diagnostics = new List<XkbDiagnostic>();
        var mappings = new List<XkbUserVariantKeyMapping>();

        foreach (var change in difference.Changes)
        {
            if (!change.IsSafeToOverride)
            {
                diagnostics.Add(new XkbDiagnostic(
                    UnsafeSourceBehaviorCode,
                    $"Physical key '{change.KeyId}' cannot be overridden because its source behavior was not represented exactly during import.",
                    change.KeyId));
                continue;
            }

            var keyNameResult = _keyNameMapper.Map(project.Keyboard.Id, change.KeyId);
            if (!keyNameResult.Success)
            {
                diagnostics.AddRange(keyNameResult.Diagnostics);
                continue;
            }

            var keysyms = TranslateKeysyms(change, diagnostics);
            if (keysyms is null)
            {
                continue;
            }

            var logicalKey = change.Current?.LogicalKey ?? change.Baseline!.LogicalKey;
            mappings.Add(new XkbUserVariantKeyMapping(
                change.KeyId,
                keyNameResult.KeyName!,
                SelectType(change.KeyId, logicalKey, keysyms.Length),
                keysyms));
        }

        if (diagnostics.Count > 0)
        {
            return new XkbUserVariantTranslationResult(false, null, diagnostics.AsReadOnly());
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
            []);
    }

    private string[]? TranslateKeysyms(
        KeyboardKeyDifference change,
        List<XkbDiagnostic> diagnostics)
    {
        if (change.Current is null)
        {
            return Enumerable.Repeat(
                    "NoSymbol",
                    Math.Max(1, HighestRelevantLevel(change)))
                .ToArray();
        }

        var keysyms = new string[Layers.Length];
        for (var index = 0; index < Layers.Length; index++)
        {
            if (!change.Current.Outputs.TryGetValue(Layers[index], out var output))
            {
                keysyms[index] = "NoSymbol";
                continue;
            }

            if (!_keysymMapper.TryMap(output, out keysyms[index]))
            {
                diagnostics.Add(new XkbDiagnostic(
                    UnsupportedOutputCode,
                    $"Output on layer '{Layers[index]}' cannot be represented as an XKB keysym.",
                    change.KeyId));
                return null;
            }
        }

        var currentHasExplicitOutputs = change.Current.Outputs.Count > 0;
        if (!currentHasExplicitOutputs)
        {
            if (!_keysymMapper.TryMap(change.Current.LogicalKey, out keysyms[0]))
            {
                diagnostics.Add(new XkbDiagnostic(
                    UnsupportedOutputCode,
                    $"Logical key '{change.Current.LogicalKey}' cannot be represented as an XKB keysym.",
                    change.KeyId));
                return null;
            }
        }

        var levelCount = Math.Max(HighestRelevantLevel(change), HighestCurrentLevel(change.Current));
        return keysyms[..Math.Max(1, levelCount)];
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
