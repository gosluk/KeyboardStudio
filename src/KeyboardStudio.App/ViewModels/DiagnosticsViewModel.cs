using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using KeyboardStudio.Core;

namespace KeyboardStudio.App;

public sealed class DiagnosticsViewModel : ObservableObject
{
    private readonly Action<string> _selectKey;

    public DiagnosticsViewModel(Action<string> selectKey)
    {
        ArgumentNullException.ThrowIfNull(selectKey);
        _selectKey = selectKey;
    }

    public ObservableCollection<DiagnosticViewModel> Items { get; } = [];

    public bool HasIssues => Items.Count > 0;

    public bool HasErrors => Items.Any(item => item.Severity == ValidationSeverity.Error);

    public string Summary => Items.Count switch
    {
        0 => "No diagnostics",
        1 => "1 diagnostic",
        _ => $"{Items.Count} diagnostics"
    };

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

        OnPropertyChanged(nameof(HasIssues));
        OnPropertyChanged(nameof(HasErrors));
        OnPropertyChanged(nameof(Summary));
    }
}
