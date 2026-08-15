using Avalonia.Controls;
using Avalonia.Interactivity;

namespace KeyboardStudio.App;

public sealed partial class GeneratedTextDialog : Window
{
    public GeneratedTextDialog()
    {
        InitializeComponent();
    }

    public GeneratedTextDialog(string title, string content)
        : this()
    {
        Title = title;
        ContentBox.Text = content;
    }

    private void CloseClick(object? sender, RoutedEventArgs eventArgs) => Close();
}
