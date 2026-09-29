using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ChartAssist.Views;

public partial class HelpWindow : Window
{
    public HelpWindow()
    {
        InitializeComponent();
        VersionText.Text = "ChartAssist " + AppInfo.VersionText;
        ProjectLink.Content = AppInfo.ProjectUrl;
        ProjectLink.NavigateUri = new Uri(AppInfo.ProjectUrl);
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
