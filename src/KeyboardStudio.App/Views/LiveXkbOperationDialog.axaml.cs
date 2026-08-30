using Avalonia.Controls;
using Avalonia.Interactivity;

namespace KeyboardStudio.App;

public sealed partial class LiveXkbOperationDialog : Window
{
    public LiveXkbOperationDialog()
        : this("Change", [])
    {
    }

    public LiveXkbOperationDialog(string action, IReadOnlyList<string> paths)
    {
        InitializeComponent();
        DataContext = new DialogContent(action, paths);
    }

    private void ConfirmClicked(object? sender, RoutedEventArgs eventArgs) => Close(true);

    private void CancelClicked(object? sender, RoutedEventArgs eventArgs) => Close(false);

    private sealed record DialogContent(string Action, IReadOnlyList<string> Paths)
    {
        public string Heading => $"{Action} per-user XKB variant?";

        public string ConfirmLabel => Action;
    }
}
