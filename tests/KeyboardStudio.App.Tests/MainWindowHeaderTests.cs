using System.Xml.Linq;
using Xunit;

namespace KeyboardStudio.App.Tests;

/// <summary>
/// Holds the application header to its contract.
/// </summary>
/// <remarks>
/// These assertions read the shipped markup rather than a rendered window. Moving commands between
/// containers is exactly the kind of edit that keeps compiling while quietly dropping a shortcut,
/// an accessible name, or a command binding, and none of that reaches a view-model test.
/// </remarks>
public sealed class MainWindowHeaderTests
{
    private static readonly XNamespace Avalonia = "https://github.com/avaloniaui";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
    private const string MainWindowName = "Views.MainWindow.axaml";

    [Fact]
    [Trait("Category", "Unit")]
    public void TheStandaloneMenuRowIsGone() =>
        Assert.Empty(MainWindow().Descendants(Avalonia + "Menu"));

    [Fact]
    [Trait("Category", "Unit")]
    public void TheFileTriggerSitsBesideTheApplicationTitle()
    {
        var header = HeaderChildren();
        var title = header.FindIndex(element =>
            (string?)element.Attribute("Text") == "KeyboardStudio");
        var trigger = header.FindIndex(IsFileTrigger);

        Assert.NotEqual(-1, title);
        Assert.Equal(title + 1, trigger);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TheFileMenuExposesEveryDocumentCommand()
    {
        var menu = HeaderChildren().Single(IsFileTrigger)
            .Descendants(Avalonia + "MenuFlyout").Single();

        var commands = menu.Elements(Avalonia + "MenuItem")
            .Select(item => (string?)item.Attribute("Command"))
            .Where(command => command is not null)
            .ToList();

        Assert.Equal(
            [
                "{Binding NewCommand}",
                "{Binding OpenCommand}",
                "{Binding ImportLayoutCommand}",
                "{Binding ImportFromFileCommand}",
                "{Binding SaveCommand}",
                "{Binding SaveAsCommand}",
            ],
            commands);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TheFileMenuOffersAnExplicitGeometryForEachShippedTemplate()
    {
        var menu = HeaderChildren().Single(IsFileTrigger)
            .Descendants(Avalonia + "MenuFlyout").Single();

        var geometries = menu.Elements(Avalonia + "MenuItem")
            .Single(item => (string?)item.Attribute("ItemsSource") == "{Binding NewDocumentOptions}");
        var container = geometries.Descendants(Avalonia + "ControlTheme").Single();
        var setters = container.Elements(Avalonia + "Setter")
            .ToDictionary(
                setter => (string)setter.Attribute("Property")!,
                setter => (string)setter.Attribute("Value")!,
                StringComparer.Ordinal);

        Assert.Equal("{Binding Name}", setters["Header"]);
        Assert.Equal("{Binding Command}", setters["Command"]);
        Assert.Equal("{Binding Name}", setters["AutomationProperties.Name"]);

        Assert.Equal(
            ["ansi-104", "iso-105"],
            new MainWindowViewModel().NewDocumentOptions
                .Select(option => option.TemplateId)
                .Order(StringComparer.Ordinal));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TheKeyboardToolbarNoLongerAsksForACreatePress()
    {
        var window = MainWindow();

        Assert.DoesNotContain(
            window.Descendants(Avalonia + "Button"),
            button => (string?)button.Attribute("Content") == "Create");
        Assert.DoesNotContain(
            window.Descendants(Avalonia + "ComboBox"),
            box => (string?)box.Attribute("ItemsSource") == "{Binding Templates}");
        Assert.DoesNotContain(
            window.Descendants(),
            element => (string?)element.Attribute("SelectedItem") == "{Binding SelectedTemplate}");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TheFileMenuShowsTheShortcutsTheWindowStillBinds()
    {
        var window = MainWindow();
        var bindings = window.Descendants(Avalonia + "KeyBinding")
            .ToDictionary(
                binding => (string)binding.Attribute("Gesture")!,
                binding => (string)binding.Attribute("Command")!,
                StringComparer.Ordinal);

        Assert.Equal("{Binding NewCommand}", bindings["Ctrl+N"]);
        Assert.Equal("{Binding OpenCommand}", bindings["Ctrl+O"]);
        Assert.Equal("{Binding SaveCommand}", bindings["Ctrl+S"]);
        Assert.Equal("{Binding SaveAsCommand}", bindings["Ctrl+Shift+S"]);

        var gestures = window.Descendants(Avalonia + "MenuItem")
            .Select(item => (string?)item.Attribute("InputGesture"))
            .Where(gesture => gesture is not null)
            .ToList();

        Assert.Equal(["Ctrl+N", "Ctrl+O", "Ctrl+S", "Ctrl+Shift+S"], gestures);
    }

    [Theory]
    [InlineData("File", "FileIconGeometry")]
    [InlineData("Appearance", "AppearanceIconGeometry")]
    [Trait("Category", "Unit")]
    public void EveryIconTriggerCarriesItsOwnName(string name, string geometry)
    {
        var trigger = HeaderChildren().Single(element =>
            (string?)element.Attribute(Avalonia + "AutomationProperties.Name") == name
            || (string?)element.Attribute("AutomationProperties.Name") == name);

        Assert.False(string.IsNullOrWhiteSpace((string?)trigger.Attribute("ToolTip.Tip")));
        Assert.Contains(
            trigger.Descendants(Avalonia + "PathIcon"),
            icon => (string?)icon.Attribute("Data") == $"{{StaticResource {geometry}}}");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TheAppearanceTriggerOffersOneKeyboardChoicePerTheme()
    {
        var trigger = HeaderChildren().Single(element =>
            (string?)element.Attribute("AutomationProperties.Name") == "Appearance");

        var options = trigger.Descendants(Avalonia + "ItemsControl").Single();
        Assert.Equal("{Binding Appearance.Options}", (string?)options.Attribute("ItemsSource"));

        var choice = options.Descendants(Avalonia + "RadioButton").Single();
        Assert.Equal("{Binding IsSelected, Mode=TwoWay}", (string?)choice.Attribute("IsChecked"));
        Assert.False(string.IsNullOrWhiteSpace((string?)choice.Attribute("GroupName")));
        Assert.Equal("{Binding Name}", (string?)choice.Attribute("AutomationProperties.Name"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TheAppearanceTriggerShowsASaveFailureWithoutADialog()
    {
        var warning = HeaderChildren()
            .Single(element => (string?)element.Attribute("AutomationProperties.Name") == "Appearance")
            .Descendants(Avalonia + "TextBlock")
            .Single(block => (string?)block.Attribute("Text") == "{Binding Appearance.Warning}");

        Assert.Equal("{Binding Appearance.HasWarning}", (string?)warning.Attribute("IsVisible"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TheHeaderLabelIsConciseAndTheFullPathIsATooltip()
    {
        var label = HeaderChildren()
            .Single(element => (string?)element.Attribute("Text") == "{Binding DocumentStatus}");

        Assert.Equal("{Binding DocumentPath}", (string?)label.Attribute("ToolTip.Tip"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void DocumentStatus_IsAFileNameAndDocumentPath_IsTheWholePath()
    {
        var viewModel = new MainWindowViewModel();

        Assert.Equal("Unsaved project", viewModel.DocumentStatus);
        Assert.DoesNotContain(Path.DirectorySeparatorChar, viewModel.DocumentStatus);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.DocumentPath));
    }

    private static bool IsFileTrigger(XElement element) =>
        (string?)element.Attribute("AutomationProperties.Name") == "File";

    private static List<XElement> HeaderChildren() =>
        MainWindow()
            .Descendants(Avalonia + "Grid")
            .First(grid => (string?)grid.Attribute("Grid.ColumnSpan") == "2")
            .Elements()
            .ToList();

    private static XElement MainWindow() =>
        XDocument.Parse(ApplicationXamlSource.Read(MainWindowName)).Root!;
}
