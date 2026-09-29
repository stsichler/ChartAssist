using Avalonia.Controls;

namespace ChartAssist.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        await LinuxDesktopSetup.RunAsync(this);
    }
}
