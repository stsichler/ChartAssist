using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ChartAssist.Core;
using ChartAssist.Core.Import;
using ChartAssist.ViewModels;

namespace ChartAssist.Views;

public partial class MainWindow : Window
{
    private const string LegalNotice =
        "Sie sind als Pilot selbst für die Aktualität Ihrer Karten verantwortlich. ChartAssist ist nur eine Hilfe "
        + "und keine zugelassene Software. Fehler sind nicht ausgeschlossen, eine Haftung besteht nicht.\n\n"
        + "ChartAssist greift nie selbst auf die DFS zu: Sie rufen die Seiten im Browser auf und speichern sie. "
        + "Die Karten sind urheberrechtlich geschützt. Beachten Sie die Nutzungsbedingungen der DFS.";

    public MainWindow()
    {
        InitializeComponent();
    }

    private AbgleichWindow? _sessionWindow;

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    protected override void OnClosed(EventArgs e)
    {
        // Das Abgleichfenster hat keinen Besitzer und würde sonst allein weiterlaufen
        _sessionWindow?.Close();
        base.OnClosed(e);
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (ViewModel.Settings.LegalNoticeAccepted != AppInfo.LegalNoticeVersion)
        {
            if (!await MessageDialog.ConfirmAsync(this, "Willkommen bei ChartAssist", LegalNotice))
            {
                Close();
                return;
            }
            ViewModel.Settings.LegalNoticeAccepted = AppInfo.LegalNoticeVersion;
            await SaveSettingsAsync();
        }

        await LoadDatabaseAsync();
        if (ViewModel.Settings.ChartFolder.Length == 0)
        {
            await MessageDialog.ShowAsync(this, "Bitte wählen Sie zuerst ein Kartenverzeichnis.");
            await OpenOptionsAsync();
        }

        await LinuxDesktopSetup.RunAsync(this, ViewModel.Settings, () => _ = SaveSettingsAsync());

#if !DEBUG
        await CheckForNewReleaseAsync();
#endif
    }

    private async Task LoadDatabaseAsync()
    {
        ChartFolderLoadResult? result = ViewModel.LoadDatabase();
        string folder = ViewModel.Settings.ChartFolder;
        if (folder.Length > 0 && result == null)
        {
            await MessageDialog.ShowAsync(this, $"Das Kartenverzeichnis wurde nicht gefunden:\n{folder}\n\nBitte prüfen Sie es unter „Optionen“.");
            return;
        }
        if (result == null)
        {
            return;
        }

        if (result.ReadFailed)
        {
            await MessageDialog.ShowAsync(this, "Die Karten-Datenbank war beschädigt und wurde aus dem Kartenverzeichnis wiederhergestellt. "
                + "Beim nächsten Kartenabgleich werden alle Karten geprüft.");
        }
        else if (result.Rebuilt && result.PreviewsMissing)
        {
            await MessageDialog.ShowAsync(this, "Einigen Karten fehlt die Vorschau. Sie werden beim nächsten Kartenabgleich neu angefordert.");
        }
        if (result.Rebuilt)
        {
            TrySaveDatabase();
        }
    }

    private async Task OpenOptionsAsync()
    {
        var options = new OptionsWindow(ViewModel.Settings);
        if (!await options.ShowDialog<bool>(this))
        {
            return;
        }
        bool folderChanged = options.ChartFolder != ViewModel.Settings.ChartFolder;
        ViewModel.Settings.ChartFolder = options.ChartFolder;
        ViewModel.Settings.ImportFolder = options.ImportFolder;
        await SaveSettingsAsync();
        if (folderChanged)
        {
            await LoadDatabaseAsync();
        }
    }

    private async Task StartSessionAsync(ImportMode mode)
    {
        MainWindowViewModel vm = ViewModel;
        if (vm.Folder == null || vm.IsSessionOpen)
        {
            return;
        }
        if (vm.IsLegacyDatabase && vm.HasAirfields)
        {
            await MessageDialog.ShowAsync(this, "Diese Karten-Datenbank stammt noch von GAT24 und wird nicht mehr unterstützt. "
                + "Bitte wählen Sie ein neues, leeres Kartenverzeichnis und abonnieren Sie die Flugplätze neu.");
            return;
        }

        var import = new ChartImport(vm.Folder, vm.Database, AppInfo.VersionText, mode);
        var session = new AbgleichViewModel(import, new ImportFolderScanner(vm.Settings.EffectiveImportFolder), OnDatabaseChanged);
        var window = new AbgleichWindow { DataContext = session };
        vm.IsSessionOpen = true;
        window.Closed += (_, _) =>
        {
            vm.IsSessionOpen = false;
            vm.Refresh();
            if (import.UpdatedCharts.Count > 0)
            {
                new UpdateOverviewWindow(import.UpdatedCharts).Show(this);
            }
        };
        _sessionWindow = window;
        window.Closed += (_, _) => _sessionWindow = null;

        // Ohne Besitzer: Ein Klick ins Abgleichfenster holt sonst auch das Hauptfenster vor den Browser
        window.Show();
    }

