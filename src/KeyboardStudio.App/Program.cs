using Avalonia;

namespace KeyboardStudio.App;

internal sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args is ["--version"])
        {
            Console.WriteLine(ApplicationReleaseInfo.DisplayVersion);
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
