using System.Collections.ObjectModel;
using System.Diagnostics;
using ChartAssist.Core.Dfs;
using ChartAssist.Core.Import;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ChartAssist.ViewModels;

/// <summary>Eintrag der Aufgabenliste: Flugplatzverzeichnis, Flugplatz oder anzufordernde Karte.</summary>
public sealed class TaskItemViewModel(string symbol, string text, Uri? link, bool isOpen, bool isChart)
{
    /// <summary>○ offen, … wartet auf Karten, ✓ erledigt.</summary>
    public string Symbol { get; } = symbol;

    public string Text { get; } = text;

    /// <summary>Die eine Seite, die ein Doppelklick im Browser öffnet.</summary>
    public Uri? Link { get; } = link;

    public bool IsOpen { get; } = isOpen;

    /// <summary>Karten stehen eingerückt unter ihrem Flugplatz.</summary>
    public bool IsChart { get; } = isChart;
}

/// <summary>Abgleichfenster (IMPORT-MODUS 5.5): Aufgabenliste, Import-Ordner, Statuszeile.</summary>
public partial class AbgleichViewModel : ViewModelBase
{
    private const string CompleteFormatTip = " Tipp: Speichern Sie als „Webseite, nur HTML“.";

    private readonly Action _databaseChanged;

    public AbgleichViewModel(ChartImport import, ImportFolderScanner scanner, Action databaseChanged)
    {
        Import = import;
        Scanner = scanner;
        _databaseChanged = databaseChanged;
        Status = import.Mode == ImportMode.AddAirfield
            ? "Öffnen Sie das Flugplatzverzeichnis und speichern Sie die Seite des neuen Flugplatzes."
            : "Speichern Sie zuerst die Seite des markierten Flugplatzes.";
        RebuildItems();
    }

    public ChartImport Import { get; }

    public ImportFolderScanner Scanner { get; }

    public string Title => Import.Mode == ImportMode.AddAirfield ? "Flugplatz hinzufügen" : "Kartenabgleich";

    public string Instructions =>
        "Doppelklick auf einen Eintrag öffnet die Seite im Browser. Speichern Sie sie dort mit Strg+S "
        + "als „Webseite, nur HTML“ in den Import-Ordner. Den Rest erledigt ChartAssist.";

    public string ImportFolderText => "Import-Ordner: " + Scanner.Folder;

    public ObservableCollection<TaskItemViewModel> Items { get; } = [];

    [ObservableProperty]
    public partial TaskItemViewModel? SelectedItem { get; set; }

    [ObservableProperty]
    public partial string Status { get; set; }

    [ObservableProperty]
    public partial bool IsTopmost { get; set; } = true;

    /// <summary>
    /// Verarbeitet die fertig gespeicherten Dateien im Import-Ordner nacheinander.
    /// </summary>
    /// <param name="confirmNewAirfield">Rückfrage "Flugplatz abonnieren?" für einen unbekannten Flugplatz.</param>
    public async Task ProcessImportFolderAsync(Func<DfsAirfieldPage, Task<bool>> confirmNewAirfield)
    {
        IReadOnlyList<string> files;
        try
        {
            files = Scanner.Scan();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Status = "Der Import-Ordner ist nicht erreichbar: " + e.Message;
            return;
        }

        foreach (string file in files)
        {
            await ProcessFileAsync(file, confirmNewAirfield);
        }
    }

    private async Task ProcessFileAsync(string file, Func<DfsAirfieldPage, Task<bool>> confirmNewAirfield)
    {
        string html;
        try
        {
            html = await File.ReadAllTextAsync(file);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return; // noch gesperrt, beim nächsten Durchlauf erneut
        }
        bool savedComplete = ImportFolderScanner.IsSavedComplete(file);

        ImportResult result;
        try
        {
            result = await Task.Run(() => Import.Import(html));
            if (result.Outcome == ImportOutcome.NewAirfieldFound && result.NewAirfield is DfsAirfieldPage page)
            {
                result = await confirmNewAirfield(page)
                    ? await Task.Run(() => Import.AddAirfield(page))
                    : new ImportResult(ImportOutcome.Processed, $"{page.Icao} wurde nicht abonniert.");
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            Debug.WriteLine(e);
            result = new ImportResult(ImportOutcome.Failed, "Fehler beim Übernehmen: " + e.Message);
        }

        try
        {
            if (result.Outcome == ImportOutcome.Failed)
            {
                Scanner.MoveToErrorFolder(file);
            }
            else
            {
                Scanner.Remove(file);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine("Import-Datei nicht entfernt: " + e.Message);
        }

        Status = result.Outcome == ImportOutcome.Failed
            ? result.Message + " Die Datei liegt jetzt im Ordner „Fehler“."
            : result.Message + (savedComplete ? CompleteFormatTip : "");
        _databaseChanged();
        RebuildItems();
    }

    /// <summary>Baut die Aufgabenliste neu auf und markiert den nächsten offenen Eintrag, ohne ihn zu öffnen (Leitplanke 3).</summary>
    public void RebuildItems()
    {
        Items.Clear();
        if (Import.Mode == ImportMode.AddAirfield)
        {
            Items.Add(new TaskItemViewModel("→", "Flugplatzverzeichnis", DfsUrls.AirfieldDirectory, isOpen: true, isChart: false));
        }

        foreach (AirfieldTask task in Import.Airfields)
        {
            string symbol = task.Status switch
            {
                AirfieldTaskStatus.Done => "✓",
                AirfieldTaskStatus.WaitingForCharts => "…",
                _ => "○",
            };
            Items.Add(new TaskItemViewModel(symbol, $"{task.Icao} – {task.Name}", task.Link ?? DfsUrls.AirfieldDirectory,
                task.Status == AirfieldTaskStatus.Open && !Import.IsFinished, isChart: false));

            foreach (PendingChart chart in Import.PendingCharts.Where(p => p.Icao == task.Icao))
            {
                Items.Add(new TaskItemViewModel("○", "Bitte speichern: " + Path.GetFileNameWithoutExtension(chart.ChartName),
                    chart.Link, isOpen: !Import.IsFinished, isChart: true));
            }
        }

        SelectedItem = Items.FirstOrDefault(i => i.IsOpen && i.Link != DfsUrls.AirfieldDirectory)
            ?? Items.FirstOrDefault(i => i.IsOpen);
    }
}
