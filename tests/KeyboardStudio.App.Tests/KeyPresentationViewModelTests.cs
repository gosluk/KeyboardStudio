using KeyboardStudio.App;
using KeyboardStudio.Core;
using Xunit;

namespace KeyboardStudio.App.Tests;

public sealed class KeyPresentationViewModelTests
{
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
