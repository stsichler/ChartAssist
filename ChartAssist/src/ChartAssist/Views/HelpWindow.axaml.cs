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
        LicensesText.Text = ReadLicenses();
    }

    private static string ReadLicenses()
    {
        using Stream stream = typeof(HelpWindow).Assembly.GetManifestResourceStream("THIRD-PARTY-NOTICES.txt")
            ?? throw new InvalidOperationException("Die Lizenztexte fehlen im Programm.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