    private void OnDatabaseChanged()
    {
        TrySaveDatabase();
        ViewModel.Refresh();
    }

    private void TrySaveDatabase()
    {
        try
        {
            ViewModel.SaveDatabase();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _ = MessageDialog.ShowAsync(this, "Die Karten-Datenbank konnte nicht gespeichert werden:\n" + e.Message);
        }
    }

    private async Task SaveSettingsAsync()
    {
        try
        {
            ViewModel.Settings.Save(Settings.DefaultPath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            await MessageDialog.ShowAsync(this, "Die Einstellungen konnten nicht gespeichert werden:\n" + e.Message);
        }
    }

    private async Task CheckForNewReleaseAsync()
    {
        using HttpClient client = ReleaseCheck.CreateHttpClient();
        ViewModel.NewRelease = await ReleaseCheck.FindNewerReleaseAsync(client, AppInfo.Version);
        ViewModel.UpdateBanner();
    }

    private async Task OpenSelectedChartAsync()
    {
        if (ViewModel.GetChartPath(ViewModel.SelectedNode) is string path)
        {
            await Launch.FileAsync(this, path);
        }
    }

    private async Task DeleteAirfieldAsync(TreeNodeViewModel node)
    {
        if (node.Airfield == null || !ViewModel.CanChangeOptions)
        {
            return;
        }
        string name = Core.Utility.AirfieldDirectoryName(node.Airfield);
        if (await MessageDialog.AskAsync(this, "Flugplatz löschen", $"Soll {name} mit allen Karten gelöscht werden?", yesIsDefault: false))
        {
            ViewModel.DeleteAirfield(node.Airfield);
        }
    }

    private async void OnBannerPressed(object? sender, PointerPressedEventArgs e)
    {
        switch (ViewModel.Banner)
        {
            case BannerKind.UpdateRequired when ViewModel.CanUpdateCharts:
                await StartSessionAsync(ImportMode.UpdateCharts);
                break;
            case BannerKind.NewRelease:
                await Launch.UriAsync(this, new Uri(ReleaseCheck.ReleasesPageUrl));
                break;
        }
    }

    private async void OnTreeDoubleTapped(object? sender, TappedEventArgs e) => await OpenSelectedChartAsync();

    private async void OnPreviewDoubleTapped(object? sender, TappedEventArgs e) => await OpenSelectedChartAsync();

    private async void OnTreeKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await OpenSelectedChartAsync();
        }
    }

    /// <summary>Rechtsklick wählt in Avalonia den Knoten nicht aus, deshalb hier auswählen und das Menü selbst öffnen.</summary>
    private void OnTreeContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if ((e.Source as Control)?.FindAncestorOfType<TreeViewItem>(includeSelf: true)?.DataContext is not TreeNodeViewModel node
            || !node.CanDeleteAirfield || !ViewModel.CanChangeOptions)
        {
            return;
        }
        ViewModel.SelectedNode = node;
        var delete = new MenuItem { Header = "Flugplatz löschen" };
        delete.Click += async (_, _) => await DeleteAirfieldAsync(node);
        var menu = new ContextMenu { ItemsSource = new[] { delete } };
        menu.Open(e.Source as Control);
        e.Handled = true;
    }

    private async void OnAddAirfield(object? sender, RoutedEventArgs e) => await StartSessionAsync(ImportMode.AddAirfield);

    private async void OnUpdateCharts(object? sender, RoutedEventArgs e) => await StartSessionAsync(ImportMode.UpdateCharts);

    private async void OnOptions(object? sender, RoutedEventArgs e) => await OpenOptionsAsync();

    private void OnHelp(object? sender, RoutedEventArgs e) => new HelpWindow().Show(this);

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
