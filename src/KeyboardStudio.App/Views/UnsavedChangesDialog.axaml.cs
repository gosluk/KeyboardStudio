using Avalonia.Controls;
using Avalonia.Interactivity;

namespace KeyboardStudio.App;

public sealed partial class UnsavedChangesDialog : Window
{
    public UnsavedChangesDialog()
    {
        InitializeComponent();
    }

    public UnsavedChangesDialog(string projectName)
        : this()
    {
        MessageText.Text = $"'{projectName}' has unsaved changes.";
    }

    private void SaveClicked(object? sender, RoutedEventArgs eventArgs) =>
        Close(ProjectReplacementChoice.Save);

    private void DiscardClicked(object? sender, RoutedEventArgs eventArgs) =>
        Close(ProjectReplacementChoice.Discard);

    private void CancelClicked(object? sender, RoutedEventArgs eventArgs) =>
        Close(ProjectReplacementChoice.Cancel);
}
