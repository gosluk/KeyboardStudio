using KeyboardStudio.Core;
using Xunit;

namespace KeyboardStudio.Core.Tests;

public sealed class KeyboardLayoutDifferTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Compare_WhenMappingsAreEquivalent_ReturnsNoChanges()
    {
        var mapping = Mapping("KeyA", LogicalKey.A, (ModifierLayer.Default, "a"));

        var result = new KeyboardLayoutDiffer().Compare(
            new KeyboardLayout { Mappings = [mapping] },
            [KeyMappingSnapshot.From(mapping)]);

        Assert.False(result.HasChanges);
        Assert.Empty(result.Changes);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Compare_WhenOneLayerChanges_CarriesTheCompleteCurrentSnapshot()
    {
        var baseline = Mapping(
            "KeyA",
            LogicalKey.A,
            (ModifierLayer.Default, "a"),
            (ModifierLayer.Shift, "A"),
            (ModifierLayer.AltGr, "ą"));
        var current = Mapping(
            "KeyA",
            LogicalKey.A,
            (ModifierLayer.Default, "x"),
            (ModifierLayer.Shift, "A"),
            (ModifierLayer.AltGr, "ą"));

        var change = Assert.Single(new KeyboardLayoutDiffer().Compare(
            new KeyboardLayout { Mappings = [current] },
            [KeyMappingSnapshot.From(baseline)]).Changes);

        Assert.Equal(KeyboardMappingChangeKind.Modified, change.Kind);
        Assert.False(change.LogicalKeyChanged);
        Assert.Equal([ModifierLayer.Default], change.ChangedLayers);
        Assert.Equal(3, change.Current!.Outputs.Count);
        Assert.Equal(new CharacterOutput("A"), change.Current.Outputs[ModifierLayer.Shift]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Compare_WhenLayerIsCleared_ReportsTheMissingLayerAsAChange()
    {
        var baseline = Mapping(
            "KeyA",
            LogicalKey.A,
            (ModifierLayer.Default, "a"),
            (ModifierLayer.AltGr, "ą"));
        var current = Mapping("KeyA", LogicalKey.A, (ModifierLayer.Default, "a"));

        var change = Assert.Single(new KeyboardLayoutDiffer().Compare(
            new KeyboardLayout { Mappings = [current] },
            [KeyMappingSnapshot.From(baseline)]).Changes);

        Assert.Equal([ModifierLayer.AltGr], change.ChangedLayers);
        Assert.False(change.Current!.Outputs.ContainsKey(ModifierLayer.AltGr));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Compare_WhenLogicalKeyChanges_ReportsItEvenIfOutputsDoNot()
    {
        var baseline = Mapping("KeyA", LogicalKey.A, (ModifierLayer.Default, "x"));
        var current = Mapping("KeyA", LogicalKey.B, (ModifierLayer.Default, "x"));

        var change = Assert.Single(new KeyboardLayoutDiffer().Compare(
            new KeyboardLayout { Mappings = [current] },
            [KeyMappingSnapshot.From(baseline)]).Changes);

        Assert.True(change.LogicalKeyChanged);
        Assert.Empty(change.ChangedLayers);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Compare_WhenMappingsAreAddedAndRemoved_OrdersChangesByPhysicalKeyId()
    {
        var baseline = Mapping("KeyZ", LogicalKey.Z, (ModifierLayer.Default, "z"));
        var current = Mapping("KeyA", LogicalKey.A, (ModifierLayer.Default, "a"));

        var changes = new KeyboardLayoutDiffer().Compare(
            new KeyboardLayout { Mappings = [current] },
            [KeyMappingSnapshot.From(baseline)]).Changes;

        Assert.Equal(["KeyA", "KeyZ"], changes.Select(change => change.KeyId));
        Assert.Equal(KeyboardMappingChangeKind.Added, changes[0].Kind);
        Assert.Equal(KeyboardMappingChangeKind.Removed, changes[1].Kind);
        Assert.Null(changes[1].Current);
    }

    private static KeyMapping Mapping(
        string keyId,
        LogicalKey logicalKey,
        params (ModifierLayer Layer, string Output)[] outputs) =>
        new()
        {
            KeyId = keyId,
            LogicalKey = logicalKey,
            Outputs = outputs.ToDictionary(
                item => item.Layer,
                item => (KeyOutput)new CharacterOutput(item.Output))
        };
}
