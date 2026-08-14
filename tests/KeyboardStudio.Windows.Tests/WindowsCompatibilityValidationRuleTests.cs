using KeyboardStudio.Core;
using KeyboardStudio.Windows;
using Xunit;

namespace KeyboardStudio.Windows.Tests;

public sealed class WindowsCompatibilityValidationRuleTests
{
    [Fact]
    public void Validate_WhenOutputHasNoLogicalKey_ReportsWindowsCompatibilityIssue()
    {
        var project = DemoProjectFactory.Create();
        var mapping = project.Layout.Find("KeyA")!;
        mapping.LogicalKey = LogicalKey.None;

        var issues = new WindowsCompatibilityValidationRule().Validate(project);

        Assert.Contains(issues, issue => issue.Code == "WIN001" && issue.KeyId == "KeyA");
    }

    [Fact]
    public void Validate_WhenModifierLayerIsUnknown_ReportsWindowsCompatibilityIssue()
    {
        var project = DemoProjectFactory.Create();
        project.Layout.Find("KeyA")!.Outputs[(ModifierLayer)99] = new CharacterOutput("a");

        var issues = new WindowsCompatibilityValidationRule().Validate(project);

        Assert.Contains(issues, issue => issue.Code == "WIN002" && issue.KeyId == "KeyA");
    }
}
