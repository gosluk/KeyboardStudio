using System.Xml.Linq;
using Xunit;

namespace KeyboardStudio.App.Tests;

/// <summary>
/// Holds the selected-key panel's layer rows to their contract.
/// </summary>
/// <remarks>
/// Read from the shipped markup rather than a rendered window, for the same reason the header is:
/// a row that loses one of its two editors, or gains one without an accessible name, still compiles
/// and still passes every view-model test.
/// </remarks>
public sealed class SelectedKeyPanelMarkupTests
{
    private static readonly XNamespace Avalonia = "https://github.com/avaloniaui";

    [Fact]
    [Trait("Category", "Unit")]
    public void ALayerRowOffersBothAKindAndTheEditorThatKindNeeds()
    {
        var row = LayerRowTemplate();

        var kinds = row.Descendants(Avalonia + "ComboBox")
            .Single(box => (string?)box.Attribute("ItemsSource") == "{Binding Kinds}");
        Assert.Equal("{Binding KindOption, Mode=TwoWay}", (string?)kinds.Attribute("SelectedItem"));

        var character = row.Descendants(Avalonia + "TextBox").Single();
        Assert.Equal("{Binding Output, Mode=TwoWay}", (string?)character.Attribute("Text"));
        Assert.Equal("{Binding IsCharacter}", (string?)character.Attribute("IsVisible"));

        var specialKey = row.Descendants(Avalonia + "ComboBox")
            .Single(box => (string?)box.Attribute("ItemsSource") == "{Binding SpecialKeys}");
        Assert.Equal("{Binding SpecialKey, Mode=TwoWay}", (string?)specialKey.Attribute("SelectedItem"));
        Assert.Equal("{Binding IsSpecialKey}", (string?)specialKey.Attribute("IsVisible"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TheTwoEditorsShareOneColumnSoOnlyTheChosenKindIsShown()
    {
        var row = LayerRowTemplate();

        var character = row.Descendants(Avalonia + "TextBox").Single();
        var specialKey = row.Descendants(Avalonia + "ComboBox")
            .Single(box => (string?)box.Attribute("ItemsSource") == "{Binding SpecialKeys}");

        Assert.Equal(
            (string?)character.Attribute("Grid.Column"),
            (string?)specialKey.Attribute("Grid.Column"));
    }

    [Theory]
    [InlineData("Output kind")]
    [InlineData("Character")]
    [InlineData("Key")]
    [Trait("Category", "Unit")]
    public void EveryEditorInARowIsNamedForScreenReaders(string name) =>
        Assert.Contains(
            LayerRowTemplate().Descendants(),
            element => (string?)element.Attribute("AutomationProperties.Name") == name);

    [Fact]
    [Trait("Category", "Unit")]
    public void TheCapabilityWarningIsShownQuietlyRatherThanAsAnError()
    {
        var warning = LayerRowTemplate()
            .Descendants(Avalonia + "TextBlock")
            .Single(block => (string?)block.Attribute("Text") == "{Binding CapabilityWarning}");

        Assert.Equal("{Binding HasCapabilityWarning}", (string?)warning.Attribute("IsVisible"));

        // A target limitation is advice, not a rejection: the row still commits the assignment, so
        // it must not wear the styling that means the value was refused.
        Assert.DoesNotContain("danger", (string?)warning.Attribute("Classes") ?? string.Empty);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void NothingInTheRowIsDisabledByCapability() =>
        Assert.DoesNotContain(
            LayerRowTemplate().DescendantsAndSelf(),
            element => ((string?)element.Attribute("IsEnabled"))?.Contains(
                "Capability", StringComparison.Ordinal) == true);

    private static XElement LayerRowTemplate() =>
        XDocument.Parse(ApplicationXamlSource.Read("Views.MainWindow.axaml"))
            .Root!
            .Descendants(Avalonia + "ItemsControl")
            .Single(control => (string?)control.Attribute("ItemsSource") == "{Binding Editor.LayerMappings}")
            .Elements(Avalonia + "ItemsControl.ItemTemplate")
            .Single()
            .Elements(Avalonia + "DataTemplate")
            .Single();
}
