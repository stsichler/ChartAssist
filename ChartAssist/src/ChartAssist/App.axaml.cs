using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ChartAssist.Core;
using ChartAssist.ViewModels;
using ChartAssist.Views;

namespace ChartAssist;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Beim ersten Start wird das Kartenverzeichnis aus ChartButlerCS übernommen (TECHNISCHE-BASIS 10)
            Settings settings = Settings.LoadOrImport(Settings.DefaultPath, Settings.ChartButlerCSPath);
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(settings),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
