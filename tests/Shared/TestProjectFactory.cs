using KeyboardStudio.Core;

namespace KeyboardStudio.Testing;

/// <summary>
/// Builds a small, fully mapped project used as a fixture across the test assemblies.
/// This is deliberately not the shipping <c>us-basic</c> seed: tests want a stable,
/// minimal graph that does not change when the seed's content is revised.
/// </summary>
public static class TestProjectFactory
{
    private static readonly (string Key, int ScanCode, double X, double Y)[] Keys =
    [
        ("Q", 0x10, 0.5, 0), ("W", 0x11, 1.5, 0), ("E", 0x12, 2.5, 0), ("R", 0x13, 3.5, 0),
        ("T", 0x14, 4.5, 0), ("Y", 0x15, 5.5, 0), ("U", 0x16, 6.5, 0), ("I", 0x17, 7.5, 0),
        ("O", 0x18, 8.5, 0), ("P", 0x19, 9.5, 0),
        ("A", 0x1E, 0.75, 1), ("S", 0x1F, 1.75, 1), ("D", 0x20, 2.75, 1), ("F", 0x21, 3.75, 1),
        ("G", 0x22, 4.75, 1), ("H", 0x23, 5.75, 1), ("J", 0x24, 6.75, 1), ("K", 0x25, 7.75, 1),
        ("L", 0x26, 8.75, 1),
        ("Z", 0x2C, 1.25, 2), ("X", 0x2D, 2.25, 2), ("C", 0x2E, 3.25, 2), ("V", 0x2F, 4.25, 2),
        ("B", 0x30, 5.25, 2), ("N", 0x31, 6.25, 2), ("M", 0x32, 7.25, 2)
    ];

    public static KeyboardProject Create()
    {
        var physicalKeys = Keys.Select(item => new PhysicalKey
        {
            Id = $"Key{item.Key}",
            ScanCode = item.ScanCode,
            X = item.X,
            Y = item.Y
        }).ToList();

        var mappings = Keys.Select(item =>
        {
            var logicalKey = Enum.Parse<LogicalKey>(item.Key, ignoreCase: false);
            return new KeyMapping
            {
                KeyId = $"Key{item.Key}",
                LogicalKey = logicalKey,
                Outputs =
                {
                    [ModifierLayer.Default] = new CharacterOutput(item.Key.ToLowerInvariant()),
                    [ModifierLayer.Shift] = new CharacterOutput(item.Key)
                }
            };
        }).ToList();

        return new KeyboardProject
        {
            Metadata = new ProjectMetadata
            {
                Name = "Demo layout",
                Description = "Minimal project used by the application skeleton.",
                Version = "0.1.0",
                Language = "und"
            },
            Keyboard = new PhysicalKeyboard
            {
                Id = "demo-iso-letter-block",
                Keys = physicalKeys
            },
            Layout = new KeyboardLayout
            {
                Mappings = mappings
            }
        };
    }
}
