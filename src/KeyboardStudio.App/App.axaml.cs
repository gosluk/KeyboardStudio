using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace KeyboardStudio.App;

public sealed partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            var viewModel = new MainWindowViewModel(new AvaloniaProjectInteractionService(window));
            window.DataContext = viewModel;
            desktop.MainWindow = window;

            // Started and deliberately not awaited: the window already has a working document to
            // draw, and importing the host's own layout is worth having a moment later rather than
            // worth making the first frame wait for. It replaces that document if it succeeds
            // before the user starts working, and leaves it alone if it does not.
            _ = viewModel.ImportHostLayoutAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
