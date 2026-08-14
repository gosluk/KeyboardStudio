using KeyboardStudio.App;
using KeyboardStudio.Core;
using Xunit;

namespace KeyboardStudio.App.Tests;

public sealed class DiagnosticsViewModelTests
{
    [Fact]
    public void Refresh_WhenIssuesExist_DisplaysSeverityCodeMessageAndKeyAssociation()
    {
        var diagnostics = new DiagnosticsViewModel(_ => { });
        var result = new ValidationResult([
            new ValidationIssue(ValidationSeverity.Info, "TEST100", "Information"),
            new ValidationIssue(ValidationSeverity.Warning, "TEST200", "Warning", "KeyB"),
            new ValidationIssue(ValidationSeverity.Error, "TEST300", "Error", "KeyA")
        ]);

        diagnostics.Refresh(result);

        Assert.Equal(3, diagnostics.Items.Count);
        Assert.Equal("3 diagnostics", diagnostics.Summary);
        Assert.True(diagnostics.HasErrors);
        var error = diagnostics.Items[0];
        Assert.Equal(ValidationSeverity.Error, error.Severity);
        Assert.Equal("TEST300", error.Code);
        Assert.Equal("Error", error.Message);
        Assert.Equal("KeyA", error.KeyId);
        Assert.Equal("Key: KeyA", error.KeyAssociation);
    }

    [Fact]
    public void SelectCommand_WhenIssueHasKey_SelectsAndHighlightsAssociatedKey()
    {
        var editor = new MainWindowViewModel().Editor;
        var result = new ValidationResult([
            new ValidationIssue(ValidationSeverity.Error, "TEST300", "Error", "KeyA")
        ]);
        var diagnostics = new DiagnosticsViewModel(keyId => editor.SelectKey(keyId));
        diagnostics.Refresh(result);
        editor.ApplyDiagnostics(result.Issues);

        diagnostics.Items[0].SelectCommand.Execute(null);

        Assert.Equal("KeyA", editor.SelectedKey?.KeyId);
        Assert.True(editor.SelectedKey?.HasError);
    }

    [Fact]
    public void ApplyDiagnostics_WhenErrorsChange_RefreshesKeyHighlighting()
    {
        var editor = new MainWindowViewModel().Editor;
        var keyA = editor.Keys.Single(key => key.KeyId == "KeyA");
        editor.ApplyDiagnostics([
            new ValidationIssue(ValidationSeverity.Error, "TEST300", "Error", "KeyA")
        ]);

        editor.ApplyDiagnostics([]);

        Assert.False(keyA.HasError);
    }
}
