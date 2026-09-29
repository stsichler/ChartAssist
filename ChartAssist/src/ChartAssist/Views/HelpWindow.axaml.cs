using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ChartAssist.Views;

public partial class HelpWindow : Window
{
    public HelpWindow()
    {
        InitializeComponent();
        VersionText.Text = "ChartAssist " + AppInfo.VersionText;
        ProjectLink.Content = AppInfo.WebsiteUrl;
        ProjectLink.NavigateUri = new Uri(AppInfo.WebsiteUrl);
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
