using System.Diagnostics;
using ChartAssist.Core.Data;
using ChartAssist.Core.Dfs;
using ChartAssist.Core.TripKit;

namespace ChartAssist.Core.Import;

public enum ImportMode
{
    /// <summary>"Karten aktualisieren": alle abonnierten Flugplätze prüfen.</summary>
    UpdateCharts,

    /// <summary>"Neuer Flugplatz": Flugplätze über das Flugplatzverzeichnis hinzufügen.</summary>
    AddAirfield,
}

public enum ImportSessionState
{
    WaitingForFirstPage,
    Running,

    /// <summary>Die erste Seite hat gezeigt, dass seit dem letzten Abgleich keine neue Ausgabe erschienen ist.</summary>
    NothingToDo,

    /// <summary>Alle Flugplätze sind geprüft; das Effective-Datum ist in der Datenbank eingetragen.</summary>
    Completed,

    /// <summary>Während der Sitzung ist eine neue Ausgabe erschienen; der Abgleich muss neu gestartet werden.</summary>
    Aborted,
}

public enum AirfieldTaskStatus
{
    Open,
    WaitingForCharts,
    Done,
}

public enum ImportOutcome
{
    /// <summary>Verarbeitet oder bewusst übergangen; die Datei kann gelöscht werden.</summary>
    Processed,

    /// <summary>Unbekannter Flugplatz: nach Rückfrage <see cref="ChartImport.AddAirfield"/> aufrufen, danach die Datei löschen.</summary>
    NewAirfieldFound,

    /// <summary>Nicht auswertbar; die Datei gehört nach <c>Import/Fehler/</c>.</summary>
    Failed,
}

public sealed record ImportResult(ImportOutcome Outcome, string Message, DfsAirfieldPage? NewAirfield = null);

/// <summary>Aufgabe "Flugplatzseite speichern" in der Aufgabenliste.</summary>
public sealed class AirfieldTask
{
    internal AirfieldTask(Airfield airfield, Uri? link, bool isNew)
    {
        Airfield = airfield;
        Link = link;
        IsNew = isNew;
    }

    public string Icao => Airfield.Icao;

    public string Name => Airfield.Name;

    /// <summary>Flugplatzseite der DFS; null, wenn der Permalink unbekannt ist (dann über das Flugplatzverzeichnis).</summary>
    public Uri? Link { get; }

    public AirfieldTaskStatus Status { get; internal set; }

    internal Airfield Airfield { get; }

    /// <summary>Neu abonniert: alle Karten werden übernommen, ohne Einträge unter "Aktualisierungen".</summary>
    internal bool IsNew { get; }
}

/// <summary>Aufgabe "Bitte speichern: Karte", zugeordnet über den Hash der Kartenseite.</summary>
/// <param name="Position">Stelle auf der Flugplatzseite, für die Reihenfolge in der Aufgabenliste.</param>
public sealed record PendingChart(string Icao, string ChartName, string ServerName, string AirfieldPermalink, string Hash, Uri Link, byte[] Preview, int Position);

/// <summary>In dieser Sitzung aktualisierte Karte, für die Übersicht am Ende.</summary>
public sealed record UpdatedChart(string Name, string Path);

/// <summary>
/// Abgleich-Sitzung des Import-Modus (IMPORT-MODUS 5.4, bisher <c>DFS_CheckForNewCharts</c>, <c>DFS_UpdateCharts</c>,
/// <c>DFS_AddNewField</c>, <c>DFS_DownloadAndCheckChart</c>, <c>DFS_RemoveOrphanCharts</c>). Wertet gespeicherte
/// Seiten aus und pflegt Kartenverzeichnis und Datenbank. Synchron und UI-frei; die UI speichert nach jedem
/// Aufruf die Datenbank über <see cref="ChartFolder.Save"/>. Der Zustand der Sitzung liegt nur im Speicher.
/// </summary>
public sealed class ChartImport
{
    private readonly ChartFolder _folder;
    private readonly ChartDatabase _database;
    private readonly string _programVersion;
    private readonly List<AirfieldTask> _airfields = [];
    private readonly List<PendingChart> _pendingCharts = [];
    private readonly List<PendingChart> _completedCharts = [];
    private readonly List<UpdatedChart> _updatedCharts = [];

    /// <param name="programVersion">Vierstellige Programmversion; eine andere Version in der Datenbank erzwingt einen vollständigen Abgleich.</param>
    public ChartImport(ChartFolder folder, ChartDatabase database, string programVersion, ImportMode mode)
    {
        _folder = folder;
        _database = database;
        _programVersion = programVersion;
        Mode = mode;

        if (mode == ImportMode.UpdateCharts)
        {
            foreach (Airfield airfield in database.Airfields)
            {
                string? permalink = database.ChartsOf(airfield).FirstOrDefault(c => c.AirfieldPermalink != null)?.AirfieldPermalink;
                _airfields.Add(new AirfieldTask(airfield, permalink == null ? null : DfsUrls.AirfieldPage(permalink), isNew: false));
            }
        }
    }

