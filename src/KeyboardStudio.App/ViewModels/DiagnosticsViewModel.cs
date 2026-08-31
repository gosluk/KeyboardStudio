using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using KeyboardStudio.Core;

namespace KeyboardStudio.App;

public sealed class DiagnosticsViewModel : ObservableObject
{
    private readonly Action<string> _selectKey;
    private bool _isExpanded;
    private bool _hadErrors;

    public DiagnosticsViewModel(Action<string> selectKey)
    {
        ArgumentNullException.ThrowIfNull(selectKey);
        _selectKey = selectKey;
    }

    public ObservableCollection<DiagnosticViewModel> Items { get; } = [];

    public bool HasIssues => Items.Count > 0;

    public bool HasErrors => Items.Any(item => item.Severity == ValidationSeverity.Error);

    /// <summary>
    /// Whether the list of diagnostics is showing.
    /// </summary>
    /// <remarks>
    /// A clean document is the normal case, and a panel that reserves the bottom of the editor to
    /// say so is taking space from the keyboard to display nothing. The list is expanded when an
    /// error appears and otherwise left to the user, who may collapse it again and have that stick
    /// until the next error.
    /// </remarks>
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    /// <summary>The highest severity present, or <c>null</c> when there is nothing to report.</summary>
    public ValidationSeverity? HighestSeverity => Items.Count == 0
        ? null
        : Items.Max(item => item.Severity);

    /// <summary>
    /// What is wrong, in words. Severity is named rather than only coloured, so the summary still
    /// says the same thing to someone who cannot tell the colours apart.
    /// </summary>
    public string Summary
    {
        get
        {
            if (Items.Count == 0)
            {
                return "No diagnostics";
            }

            var parts = new List<string>(3);
            Describe(ValidationSeverity.Error, "error", parts);
            Describe(ValidationSeverity.Warning, "warning", parts);
            Describe(ValidationSeverity.Info, "note", parts);
            return string.Join(", ", parts);
        }
    }

    public void Refresh(ValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        Items.Clear();
        foreach (var issue in result.Issues
                     .OrderByDescending(issue => issue.Severity)
                     .ThenBy(issue => issue.Code, StringComparer.Ordinal))
        {
            Items.Add(new DiagnosticViewModel(issue, _selectKey));
        }

        var hadErrors = _hadErrors;
        _hadErrors = HasErrors;

        // Only on the edge into error. Expanding on every refresh would reopen a panel the user
        // had just closed, at every keystroke, for as long as the error stood.
        if (_hadErrors && !hadErrors)
        {
            IsExpanded = true;
        }

        OnPropertyChanged(nameof(HasIssues));
        OnPropertyChanged(nameof(HasErrors));
        OnPropertyChanged(nameof(HighestSeverity));
        OnPropertyChanged(nameof(Summary));
    }

    private void Describe(ValidationSeverity severity, string noun, List<string> parts)
    {
        var count = Items.Count(item => item.Severity == severity);
        if (count == 0)
        {
            return;
        }

        parts.Add(count == 1 ? $"1 {noun}" : $"{count} {noun}s");
    }
}
