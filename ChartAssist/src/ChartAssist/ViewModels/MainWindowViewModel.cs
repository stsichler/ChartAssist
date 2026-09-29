using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media.Imaging;
using ChartAssist.Core;
using ChartAssist.Core.Data;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ChartAssist.ViewModels;

public enum BannerKind
{
    None,

    /// <summary>Rot: Kartenabgleich nötig; ein Klick startet ihn.</summary>
    UpdateRequired,

    /// <summary>Rot: Hinweis ohne Aktion, z. B. veraltete Datenbank.</summary>
    Warning,

    /// <summary>Blau: neue Programmversion; ein Klick öffnet die Download-Seite.</summary>
    NewRelease,
}

/// <summary>Zustand des Hauptfensters. Dialoge und weitere Fenster öffnet das Fenster selbst.</summary>
public partial class MainWindowViewModel : ViewModelBase
{
    /// <summary>Die BasicVFR erscheint alle 28 Tage (IMPORT-MODUS 4).</summary>
    public const int EditionCycleDays = 28;

    public MainWindowViewModel(Settings settings)
    {
        Settings = settings;
    }

    public MainWindowViewModel()
        : this(new Settings())
    {
    }

    public Settings Settings { get; private set; }

    public ChartFolder? Folder { get; private set; }

    public ChartDatabase Database { get; private set; } = new();

    public ObservableCollection<TreeNodeViewModel> Nodes { get; } = [];

    [ObservableProperty]
    public partial TreeNodeViewModel? SelectedNode { get; set; }

