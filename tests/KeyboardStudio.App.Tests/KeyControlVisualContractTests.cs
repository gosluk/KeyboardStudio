using System.Xml.Linq;
using Xunit;

namespace KeyboardStudio.App.Tests;

/// <summary>
/// Protects the physical layering and state overlays that make a keycap read as a keycap.
/// </summary>
public sealed class KeyControlVisualContractTests
{
    private const string KeyControlName = "Controls.KeyControl.axaml";

    private static readonly XNamespace Avalonia = "https://github.com/avaloniaui";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    [Trait("Category", "Unit")]
    public void KeyFaceTemplateKeepsItsPhysicalAndStateLayersSeparate()
    {
        var template = KeyFaceTemplate();
        var requiredParts = new[]
        {
            "PART_KeyBase",
            "PART_Face",
            "PART_TopHighlight",
            "PART_StateRing",
            "PART_ErrorMarker",
        };

        var parts = requiredParts
            .Select(name => Assert.Single(template.Descendants(), element => PartName(element) == name))
            .ToList();

        Assert.Equal(requiredParts.Length, parts.Distinct().Count());
        Assert.All(parts, part => Assert.Equal("Border", part.Name.LocalName));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void SelectionErrorAndFocusPaintTheOverlayWithoutChangingLayout()
    {
        var styles = KeyFaceTheme().Elements(Avalonia + "Style").ToList();
        var states = new[]
        {
            (Selector: ".selected", Brush: "KeySelectedBorderBrush"),
            (Selector: ".error", Brush: "KeyErrorBorderBrush"),
            (Selector: ":focus-visible", Brush: "FocusBorderBrush"),
        };

        foreach (var state in states)
        {
            var stateStyles = styles
                .Where(style => Selector(style).Contains(state.Selector, StringComparison.Ordinal))
                .ToList();
            var ringStyles = stateStyles.Where(TargetsPart("PART_StateRing")).ToList();
            var ringStyle = Assert.Single(ringStyles);

            Assert.Contains(
                ringStyle.Elements(Avalonia + "Setter"),
                setter => IsSetter(setter, "BorderBrush", DynamicResource(state.Brush)));
            Assert.DoesNotContain(
                stateStyles.SelectMany(style => style.Elements(Avalonia + "Setter")),
                setter => IsSetterFor(setter, "BorderThickness"));
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void PressingAKeyMovesItsFaceInsteadOfItsWholeCell()
    {
        var template = KeyFaceTemplate();
        var face = Assert.Single(template.Descendants(), element => PartName(element) == "PART_Face");
        var restingTransform = (string?)face.Attribute("RenderTransform");
        var pressedFaceStyle = Assert.Single(
            KeyFaceTheme().Elements(Avalonia + "Style"),
            style =>
                Selector(style).Contains(":pressed", StringComparison.Ordinal) &&
                TargetsPart("PART_Face")(style));
        var pressedTransform = Assert.Single(
            pressedFaceStyle.Elements(Avalonia + "Setter"),
            setter => IsSetterFor(setter, "RenderTransform"));

        Assert.NotNull(restingTransform);
        Assert.Contains("translateY", pressedTransform.Attribute("Value")!.Value, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(restingTransform, pressedTransform.Attribute("Value")!.Value);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void UnmappedKeysUsePurposeNamedThemeResources()
    {
        var setters = KeyFaceTheme().Elements(Avalonia + "Style")
            .Where(style => Selector(style).Contains(".unmapped", StringComparison.Ordinal))
            .SelectMany(style => style.Elements(Avalonia + "Setter"))
            .ToList();

        Assert.Contains(setters, setter => IsSetter(setter, "Background", DynamicResource("KeyUnmappedFaceBrush")));
        Assert.Contains(setters, setter => IsSetter(setter, "Foreground", DynamicResource("KeyUnmappedLegendBrush")));

        var paintedValues = setters
            .Where(setter => (string?)setter.Attribute("Property") is "Background" or "BorderBrush" or "Foreground")
            .Select(setter => setter.Attribute("Value")!.Value)
            .ToList();

        Assert.NotEmpty(paintedValues);
        Assert.All(
            paintedValues,
            value => Assert.StartsWith("{DynamicResource KeyUnmapped", value, StringComparison.Ordinal));
    }

    private static XElement KeyFaceTheme() =>
        XDocument.Parse(ApplicationXamlSource.Read(KeyControlName))
            .Descendants(Avalonia + "ControlTheme")
            .Single(theme => (string?)theme.Attribute(Xaml + "Key") == "KeyFaceTheme");

    private static XElement KeyFaceTemplate() =>
        KeyFaceTheme()
            .Elements(Avalonia + "Setter")
            .Single(setter => IsSetterFor(setter, "Template"))
            .Element(Avalonia + "ControlTemplate")!;

    private static Func<XElement, bool> TargetsPart(string partName) =>
        style => Selector(style).Contains($"#{partName}", StringComparison.Ordinal);

    private static string Selector(XElement style) =>
        (string?)style.Attribute("Selector") ?? string.Empty;

    private static string? PartName(XElement element) =>
        (string?)element.Attribute("Name") ?? (string?)element.Attribute(Xaml + "Name");

    private static bool IsSetterFor(XElement setter, string property) =>
        (string?)setter.Attribute("Property") == property;

    private static bool IsSetter(XElement setter, string property, string value) =>
        IsSetterFor(setter, property) && (string?)setter.Attribute("Value") == value;

    private static string DynamicResource(string name) => $"{{DynamicResource {name}}}";
}
