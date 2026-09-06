using System.Xml.Linq;
using Xunit;

namespace KeyboardStudio.App.Tests;

/// <summary>
/// Protects the elevation and interaction cues shared by application chrome and controls.
/// </summary>
public sealed class AppStylesVisualContractTests
{
    private const string AppStylesName = "Styles.AppStyles.axaml";

    private static readonly XNamespace Avalonia = "https://github.com/avaloniaui";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    [Trait("Category", "Unit")]
    public void AppButtonOwnsALayeredTemplateWithHighlightAndElevation()
    {
        var template = Template(Theme("AppButtonTheme", "Button"));
        var surface = Assert.Single(template.Descendants(), element => PartName(element) == "PART_Surface");
        var highlight = Assert.Single(template.Descendants(), element => PartName(element) == "PART_Highlight");

        Assert.Contains(highlight, surface.Descendants());
        Assert.Contains(surface.Descendants(), element => element.Name == Avalonia + "ContentPresenter");
        Assert.Equal(DynamicResource("ButtonBoxShadow"), (string?)surface.Attribute("BoxShadow"));
        Assert.Equal(DynamicResource("ButtonHighlightBrush"), (string?)highlight.Attribute("Background"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TooltipsOwnAnElevatedTemplate()
    {
        var template = Template(Theme("AppToolTipTheme", "ToolTip"));
        var elevatedSurface = Assert.Single(
            template.Descendants(),
            element => element.Attribute("BoxShadow") is not null);

        Assert.Equal(DynamicResource("ElevatedBoxShadow"), (string?)elevatedSurface.Attribute("BoxShadow"));
        Assert.Contains(elevatedSurface.Descendants(), element => element.Name == Avalonia + "ContentPresenter");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void MenuItemsHaveAThemeOwnedHoverSurface()
    {
        var hoverStyle = Assert.Single(
            Styles(),
            style => Selectors(style).Contains("MenuItem:pointerover", StringComparer.Ordinal));

        Assert.Contains(
            hoverStyle.Elements(Avalonia + "Setter"),
            setter => IsSetter(setter, "Background", DynamicResource("MenuHoverSurfaceBrush")));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void KeyboardBezelKeepsItsSemanticDepthShadow()
    {
        var bezelStyle = Assert.Single(
            Styles(),
            style => Selectors(style).Contains("Border.bezel", StringComparer.Ordinal));

        Assert.Contains(
            bezelStyle.Elements(Avalonia + "Setter"),
            setter => IsSetter(setter, "BoxShadow", DynamicResource("KeyboardBezelBoxShadow")));
    }

    private static XElement Theme(string key, string targetType) =>
        Document()
            .Descendants(Avalonia + "ControlTheme")
            .Single(theme =>
                (string?)theme.Attribute(Xaml + "Key") == key &&
                (string?)theme.Attribute("TargetType") == targetType);

    private static XElement Template(XElement theme) =>
        theme.Elements(Avalonia + "Setter")
            .Single(setter => IsSetterFor(setter, "Template"))
            .Element(Avalonia + "ControlTemplate")!;

    private static IEnumerable<XElement> Styles() =>
        Document().Root!.Elements(Avalonia + "Style");

    private static XDocument Document() =>
        XDocument.Parse(ApplicationXamlSource.Read(AppStylesName));

    private static string[] Selectors(XElement style) =>
        ((string?)style.Attribute("Selector") ?? string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    private static string? PartName(XElement element) =>
        (string?)element.Attribute("Name") ?? (string?)element.Attribute(Xaml + "Name");

    private static bool IsSetterFor(XElement setter, string property) =>
        (string?)setter.Attribute("Property") == property;

    private static bool IsSetter(XElement setter, string property, string value) =>
        IsSetterFor(setter, property) && (string?)setter.Attribute("Value") == value;

    private static string DynamicResource(string name) => $"{{DynamicResource {name}}}";
}
