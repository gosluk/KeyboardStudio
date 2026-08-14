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
}
