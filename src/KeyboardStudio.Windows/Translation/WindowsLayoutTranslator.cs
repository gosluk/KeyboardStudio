using KeyboardStudio.Core;

namespace KeyboardStudio.Windows;

public static class WindowsLayoutTranslator
{
    public static WindowsKeyboardLayout Translate(KeyboardProject project)
    {
        var physicalKeys = project.Keyboard.Keys.ToDictionary(key => key.Id, StringComparer.Ordinal);
        var vscToVkMappings = new List<VscToVkMapping>();
        var extendedVscToVkMappings = new List<ExtendedVscToVkMapping>();
        var characters = new List<WindowsCharacterMapping>();
        var entries = new List<WindowsMappingEntry>();

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

                if (mapping.Outputs.Values.OfType<CharacterOutput>().Any())
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

            foreach (var output in mapping.Outputs.OrderBy(pair => pair.Key))
            {
                if (output.Value is not CharacterOutput characterOutput || string.IsNullOrEmpty(characterOutput.Value))
                {
                    continue;
                }

                var rune = characterOutput.Value.EnumerateRunes().First();
                entries.Add(new WindowsMappingEntry((byte)key.ScanCode, output.Key, rune.Value));
            }
        }

        return new WindowsKeyboardLayout(
            vscToVkMappings.OrderBy(mapping => mapping.ScanCode).ThenBy(mapping => mapping.VirtualKey).ToArray(),
            extendedVscToVkMappings.OrderBy(mapping => mapping.ScanCode).ThenBy(mapping => mapping.VirtualKey).ToArray(),
            WindowsModifierTable.CreateV1(),
            new WindowsCharacterTable(
                characters.Any(mapping => mapping.AltGr.HasValue || mapping.ShiftAltGr.HasValue) ? 4 : 2,
                characters.OrderBy(mapping => mapping.VirtualKey).ToArray()),
            entries);
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
