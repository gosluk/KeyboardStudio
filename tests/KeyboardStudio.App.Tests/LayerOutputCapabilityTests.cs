using KeyboardStudio.App;
using KeyboardStudio.Build;
using KeyboardStudio.Core;
using Xunit;

namespace KeyboardStudio.App.Tests;

/// <summary>
/// Covers the advisory the layer rows show when a build target could not produce an assignment.
/// </summary>
/// <remarks>
/// These verdicts restate what the build-time rules already decide, so the value is in their being
/// target-scoped and advisory: the editor names the cost of an assignment rather than blocking it,
/// which is what lets an imported layout stay editable even where it holds something the target
/// cannot build.
/// </remarks>
public sealed class LayerOutputCapabilityTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void NoTargets_SayNothing() =>
        Assert.Null(LayerOutputCapability.Describe(
            [],
            LogicalKey.ArrowUp,
            ModifierLayer.Default,
            new CharacterOutput("q")));

    [Fact]
    [Trait("Category", "Unit")]
    public void AnEmptyLayerIsNeverWorthAWarning() =>
        Assert.Null(LayerOutputCapability.Describe(
            [BuildTarget.LinuxXkb],
            LogicalKey.None,
            ModifierLayer.Default,
            null));

    [Theory]
    [InlineData(ModifierLayer.Default)]
    [InlineData(ModifierLayer.Shift)]
    [InlineData(ModifierLayer.AltGr)]
    [InlineData(ModifierLayer.ShiftAltGr)]
    [Trait("Category", "Unit")]
    public void Xkb_TakesACharacterOrAFunctionalKeyOnEveryLayer(ModifierLayer layer)
    {
        Assert.Null(LayerOutputCapability.Describe(
            [BuildTarget.LinuxXkb],
            LogicalKey.ArrowUp,
            layer,
            new SpecialKeyOutput(LogicalKey.Home)));

        Assert.Null(LayerOutputCapability.Describe(
            [BuildTarget.LinuxXkb],
            LogicalKey.ArrowUp,
            layer,
            new CharacterOutput("q")));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Xkb_AsksForAKeyWhenAFunctionalOutputNamesNone() =>
        // Not reachable from the picker, which omits None. An import is what puts one here, and the
        // row has to say so rather than silently generating nothing.
        Assert.Contains(
            "Choose a key for this layer",
            LayerOutputCapability.Describe(
                [BuildTarget.LinuxXkb],
                LogicalKey.ArrowUp,
                ModifierLayer.Default,
                new SpecialKeyOutput(LogicalKey.None))!,
            StringComparison.Ordinal);
}
