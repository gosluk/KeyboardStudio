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
/// target-scoped: the same assignment must be quiet for one target and spoken for another, which is
/// the whole reason the editor advises rather than blocks.
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
            [BuildTarget.WindowsX64],
            LogicalKey.None,
            ModifierLayer.Default,
            null));

    [Fact]
    [Trait("Category", "Unit")]
    public void Windows_CannotTypeACharacterFromAScanOnlyKey() =>
        Assert.Contains(
            "cannot type a character",
            LayerOutputCapability.Describe(
                [BuildTarget.WindowsX64],
                LogicalKey.ArrowUp,
                ModifierLayer.Default,
                new CharacterOutput("q"))!,
            StringComparison.Ordinal);

    [Fact]
    [Trait("Category", "Unit")]
    public void Windows_AcceptsACharacterFromAKeyThatTypes() =>
        Assert.Null(LayerOutputCapability.Describe(
            [BuildTarget.WindowsX64],
            LogicalKey.Q,
            ModifierLayer.Default,
            new CharacterOutput("q")));

    [Fact]
    [Trait("Category", "Unit")]
    public void Windows_AcceptsAFunctionalKeyThatIsTheKeyBeingItself() =>
        Assert.Null(LayerOutputCapability.Describe(
            [BuildTarget.WindowsX64],
            LogicalKey.ArrowUp,
            ModifierLayer.Default,
            new SpecialKeyOutput(LogicalKey.ArrowUp)));

    [Theory]
    [InlineData(ModifierLayer.Shift)]
    [InlineData(ModifierLayer.AltGr)]
    [InlineData(ModifierLayer.ShiftAltGr)]
    [Trait("Category", "Unit")]
    public void Windows_CannotRemapAFunctionalKeyPerLayer(ModifierLayer layer) =>
        Assert.Contains(
            "functional key on this layer",
            LayerOutputCapability.Describe(
                [BuildTarget.WindowsX64],
                LogicalKey.ArrowUp,
                layer,
                new SpecialKeyOutput(LogicalKey.Home))!,
            StringComparison.Ordinal);

    [Fact]
    [Trait("Category", "Unit")]
    public void Windows_WantsALogicalKeyBeforeAnyOutput() =>
        Assert.Contains(
            "logical key",
            LayerOutputCapability.Describe(
                [BuildTarget.WindowsX64],
                LogicalKey.None,
                ModifierLayer.Default,
                new CharacterOutput("q"))!,
            StringComparison.Ordinal);

    [Theory]
    [InlineData(ModifierLayer.Shift)]
    [InlineData(ModifierLayer.AltGr)]
    [Trait("Category", "Unit")]
    public void Xkb_TakesEverythingWindowsRefuses(ModifierLayer layer)
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
    public void BothTargetsTogether_ReportTheStricterOne() =>
        Assert.NotNull(LayerOutputCapability.Describe(
            [BuildTarget.LinuxXkb, BuildTarget.WindowsX64],
            LogicalKey.ArrowUp,
            ModifierLayer.Shift,
            new SpecialKeyOutput(LogicalKey.Home)));
}
