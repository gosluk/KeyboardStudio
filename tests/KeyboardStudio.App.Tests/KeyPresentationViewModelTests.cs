using KeyboardStudio.App;
using KeyboardStudio.Core;
using KeyboardStudio.Persistence;
using Xunit;

namespace KeyboardStudio.App.Tests;

public sealed class KeyPresentationViewModelTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Layers_WhenCreated_ExposeTheFourSupportedFriendlyLabels()
    {
        var editor = new MainWindowViewModel().Editor;

        Assert.Collection(
            editor.Layers,
            layer => Assert.Equal((ModifierLayer.Default, "Default"), (layer.Value, layer.Label)),
            layer => Assert.Equal((ModifierLayer.Shift, "Shift"), (layer.Value, layer.Label)),
            layer => Assert.Equal((ModifierLayer.AltGr, "AltGr"), (layer.Value, layer.Label)),
            layer => Assert.Equal((ModifierLayer.ShiftAltGr, "Shift + AltGr"), (layer.Value, layer.Label)));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ActiveLayerOption_WhenChanged_UsesStableCoreLayerValue()
    {
        var editor = new MainWindowViewModel().Editor;

        editor.ActiveLayerOption = editor.Layers.Single(layer => layer.Value == ModifierLayer.AltGr);

        Assert.Equal(ModifierLayer.AltGr, editor.ActiveLayer);
        Assert.Equal("AltGr", editor.ActiveLayerOption.Label);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void SelectCommand_WhenAnotherKeyIsSelected_MovesSelectedState()
    {
        var editor = new MainWindowViewModel().Editor;
        var firstKey = editor.Keys[0];
        var secondKey = editor.Keys[1];

        Assert.True(firstKey.IsSelected);
        Assert.False(secondKey.IsSelected);

        secondKey.SelectCommand.Execute(null);

        Assert.False(firstKey.IsSelected);
        Assert.True(secondKey.IsSelected);
        Assert.Same(secondKey, editor.SelectedKey);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ActiveLayer_WhenChanged_PreservesSelectedKey()
    {
        var editor = new MainWindowViewModel().Editor;
        var selectedKey = editor.Keys[10];
        selectedKey.SelectCommand.Execute(null);

        editor.ActiveLayer = ModifierLayer.ShiftAltGr;

        Assert.Same(selectedKey, editor.SelectedKey);
        Assert.True(selectedKey.IsSelected);
        Assert.Single(editor.Keys, key => key.IsSelected);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void SelectKey_WhenKeyExists_SelectsItAndRefreshesDetails()
    {
        var editor = new MainWindowViewModel().Editor;

        var selected = editor.SelectKey("KeyA");

        Assert.True(selected);
        Assert.Equal("KeyA", editor.SelectedKey?.KeyId);
        Assert.Equal("0x1E", editor.SelectedKey?.ScanCode);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void SelectKey_WhenKeyDoesNotExist_PreservesSelection()
    {
        var editor = new MainWindowViewModel().Editor;
        var original = editor.SelectedKey;

        var selected = editor.SelectKey("MissingKey");

        Assert.False(selected);
        Assert.Same(original, editor.SelectedKey);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void SelectedOutput_WhenMappingChanges_UpdatesPresentationStateForActiveLayer()
    {
        var editor = new MainWindowViewModel().Editor;
        var selectedKey = Assert.IsType<KeyViewModel>(editor.SelectedKey);

        Assert.Equal(selectedKey.KeyId, selectedKey.Hint);
        Assert.True(selectedKey.IsUnmapped);

        editor.SelectedOutput = "x";

        Assert.Equal("x", selectedKey.Label);
        Assert.False(selectedKey.IsUnmapped);

        editor.ActiveLayer = ModifierLayer.Shift;

        Assert.True(selectedKey.IsUnmapped);
        Assert.NotEqual("x", selectedKey.Label);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void LayerMappings_WhenKeyIsSelected_ShowAllFourOutputsAtOnce()
    {
        var editor = new MainWindowViewModel().Editor;
        Assert.True(editor.SelectKey("KeyA"));

        editor.LayerMappings.Single(mapping => mapping.Layer == ModifierLayer.Default).Output = "a";
        editor.LayerMappings.Single(mapping => mapping.Layer == ModifierLayer.Shift).Output = "A";
        editor.LayerMappings.Single(mapping => mapping.Layer == ModifierLayer.AltGr).Output = "ą";
        editor.LayerMappings.Single(mapping => mapping.Layer == ModifierLayer.ShiftAltGr).Output = "Ą";

        Assert.Collection(
            editor.LayerMappings,
            mapping => Assert.Equal((ModifierLayer.Default, "a"), (mapping.Layer, mapping.Output)),
            mapping => Assert.Equal((ModifierLayer.Shift, "A"), (mapping.Layer, mapping.Output)),
            mapping => Assert.Equal((ModifierLayer.AltGr, "ą"), (mapping.Layer, mapping.Output)),
            mapping => Assert.Equal((ModifierLayer.ShiftAltGr, "Ą"), (mapping.Layer, mapping.Output)));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void SelectedKey_WhenChanged_RefreshesMappingPanelDetails()
    {
        var editor = new MainWindowViewModel().Editor;
        Assert.True(editor.SelectKey("KeyA"));
        editor.LayerMappings[0].Output = "a";

        Assert.True(editor.SelectKey("KeyB"));

        Assert.Equal("KeyB", editor.SelectedKey?.KeyId);
        Assert.Equal("0x30", editor.SelectedKey?.ScanCode);
        Assert.Equal(LogicalKey.None, editor.SelectedLogicalKey);
        Assert.All(editor.LayerMappings, mapping => Assert.Empty(mapping.Output));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void SelectedLogicalKey_WhenChanged_UpdatesSelectedKeyMapping()
    {
        var editor = new MainWindowViewModel().Editor;
        Assert.True(editor.SelectKey("Enter"));

        editor.SelectedLogicalKey = LogicalKey.Enter;

        Assert.Equal(LogicalKey.Enter, editor.SelectedKey?.Mapping?.LogicalKey);
        Assert.Equal("Enter", editor.SelectedKey?.Hint);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void LayerMapping_WhenMultipleScalarsAreEntered_ShowsErrorAndPreservesDomainOutput()
    {
        var viewModel = new MainWindowViewModel();
        var editor = viewModel.Editor;
        Assert.True(editor.SelectKey("KeyA"));
        var mapping = editor.LayerMappings.Single(item => item.Layer == ModifierLayer.Default);
        mapping.Output = "a";

        mapping.Output = "ab";

        Assert.True(mapping.HasValidationError);
        Assert.Contains("one Unicode scalar", mapping.ValidationMessage, StringComparison.OrdinalIgnoreCase);
        var output = Assert.IsType<CharacterOutput>(
            viewModel.Project.Layout.Find("KeyA")?.Outputs[ModifierLayer.Default]);
        Assert.Equal("a", output.Value);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ClearCommands_WhenInvoked_ClearOneOrAllSelectedOutputs()
    {
        var editor = new MainWindowViewModel().Editor;
        Assert.True(editor.SelectKey("KeyA"));
        editor.LayerMappings[0].Output = "a";
        editor.LayerMappings[1].Output = "A";

        editor.LayerMappings[0].ClearCommand.Execute(null);

        Assert.Empty(editor.LayerMappings[0].Output);
        Assert.Equal("A", editor.LayerMappings[1].Output);

        editor.ClearAllOutputsCommand.Execute(null);

        Assert.All(editor.LayerMappings, mapping => Assert.Empty(mapping.Output));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void UnmapLogicalKeyCommand_WhenInvoked_SetsLogicalKeyToNone()
    {
        var editor = new MainWindowViewModel().Editor;
        Assert.True(editor.SelectKey("Enter"));
        editor.SelectedLogicalKey = LogicalKey.Enter;

        editor.UnmapLogicalKeyCommand.Execute(null);

        Assert.Equal(LogicalKey.None, editor.SelectedLogicalKey);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void MappingMutation_WhenValueChanges_MarksDocumentDirtyOnlyOnce()
    {
        var document = new ProjectDocumentService(
            new JsonKeyboardProjectStore(),
            DemoProjectFactory.Create);
        var project = document.CreateNew();
        var template = new KeyboardTemplateProvider().Templates.Single(item => item.Id == "iso-105");
        var changes = 0;
        var editor = new KeyboardEditorViewModel(
            new KeyboardEditor(project),
            template,
            () =>
            {
                changes++;
                document.MarkDirty();
            });
        Assert.True(editor.SelectKey("KeyA"));

        editor.LayerMappings[0].Output = "x";
        editor.LayerMappings[0].Output = "x";

        Assert.True(document.IsDirty);
        Assert.Equal(1, changes);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void InvalidMappingMutation_WhenRejected_DoesNotMarkDocumentDirty()
    {
        var changes = 0;
        var project = DemoProjectFactory.Create();
        var template = new KeyboardTemplateProvider().Templates.Single(item => item.Id == "iso-105");
        var editor = new KeyboardEditorViewModel(
            new KeyboardEditor(project),
            template,
            () => changes++);
        Assert.True(editor.SelectKey("KeyA"));

        editor.LayerMappings[0].Output = "ab";

        Assert.Equal(0, changes);
    }
}
