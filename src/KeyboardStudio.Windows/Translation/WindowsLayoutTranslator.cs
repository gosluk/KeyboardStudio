using KeyboardStudio.Core;

namespace KeyboardStudio.Windows;

public static class WindowsLayoutTranslator
{
    public static WindowsKeyboardLayout Translate(KeyboardProject project)
    {
        var physicalKeys = project.Keyboard.Keys.ToDictionary(key => key.Id, StringComparer.Ordinal);
        var vscToVkMappings = new List<VscToVkMapping>();
        var extendedVscToVkMappings = new List<ExtendedVscToVkMapping>();
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
            entries);
    }
}
