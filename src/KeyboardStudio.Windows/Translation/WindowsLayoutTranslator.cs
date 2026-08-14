using KeyboardStudio.Core;

namespace KeyboardStudio.Windows;

public static class WindowsLayoutTranslator
{
    public static WindowsKeyboardLayout Translate(KeyboardProject project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var validation = new KeyboardProjectValidator(
        [
            new PhysicalKeyboardValidationRule(),
            new MappingValidationRule(),
            new WindowsCompatibilityValidationRule()
        ]).Validate(project);
        if (validation.HasErrors)
        {
            throw new WindowsTranslationException(validation.Issues);
        }

        var physicalKeys = project.Keyboard.Keys.ToDictionary(key => key.Id, StringComparer.Ordinal);
        var vscToVkMappings = new List<VscToVkMapping>();
        var extendedVscToVkMappings = new List<ExtendedVscToVkMapping>();
        var keyNames = new List<WindowsKeyNameMapping>();
        var extendedKeyNames = new List<WindowsKeyNameMapping>();
        var characters = new List<WindowsCharacterMapping>();

        foreach (var mapping in project.Layout.Mappings.OrderBy(mapping => mapping.KeyId, StringComparer.Ordinal))
        {
            if (!physicalKeys.TryGetValue(mapping.KeyId, out var key))
            {
                continue;
            }

            if (WindowsVirtualKeyMapper.TryMap(mapping.LogicalKey, out var virtualKey))
            {
                var scanCode = checked((byte)key.ScanCode);
                if (key.Extended)
                {
                    extendedVscToVkMappings.Add(new ExtendedVscToVkMapping(scanCode, virtualKey));
                }
                else
                {
                    vscToVkMappings.Add(new VscToVkMapping(scanCode, virtualKey));
                }

                if (WindowsKeyNameMapper.TryGetDisplayName(mapping.LogicalKey, out var displayName))
                {
                    var keyName = new WindowsKeyNameMapping(scanCode, displayName);
                    if (key.Extended)
                    {
                        extendedKeyNames.Add(keyName);
                    }
                    else
                    {
                        keyNames.Add(keyName);
                    }
                }

                if (WindowsLogicalKeyClassifier.ProducesCharacters(mapping.LogicalKey) &&
                    mapping.Outputs.Values.OfType<CharacterOutput>().Any())
                {
                    characters.Add(new WindowsCharacterMapping(
                        virtualKey,
                        IsLetter(mapping.LogicalKey)
                            ? WindowsCharacterAttributes.CapsLock
                            : WindowsCharacterAttributes.None,
                        GetCharacter(mapping, ModifierLayer.Default),
                        GetCharacter(mapping, ModifierLayer.Shift),
                        GetCharacter(mapping, ModifierLayer.AltGr),
                        GetCharacter(mapping, ModifierLayer.ShiftAltGr)));
                }
            }

        }

        return new WindowsKeyboardLayout(
            vscToVkMappings.OrderBy(mapping => mapping.ScanCode).ThenBy(mapping => mapping.VirtualKey).ToArray(),
            extendedVscToVkMappings.OrderBy(mapping => mapping.ScanCode).ThenBy(mapping => mapping.VirtualKey).ToArray(),
            keyNames.OrderBy(mapping => mapping.ScanCode).ThenBy(mapping => mapping.DisplayName, StringComparer.Ordinal).ToArray(),
            extendedKeyNames.OrderBy(mapping => mapping.ScanCode).ThenBy(mapping => mapping.DisplayName, StringComparer.Ordinal).ToArray(),
            WindowsModifierTable.CreateV1(),
            new WindowsCharacterTable(
                characters.Any(mapping => mapping.AltGr.HasValue || mapping.ShiftAltGr.HasValue) ? 4 : 2,
                characters.OrderBy(mapping => mapping.VirtualKey).ToArray()));
    }

    private static char? GetCharacter(KeyMapping mapping, ModifierLayer layer)
    {
        if (!mapping.Outputs.TryGetValue(layer, out var output) || output is not CharacterOutput characterOutput)
        {
            return null;
        }

        var rune = characterOutput.Value.EnumerateRunes().First();
        return checked((char)rune.Value);
    }

    private static bool IsLetter(LogicalKey logicalKey) =>
        logicalKey is >= LogicalKey.A and <= LogicalKey.Z;
}
