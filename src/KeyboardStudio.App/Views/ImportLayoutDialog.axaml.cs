using Avalonia.Controls;
using Avalonia.Interactivity;

namespace KeyboardStudio.App;

/// <summary>
/// The import dialog. It shows what <see cref="LayoutImportViewModel"/> found and answers one
/// question — whether the user accepted the import — leaving what to do with the result to the
/// caller that opened it.
/// </summary>
public sealed partial class ImportLayoutDialog : Window
{
    public ImportLayoutDialog()
    {
        InitializeComponent();
    }

    public ImportLayoutDialog(LayoutImportViewModel viewModel)
        : this()
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        DataContext = viewModel;
    }

    private void ImportClicked(object? sender, RoutedEventArgs eventArgs) => Close(true);

    private void CancelClicked(object? sender, RoutedEventArgs eventArgs) => Close(false);
}
