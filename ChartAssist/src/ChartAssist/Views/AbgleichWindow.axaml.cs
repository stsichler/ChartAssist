using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
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
            if (ViewModel.IsComplete)
            {
                // Kurz stehen lassen, damit der letzte Haken zu sehen ist
                _timer.Stop();
                await Task.Delay(TimeSpan.FromSeconds(1.5));
                ViewModel.ClosedAutomatically = true;
                Close();
            }
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

    /// <summary>
    /// Ein Klick auf einen Eintrag öffnet ihn. Bewusst am Klick und nicht an der Auswahl: ChartAssist markiert
    /// nach jedem Import den nächsten Eintrag selbst, das darf nie eine Seite öffnen (Leitplanke 3).
    /// Ein Doppelklick löst nur einmal Tapped aus und öffnet daher ebenfalls nur eine Seite.
    /// </summary>
    private async void OnTaskTapped(object? sender, TappedEventArgs e)
    {
        if ((e.Source as Control)?.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext is TaskItemViewModel { Link: Uri link })
        {
            await Launch.UriAsync(this, link);
        }
    }

    private async void OnCopyImportFolder(object? sender, RoutedEventArgs e)
    {
        if (Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(ViewModel.ImportFolder);
            ViewModel.Status = "Pfad des Import-Ordners kopiert.";
        }
    }

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
