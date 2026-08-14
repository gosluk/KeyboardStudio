using KeyboardStudio.Core;
using KeyboardStudio.Persistence;
using Xunit;

namespace KeyboardStudio.Core.Tests;

public sealed class KeyboardEditorTests
{
    [Fact]
    public void MapCharacter_WhenAltGrCharacterIsAssigned_UpdatesSelectedLayer()
    {
        var project = DemoProjectFactory.Create();
        var editor = new KeyboardEditor(project);

        editor.MapCharacter("KeyA", ModifierLayer.AltGr, "ą");

        var mapping = project.Layout.Find("KeyA");
        var output = Assert.IsType<CharacterOutput>(mapping!.Outputs[ModifierLayer.AltGr]);
        Assert.Equal("ą", output.Value);
    }

    [Theory]
    [InlineData(LogicalKey.A)]
    [InlineData(LogicalKey.Digit7)]
    [InlineData(LogicalKey.Semicolon)]
    [InlineData(LogicalKey.Enter)]
    [InlineData(LogicalKey.NumpadEnter)]
    [InlineData(LogicalKey.ArrowLeft)]
    public void MapLogicalKey_WhenSupportedConceptIsAssigned_UpdatesMapping(LogicalKey logicalKey)
    {
        var project = DemoProjectFactory.Create();
        var editor = new KeyboardEditor(project);

        editor.MapLogicalKey("KeyA", logicalKey);

        Assert.Equal(logicalKey, project.Layout.Find("KeyA")?.LogicalKey);
    }

    [Fact]
    public void MapCharacter_WhenSupplementaryUnicodeScalarIsAssigned_AcceptsOutput()
    {
        var project = DemoProjectFactory.Create();
        var editor = new KeyboardEditor(project);

        editor.MapCharacter("KeyA", ModifierLayer.AltGr, "😀");

        var output = Assert.IsType<CharacterOutput>(
            project.Layout.Find("KeyA")?.Outputs[ModifierLayer.AltGr]);
        Assert.Equal("😀", output.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("a\u0301")]
    [InlineData("\uD800")]
    public void MapCharacter_WhenValueIsNotOneUnicodeScalar_RejectsOutput(string value)
    {
        var project = DemoProjectFactory.Create();
        var editor = new KeyboardEditor(project);

        var exception = Assert.Throws<ArgumentException>(() =>
            editor.MapCharacter("KeyA", ModifierLayer.AltGr, value));

        Assert.Contains("one Unicode scalar", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(project.Layout.Find("KeyA")!.Outputs.ContainsKey(ModifierLayer.AltGr));
    }

    [Fact]
    public void Validate_WhenPhysicalKeyIdIsDuplicated_ReportsKey001Error()
    {
        var project = DemoProjectFactory.Create();
        project.Keyboard.Keys.Add(new PhysicalKey
        {
            Id = "KeyA",
            ScanCode = 0x60
        });

        var issues = new KeyboardProjectValidator().Validate(project);

        Assert.Contains(issues, issue => issue.Code == "KEY001" && issue.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void Validate_WhenProjectVersionIsEmpty_ReportsMeta002Error()
    {
        var source = DemoProjectFactory.Create();
        var project = new KeyboardProject
        {
            Metadata = new ProjectMetadata
            {
                Name = source.Metadata.Name,
                Description = source.Metadata.Description,
                Version = string.Empty,
                Language = source.Metadata.Language
            },
            Keyboard = source.Keyboard,
            Layout = source.Layout
        };

        var issues = new KeyboardProjectValidator().Validate(project);

        Assert.Contains(issues, issue => issue.Code == "META002" && issue.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void Validate_WhenLanguageIsEmpty_ReportsMeta003Error()
    {
        var source = DemoProjectFactory.Create();
        var project = new KeyboardProject
        {
            Metadata = new ProjectMetadata
            {
                Name = source.Metadata.Name,
                Description = source.Metadata.Description,
                Version = source.Metadata.Version,
                Language = string.Empty
            },
            Keyboard = source.Keyboard,
            Layout = source.Layout
        };

        var issues = new KeyboardProjectValidator().Validate(project);

        Assert.Contains(issues, issue => issue.Code == "META003" && issue.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public async Task SaveAndLoad_WhenCharacterOutputExists_PreservesPolymorphicOutput()
    {
        var project = DemoProjectFactory.Create();
        new KeyboardEditor(project).MapCharacter("KeyA", ModifierLayer.AltGr, "ą");
        var store = new JsonKeyboardProjectStore();
        await using var stream = new MemoryStream();

        await store.SaveAsync(project, stream);
        stream.Position = 0;
        var loaded = await store.LoadAsync(stream);

        var output = Assert.IsType<CharacterOutput>(loaded.Layout.Find("KeyA")!.Outputs[ModifierLayer.AltGr]);
        Assert.Equal("ą", output.Value);
        Assert.Equal(project.Keyboard.Keys.Count, loaded.Keyboard.Keys.Count);
    }

    [Fact]
    public async Task SaveAndLoad_WhenProjectMetadataExists_PreservesMetadata()
    {
        var project = DemoProjectFactory.Create();
        var store = new JsonKeyboardProjectStore();
        await using var stream = new MemoryStream();

        await store.SaveAsync(project, stream);
        stream.Position = 0;
        var loaded = await store.LoadAsync(stream);

        Assert.Equal(project.Metadata.Name, loaded.Metadata.Name);
        Assert.Equal(project.Metadata.Description, loaded.Metadata.Description);
        Assert.Equal(project.Metadata.Version, loaded.Metadata.Version);
        Assert.Equal(project.Metadata.Language, loaded.Metadata.Language);
    }
}
