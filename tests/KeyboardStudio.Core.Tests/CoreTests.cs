using KeyboardStudio.Core;
using KeyboardStudio.Persistence;
using KeyboardStudio.Testing;
using Xunit;

namespace KeyboardStudio.Core.Tests;

public sealed class KeyboardEditorTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void MapCharacter_WhenAltGrCharacterIsAssigned_UpdatesSelectedLayer()
    {
        var project = TestProjectFactory.Create();
        var editor = new KeyboardEditor(project);

        editor.MapCharacter("KeyA", ModifierLayer.AltGr, "ą");

        var mapping = project.Layout.Find("KeyA");
        var output = Assert.IsType<CharacterOutput>(mapping!.Outputs[ModifierLayer.AltGr]);
        Assert.Equal("ą", output.Value);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(LogicalKey.A)]
    [InlineData(LogicalKey.Digit7)]
    [InlineData(LogicalKey.Semicolon)]
    [InlineData(LogicalKey.Enter)]
    [InlineData(LogicalKey.NumpadEnter)]
    [InlineData(LogicalKey.ArrowLeft)]
    public void MapLogicalKey_WhenSupportedConceptIsAssigned_UpdatesMapping(LogicalKey logicalKey)
    {
        var project = TestProjectFactory.Create();
        var editor = new KeyboardEditor(project);

        editor.MapLogicalKey("KeyA", logicalKey);

        Assert.Equal(logicalKey, project.Layout.Find("KeyA")?.LogicalKey);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void MapCharacter_WhenSupplementaryUnicodeScalarIsAssigned_AcceptsOutput()
    {
        var project = TestProjectFactory.Create();
        var editor = new KeyboardEditor(project);

        editor.MapCharacter("KeyA", ModifierLayer.AltGr, "😀");

        var output = Assert.IsType<CharacterOutput>(
            project.Layout.Find("KeyA")?.Outputs[ModifierLayer.AltGr]);
        Assert.Equal("😀", output.Value);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("a\u0301")]
    [InlineData("\uD800")]
    public void MapCharacter_WhenValueIsNotOneUnicodeScalar_RejectsOutput(string value)
    {
        var project = TestProjectFactory.Create();
        var editor = new KeyboardEditor(project);

        var exception = Assert.Throws<ArgumentException>(() =>
            editor.MapCharacter("KeyA", ModifierLayer.AltGr, value));

        Assert.Contains("one Unicode scalar", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(project.Layout.Find("KeyA")!.Outputs.ContainsKey(ModifierLayer.AltGr));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ClearMapping_WhenLayerIsMapped_RemovesOnlySelectedLayer()
    {
        var project = TestProjectFactory.Create();
        var editor = new KeyboardEditor(project);

        editor.ClearMapping("KeyA", ModifierLayer.Default);

        var mapping = project.Layout.Find("KeyA")!;
        Assert.False(mapping.Outputs.ContainsKey(ModifierLayer.Default));
        Assert.True(mapping.Outputs.ContainsKey(ModifierLayer.Shift));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ClearAllOutputs_WhenKeyHasMappings_RemovesEveryLayerButKeepsLogicalKey()
    {
        var project = TestProjectFactory.Create();
        var editor = new KeyboardEditor(project);
        editor.MapCharacter("KeyA", ModifierLayer.AltGr, "ą");

        editor.ClearAllOutputs("KeyA");

        var mapping = project.Layout.Find("KeyA")!;
        Assert.Empty(mapping.Outputs);
        Assert.Equal(LogicalKey.A, mapping.LogicalKey);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Mutations_WhenValueChanges_ReturnChangeInformation()
    {
        var editor = new KeyboardEditor(TestProjectFactory.Create());

        Assert.True(editor.MapCharacter("KeyA", ModifierLayer.AltGr, "ą"));
        Assert.False(editor.MapCharacter("KeyA", ModifierLayer.AltGr, "ą"));
        Assert.True(editor.MapLogicalKey("KeyA", LogicalKey.Enter));
        Assert.False(editor.MapLogicalKey("KeyA", LogicalKey.Enter));
        Assert.True(editor.ClearMapping("KeyA", ModifierLayer.AltGr));
        Assert.False(editor.ClearMapping("KeyA", ModifierLayer.AltGr));
        Assert.True(editor.ClearAllOutputs("KeyA"));
        Assert.False(editor.ClearAllOutputs("KeyA"));
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("Map")]
    [InlineData("Clear")]
    [InlineData("ClearAll")]
    public void Mutation_WhenPhysicalKeyIdIsUnknown_RejectsOperation(string operation)
    {
        var editor = new KeyboardEditor(TestProjectFactory.Create());

        Assert.Throws<ArgumentException>(() =>
        {
            switch (operation)
            {
                case "Map":
                    editor.MapLogicalKey("MissingKey", LogicalKey.A);
                    break;
                case "Clear":
                    editor.ClearMapping("MissingKey", ModifierLayer.Default);
                    break;
                case "ClearAll":
                    editor.ClearAllOutputs("MissingKey");
                    break;
            }
        });
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Validate_WhenPhysicalKeyIdIsDuplicated_ReportsKey001Error()
    {
        var project = TestProjectFactory.Create();
        project.Keyboard.Keys.Add(new PhysicalKey
        {
            Id = "KeyA",
            ScanCode = 0x60
        });

        var issues = new KeyboardProjectValidator().Validate(project).Issues;

        Assert.Contains(issues, issue =>
            issue.Code == KeyboardProjectDiagnosticCodes.DuplicatePhysicalKeyId &&
            issue.Severity == ValidationSeverity.Error);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Validate_WhenProjectVersionIsEmpty_ReportsMeta002Error()
    {
        var source = TestProjectFactory.Create();
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

        var issues = new KeyboardProjectValidator().Validate(project).Issues;

        Assert.Contains(issues, issue =>
            issue.Code == KeyboardProjectDiagnosticCodes.MissingProjectVersion &&
            issue.Severity == ValidationSeverity.Error);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Validate_WhenLanguageIsEmpty_ReportsMeta003Error()
    {
        var source = TestProjectFactory.Create();
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

        var issues = new KeyboardProjectValidator().Validate(project).Issues;

        Assert.Contains(issues, issue =>
            issue.Code == KeyboardProjectDiagnosticCodes.MissingProjectLanguage &&
            issue.Severity == ValidationSeverity.Error);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SaveAndLoad_WhenCharacterOutputExists_PreservesPolymorphicOutput()
    {
        var project = TestProjectFactory.Create();
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
    [Trait("Category", "Unit")]
    public async Task SaveAndLoad_WhenProjectMetadataExists_PreservesMetadata()
    {
        var project = TestProjectFactory.Create();
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
