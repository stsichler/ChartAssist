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

    /// <summary>Die eine Seite, die ein Klick im Browser öffnet.</summary>
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
        "Klick auf einen Eintrag öffnet die Seite im Browser. Speichern Sie sie dort mit Strg+S "
        + "als „Webseite, nur HTML“ in den Import-Ordner. Den Rest erledigt ChartAssist.";

    public string ImportFolder => Scanner.Folder;

    public ObservableCollection<TaskItemViewModel> Items { get; } = [];

    [ObservableProperty]
    public partial TaskItemViewModel? SelectedItem { get; set; }

    [ObservableProperty]
    public partial string Status { get; set; }

    [ObservableProperty]
    public partial bool IsTopmost { get; set; } = true;

    /// <summary>
    /// Alles erledigt: Abgleich vollständig, keine Aktualisierung nötig, oder beim Hinzufügen alle Karten des neuen
    /// Flugplatzes übernommen. Dann schließt sich das Fenster selbst. Nach einem Abbruch bleibt es offen.
    /// </summary>
    public bool IsComplete => Import.State is ImportSessionState.Completed or ImportSessionState.NothingToDo
        || (Import.Mode == ImportMode.AddAirfield && Import.Airfields.Count > 0
            && Import.Airfields.All(t => t.Status == AirfieldTaskStatus.Done) && Import.PendingCharts.Count == 0);

    /// <summary>Das Fenster wurde geschlossen, weil alles erledigt war.</summary>
    public bool ClosedAutomatically { get; set; }

    /// <summary>
    /// Verarbeitet die fertig gespeicherten Dateien im Import-Ordner nacheinander.
    /// </summary>
    /// <param name="confirmNewAirfield">Rückfrage "Flugplatz abonnieren?" für einen unbekannten Flugplatz.</param>
    /// <returns>true, wenn mindestens eine Datei verarbeitet und die Aufgabenliste neu aufgebaut wurde.</returns>
    public async Task<bool> ProcessImportFolderAsync(Func<DfsAirfieldPage, Task<bool>> confirmNewAirfield)
    {
        IReadOnlyList<string> files;
        try
        {
            files = Scanner.Scan();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Status = "Der Import-Ordner ist nicht erreichbar: " + e.Message;
            return false;
        }

        foreach (string file in files)
        {
            await ProcessFileAsync(file, confirmNewAirfield);
        }
        return files.Count > 0;
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

    /// <summary>
    /// Baut die Aufgabenliste neu auf und markiert den nächsten offenen Eintrag, ohne ihn zu öffnen (Leitplanke 3).
    /// Regel für alle Einträge: abgehakt, sobald die Seite, auf die der Link zeigt, übernommen ist.
    /// </summary>
    public void RebuildItems()
    {
        Items.Clear();

        // Das Flugplatzverzeichnis beim Hinzufügen, und beim Abgleich für Flugplätze ohne bekannten Permalink
        if (Import.Mode == ImportMode.AddAirfield || Import.Airfields.Any(t => t.Link == null))
        {
            bool saved = Import.OtherPageImported;
            Items.Add(new TaskItemViewModel(saved ? "✓" : "○", "Flugplatzverzeichnis", DfsUrls.AirfieldDirectory,
                isOpen: !saved && !Import.IsFinished, isChart: false));
        }

        foreach (AirfieldTask task in Import.Airfields)
        {
            // Abgehakt, sobald die Flugplatzseite verarbeitet ist; fehlende Karten stehen eingerückt darunter
            bool pageSaved = task.Status != AirfieldTaskStatus.Open;
            string text = task.Link == null ? $"{task.Icao} – {task.Name} (über Flugplatzverzeichnis)" : $"{task.Icao} – {task.Name}";
            Items.Add(new TaskItemViewModel(pageSaved ? "✓" : "○", text, task.Link,
                isOpen: !pageSaved && !Import.IsFinished, isChart: false));

            // Übernommene Karten bleiben abgehakt stehen, in der Reihenfolge der Flugplatzseite. "Bitte speichern:"
            // nur bei geänderten Karten; bei einem neuen Flugplatz ist ohnehin klar, dass alle zu speichern sind
            var charts = Import.CompletedCharts.Where(c => c.Icao == task.Icao).Select(c => (Chart: c, Done: true))
                .Concat(Import.PendingCharts.Where(p => p.Icao == task.Icao).Select(p => (Chart: p, Done: false)))
                .OrderBy(c => c.Chart.Position);
            foreach ((PendingChart chart, bool done) in charts)
            {
                string name = Path.GetFileNameWithoutExtension(chart.ChartName);
                Items.Add(done
                    ? new TaskItemViewModel("✓", name, chart.Link, isOpen: false, isChart: true)
                    : new TaskItemViewModel("○", task.IsNew ? name : "Bitte speichern: " + name, chart.Link, isOpen: !Import.IsFinished, isChart: true));
            }
        }

        SelectedItem = Items.FirstOrDefault(i => i.IsOpen && i.Link != null && i.Link != DfsUrls.AirfieldDirectory)
            ?? Items.FirstOrDefault(i => i.IsOpen);
    }
}
