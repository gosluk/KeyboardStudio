using System.Text;
using System.Text.Json;
using KeyboardStudio.Core;
using KeyboardStudio.Persistence;
using Xunit;

namespace KeyboardStudio.Core.Tests;

public sealed class ProjectPersistenceTests
{
    [Fact]
    public async Task SaveAndLoad_WhenCurrentSchemaIsUsed_PreservesSchemaVersion()
    {
        var project = DemoProjectFactory.Create();
        var store = new JsonKeyboardProjectStore();
        await using var stream = new MemoryStream();

        await store.SaveAsync(project, stream);
        stream.Position = 0;

        using (var document = await JsonDocument.ParseAsync(stream))
        {
            Assert.Equal(
                KeyboardProjectSchema.CurrentVersion,
                document.RootElement.GetProperty("schemaVersion").GetInt32());
        }

        stream.Position = 0;
        var loaded = await store.LoadAsync(stream);

        Assert.Equal(KeyboardProjectSchema.CurrentVersion, loaded.SchemaVersion);
    }

    [Fact]
    public async Task SaveAndLoad_WhenAllMappedOutputKindsExist_PreservesEquivalentDomainState()
    {
        var project = DemoProjectFactory.Create();
        var mapping = project.Layout.Find("KeyA")!;
        mapping.Outputs[ModifierLayer.AltGr] = new SpecialKeyOutput(LogicalKey.Space);
        mapping.Outputs[ModifierLayer.ShiftAltGr] = new NoOutput();
        var store = new JsonKeyboardProjectStore();
        await using var stream = new MemoryStream();

        await store.SaveAsync(project, stream);
        stream.Position = 0;
        var loaded = await store.LoadAsync(stream);

        AssertEquivalent(project, loaded);
    }

    [Fact]
    public async Task LoadAsync_WhenSchemaVersionIsMissing_ReportsMissingSchemaVersion()
    {
        var store = new JsonKeyboardProjectStore();
        using var stream = CreateStream("{}");

        var exception = await Assert.ThrowsAsync<ProjectLoadException>(() => store.LoadAsync(stream));

        Assert.Equal(ProjectLoadErrorCode.MissingSchemaVersion, exception.ErrorCode);
        Assert.Null(exception.SchemaVersion);
    }

    [Fact]
    public async Task LoadAsync_WhenSchemaVersionIsFuture_RejectsProject()
    {
        var futureVersion = KeyboardProjectSchema.CurrentVersion + 1;
        var store = new JsonKeyboardProjectStore();
        using var stream = CreateStream($"{{\"schemaVersion\":{futureVersion}}}");

        var exception = await Assert.ThrowsAsync<ProjectLoadException>(() => store.LoadAsync(stream));

        Assert.Equal(ProjectLoadErrorCode.UnsupportedFutureSchema, exception.ErrorCode);
        Assert.Equal(futureVersion, exception.SchemaVersion);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("\"1\"")]
    public async Task LoadAsync_WhenSchemaVersionIsInvalid_ReportsInvalidSchemaVersion(string schemaValue)
    {
        var store = new JsonKeyboardProjectStore();
        using var stream = CreateStream($"{{\"schemaVersion\":{schemaValue}}}");

        var exception = await Assert.ThrowsAsync<ProjectLoadException>(() => store.LoadAsync(stream));

        Assert.Equal(ProjectLoadErrorCode.InvalidSchemaVersion, exception.ErrorCode);
    }

    [Fact]
    public async Task LoadAsync_WhenDtoLogicalKeyIsUnknown_ReportsInvalidProject()
    {
        var json = $$"""
            {
              "schemaVersion": {{KeyboardProjectSchema.CurrentVersion}},
              "metadata": {
                "name": "Invalid project",
                "description": "",
                "version": "1.0.0",
                "language": "und"
              },
              "keyboard": {
                "id": "test",
                "keys": []
              },
              "layout": {
                "mappings": [
                  {
                    "keyId": "KeyA",
                    "logicalKey": "futureKey",
                    "outputs": {}
                  }
                ]
              }
            }
            """;
        var store = new JsonKeyboardProjectStore();
        using var stream = CreateStream(json);

        var exception = await Assert.ThrowsAsync<ProjectLoadException>(() => store.LoadAsync(stream));

        Assert.Equal(ProjectLoadErrorCode.InvalidProject, exception.ErrorCode);
        Assert.Equal(KeyboardProjectSchema.CurrentVersion, exception.SchemaVersion);
    }

