using KeyboardStudio.App;
using KeyboardStudio.Build;
using KeyboardStudio.Core;
using Xunit;

namespace KeyboardStudio.App.Tests;

/// <summary>
/// Covers assigning either kind of output from the selected-key panel.
/// </summary>
/// <remarks>
/// The panel used to hold a bare string, so a functional output — the kind an XKB import produces in
/// quantity — reached it as an empty row. These tests pin the three consequences of that, each of
/// which was a real defect: the assignment was invisible, Clear could not remove it, and typing a
/// character destroyed it without a word.
/// </remarks>
public sealed class LayerOutputAssignmentTests
{
    private static (MainWindowViewModel Model, KeyboardEditorViewModel Editor, KeyViewModel Key) Create()
    {
        var model = new MainWindowViewModel();
        var editor = model.Editor;
        var key = editor.Keys[0];
        key.SelectCommand.Execute(null);
        return (model, editor, key);
    }

    private static LayerMappingViewModel Row(KeyboardEditorViewModel editor, ModifierLayer layer) =>
        editor.LayerMappings.Single(mapping => mapping.Layer == layer);

    private static KeyOutput? Stored(MainWindowViewModel model, string keyId, ModifierLayer layer) =>
        model.Project.Layout.Find(keyId) is { } mapping &&
        mapping.Outputs.TryGetValue(layer, out var output)
            ? output
            : null;

