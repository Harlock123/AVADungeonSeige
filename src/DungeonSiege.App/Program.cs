using Avalonia;

namespace DungeonSiege.App;

// A classic Program class (not top-level statements) so the Avalonia previewer
// can discover BuildAvaloniaApp and Windows gets an STA main thread.
internal static class Program
{
    // Don't touch Avalonia APIs before AppMain: the platform isn't initialised yet.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
#if DEBUG
        .WithDeveloperTools()
#endif
        .LogToTrace();
}
