using KeyboardStudio.App;
using KeyboardStudio.Core;
using Xunit;

namespace KeyboardStudio.App.Tests;

public sealed class KeyPresentationViewModelTests
{
    [Fact]
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
    public void ActiveLayerOption_WhenChanged_UsesStableCoreLayerValue()
    {
        var editor = new MainWindowViewModel().Editor;

        editor.ActiveLayerOption = editor.Layers.Single(layer => layer.Value == ModifierLayer.AltGr);

        Assert.Equal(ModifierLayer.AltGr, editor.ActiveLayer);
        Assert.Equal("AltGr", editor.ActiveLayerOption.Label);
    }

    [Fact]
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
    public void SelectKey_WhenKeyExists_SelectsItAndRefreshesDetails()
    {
        var editor = new MainWindowViewModel().Editor;

        var selected = editor.SelectKey("KeyA");

        Assert.True(selected);
        Assert.Equal("KeyA", editor.SelectedKey?.KeyId);
        Assert.Equal("0x1E", editor.SelectedKey?.ScanCode);
    }

    [Fact]
    public void SelectKey_WhenKeyDoesNotExist_PreservesSelection()
    {
        var editor = new MainWindowViewModel().Editor;
        var original = editor.SelectedKey;

        var selected = editor.SelectKey("MissingKey");

        Assert.False(selected);
        Assert.Same(original, editor.SelectedKey);
    }

    [Fact]
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
}
