using KeyboardStudio.Build;
using KeyboardStudio.Core;
using KeyboardStudio.Windows;
using Xunit;

namespace KeyboardStudio.Windows.Tests;

public sealed class WindowsGoldenFileTests
{
    public static TheoryData<string, KeyboardProject, WindowsLayoutMetadata> Fixtures => new()
    {
        { "MinimalUs", CreateMinimalUs(), new WindowsLayoutMetadata("kbd-minimal", "Minimal US") },
        {
            "AltGrUnicode",
            CreateAltGrUnicode(),
            new WindowsLayoutMetadata("kbd-altgr", "AltGr Unicode", "1.2.0.0")
        },
        { "IsoExample", CreateIsoExample(), new WindowsLayoutMetadata("kbd-iso", "ISO Example", "2.0.0.0") }
    };

    [Theory]
    [MemberData(nameof(Fixtures))]
    public async Task Generate_WhenComparedWithReferenceFixture_MatchesEveryFileExactly(
        string fixtureName,
        KeyboardProject project,
        WindowsLayoutMetadata metadata)
    {
        var artifact = await new WindowsArtifactGenerator(metadata).GenerateAsync(
            project,
            new BuildOptions(BuildTarget.WindowsX64, "out"));
        var fixtureDirectory = GetFixtureDirectory(fixtureName);

        if (string.Equals(
                Environment.GetEnvironmentVariable("KEYBOARDSTUDIO_UPDATE_GOLDENS"),
                "1",
                StringComparison.Ordinal))
        {
            fixtureDirectory = Path.Combine(FindRepositoryRoot(), "tests", "KeyboardStudio.Windows.Tests", "Fixtures", fixtureName);
            Directory.CreateDirectory(fixtureDirectory);
            foreach (var file in artifact.Source.Files)
            {
                await File.WriteAllTextAsync(Path.Combine(fixtureDirectory, file.Key), file.Value);
            }
        }

        var expectedFiles = Directory.GetFiles(fixtureDirectory)
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(artifact.Source.Files.Keys, expectedFiles);

        foreach (var file in artifact.Source.Files)
        {
            var expected = await File.ReadAllTextAsync(Path.Combine(fixtureDirectory, file.Key));
            Assert.Equal(NormalizeNewlines(expected), NormalizeNewlines(file.Value));
        }
    }

    private static KeyboardProject CreateMinimalUs() =>
        CreateProject(
            "minimal-us",
            new TestKey("KeyA", 0x1E, false, LogicalKey.A, 'a', 'A'));

    private static KeyboardProject CreateAltGrUnicode() =>
        CreateProject(
            "altgr-unicode",
            new TestKey("KeyA", 0x1E, false, LogicalKey.A, 'a', 'A', 'ą', 'Ą'),
            new TestKey("ArrowLeft", 0x4B, true, LogicalKey.ArrowLeft));

    private static KeyboardProject CreateIsoExample() =>
        CreateProject(
            "iso-example",
            new TestKey("IntlBackslash", 0x56, false, LogicalKey.InternationalBackslash, '\\', '|'),
            new TestKey("Enter", 0x1C, false, LogicalKey.Enter));

    private static KeyboardProject CreateProject(string id, params TestKey[] keys)
    {
        var physicalKeys = keys.Select(key => new PhysicalKey
        {
            Id = key.Id,
            ScanCode = key.ScanCode,
            Extended = key.Extended
        }).ToList();
        var mappings = keys.Select(key =>
        {
            var mapping = new KeyMapping
            {
                KeyId = key.Id,
                LogicalKey = key.LogicalKey
            };
            AddCharacter(mapping, ModifierLayer.Default, key.Default);
            AddCharacter(mapping, ModifierLayer.Shift, key.Shift);
            AddCharacter(mapping, ModifierLayer.AltGr, key.AltGr);
            AddCharacter(mapping, ModifierLayer.ShiftAltGr, key.ShiftAltGr);
            return mapping;
        }).ToList();

        return new KeyboardProject
        {
            Metadata = new ProjectMetadata
            {
                Name = id,
                Description = "Golden source fixture",
                Version = "1.0.0",
                Language = "en-US"
            },
            Keyboard = new PhysicalKeyboard { Id = id, Keys = physicalKeys },
            Layout = new KeyboardLayout { Mappings = mappings }
        };
    }

    private static void AddCharacter(KeyMapping mapping, ModifierLayer layer, char? value)
    {
        if (value.HasValue)
        {
            mapping.Outputs[layer] = new CharacterOutput(value.Value.ToString());
        }
    }

    private static string GetFixtureDirectory(string fixtureName) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureName);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Environment.CurrentDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "KeyboardStudio.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not find the KeyboardStudio repository root.");
    }

    private static string NormalizeNewlines(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);

    private sealed record TestKey(
        string Id,
        byte ScanCode,
        bool Extended,
        LogicalKey LogicalKey,
        char? Default = null,
        char? Shift = null,
        char? AltGr = null,
        char? ShiftAltGr = null);
}
