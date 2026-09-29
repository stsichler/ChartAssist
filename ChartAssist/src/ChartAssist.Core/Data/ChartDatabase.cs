namespace ChartAssist.Core.Data;

/// <summary>
/// Inhalt von <c>.ChartButler.xml</c> (TECHNISCHE-BASIS 4.3). Alle Listen behalten die Reihenfolge
/// der Datei, damit Lesen und Schreiben byteweise dieselbe Datei ergeben.
/// </summary>
public sealed class ChartDatabase
{
    /// <summary>Höchstzahl der gemerkten Aktualisierungen, wie in ChartButlerCS.</summary>
    public const int MaxUpdates = 5;

    public List<Airfield> Airfields { get; } = [];

    /// <summary>
    /// Alle Karten, flach und in Einfügereihenfolge (Tabelle AFCharts). Karten verschiedener
    /// Flugplätze können sich abwechseln, die Zuordnung erfolgt über <see cref="Chart.Icao"/>.
    /// </summary>
    public List<Chart> Charts { get; } = [];

    /// <summary>Effective-Datum des letzten vollständigen Abgleichs (Tabelle AIP).</summary>
    public DateOnly? AipLastUpdate { get; set; }

    /// <summary>Die letzten Aktualisierungen, älteste zuerst (Tabelle Updates).</summary>
    public List<DateOnly> Updates { get; } = [];

    /// <summary>Programmversion, die die Datei zuletzt geschrieben hat (Tabelle ChartButler).</summary>
    public string? Version { get; set; }

    /// <summary>"DFS"; null oder "GAT24" kennzeichnen Altbestände (Tabelle ChartButler).</summary>
    public string? DataSource { get; set; }

    public Airfield? FindAirfield(string icao) => Airfields.Find(a => a.Icao == icao);

    public Chart? FindChart(string name) => Charts.Find(c => c.Name == name);

    public IEnumerable<Chart> ChartsOf(Airfield airfield) => Charts.Where(c => c.Icao == airfield.Icao);

    /// <summary>Trägt eine Aktualisierung ein und vergisst die älteste, wenn es mehr als <see cref="MaxUpdates"/> sind.</summary>
    public void AddUpdate(DateOnly date)
    {
        if (Updates.Contains(date))
        {
            return;
        }
        Updates.Add(date);
        while (Updates.Count > MaxUpdates)
        {
            Updates.RemoveAt(0);
        }
    }

    /// <summary>Entfernt den Flugplatz samt seiner Karten aus der Datenbank (nicht aus dem Verzeichnis).</summary>
    public void RemoveAirfield(Airfield airfield)
    {
        Charts.RemoveAll(c => c.Icao == airfield.Icao);
        Airfields.Remove(airfield);
    }
}

public sealed class Airfield
{
    public string Icao { get; set; } = "";

    /// <summary>Name wie im Verzeichnisnamen "ICAO - Name" (AFname).</summary>
    public string Name { get; set; } = "";

    public DateOnly? LastUpdate { get; set; }
}

public sealed class Chart
{
    public string Icao { get; set; } = "";

    /// <summary>Dateiname der Karte, global eindeutig (Cname), z. B. "EDFM Mannheim City 1.png".</summary>
    public string Name { get; set; } = "";

    public DateOnly CreationDate { get; set; }

    public DateOnly? LastUpdate { get; set; }

    /// <summary>Permalink der Flugplatzseite, Teil von Crypt vor '#', z. B. "C01A45.html".</summary>
    public string? AirfieldPermalink { get; set; }

    /// <summary>Kartenname der DFS, Teil von Crypt nach '#'.</summary>
    public string? ServerName { get; set; }

    /// <summary>Das TripKit ist ebenfalls als Karte eingetragen, aber ohne Crypt.</summary>
    public bool IsTripKit => AirfieldPermalink == null;
}
