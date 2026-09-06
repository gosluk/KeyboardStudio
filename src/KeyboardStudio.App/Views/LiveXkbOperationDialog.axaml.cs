using Avalonia.Controls;
using Avalonia.Interactivity;

namespace KeyboardStudio.App;

public sealed partial class LiveXkbOperationDialog : Window
{
    private const string PathExplanation =
        "KeyboardStudio will change only the following per-user XKB and state paths. " +
        "Distribution files and the active desktop layout are not changed.";

    public LiveXkbOperationDialog()
        : this("Change", [])
    {
    }

    public LiveXkbOperationDialog(string action, IReadOnlyList<string> paths)
        : this($"{action} per-user XKB variant?", PathExplanation, paths, action)
    {
    }

    public LiveXkbOperationDialog(
        string heading,
        string explanation,
        IReadOnlyList<string> items,
        string confirmLabel)
    {
        InitializeComponent();
        DataContext = new DialogContent(heading, explanation, items, confirmLabel);
    }

    private void ConfirmClicked(object? sender, RoutedEventArgs eventArgs) => Close(true);

    private void CancelClicked(object? sender, RoutedEventArgs eventArgs) => Close(false);

    private sealed record DialogContent(
        string Heading,
        string Explanation,
        IReadOnlyList<string> Items,
        string ConfirmLabel);
}
