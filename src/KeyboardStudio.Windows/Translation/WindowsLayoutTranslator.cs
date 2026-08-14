using KeyboardStudio.Core;

namespace KeyboardStudio.Windows;

internal static class WindowsLayoutTranslator
{
    public static WindowsKeyboardLayout Translate(KeyboardProject project)
    {
        var physicalKeys = project.Keyboard.Keys.ToDictionary(key => key.Id, StringComparer.Ordinal);
        var entries = new List<WindowsMappingEntry>();

        foreach (var mapping in project.Layout.Mappings.OrderBy(mapping => mapping.KeyId, StringComparer.Ordinal))
        {
            if (!physicalKeys.TryGetValue(mapping.KeyId, out var key))
            {
                continue;
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

        return new WindowsKeyboardLayout(entries);
    }
}
