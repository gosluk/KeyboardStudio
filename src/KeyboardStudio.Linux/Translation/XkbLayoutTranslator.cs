using KeyboardStudio.Core;

namespace KeyboardStudio.Linux;

public sealed class XkbLayoutTranslator
{
    public const string UnsupportedOutputCode = "KSL002";

    private static readonly ModifierLayer[] Layers =
    [
        ModifierLayer.Default,
        ModifierLayer.Shift,
        ModifierLayer.AltGr,
        ModifierLayer.ShiftAltGr
    ];

    private readonly IXkbKeyNameMapper _keyNameMapper;
    private readonly IXkbKeysymMapper _keysymMapper;

    public XkbLayoutTranslator()
        : this(new XkbKeyNameMapper(), new XkbKeysymMapper())
    {
    }

    public XkbLayoutTranslator(IXkbKeyNameMapper keyNameMapper, IXkbKeysymMapper keysymMapper)
    {
        _keyNameMapper = keyNameMapper ?? throw new ArgumentNullException(nameof(keyNameMapper));
        _keysymMapper = keysymMapper ?? throw new ArgumentNullException(nameof(keysymMapper));
    }

    public XkbTranslationResult Translate(KeyboardProject project, XkbLayoutMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(metadata);

        var diagnostics = new List<XkbDiagnostic>();
        var mappings = new List<XkbKeyMapping>();
        foreach (var mapping in project.Layout.Mappings.OrderBy(item => item.KeyId, StringComparer.Ordinal))
        {
            var keyNameResult = _keyNameMapper.Map(project.Keyboard.Id, mapping.KeyId);
            if (!keyNameResult.Success)
            {
                diagnostics.AddRange(keyNameResult.Diagnostics);
                continue;
            }

            var keysyms = TranslateKeysyms(mapping, diagnostics);
            if (keysyms is null)
            {
                continue;
            }

            mappings.Add(new XkbKeyMapping(
                mapping.KeyId,
                keyNameResult.KeyName!,
                SelectType(mapping.LogicalKey, keysyms.Length),
                keysyms));
        }

        if (diagnostics.Count > 0)
        {
            return new XkbTranslationResult(false, null, diagnostics);
        }

        var ordered = mappings.OrderBy(mapping => mapping.KeyName, StringComparer.Ordinal).ToArray();
        return new XkbTranslationResult(
            true,
            new XkbKeyboardLayout(metadata, ordered, ordered.Any(mapping => mapping.Keysyms.Count >= 3)),
            []);
    }

    private string[]? TranslateKeysyms(
        KeyMapping mapping,
        List<XkbDiagnostic> diagnostics)
    {
        var keysyms = new string[Layers.Length];
        var highestLevel = -1;
        for (var index = 0; index < Layers.Length; index++)
        {
            if (mapping.Outputs.TryGetValue(Layers[index], out var output))
            {
                if (!_keysymMapper.TryMap(output, out keysyms[index]))
                {
                    diagnostics.Add(new XkbDiagnostic(
                        UnsupportedOutputCode,
                        $"Output on layer '{Layers[index]}' cannot be represented as an XKB keysym.",
                        mapping.KeyId));
                    return null;
                }
            }
            else
            {
                keysyms[index] = "NoSymbol";
            }

            if (!string.Equals(keysyms[index], "NoSymbol", StringComparison.Ordinal))
            {
                highestLevel = index;
            }
        }

        if (highestLevel < 0)
        {
            if (!_keysymMapper.TryMap(mapping.LogicalKey, out keysyms[0]))
            {
                diagnostics.Add(new XkbDiagnostic(
                    UnsupportedOutputCode,
                    $"Logical key '{mapping.LogicalKey}' cannot be represented as an XKB keysym.",
                    mapping.KeyId));
                return null;
            }

            highestLevel = 0;
        }

        return keysyms[..(highestLevel + 1)];
    }

    private static XkbKeyType SelectType(LogicalKey logicalKey, int levelCount)
    {
        var alphabetic = logicalKey is >= LogicalKey.A and <= LogicalKey.Z;
        return levelCount switch
        {
            <= 1 => XkbKeyType.OneLevel,
            2 when alphabetic => XkbKeyType.Alphabetic,
            2 => XkbKeyType.TwoLevel,
            _ when alphabetic => XkbKeyType.FourLevelAlphabetic,
            _ => XkbKeyType.FourLevel
        };
    }
}