    public ImportMode Mode { get; }

    public ImportSessionState State { get; private set; } = ImportSessionState.WaitingForFirstPage;

    /// <summary>Effective-Datum der ersten Seite der Sitzung.</summary>
    public DateOnly? Effective { get; private set; }

    public IReadOnlyList<AirfieldTask> Airfields => _airfields;

    public IReadOnlyList<PendingChart> PendingCharts => _pendingCharts;

    /// <summary>In dieser Sitzung angeforderte und übernommene Karten; sie bleiben abgehakt in der Aufgabenliste.</summary>
    public IReadOnlyList<PendingChart> CompletedCharts => _completedCharts;

    public IReadOnlyList<UpdatedChart> UpdatedCharts => _updatedCharts;

    public bool IsFinished => State is ImportSessionState.NothingToDo or ImportSessionState.Completed or ImportSessionState.Aborted;

    /// <summary>Wertet eine gespeicherte Seite aus.</summary>
    public ImportResult Import(string html)
    {
        if (IsFinished)
        {
            return new ImportResult(ImportOutcome.Processed, "Der Abgleich ist bereits beendet, die Seite wird nicht mehr gebraucht.");
        }

        DfsPageKind kind = DfsPageParser.DetectKind(html);
        if (kind == DfsPageKind.Unknown)
        {
            return new ImportResult(ImportOutcome.Failed, "Die Datei ist keine Seite der BasicVFR.");
        }

        try
        {
            if (CheckEffectiveDate(DfsPageParser.ParseEffectiveDate(html)) is ImportResult stop)
            {
                return stop;
            }
            return kind switch
            {
                DfsPageKind.AirfieldPage => ApplyAirfieldPage(DfsPageParser.ParseAirfieldPage(html)),
                DfsPageKind.ChartPage => ApplyChartPage(DfsPageParser.ParseChartPage(html)),
                _ => new ImportResult(ImportOutcome.Processed, $"Effective: {Format(Effective!.Value)} erkannt. Bitte jetzt die Flugplatzseiten speichern."),
            };
        }
        catch (DfsPageException e)
        {
            return new ImportResult(ImportOutcome.Failed, e.Message);
        }
    }

    /// <summary>Abonniert einen neuen Flugplatz, nachdem der Benutzer zugestimmt hat. Alle Karten werden angefordert.</summary>
    public ImportResult AddAirfield(DfsAirfieldPage page)
    {
        if (_database.FindAirfield(page.Icao) != null || _airfields.Any(t => t.Icao == page.Icao))
        {
            return ApplyAirfieldPage(page);
        }

        var airfield = new Airfield { Icao = page.Icao, Name = Utility.GetFilenameFor(page.Name) };
        var task = new AirfieldTask(airfield, DfsUrls.AirfieldPage(page.Permalink), isNew: true);
        _airfields.Add(task);
        return CheckCharts(task, page, warning: null);
    }

    private ImportResult? CheckEffectiveDate(DateOnly effective)
    {
        if (Effective == null)
        {
            Effective = effective;
            State = ImportSessionState.Running;
            if (Mode == ImportMode.UpdateCharts && _database.AipLastUpdate == effective && _database.Version == _programVersion)
            {
                State = ImportSessionState.NothingToDo;
                return new ImportResult(ImportOutcome.Processed,
                    $"Keine Aktualisierung notwendig: Die Karten sind aktuell (Effective: {Format(effective)}).");
            }
            return null;
        }

        if (effective != Effective)
        {
            State = ImportSessionState.Aborted;
            return new ImportResult(ImportOutcome.Processed,
                $"Die DFS hat während des Abgleichs eine neue Ausgabe veröffentlicht (Effective: {Format(effective)}). Bitte den Abgleich neu starten.");
        }
        return null;
    }

    private ImportResult ApplyAirfieldPage(DfsAirfieldPage page)
    {
        // Zuordnung zuerst über den Permalink, sonst über den ICAO-Code
        AirfieldTask? task = _airfields.Find(t => _database.ChartsOf(t.Airfield).Any(c => c.AirfieldPermalink == page.Permalink))
            ?? _airfields.Find(t => t.IsNew && t.Link == DfsUrls.AirfieldPage(page.Permalink));
        string? warning = null;
        if (task == null)
        {
            task = _airfields.Find(t => t.Icao == page.Icao);
            if (task != null && _database.ChartsOf(task.Airfield).Any(c => c.AirfieldPermalink != null))
            {
                warning = $"Achtung: Der Permalink von {task.Icao} hat sich geändert.";
            }
        }
        if (task == null && _database.FindAirfield(page.Icao) is Airfield known)
        {
            // Abonniert, aber im Modus "Neuer Flugplatz" noch ohne Aufgabe
            task = new AirfieldTask(known, DfsUrls.AirfieldPage(page.Permalink), isNew: false);
            _airfields.Add(task);
        }
        if (task == null)
        {
            return new ImportResult(ImportOutcome.NewAirfieldFound, $"Flugplatz {page.Icao} – {page.Name} abonnieren?", page);
        }
        return CheckCharts(task, page, warning);
    }

