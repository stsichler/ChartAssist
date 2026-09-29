using System.Diagnostics;
using ChartAssist.Core.Data;

namespace ChartAssist.Core;

/// <summary>
/// Das Kartenverzeichnis mit seiner Datenbank <c>.ChartButler.xml</c>: laden, speichern und bei Bedarf
/// aus den vorhandenen Dateien wiederherstellen (bisher <c>readDataBase</c>, <c>updateDataBase</c> und
/// <c>rebuildDataBaseFromChartDir</c> in ChartButlerCS). Alle Pfade werden aus <see cref="Path"/> gebildet,
/// das aktuelle Verzeichnis des Prozesses bleibt unberührt (TECHNISCHE-BASIS 4.4).
/// </summary>
public sealed class ChartFolder(string path)
{
    public string Path { get; } = path;

    public string DatabasePath => System.IO.Path.Combine(Path, ChartDatabaseXml.FileName);

    /// <summary>
    /// Lädt die Datenbank. Fehlt sie, ist sie defekt oder enthält sie keine Flugplätze,
    /// wird sie aus den Flugplatzverzeichnissen wiederhergestellt.
    /// </summary>
    public ChartFolderLoadResult Load()
    {
        ChartDatabase? database = null;
        bool readFailed = false;
        if (File.Exists(DatabasePath))
        {
            try
            {
                database = ChartDatabaseXml.Read(DatabasePath);
            }
            catch (InvalidDataException e)
            {
                Debug.WriteLine("Karten-Datenbank defekt: " + e.Message);
                readFailed = true;
            }
        }

        if (database == null || database.Airfields.Count == 0)
        {
            ChartDatabase rebuilt = Rebuild(out bool previewsMissing);
            return new ChartFolderLoadResult(rebuilt, readFailed, Rebuilt: true, previewsMissing);
        }
        return new ChartFolderLoadResult(database, readFailed, Rebuilt: false, PreviewsMissing: false);
    }

    /// <summary>
    /// Baut die Datenbank aus den Verzeichnissen "ICAO - Name" auf. Karten sind die PNGs und das
    /// TripKit-PDF. Ohne Vorschaudatei lässt sich die Aktualität einer Karte nicht prüfen, dann ist
    /// <paramref name="previewsMissing"/> true.
    /// </summary>
    public ChartDatabase Rebuild(out bool previewsMissing)
    {
        var database = new ChartDatabase();
        previewsMissing = false;

        foreach (string directory in SortedEntries(Directory.GetDirectories(Path)))
        {
            string directoryName = System.IO.Path.GetFileName(directory);
            if (!Utility.IsAirfieldDirectoryName(directoryName))
            {
                continue;
            }

            var airfield = new Airfield { Icao = directoryName[..4], Name = directoryName[7..] };
            if (database.FindAirfield(airfield.Icao) != null)
            {
                Debug.WriteLine("Flugplatz doppelt, Verzeichnis ignoriert: " + directoryName);
                continue;
            }
            database.Airfields.Add(airfield);

            foreach (string file in SortedEntries(Directory.GetFiles(directory)))
            {
                string fileName = System.IO.Path.GetFileName(file);
                string extension = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
                string? previewExtension = extension switch
                {
                    ".pdf" => "jpg",
                    ".png" when !fileName.EndsWith("_preview.png", StringComparison.Ordinal) => "png",
                    _ => null,
                };
                if (previewExtension == null || database.FindChart(fileName) != null)
                {
                    continue;
                }

                var chart = new Chart
                {
                    Icao = airfield.Icao,
                    Name = fileName,
                    CreationDate = DateOnly.FromDateTime(File.GetLastWriteTime(file)),
                };
                database.Charts.Add(chart);
                if (!File.Exists(Utility.BuildChartPreviewPath(Path, airfield, chart, previewExtension)))
                {
                    previewsMissing = true;
                }
            }
        }
        return database;
    }

    /// <summary>
    /// Speichert die Datenbank, aber nur, wenn sich der Inhalt geändert hat. Ohne Karten wird die Datei
    /// gelöscht. <see cref="ChartDatabase.Version"/> und <see cref="ChartDatabase.DataSource"/> setzt der Aufrufer.
    /// </summary>
    /// <returns>true, wenn die Datei geschrieben oder gelöscht wurde.</returns>
    public bool Save(ChartDatabase database)
    {
        if (database.Charts.Count == 0)
        {
            if (!File.Exists(DatabasePath))
            {
                return false;
            }
            File.Delete(DatabasePath);
            return true;
        }

        byte[] content = ChartDatabaseXml.ToBytes(database);
        if (Utility.FileEquals(DatabasePath, content))
        {
            return false;
        }

        Utility.WriteFileAtomic(DatabasePath, content, hidden: true);
        return true;
    }

    // Reihenfolge der Einträge ist je nach Dateisystem verschieden, sortiert ist das Ergebnis reproduzierbar
    private static string[] SortedEntries(string[] entries)
    {
        Array.Sort(entries, StringComparer.Ordinal);
        return entries;
    }
}

/// <param name="Database">Die geladene oder wiederhergestellte Datenbank.</param>
/// <param name="ReadFailed">Die Datenbankdatei war vorhanden, aber defekt.</param>
/// <param name="Rebuilt">Die Datenbank wurde aus den Verzeichnissen wiederhergestellt.</param>
/// <param name="PreviewsMissing">Beim Wiederherstellen fehlten Vorschaudateien; der nächste Abgleich fordert diese Karten neu an.</param>
public sealed record ChartFolderLoadResult(ChartDatabase Database, bool ReadFailed, bool Rebuilt, bool PreviewsMissing);
