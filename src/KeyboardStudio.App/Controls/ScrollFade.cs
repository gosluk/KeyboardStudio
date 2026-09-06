using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace KeyboardStudio.App.Controls;

/// <summary>
/// Fades a <see cref="ScrollViewer"/>'s clipped edges out to transparency instead of cutting them.
/// </summary>
/// <remarks>
/// A scroller ends its content mid-glyph at the viewport edge, which reads as a rendering fault
/// rather than as "there is more this way": the half-row above the fold looks broken instead of
/// looking continued. Fading it out says the same thing without the seam.
///
/// The fade is applied only to an edge that actually has content beyond it, and it ramps in over
/// the first <see cref="EdgeProperty"/> pixels of travel rather than appearing at once, so a
/// scroller sitting at the top of its content keeps its first row at full strength and nothing is
/// ever dimmed that is not in fact clipped.
///
/// The mask goes on the content presenter rather than on the scroller itself. The scrollbar is the
/// other thing in the frame saying there is more to see, and fading it along with what it describes
/// would take the strength out of both.
/// </remarks>
public static class ScrollFade
{
    /// <summary>How many pixels the fade spans at each clipped edge. Zero leaves the edge hard.</summary>
    public static readonly AttachedProperty<double> EdgeProperty =
        AvaloniaProperty.RegisterAttached<ScrollViewer, double>("Edge", typeof(ScrollFade));

    static ScrollFade()
    {
        EdgeProperty.Changed.AddClassHandler<ScrollViewer>((viewer, _) => Attach(viewer));
    }

    public static double GetEdge(ScrollViewer viewer)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        return viewer.GetValue(EdgeProperty);
    }

    public static void SetEdge(ScrollViewer viewer, double value)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        viewer.SetValue(EdgeProperty, value);
    }

    /// <summary>
    /// The opacity mask for a viewport of <paramref name="viewport"/> pixels showing
    /// <paramref name="offset"/> into <paramref name="extent"/>, or <see langword="null"/> when
    /// nothing is clipped and the content should be drawn as it is.
    /// </summary>
    /// <remarks>
    /// Separated from the control so the arithmetic that decides which edge fades, and by how much,
    /// can be read at every scroll position without a window to scroll.
    /// </remarks>
    public static LinearGradientBrush? CreateMask(double viewport, double extent, double offset, double edge)
    {
        if (edge <= 0
            || !double.IsFinite(viewport)
            || !double.IsFinite(extent)
            || !double.IsFinite(offset))
        {
            return null;
        }

        // Two fades and something between them will not fit, and a viewport that is entirely fade
        // hides the content it is meant to be softening the end of.
        if (viewport <= edge * 2)
        {
            return null;
        }

        var above = Math.Clamp(offset / edge, 0, 1);
        var below = Math.Clamp((extent - viewport - offset) / edge, 0, 1);
        if (above <= 0 && below <= 0)
        {
            return null;
        }

        var band = edge / viewport;
        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            [
                new GradientStop(Veil(above), 0),
                new GradientStop(Colors.Black, band),
                new GradientStop(Colors.Black, 1 - band),
                new GradientStop(Veil(below), 1),
            ],
        };
    }

    private static void Attach(ScrollViewer viewer)
    {
        // Idempotent: the property can be set again by a style or a second load, and a scroller
        // handling its own scroll twice would rebuild the same mask twice per pixel of travel.
        viewer.TemplateApplied -= OnTemplateApplied;
        viewer.ScrollChanged -= OnScrollChanged;

        if (GetEdge(viewer) > 0)
        {
            viewer.TemplateApplied += OnTemplateApplied;
            viewer.ScrollChanged += OnScrollChanged;
        }

        Refresh(viewer);
    }

    private static void OnTemplateApplied(object? sender, TemplateAppliedEventArgs e) =>
        Refresh(sender as ScrollViewer);

    private static void OnScrollChanged(object? sender, ScrollChangedEventArgs e) =>
        Refresh(sender as ScrollViewer);

    private static void Refresh(ScrollViewer? viewer)
    {
        if (viewer?.Presenter is not Visual presenter)
        {
            return;
        }

        presenter.OpacityMask = CreateMask(
            viewer.Viewport.Height,
            viewer.Extent.Height,
            viewer.Offset.Y,
            GetEdge(viewer));
    }

    /// <summary>
    /// Black at full strength is what an opacity mask reads as "keep this"; only the alpha is used,
    /// so the colour is arbitrary and the fade lives entirely in the channel that carries it.
    /// </summary>
    private static Color Veil(double strength) =>
        Color.FromArgb((byte)Math.Round(255 * (1 - strength)), 0, 0, 0);
}