    /// <summary>Vergleicht die Karten der Flugplatzseite mit dem Kartenverzeichnis (bisher <c>DFS_DownloadAndCheckChart</c>, erster Teil).</summary>
    private ImportResult CheckCharts(AirfieldTask task, DfsAirfieldPage page, string? warning)
    {
        Airfield airfield = task.Airfield;
        _pendingCharts.RemoveAll(p => p.Icao == airfield.Icao);

        var serverNames = new HashSet<string>(StringComparer.Ordinal);
        for (int position = 0; position < page.Charts.Count; position++)
        {
            DfsChartEntry entry = page.Charts[position];
            string serverName = entry.Name.StartsWith(airfield.Icao, StringComparison.Ordinal) ? entry.Name : airfield.Icao + " " + entry.Name;
            serverNames.Add(serverName);

            Chart? chart = task.IsNew ? null
                : _database.Charts.Find(c => c.AirfieldPermalink == page.Permalink && c.ServerName == serverName);
            if (chart == null)
            {
                chart = _database.FindChart(Utility.GetFilenameFor(serverName) + ".png");
                if (chart != null)
                {
                    chart.AirfieldPermalink = page.Permalink;
                    chart.ServerName = serverName;
                }
            }
            string chartName = chart?.Name ?? Utility.GetFilenameFor(serverName) + ".png";

            var probe = new Chart { Icao = airfield.Icao, Name = chartName };
            bool isCurrent = !task.IsNew && chart != null
                && File.Exists(Utility.BuildChartPath(_folder.Path, airfield, probe))
                && Utility.FileEquals(Utility.BuildChartPreviewPath(_folder.Path, airfield, probe, "png"), entry.PreviewPng);
            if (!isCurrent)
            {
                _completedCharts.RemoveAll(c => c.Icao == airfield.Icao && c.ChartName == chartName);
                _pendingCharts.Add(new PendingChart(airfield.Icao, chartName, serverName, page.Permalink, entry.Hash,
                    ChartLink(entry, page.Effective), entry.PreviewPng, position));
            }
        }

        int removed = task.IsNew ? 0 : RemoveOrphanCharts(airfield, serverNames);
        int pending = _pendingCharts.Count(p => p.Icao == airfield.Icao);

        string message;
        if (pending == 0)
        {
            if (removed > 0)
            {
                UpdateTripKit(task);
            }
            message = removed > 0
                ? $"{airfield.Icao}: aktuell, {removed} entfallene Karte(n) entfernt."
                : $"{airfield.Icao}: alle Karten aktuell.";
            CompleteAirfield(task);
        }
        else
        {
            task.Status = AirfieldTaskStatus.WaitingForCharts;
            message = $"{airfield.Icao}: {pending} Karte(n) bitte speichern.";
        }
        return new ImportResult(ImportOutcome.Processed, warning == null ? message : warning + " " + message);
    }

