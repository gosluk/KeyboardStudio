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
            var settingsStore = new JsonApplicationSettingsStore(new LocalApplicationSettingsPathProvider());
            var themeService = new AvaloniaApplicationThemeService(this);
            var startup = new ApplicationStartupSequence(settingsStore, themeService);

            // The saved appearance is restored inside Start, before the window it returns exists,
            // so the first frame is already drawn in the theme the user chose. The header shares
            // the same store and service, so a later choice writes to the file startup read.
            var window = startup.Start(() =>
            {
                var shell = new MainWindow();
                shell.DataContext = new MainWindowViewModel(
                    new AvaloniaProjectInteractionService(shell),
                    new AppearanceViewModel(settingsStore, themeService));
                return shell;
            });

            desktop.MainWindow = window;

            // Started and deliberately not awaited: the window already has a working document to
            // draw, and importing the host's own layout is worth having a moment later rather than
            // worth making the first frame wait for. It replaces that document if it succeeds
            // before the user starts working, and leaves it alone if it does not.
            _ = ((MainWindowViewModel)window.DataContext!).ImportHostLayoutAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
