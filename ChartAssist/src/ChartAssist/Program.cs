using Avalonia;

namespace ChartAssist;

internal sealed class Program
{
    // Vor AppMain keine Avalonia-APIs und keinen Code verwenden, der einen
    // SynchronizationContext braucht: Es ist noch nichts initialisiert.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Wird auch von der Vorschau in der IDE verwendet, nicht entfernen.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