    /// <summary>Übernimmt eine angeforderte Karte (bisher <c>DFS_DownloadAndCheckChart</c>, zweiter Teil).</summary>
    private ImportResult ApplyChartPage(DfsChartPage page)
    {
        PendingChart? pending = _pendingCharts.Find(p => string.Equals(p.Hash, page.Hash, StringComparison.OrdinalIgnoreCase));
        if (pending == null)
        {
            return new ImportResult(ImportOutcome.Processed, $"Karte \"{page.Name}\" wurde nicht angefordert und wird übergangen.");
        }
        if (!string.Equals(page.Name, pending.ServerName, StringComparison.Ordinal)
            && !pending.ServerName.EndsWith(" " + page.Name, StringComparison.Ordinal))
        {
            Debug.WriteLine($"Kartenname weicht ab: erwartet \"{pending.ServerName}\", Seite \"{page.Name}\"");
        }

        AirfieldTask task = _airfields.First(t => t.Icao == pending.Icao);
        Airfield airfield = task.Airfield;
        if (_database.FindAirfield(airfield.Icao) == null)
        {
            _database.Airfields.Add(airfield);
        }
        Directory.CreateDirectory(Path.Combine(_folder.Path, Utility.AirfieldDirectoryName(airfield)));

        Chart? chart = _database.FindChart(pending.ChartName);
        if (chart == null)
        {
            chart = new Chart { Icao = airfield.Icao, Name = pending.ChartName };
            _database.Charts.Add(chart);
        }
        chart.AirfieldPermalink = pending.AirfieldPermalink;
        chart.ServerName = pending.ServerName;

        // Die Vorschau erst nach der Karte schreiben: Bricht etwas ab, gilt die Karte beim nächsten Mal wieder als geändert
        string chartPath = Utility.BuildChartPath(_folder.Path, airfield, chart);
        Utility.WriteFileAtomic(chartPath, page.Png);
        Utility.WriteFileAtomic(Utility.BuildChartPreviewPath(_folder.Path, airfield, chart, "png"), pending.Preview, hidden: true);

        DateOnly chartUpdate = page.Date ?? Effective!.Value;
        chart.CreationDate = DateOnly.FromDateTime(DateTime.Now);
        chart.LastUpdate = chartUpdate;
        airfield.LastUpdate = chartUpdate;
        if (!task.IsNew)
        {
            _database.AddUpdate(chartUpdate);
        }

        _pendingCharts.Remove(pending);
        _completedCharts.Add(pending);
        _updatedCharts.Add(new UpdatedChart(chart.Name, chartPath));

        int remaining = _pendingCharts.Count(p => p.Icao == airfield.Icao);
        if (remaining > 0)
        {
            return new ImportResult(ImportOutcome.Processed, $"{chart.Name} übernommen, noch {remaining} Karte(n) für {airfield.Icao}.");
        }

        UpdateTripKit(task);
        CompleteAirfield(task);
        return new ImportResult(ImportOutcome.Processed, $"{chart.Name} übernommen. {airfield.Icao} ist aktuell.");
    }

    /// <summary>Entfernt Karten, die es auf der Flugplatzseite nicht mehr gibt (bisher <c>DFS_RemoveOrphanCharts</c>).</summary>
    private int RemoveOrphanCharts(Airfield airfield, HashSet<string> serverNames)
    {
        // Das TripKit hat keinen Permalink und bleibt; aus dem Verzeichnis wiederhergestellte Karten ohne Permalink ebenso
        List<Chart> orphans = _database.ChartsOf(airfield)
            .Where(c => c.AirfieldPermalink != null && (c.ServerName == null || !serverNames.Contains(c.ServerName)))
            .ToList();
        foreach (Chart chart in orphans)
        {
            File.Delete(Utility.BuildChartPath(_folder.Path, airfield, chart));
            File.Delete(Utility.BuildChartPreviewPath(_folder.Path, airfield, chart, "png"));
            _database.Charts.Remove(chart);
        }
        return orphans.Count;
    }

    private void UpdateTripKit(AirfieldTask task)
    {
        Chart? tripKit = TripKitBuilder.Update(_folder, _database, task.Airfield);
        if (tripKit == null)
        {
            return;
        }
        _updatedCharts.Add(new UpdatedChart(tripKit.Name, Utility.BuildChartPath(_folder.Path, task.Airfield, tripKit)));
        if (!task.IsNew && tripKit.LastUpdate is DateOnly date)
        {
            _database.AddUpdate(date);
        }
    }

    private void CompleteAirfield(AirfieldTask task)
    {
        task.Status = AirfieldTaskStatus.Done;

        if (Mode == ImportMode.AddAirfield)
        {
            // Wie ChartButlerCS: Das Datum der Ausgabe nur eintragen, wenn es noch keins gibt
            if (_database.AipLastUpdate == null && _database.Charts.Count > 0)
            {
                MarkDatabaseCurrent();
            }
            _database.DataSource = "DFS";
            return;
        }

        if (_airfields.All(t => t.Status == AirfieldTaskStatus.Done))
        {
            MarkDatabaseCurrent();
            State = ImportSessionState.Completed;
        }
    }

    private void MarkDatabaseCurrent()
    {
        _database.AipLastUpdate = Effective;
        _database.Version = _programVersion;
        _database.DataSource = "DFS";
    }

    /// <summary>Absoluter href ("komplett") oder aus Effective-Datum und Hash gebildet ("nur HTML", IMPORT-MODUS 4, Frage 5).</summary>
    private static Uri ChartLink(DfsChartEntry entry, DateOnly effective) =>
        entry.Href.StartsWith(DfsUrls.BaseUrl, StringComparison.OrdinalIgnoreCase) && Uri.TryCreate(entry.Href, UriKind.Absolute, out Uri? link)
            ? link
            : DfsUrls.ChartPage(effective, entry.Hash);

    private static string Format(DateOnly date) => DfsPageParser.FormatDate(date);
}