    [ObservableProperty]
    public partial Bitmap? Preview { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; } = "ChartAssist";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBannerVisible), nameof(IsBannerWarning))]
    public partial BannerKind Banner { get; set; }

    [ObservableProperty]
    public partial string BannerText { get; set; } = "";

    /// <summary>Statusleiste: Ausgabe des Kartensatzes und nächste planmäßige Ausgabe.</summary>
    [ObservableProperty]
    public partial string StatusText { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStartSession), nameof(CanUpdateCharts), nameof(CanChangeOptions))]
    public partial bool IsSessionOpen { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStartSession), nameof(CanUpdateCharts))]
    public partial bool HasFolder { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUpdateCharts))]
    public partial bool HasAirfields { get; set; }

    public string VersionText => "Version " + AppInfo.VersionText;

    /// <summary>Neuere Programmversion auf GitHub, falls gefunden.</summary>
    public Version? NewRelease { get; set; }

    public bool IsBannerVisible => Banner != BannerKind.None;

    public bool IsBannerWarning => Banner is BannerKind.UpdateRequired or BannerKind.Warning;

    public bool CanStartSession => HasFolder && !IsSessionOpen;

    public bool CanUpdateCharts => CanStartSession && HasAirfields;

    /// <summary>Während eines Abgleichs darf das Kartenverzeichnis nicht wechseln (IMPORT-MODUS 5.5).</summary>
    public bool CanChangeOptions => !IsSessionOpen;

    /// <summary>Veraltete Datenbank mit GAT24 als Quelle; wird nicht mehr migriert (IMPORT-MODUS 5.6).</summary>
    public bool IsLegacyDatabase => Database.DataSource == "GAT24" || (Database.DataSource == null && Database.Version != null);

    public void ApplySettings(Settings settings)
    {
        Settings = settings;
    }

    /// <summary>Lädt die Datenbank des eingestellten Kartenverzeichnisses.</summary>
    /// <returns>Das Ergebnis, oder null, wenn kein Verzeichnis eingestellt ist oder es fehlt.</returns>
    public ChartFolderLoadResult? LoadDatabase()
    {
        string path = Settings.ChartFolder;
        Folder = path.Length > 0 && Directory.Exists(path) ? new ChartFolder(path) : null;
        HasFolder = Folder != null;
        Title = path.Length > 0 ? "ChartAssist – " + path : "ChartAssist";

        ChartFolderLoadResult? result = Folder?.Load();
        Database = result?.Database ?? new ChartDatabase();
        Refresh();
        return result;
    }

    /// <summary>Speichert die Datenbank, falls sie sich geändert hat.</summary>
    public void SaveDatabase()
    {
        Folder?.Save(Database);
    }

    /// <summary>Baut Baum und Banner neu auf, z. B. nach jedem Import.</summary>
    public void Refresh()
    {
        string? selectedChart = SelectedNode?.Chart?.Name;
        string? selectedAirfield = SelectedNode?.Airfield?.Icao;

        Nodes.Clear();
        var airfields = new TreeNodeViewModel("Flugplätze") { IsGroup = true, IsExpanded = true };
        foreach (Airfield airfield in Database.Airfields.OrderBy(a => a.Icao, StringComparer.Ordinal))
        {
            var node = new TreeNodeViewModel(Utility.AirfieldDirectoryName(airfield)) { Airfield = airfield, CanDeleteAirfield = true };
            foreach (Chart chart in Database.ChartsOf(airfield).OrderBy(c => c.Name, StringComparer.Ordinal))
            {
                node.Children.Add(new TreeNodeViewModel(chart.Name) { Airfield = airfield, Chart = chart, CanDeleteAirfield = true });
            }
            airfields.Children.Add(node);
        }
        Nodes.Add(airfields);

        var updates = new TreeNodeViewModel("Aktualisierungen") { IsGroup = true };
        foreach (DateOnly date in Database.Updates.OrderDescending())
        {
            var node = new TreeNodeViewModel(date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture));
            foreach (Chart chart in Database.Charts.Where(c => c.LastUpdate == date).OrderBy(c => c.Name, StringComparer.Ordinal))
            {
                node.Children.Add(new TreeNodeViewModel(chart.Name) { Airfield = Database.FindAirfield(chart.Icao), Chart = chart });
            }
            if (node.Children.Count > 0)
            {
                updates.Children.Add(node);
            }
        }
        Nodes.Add(updates);

        HasAirfields = Database.Airfields.Count > 0;
        SelectedNode = FindNode(selectedChart, selectedAirfield);
        UpdateBanner();
    }

    /// <summary>Entfernt den Flugplatz aus Datenbank und Kartenverzeichnis.</summary>
    public void DeleteAirfield(Airfield airfield)
    {
        if (Folder == null)
        {
            return;
        }
        string directory = Path.Combine(Folder.Path, Utility.AirfieldDirectoryName(airfield));
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Die Datenbank wird trotzdem bereinigt; Reste im Verzeichnis kann der Benutzer selbst löschen
        }
        Database.RemoveAirfield(airfield);
        SaveDatabase();
        Refresh();
    }

    /// <summary>Datei, die ein Doppelklick auf den Knoten öffnet.</summary>
    public string? GetChartPath(TreeNodeViewModel? node) =>
        Folder != null && node?.Airfield != null && node.Chart != null
            ? Utility.BuildChartPath(Folder.Path, node.Airfield, node.Chart)
            : null;

    /// <summary>Nächste planmäßige Ausgabe der BasicVFR nach der angegebenen (28-Tage-Zyklus, IMPORT-MODUS 4).</summary>
    public static DateOnly NextEdition(DateOnly edition) => edition.AddDays(EditionCycleDays);

    public void UpdateBanner()
    {
        UpdateStatus();

        if (HasAirfields && IsLegacyDatabase)
        {
            Show(BannerKind.Warning, "Diese Datenbank stammt noch von GAT24. Bitte die Flugplätze neu abonnieren.");
        }
        else if (HasAirfields && (Database.AipLastUpdate == null || Database.Version != AppInfo.VersionText))
        {
            Show(BannerKind.UpdateRequired, "Bitte führen Sie einen Kartenabgleich durch. Hier klicken zum Starten.");
        }
        else if (HasAirfields && Database.AipLastUpdate is DateOnly last
            && DateOnly.FromDateTime(DateTime.Today) >= NextEdition(last))
        {
            string expected = Format(NextEdition(last));
            Show(BannerKind.UpdateRequired, $"Seit {expected} ist eine neue Ausgabe der BasicVFR zu erwarten. Hier klicken für den Kartenabgleich.");
        }
        else if (NewRelease != null)
        {
            Show(BannerKind.NewRelease, $"ChartAssist {NewRelease} ist verfügbar. Hier klicken zum Herunterladen.");
        }
        else
        {
            Show(BannerKind.None, "");
        }
    }

    private void UpdateStatus()
    {
        if (!HasFolder)
        {
            StatusText = "Kein Kartenverzeichnis gewählt";
        }
        else if (Database.AipLastUpdate is not DateOnly edition)
        {
            StatusText = "Kartenstand: noch kein vollständiger Kartenabgleich";
        }
        else
        {
            DateOnly next = NextEdition(edition);
            string nextText = DateOnly.FromDateTime(DateTime.Today) >= next
                ? $"Neue Ausgabe seit {Format(next)} erwartet"
                : $"Nächste Ausgabe voraussichtlich am {Format(next)}";
            StatusText = $"Kartenstand: Ausgabe vom {Format(edition)}  ·  {nextText}";
        }
    }

    private static string Format(DateOnly date) => date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    partial void OnSelectedNodeChanged(TreeNodeViewModel? value)
    {
        Bitmap? old = Preview;
        Preview = LoadPreview(value);
        old?.Dispose();
    }

    private Bitmap? LoadPreview(TreeNodeViewModel? node)
    {
        if (Folder == null || node?.Airfield == null || node.Chart == null)
        {
            return null;
        }
        // PNG-Karten werden direkt angezeigt, das TripKit über seine JPEG-Vorschau
        string path = node.Chart.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            ? Utility.BuildChartPath(Folder.Path, node.Airfield, node.Chart)
            : Utility.BuildChartPreviewPath(Folder.Path, node.Airfield, node.Chart, "jpg");
        try
        {
            // Über den Speicher laden, damit die Datei nicht gesperrt bleibt: Der Import kann sie ersetzen
            using var stream = new MemoryStream(File.ReadAllBytes(path));
            return new Bitmap(stream);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    private TreeNodeViewModel? FindNode(string? chartName, string? icao)
    {
        foreach (TreeNodeViewModel airfield in Nodes[0].Children)
        {
            if (chartName != null && airfield.Children.FirstOrDefault(c => c.Chart?.Name == chartName) is TreeNodeViewModel chart)
            {
                airfield.IsExpanded = true;
                return chart;
            }
            if (chartName == null && airfield.Airfield?.Icao == icao)
            {
                return airfield;
            }
        }
        return null;
    }

    private void Show(BannerKind kind, string text)
    {
        Banner = kind;
        BannerText = text;
    }
}
