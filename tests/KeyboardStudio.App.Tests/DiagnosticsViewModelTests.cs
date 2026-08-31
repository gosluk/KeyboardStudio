using KeyboardStudio.App;
using KeyboardStudio.Core;
using Xunit;

namespace KeyboardStudio.App.Tests;

public sealed class DiagnosticsViewModelTests
{
    [Fact]
    [Trait("Category", "Unit")]
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
        Assert.Equal("1 error, 1 warning, 1 note", diagnostics.Summary);
        Assert.True(diagnostics.HasErrors);
        var error = diagnostics.Items[0];
        Assert.Equal(ValidationSeverity.Error, error.Severity);
        Assert.Equal("TEST300", error.Code);
        Assert.Equal("Error", error.Message);
        Assert.Equal("KeyA", error.KeyId);
        Assert.Equal("Key: KeyA", error.KeyAssociation);
    }

    [Fact]
    [Trait("Category", "Unit")]
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
    [Trait("Category", "Unit")]
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

    [Fact]
    [Trait("Category", "Unit")]
    public void MappingMutation_WhenOutputHasNoLogicalKey_ContinuouslyRefreshesDiagnostics()
    {
        var viewModel = TestMainWindow.WithEmptyProject();
        Assert.True(viewModel.Editor.SelectKey("KeyA"));

        viewModel.Editor.LayerMappings[0].Output = "a";

        Assert.Contains(viewModel.Diagnostics.Items, item =>
            item.Code == KeyboardProjectDiagnosticCodes.OutputWithoutLogicalKey &&
            item.Severity == ValidationSeverity.Warning &&
            item.KeyId == "KeyA");
        Assert.DoesNotContain(viewModel.Diagnostics.Items, item =>
            item.Severity == ValidationSeverity.Error && item.KeyId == "KeyA");
        Assert.False(viewModel.Editor.SelectedKey?.HasError);

        viewModel.Editor.SelectedLogicalKey = LogicalKey.A;

        Assert.DoesNotContain(viewModel.Diagnostics.Items, item => item.KeyId == "KeyA");
        Assert.False(viewModel.Editor.SelectedKey?.HasError);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void MappingMutation_WhenEditIsRejected_DoesNotRunProjectValidation()
    {
        var validator = new CountingProjectValidator();
        var viewModel = TestMainWindow.WithEmptyProject(
            new TestProjectInteractionService(),
            validator);
        Assert.True(viewModel.Editor.SelectKey("KeyA"));
        Assert.Equal(1, validator.CallCount);

        viewModel.Editor.LayerMappings[0].Output = "ab";

        Assert.Equal(1, validator.CallCount);

        viewModel.Editor.LayerMappings[0].Output = "a";

        Assert.Equal(2, validator.CallCount);
    }

    private sealed class CountingProjectValidator : IKeyboardProjectValidator
    {
        public int CallCount { get; private set; }

        public ValidationResult Validate(KeyboardProject project)
        {
            CallCount++;
            return new ValidationResult([]);
        }
    }

    private sealed class TestProjectInteractionService : IProjectInteractionService
    {
        public Task<ProjectReplacementChoice> ConfirmUnsavedChangesAsync(string projectName) =>
            Task.FromResult(ProjectReplacementChoice.Cancel);

        public Task<string?> SelectOpenPathAsync() => Task.FromResult<string?>(null);

        public Task<string?> SelectSavePathAsync(string suggestedFileName) =>
            Task.FromResult<string?>(null);

        public Task<bool> ShowLayoutImportAsync(LayoutImportViewModel viewModel) =>
            Task.FromResult(false);

        public Task<string?> SelectSymbolsFilePathAsync() => Task.FromResult<string?>(null);

        public Task ShowErrorAsync(string title, string message) => Task.CompletedTask;
    }

    [Theory]
    [InlineData(0, "No diagnostics")]
    [InlineData(1, "1 error")]
    [InlineData(3, "3 errors")]
    [Trait("Category", "Unit")]
    public void Summary_NamesWhatIsWrongRatherThanCountingIt(int errors, string expected)
    {
        var diagnostics = new DiagnosticsViewModel(_ => { });

        diagnostics.Refresh(new ValidationResult(
            Enumerable.Range(0, errors)
                .Select(index => new ValidationIssue(
                    ValidationSeverity.Error,
                    $"TEST{index:000}",
                    "Error"))
                .ToList()));

        // Severity is named, not only coloured: the summary has to say the same thing to someone
        // who cannot tell the colours apart.
        Assert.Equal(expected, diagnostics.Summary);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Refresh_WhenThereIsNothingToReport_StaysCollapsed()
    {
        var diagnostics = new DiagnosticsViewModel(_ => { });

        diagnostics.Refresh(new ValidationResult([]));

        Assert.False(diagnostics.IsExpanded);
        Assert.False(diagnostics.HasIssues);
        Assert.Null(diagnostics.HighestSeverity);
        Assert.Equal("No diagnostics", diagnostics.Summary);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Refresh_WhenOnlyWarningsAppear_LeavesTheListCollapsed()
    {
        var diagnostics = new DiagnosticsViewModel(_ => { });

        diagnostics.Refresh(new ValidationResult([
            new ValidationIssue(ValidationSeverity.Warning, "TEST200", "Warning")
        ]));

        Assert.False(diagnostics.IsExpanded);
        Assert.True(diagnostics.HasIssues);
        Assert.Equal(ValidationSeverity.Warning, diagnostics.HighestSeverity);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Refresh_WhenAnErrorAppears_OpensTheList()
    {
        var diagnostics = new DiagnosticsViewModel(_ => { });
        diagnostics.Refresh(new ValidationResult([]));

        diagnostics.Refresh(new ValidationResult([
            new ValidationIssue(ValidationSeverity.Error, "TEST300", "Error")
        ]));

        Assert.True(diagnostics.IsExpanded);
        Assert.Equal(ValidationSeverity.Error, diagnostics.HighestSeverity);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Refresh_WhenTheUserClosesTheListWhileTheErrorStands_LeavesItClosed()
    {
        var diagnostics = new DiagnosticsViewModel(_ => { });
        var error = new ValidationResult([
            new ValidationIssue(ValidationSeverity.Error, "TEST300", "Error")
        ]);
        diagnostics.Refresh(error);
        diagnostics.IsExpanded = false;

        // Every edit revalidates. Reopening here would fight the user at every keystroke for as
        // long as the error stood.
        diagnostics.Refresh(error);

        Assert.False(diagnostics.IsExpanded);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Refresh_WhenAnErrorReturnsAfterBeingFixed_OpensTheListAgain()
    {
        var diagnostics = new DiagnosticsViewModel(_ => { });
        var error = new ValidationResult([
            new ValidationIssue(ValidationSeverity.Error, "TEST300", "Error")
        ]);
        diagnostics.Refresh(error);
        diagnostics.IsExpanded = false;
        diagnostics.Refresh(new ValidationResult([]));

        diagnostics.Refresh(error);

        Assert.True(diagnostics.IsExpanded);
    }
}
