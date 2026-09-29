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
    private bool _lastPressWasRightButton;

    public AbgleichWindow()
    {
        InitializeComponent();
        _timer.Tick += OnTimerTick;

        // Tastatur auf Fensterebene: Nach jedem Import wird die Liste neu aufgebaut, dabei geht der Fokus des
        // Listeneintrags verloren. Pfeiltasten und Enter sollen trotzdem immer funktionieren.
        AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);

        // Welche Maustaste zuletzt gedrückt wurde: Ein Rechtsklick öffnet nur das Kontextmenü, nie die Seite
        TaskList.AddHandler(PointerPressedEvent,
            (_, e) => _lastPressWasRightButton = e.GetCurrentPoint(TaskList).Properties.IsRightButtonPressed,
            RoutingStrategies.Tunnel);
    }

    private AbgleichViewModel ViewModel => (AbgleichViewModel)DataContext!;

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _timer.Start();
        FocusSelectedItem();
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
            if (await ViewModel.ProcessImportFolderAsync(ConfirmNewAirfieldAsync))
            {
                FocusSelectedItem();
            }
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
        if (ViewModel.SelectedItem is { } item)
        {
            await OpenAsync(item);
        }
    }

    /// <summary>Flugplätze ohne bekannten Permalink haben keinen Link; sie werden über das Flugplatzverzeichnis aufgerufen.</summary>
    private async Task OpenAsync(TaskItemViewModel item)
    {
        if (item.Link is Uri link)
        {
            await Launch.UriAsync(this, link);
        }
        else
        {
            ViewModel.Status = "Für diesen Flugplatz ist kein direkter Link bekannt. Bitte über das Flugplatzverzeichnis aufrufen und die Seite speichern.";
        }
    }

    /// <summary>
    /// Ein Klick auf einen Eintrag öffnet ihn. Bewusst am Klick und nicht an der Auswahl: ChartAssist markiert
    /// nach jedem Import den nächsten Eintrag selbst, das darf nie eine Seite öffnen (Leitplanke 3).
    /// Ein Doppelklick löst nur einmal Tapped aus und öffnet daher ebenfalls nur eine Seite.
    /// </summary>
    private async void OnTaskTapped(object? sender, TappedEventArgs e)
    {
        if (_lastPressWasRightButton)
        {
            return;
        }
        if ((e.Source as Control)?.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext is TaskItemViewModel item)
        {
            await OpenAsync(item);
        }
    }

    /// <summary>Kontextmenü "Link kopieren", z. B. zum Einfügen in die Adressleiste des Browsers.</summary>
    private void OnTaskContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if ((e.Source as Control)?.FindAncestorOfType<ListBoxItem>(includeSelf: true) is not { DataContext: TaskItemViewModel { Link: Uri link } } container)
        {
            return;
        }
        var copy = new MenuItem { Header = "Link kopieren" };
        copy.Click += async (_, _) =>
        {
            if (Clipboard is { } clipboard)
            {
                await clipboard.SetTextAsync(link.AbsoluteUri);
                ViewModel.Status = "Link kopiert: " + link.AbsoluteUri;
            }
        };
        new ContextMenu { ItemsSource = new[] { copy } }.Open(container);
        e.Handled = true;
    }

    private async void OnCopyImportFolder(object? sender, RoutedEventArgs e)
    {
        if (Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(ViewModel.ImportFolder);
            ViewModel.Status = "Pfad des Import-Ordners kopiert.";
        }
    }

    /// <summary>
    /// Pfeiltasten verschieben nur die Markierung, Enter öffnet genau den markierten Eintrag (Leitplanken 2 und 3).
    /// Auf Knöpfen und der Checkbox behalten die Tasten ihre normale Bedeutung.
    /// </summary>
    private async void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (FocusManager?.GetFocusedElement() is Button or CheckBox)
        {
            return;
        }
        switch (e.Key)
        {
            case Key.Up:
                e.Handled = true;
                MoveSelection(-1);
                break;
            case Key.Down:
                e.Handled = true;
                MoveSelection(+1);
                break;
            case Key.Enter:
                e.Handled = true;
                await OpenSelectedAsync();
                break;
        }
    }

    private void MoveSelection(int step)
    {
        int count = ViewModel.Items.Count;
        if (count == 0)
        {
            return;
        }
        int index = ViewModel.SelectedItem == null ? -1 : ViewModel.Items.IndexOf(ViewModel.SelectedItem);
        index = index < 0 ? 0 : Math.Clamp(index + step, 0, count - 1);
        ViewModel.SelectedItem = ViewModel.Items[index];
        FocusSelectedItem();
    }

    /// <summary>Gibt dem markierten Eintrag den Tastaturfokus, auch nachdem die Liste neu aufgebaut wurde.</summary>
    private void FocusSelectedItem()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (ViewModel.SelectedItem is { } item)
            {
                TaskList.ScrollIntoView(item);
                (TaskList.ContainerFromItem(item) ?? TaskList).Focus(NavigationMethod.Directional);
            }
            else
            {
                TaskList.Focus();
            }
        }, DispatcherPriority.Background);
    }

    private async void OnOpenInBrowser(object? sender, RoutedEventArgs e) => await OpenSelectedAsync();

    private async void OnOpenImportFolder(object? sender, RoutedEventArgs e) => await Launch.FolderAsync(this, ViewModel.Scanner.Folder);

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