    [Fact]
    public async Task LoadAsync_WhenJsonIsMalformed_ReportsInvalidJson()
    {
        var store = new JsonKeyboardProjectStore();
        using var stream = CreateStream("{");

        var exception = await Assert.ThrowsAsync<ProjectLoadException>(() => store.LoadAsync(stream));

        Assert.Equal(ProjectLoadErrorCode.InvalidJson, exception.ErrorCode);
    }

    [Fact]
    public async Task SaveAsync_WhenSchemaVersionIsNotCurrent_RejectsProject()
    {
        var source = DemoProjectFactory.Create();
        var project = new KeyboardProject
        {
            SchemaVersion = KeyboardProjectSchema.CurrentVersion + 1,
            Metadata = source.Metadata,
            Keyboard = source.Keyboard,
            Layout = source.Layout
        };
        var store = new JsonKeyboardProjectStore();
        await using var stream = new MemoryStream();

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(project, stream));
    }

    private static void AssertEquivalent(KeyboardProject expected, KeyboardProject actual)
    {
        Assert.Equal(expected.SchemaVersion, actual.SchemaVersion);
        Assert.Equal(expected.Metadata.Name, actual.Metadata.Name);
        Assert.Equal(expected.Metadata.Description, actual.Metadata.Description);
        Assert.Equal(expected.Metadata.Version, actual.Metadata.Version);
        Assert.Equal(expected.Metadata.Language, actual.Metadata.Language);
        Assert.Equal(expected.Keyboard.Id, actual.Keyboard.Id);
        Assert.Equal(expected.Keyboard.Keys.Count, actual.Keyboard.Keys.Count);
        Assert.Equal(expected.Layout.Mappings.Count, actual.Layout.Mappings.Count);

        for (var index = 0; index < expected.Keyboard.Keys.Count; index++)
        {
            var expectedKey = expected.Keyboard.Keys[index];
            var actualKey = actual.Keyboard.Keys[index];
            Assert.Equal(expectedKey.Id, actualKey.Id);
            Assert.Equal(expectedKey.ScanCode, actualKey.ScanCode);
            Assert.Equal(expectedKey.Extended, actualKey.Extended);
            Assert.Equal(expectedKey.X, actualKey.X);
            Assert.Equal(expectedKey.Y, actualKey.Y);
            Assert.Equal(expectedKey.Width, actualKey.Width);
            Assert.Equal(expectedKey.Height, actualKey.Height);
        }

        foreach (var expectedMapping in expected.Layout.Mappings)
        {
            var actualMapping = actual.Layout.Find(expectedMapping.KeyId);
            Assert.NotNull(actualMapping);
            Assert.Equal(expectedMapping.LogicalKey, actualMapping.LogicalKey);
            Assert.Equal(expectedMapping.Outputs.Count, actualMapping.Outputs.Count);

            foreach (var expectedOutput in expectedMapping.Outputs)
            {
                Assert.True(actualMapping.Outputs.TryGetValue(expectedOutput.Key, out var actualOutput));
                AssertOutputEquivalent(expectedOutput.Value, actualOutput!);
            }
        }
    }

    private static void AssertOutputEquivalent(KeyOutput expected, KeyOutput actual)
    {
        switch (expected)
        {
            case CharacterOutput expectedCharacter:
                Assert.Equal(expectedCharacter.Value, Assert.IsType<CharacterOutput>(actual).Value);
                break;
            case SpecialKeyOutput expectedSpecialKey:
                Assert.Equal(expectedSpecialKey.Key, Assert.IsType<SpecialKeyOutput>(actual).Key);
                break;
            case NoOutput:
                Assert.IsType<NoOutput>(actual);
                break;
            default:
                throw new InvalidOperationException($"Unsupported expected output type '{expected.GetType().Name}'.");
        }
    }

    private static MemoryStream CreateStream(string content) => new(Encoding.UTF8.GetBytes(content));
}
