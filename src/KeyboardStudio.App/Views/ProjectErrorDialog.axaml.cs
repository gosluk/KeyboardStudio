using Avalonia.Controls;
using Avalonia.Interactivity;

namespace KeyboardStudio.App;

public sealed partial class ProjectErrorDialog : Window
{
    public ProjectErrorDialog()
    {
        InitializeComponent();
    }

    public ProjectErrorDialog(string title, string message)
        : this()
    {
        Title = title;
        MessageText.Text = message;
    }

    private void OkClicked(object? sender, RoutedEventArgs eventArgs) => Close();
}
