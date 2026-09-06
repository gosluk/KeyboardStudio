using Avalonia;
using Avalonia.Media;
using KeyboardStudio.App.Controls;
using Xunit;

namespace KeyboardStudio.App.Tests;

/// <summary>
/// Holds the scroll fade to the one rule that makes it honest: it dims an edge only when there is
/// something behind that edge.
/// </summary>
/// <remarks>
/// A mask that is always on is worse than no mask. It puts the first row of a list permanently in
/// half strength and tells the reader there is more above when there is not, which is the opposite
/// of what the effect is for. Every case here is a scroll position, asked of the arithmetic rather
/// than of a window, so the positions that never come up by hand are covered too.
/// </remarks>
public sealed class ScrollFadeTests
{
    private const double Viewport = 400;
    private const double Extent = 1000;
    private const double Edge = 24;

    [Fact]
    [Trait("Category", "Unit")]
    public void ContentThatFitsIsNotMaskedAtAll() =>
        Assert.Null(ScrollFade.CreateMask(Viewport, Viewport, 0, Edge));

    [Fact]
    [Trait("Category", "Unit")]
    public void AtTheTopOnlyTheBottomFades()
    {
        var mask = Assert.IsType<LinearGradientBrush>(
            ScrollFade.CreateMask(Viewport, Extent, 0, Edge));

        Assert.Equal(255, Alpha(mask, 0));
        Assert.Equal(0, Alpha(mask, 3));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AtTheBottomOnlyTheTopFades()
    {
        var mask = Assert.IsType<LinearGradientBrush>(
            ScrollFade.CreateMask(Viewport, Extent, Extent - Viewport, Edge));

        Assert.Equal(0, Alpha(mask, 0));
        Assert.Equal(255, Alpha(mask, 3));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void InTheMiddleBothEndsFade()
    {
        var mask = Assert.IsType<LinearGradientBrush>(
            ScrollFade.CreateMask(Viewport, Extent, 300, Edge));

        Assert.Equal(0, Alpha(mask, 0));
        Assert.Equal(0, Alpha(mask, 3));
    }

    [Theory]
    [InlineData(0, 255)]
    [InlineData(6, 191)]
    [InlineData(12, 128)]
    [InlineData(24, 0)]
    [InlineData(96, 0)]
    [Trait("Category", "Unit")]
    public void TheFadeRampsInOverTheFirstPixelsRatherThanSwitchingOn(double offset, byte alpha)
    {
        var mask = ScrollFade.CreateMask(Viewport, Extent, offset, Edge);

        // Offset zero is the one position with nothing above it, so it is also the one position
        // with no mask at the top — and at that position nothing has scrolled far enough for the
        // bottom to matter either way.
        Assert.Equal(alpha, mask is null ? (byte)255 : Alpha(mask, 0));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TheFadedBandIsTheRequestedPixelsAtBothEnds()
    {
        var mask = Assert.IsType<LinearGradientBrush>(
            ScrollFade.CreateMask(Viewport, Extent, 300, Edge));

        Assert.Equal(0d, mask.GradientStops[0].Offset);
        Assert.Equal(Edge / Viewport, mask.GradientStops[1].Offset, 6);
        Assert.Equal(1 - (Edge / Viewport), mask.GradientStops[2].Offset, 6);
        Assert.Equal(1d, mask.GradientStops[3].Offset);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TheMaskRunsDownThePageRatherThanAcrossIt()
    {
        var mask = Assert.IsType<LinearGradientBrush>(
            ScrollFade.CreateMask(Viewport, Extent, 300, Edge));

        Assert.Equal(new RelativePoint(0, 0, RelativeUnit.Relative), mask.StartPoint);
        Assert.Equal(new RelativePoint(0, 1, RelativeUnit.Relative), mask.EndPoint);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    [Trait("Category", "Unit")]
    public void AnEdgeOfNothingLeavesTheContentAlone(double edge) =>
        Assert.Null(ScrollFade.CreateMask(Viewport, Extent, 300, edge));

    [Fact]
    [Trait("Category", "Unit")]
    public void AViewportTooShortToHoldTwoFadesKeepsItsHardEdges() =>
        Assert.Null(ScrollFade.CreateMask(Edge * 2, Extent, 100, Edge));

    [Theory]
    [InlineData(double.NaN, 1000, 0)]
    [InlineData(400, double.NaN, 0)]
    [InlineData(400, 1000, double.NaN)]
    [InlineData(double.PositiveInfinity, 1000, 0)]
    [Trait("Category", "Unit")]
    public void AnUnmeasuredScrollerAsksForNoMask(double viewport, double extent, double offset) =>
        Assert.Null(ScrollFade.CreateMask(viewport, extent, offset, Edge));

    private static byte Alpha(LinearGradientBrush mask, int stop) =>
        mask.GradientStops[stop].Color.A;
}