    [Fact]
    [Trait("Category", "Unit")]
    public void AFunctionalKeyCanBeAssignedAndReachesTheModelAsASpecialKeyOutput()
    {
        var (model, editor, key) = Create();
        var row = Row(editor, ModifierLayer.Default);

        row.SpecialKey = row.SpecialKeys.Single(option => option.Key == LogicalKey.Numpad8);

        Assert.Equal(KeyOutputKind.SpecialKey, row.Kind);
        Assert.Equal(
            new SpecialKeyOutput(LogicalKey.Numpad8),
            Stored(model, key.KeyId, ModifierLayer.Default));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AFunctionalAssignmentIsVisibleWhenTheKeyIsSelectedAgain()
    {
        var (_, editor, key) = Create();
        Row(editor, ModifierLayer.Shift).SpecialKey =
            editor.LayerMappings[0].SpecialKeys.Single(option => option.Key == LogicalKey.Numpad8);

        editor.Keys[1].SelectCommand.Execute(null);
        key.SelectCommand.Execute(null);

        var row = Row(editor, ModifierLayer.Shift);
        Assert.Equal(KeyOutputKind.SpecialKey, row.Kind);
        Assert.Equal(LogicalKey.Numpad8, row.SpecialKey?.Key);
        Assert.True(row.IsSpecialKey);
        Assert.False(row.IsCharacter);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ClearRemovesAFunctionalAssignmentRatherThanLeavingItInPlace()
    {
        var (model, editor, key) = Create();
        var row = Row(editor, ModifierLayer.Default);
        row.SpecialKey = row.SpecialKeys.Single(option => option.Key == LogicalKey.Numpad8);

        row.ClearCommand.Execute(null);

        Assert.Null(Stored(model, key.KeyId, ModifierLayer.Default));
        Assert.Equal(KeyOutputKind.None, row.Kind);
        Assert.Null(row.SpecialKey);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TheKeycapAndTheInspectorAgreeOnAFunctionalAssignment()
    {
        var (_, editor, key) = Create();
        var row = Row(editor, ModifierLayer.Default);

        row.SpecialKey = row.SpecialKeys.Single(option => option.Key == LogicalKey.Numpad8);

        // The keycap and its tooltip have always drawn this; the panel is what used to disagree.
        Assert.Equal("Num 8", key.DefaultAssignment);
        Assert.StartsWith("Num 8", row.SpecialKey!.Label, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TypingACharacterStillCostsOneKeystrokeAndSelectsItsOwnKind()
    {
        var (model, editor, key) = Create();
        var row = Row(editor, ModifierLayer.Default);

        row.Output = "q";

        Assert.Equal(KeyOutputKind.Character, row.Kind);
        Assert.True(row.IsCharacter);
        Assert.Equal(new CharacterOutput("q"), Stored(model, key.KeyId, ModifierLayer.Default));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void SwitchingKindToNoOutputClearsTheLayer()
    {
        var (model, editor, key) = Create();
        var row = Row(editor, ModifierLayer.AltGr);
        row.Output = "q";

        row.Kind = KeyOutputKind.None;

        Assert.Null(Stored(model, key.KeyId, ModifierLayer.AltGr));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ChoosingTheKeyKindWithoutAKeyYetLeavesTheLayerEmptyRatherThanInvalid()
    {
        var (model, editor, key) = Create();
        var row = Row(editor, ModifierLayer.ShiftAltGr);

        row.Kind = KeyOutputKind.SpecialKey;

        Assert.Null(Stored(model, key.KeyId, ModifierLayer.ShiftAltGr));
        Assert.False(row.HasValidationError);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TheSeedDocumentsOwnFunctionalKeysAreVisibleInThePanel()
    {
        // The document a new window opens on already carries dozens of functional outputs, so this
        // was never an import-only problem: every one of those keys showed an empty row.
        var (model, editor, _) = Create();

        var functional = model.Project.Layout.Mappings
            .SelectMany(mapping => mapping.Outputs.Values)
            .OfType<SpecialKeyOutput>()
            .ToList();
        Assert.NotEmpty(functional);

        var escape = editor.Keys.Single(key => key.KeyId == "Escape");
        escape.SelectCommand.Execute(null);

        var row = Row(editor, ModifierLayer.Default);
        Assert.Equal(KeyOutputKind.SpecialKey, row.Kind);
        Assert.Equal(LogicalKey.Escape, row.SpecialKey?.Key);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TypingOverAFunctionalAssignmentReplacesItThroughAVisibleKindChange()
    {
        var (model, editor, _) = Create();
        var escape = editor.Keys.Single(key => key.KeyId == "Escape");
        escape.SelectCommand.Execute(null);

        var row = Row(editor, ModifierLayer.Default);
        Assert.Equal(KeyOutputKind.SpecialKey, row.Kind);

        row.Output = "e";

        // Still allowed, but the row now says what it is rather than having looked empty all along.
        Assert.Equal(KeyOutputKind.Character, row.Kind);
        Assert.Equal(new CharacterOutput("e"), Stored(model, "Escape", ModifierLayer.Default));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TheFunctionalKeyPickerUsesTheSameWordsAsTheKeycap()
    {
        var (_, editor, _) = Create();
        var options = editor.LayerMappings[0].SpecialKeys;

        Assert.DoesNotContain(options, option => option.Key == LogicalKey.None);

        // A legend that is already a name stands alone; a bare glyph gets the enum after it.
        Assert.Contains(options, option => option.Label == "Num 8");
        Assert.Contains(options, option => option.Label == "Caps Lock");
        Assert.Contains(options, option => option.Label == "↑  (ArrowUp)");
        Assert.Contains(options, option => option.Label == "8  (Digit8)");
        Assert.Contains(options, option => option.Label == "F5");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void NoTwoFunctionalKeysShareALabel()
    {
        var (_, editor, _) = Create();
        var labels = editor.LayerMappings[0].SpecialKeys.Select(option => option.Label).ToList();

        // Two keys wear the same legend on a keycap — both backslashes — and the picker has to tell
        // them apart even though the keyboard does not.
        Assert.Equal(labels.Count, labels.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AWindowsOnlyLimitationIsNamedButTheAssignmentIsStillMade()
    {
        var (model, editor, key) = Create();
        editor.ApplyBuildTargets([BuildTarget.WindowsX64]);
        editor.SelectedLogicalKey = LogicalKey.ArrowUp;

        var row = Row(editor, ModifierLayer.Default);
        row.Output = "q";

        Assert.True(row.HasCapabilityWarning);
        Assert.Contains("Windows", row.CapabilityWarning!, StringComparison.Ordinal);

        // Advisory, not a block: the document keeps what the user asked for.
        Assert.Equal(new CharacterOutput("q"), Stored(model, key.KeyId, ModifierLayer.Default));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TheSameAssignmentCarriesNoWarningWhenOnlyXkbIsBeingBuilt()
    {
        var (_, editor, _) = Create();
        editor.ApplyBuildTargets([BuildTarget.LinuxXkb]);
        editor.SelectedLogicalKey = LogicalKey.ArrowUp;

        var row = Row(editor, ModifierLayer.Default);
        row.Output = "q";

        Assert.False(row.HasCapabilityWarning);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void WarningsFollowTheLogicalKeyRatherThanStickingToTheRow()
    {
        var (_, editor, _) = Create();
        editor.ApplyBuildTargets([BuildTarget.WindowsX64]);
        editor.SelectedLogicalKey = LogicalKey.ArrowUp;
        Row(editor, ModifierLayer.Default).Output = "q";

        Assert.True(Row(editor, ModifierLayer.Default).HasCapabilityWarning);

        editor.SelectedLogicalKey = LogicalKey.Q;

        Assert.False(Row(editor, ModifierLayer.Default).HasCapabilityWarning);
    }
}
