using KeyboardStudio.Core;
using KeyboardStudio.Windows;
using Xunit;

namespace KeyboardStudio.Windows.Tests;

public sealed class WindowsCompatibilityValidationRuleTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void WindowsDiagnosticCodes_WhenRead_HaveStableValues()
    {
        Assert.Equal("KSW001", WindowsDiagnosticCodes.UnsupportedLogicalKeyMapping);
        Assert.Equal("KSW002", WindowsDiagnosticCodes.UnsupportedModifierCombination);
        Assert.Equal("KSW003", WindowsDiagnosticCodes.UnsupportedCharacterMapping);
        Assert.Equal("KSW004", WindowsDiagnosticCodes.UnsupportedSpecialKeyMapping);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Validate_WhenOutputHasNoLogicalKey_ReportsWindowsCompatibilityIssue()
    {
        var project = DemoProjectFactory.Create();
        var mapping = project.Layout.Find("KeyA")!;
        mapping.LogicalKey = LogicalKey.None;

        var issues = new WindowsCompatibilityValidationRule().Validate(project);

        Assert.Contains(issues, issue =>
            issue.Code == WindowsDiagnosticCodes.UnsupportedLogicalKeyMapping && issue.KeyId == "KeyA");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Validate_WhenSpecialOutputHasNoLogicalKey_ReportsWindowsCompatibilityIssue()
    {
        var project = DemoProjectFactory.Create();
        project.Layout.Find("KeyA")!.Outputs[ModifierLayer.Default] = new SpecialKeyOutput(LogicalKey.None);

        var issues = new WindowsCompatibilityValidationRule().Validate(project);

        Assert.Contains(issues, issue =>
            issue.Code == WindowsDiagnosticCodes.UnsupportedLogicalKeyMapping && issue.KeyId == "KeyA");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Validate_WhenModifierLayerIsUnknown_ReportsWindowsCompatibilityIssue()
    {
        var project = DemoProjectFactory.Create();
        project.Layout.Find("KeyA")!.Outputs[(ModifierLayer)99] = new CharacterOutput("a");

        var issues = new WindowsCompatibilityValidationRule().Validate(project);

        Assert.Contains(issues, issue =>
            issue.Code == WindowsDiagnosticCodes.UnsupportedModifierCombination && issue.KeyId == "KeyA");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Validate_WhenCharacterIsOutsideBmp_ReportsUnsupportedCharacterMapping()
    {
        var project = DemoProjectFactory.Create();
        project.Layout.Find("KeyA")!.Outputs[ModifierLayer.AltGr] = new CharacterOutput("😀");

        var issues = new WindowsCompatibilityValidationRule().Validate(project);

        Assert.Contains(issues, issue =>
            issue.Code == WindowsDiagnosticCodes.UnsupportedCharacterMapping && issue.KeyId == "KeyA");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Validate_WhenSpecialOutputChangesByLayer_ReportsUnsupportedSpecialKeyMapping()
    {
        var project = DemoProjectFactory.Create();
        project.Layout.Find("KeyA")!.Outputs[ModifierLayer.Default] = new SpecialKeyOutput(LogicalKey.Enter);

        var issues = new WindowsCompatibilityValidationRule().Validate(project);

        Assert.Contains(issues, issue =>
            issue.Code == WindowsDiagnosticCodes.UnsupportedSpecialKeyMapping && issue.KeyId == "KeyA");
    }
}
