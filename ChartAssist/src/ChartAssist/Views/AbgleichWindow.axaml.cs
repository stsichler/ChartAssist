using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ChartAssist.Core.Dfs;
using ChartAssist.ViewModels;

namespace ChartAssist.Views;

/// <summary>
/// Nicht-modales Abgleichfenster. Der Import-Ordner wird nur abgefragt, solange das Fenster offen ist,
/// und nie, während noch eine Datei verarbeitet wird (IMPORT-MODUS 5.3, 5.5).
/// </summary>
public partial class AbgleichWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1.5) };
    private bool _busy;

    public AbgleichWindow()
    {
        InitializeComponent();
        _timer.Tick += OnTimerTick;
    }

    private AbgleichViewModel ViewModel => (AbgleichViewModel)DataContext!;

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _timer.Start();
        TaskList.Focus();
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        base.OnClosed(e);
    }

    private async void OnTimerTick(object? sender, EventArgs e)
    {
        if (_busy)
        {
            return;
        }
        _busy = true;
        try
        {
            await ViewModel.ProcessImportFolderAsync(ConfirmNewAirfieldAsync);
        }
        finally
        {
            _busy = false;
        }
    }

    private Task<bool> ConfirmNewAirfieldAsync(DfsAirfieldPage page) =>
        MessageDialog.AskAsync(this, "Neuer Flugplatz", $"Flugplatz {page.Icao} – {page.Name} abonnieren?");

    /// <summary>Öffnet genau die eine Seite des markierten Eintrags (IMPORT-MODUS 2, Leitplanke 2).</summary>
    private async Task OpenSelectedAsync()
    {
        if (ViewModel.SelectedItem?.Link is Uri link)
        {
            await Launch.UriAsync(this, link);
        }
    }

    private async void OnTaskDoubleTapped(object? sender, TappedEventArgs e) => await OpenSelectedAsync();

    private async void OnTaskKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await OpenSelectedAsync();
        }
    }

    private async void OnOpenInBrowser(object? sender, RoutedEventArgs e) => await OpenSelectedAsync();

    private async void OnOpenImportFolder(object? sender, RoutedEventArgs e) => await Launch.FolderAsync(this, ViewModel.Scanner.Folder);

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
