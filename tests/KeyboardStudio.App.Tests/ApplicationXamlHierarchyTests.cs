using System.Xml.Linq;
using Xunit;

namespace KeyboardStudio.App.Tests;

/// <summary>
/// Holds the editor to the hierarchy and accessibility rules the themes depend on.
/// </summary>
/// <remarks>
/// A palette only helps if the emphasis it carries means something. These read the shipped markup,
/// because "every button looks equally important again" and "this icon has no name" are the kind of
/// regression that no view-model test can see and no compiler can catch.
/// </remarks>
public sealed class ApplicationXamlHierarchyTests
{
    private static readonly XNamespace Avalonia = "https://github.com/avaloniaui";

    /// <summary>The actions allowed to present themselves as the one thing a surface is asking for.</summary>
    private static readonly string[] PrimaryActions =
    [
        "Click:ConfirmClicked",
        "Click:ImportClicked",
        "Click:OkClicked",
        "Click:SaveClicked",
        "{Binding Build.BuildCommand}",
        "{Binding LinuxVariant.InstallCommand}",
    ];

    [Fact]
    [Trait("Category", "Unit")]
    public void OnlyCommittingActionsPresentThemselvesAsPrimary()
    {
        var primary = Buttons()
            .Where(button => Classes(button).Contains("primary"))
            .Select(Action)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(PrimaryActions, primary);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void EveryDestructiveActionSaysSoBeforeItIsPressed()
    {
        var destructive = Buttons()
            .Where(button => Classes(button).Contains("destructive"))
            .Select(Action)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(["Click:DiscardClicked", "{Binding LinuxVariant.UninstallCommand}"], destructive);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void UtilityActionsStayQuiet()
    {
        var loud = Buttons()
            .Where(button => (string?)button.Attribute("Content") is
                "Open output" or "Copy build log" or "Copy artifact path" or "Refresh" or
                "Open bundle" or "Inspect" or "Clear all outputs" or "Unmap logical key")
            .Where(button => !Classes(button).Contains("quiet"))
            .Select(button => (string?)button.Attribute("Content"))
            .ToList();

        Assert.Empty(loud);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void EveryIconOnlyControlHasAName()
    {
        var unnamed = Views()
            .SelectMany(view => view.Descendants(Avalonia + "Button"))
            .Where(button => button.Elements(Avalonia + "PathIcon").Any())
            .Where(button => string.IsNullOrWhiteSpace((string?)button.Attribute("AutomationProperties.Name")))
            .ToList();

        Assert.Empty(unnamed);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void FocusRunsFromTheHeaderThroughTheKeyboardAndInspectorToDiagnostics()
    {
        var order = MainWindow()
            .Descendants()
            .Where(element => element.Attribute("TabIndex") is not null)
            .Select(element => (
                Index: int.Parse((string)element.Attribute("TabIndex")!, System.Globalization.CultureInfo.InvariantCulture),
                Element: element))
            .OrderBy(entry => entry.Index)
            .ToList();

        Assert.Equal([0, 1, 2, 3], order.Select(entry => entry.Index));
        Assert.Equal("Grid", order[0].Element.Name.LocalName);
        Assert.Equal("Grid", order[1].Element.Name.LocalName);
        Assert.Equal("ScrollViewer", order[2].Element.Name.LocalName);
        Assert.Equal("Border", order[3].Element.Name.LocalName);
        Assert.Contains("subtle", Classes(order[3].Element));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void DiagnosticsCollapseToOneLineAndAnnounceThemselves()
    {
        var expander = MainWindow().Descendants(Avalonia + "Expander").Single();

        Assert.Equal("Diagnostics", (string?)expander.Attribute("AutomationProperties.Name"));
        Assert.Equal("{Binding Diagnostics.IsExpanded, Mode=TwoWay}", (string?)expander.Attribute("IsExpanded"));
        Assert.Equal("{Binding Diagnostics.HasIssues}", (string?)expander.Attribute("IsEnabled"));

        // The editor column gives the keyboard the room and diagnostics only what they need.
        var editorColumn = MainWindow()
            .Descendants(Avalonia + "Grid")
            .Single(grid => (string?)grid.Attribute("TabIndex") == "1");
        Assert.Equal("*,Auto", (string?)editorColumn.Attribute("RowDefinitions"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void SecondaryCardsCarryLessWeightThanTheKeyboard()
    {
        var cards = MainWindow()
            .Descendants(Avalonia + "Border")
            .Where(border => Classes(border).Contains("card"))
            .ToList();

        var plain = cards.Where(card => !Classes(card).Contains("subtle")).ToList();

        // Exactly one card is allowed to sit above the rest, and it is the one holding the keyboard.
        Assert.Single(plain);
        Assert.Contains(plain[0].Descendants(Avalonia + "Border"), border => Classes(border).Contains("bezel"));
        Assert.Equal(
            cards.Count - 1,
            cards.Count(card => Classes(card).Contains("subtle")));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void NoSupportingTextIsSmallerThanTheKeyboardItSupports()
    {
        var tooSmall = Views()
            .SelectMany(view => view.Descendants())
            .Select(element => (string?)element.Attribute("FontSize"))
            .Where(size => size is not null)
            .Select(size => double.Parse(size!, System.Globalization.CultureInfo.InvariantCulture))
            .Where(size => size < 12)
            .ToList();

        // The keycap legends are the exception and live in KeyControl, where the whole keyboard is
        // scaled as one surface rather than read at its authored size.
        Assert.Empty(tooSmall);
    }

    /// <summary>What a button does, however it was wired.</summary>
    private static string Action(XElement button) =>
        (string?)button.Attribute("Command")
        ?? $"Click:{(string?)button.Attribute("Click")}";

    private static IEnumerable<XElement> Buttons() =>
        Views().SelectMany(view => view.Descendants(Avalonia + "Button"));

    private static string[] Classes(XElement element) =>
        ((string?)element.Attribute("Classes") ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private static IEnumerable<XElement> Views() =>
        ApplicationXamlSource.All
            .Where(entry => entry.Key.StartsWith("Views.", StringComparison.Ordinal))
            .Select(entry => XDocument.Parse(entry.Value).Root!);

    private static XElement MainWindow() =>
        XDocument.Parse(ApplicationXamlSource.Read("Views.MainWindow.axaml")).Root!;
}
