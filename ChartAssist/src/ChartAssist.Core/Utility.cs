using System.Text;
using System.Text.RegularExpressions;
using ChartAssist.Core.Data;

namespace ChartAssist.Core;

/// <summary>Pfade und Dateinamen im Kartenverzeichnis, übernommen aus ChartButlerCS (TECHNISCHE-BASIS 10).</summary>
public static partial class Utility
{
    /// <summary>Verzeichnisname eines Flugplatzes, z. B. "EDFM - Mannheim City".</summary>
    public static string AirfieldDirectoryName(Airfield airfield) => airfield.Icao + " - " + airfield.Name;

    /// <summary>Verzeichnisnamen von Flugplätzen. Alles andere, z. B. der Import-Ordner, wird ignoriert.</summary>
    public static bool IsAirfieldDirectoryName(string name) => AirfieldDirectoryRegex().IsMatch(name);

    public static string BuildChartPath(string chartFolder, Airfield airfield, Chart chart) =>
        Path.Combine(chartFolder, AirfieldDirectoryName(airfield), chart.Name);

    /// <summary>Versteckte Vorschaudatei, z. B. ".EDFM Mannheim City 1.png_preview.png".</summary>
    public static string BuildChartPreviewPath(string chartFolder, Airfield airfield, Chart chart, string extension) =>
        Path.Combine(chartFolder, AirfieldDirectoryName(airfield), "." + chart.Name + "_preview." + extension);

    /// <summary>
    /// Bildet einen Dateinamen nach den Regeln von Windows, auf allen Plattformen, damit dasselbe
    /// Kartenverzeichnis überall funktioniert. Für Namen, die unter Windows schon gültig waren,
    /// ist das Ergebnis dasselbe wie in ChartButlerCS.
    /// </summary>
    public static string GetFilenameFor(string name)
    {
        var filename = new StringBuilder(name.Length);
        foreach (char c in name)
        {
            if (c is '/' or '\\' or '<' or '>' or ':' or '*' or '?' or '|')
            {
                filename.Append('-');
            }
            else if (c is not ('\'' or '"') && !char.IsControl(c))
            {
                filename.Append(c);
            }
        }

        string result = filename.ToString().Trim().TrimEnd('.', ' ');
        if (ReservedNameRegex().IsMatch(result))
        {
            result = "_" + result;
        }
        return result;
    }

    /// <summary>Binärer Vergleich zweier Dateien. Fehlt eine davon, sind sie verschieden.</summary>
    public static bool FileEquals(string path1, string path2)
    {
        if (!File.Exists(path1) || !File.Exists(path2))
        {
            return false;
        }
        if (new FileInfo(path1).Length != new FileInfo(path2).Length)
        {
            return false;
        }
        return File.ReadAllBytes(path1).AsSpan().SequenceEqual(File.ReadAllBytes(path2));
    }

    /// <summary>Vergleicht eine Datei mit Bytes im Speicher, z. B. eine Vorschau aus einer gespeicherten Seite.</summary>
    public static bool FileEquals(string path, ReadOnlySpan<byte> content) =>
        File.Exists(path) && new FileInfo(path).Length == content.Length && File.ReadAllBytes(path).AsSpan().SequenceEqual(content);

    /// <summary>
    /// Schreibt über eine Temp-Datei im Zielverzeichnis und benennt sie dann um. So ist die Datei nie halb
    /// geschrieben und bekommt die normalen Dateirechte des Benutzers (nicht 0600 wie bei Path.GetTempFileName).
    /// </summary>
    public static void WriteFileAtomic(string path, ReadOnlySpan<byte> content, bool hidden = false)
    {
        string directory = Path.GetDirectoryName(path)!;
        string tempPath = Path.Combine(directory, "." + Path.GetFileName(path) + ".tmp");
        try
        {
            using (FileStream stream = File.Create(tempPath))
            {
                stream.Write(content);
            }
            if (hidden && OperatingSystem.IsWindows())
            {
                File.SetAttributes(tempPath, File.GetAttributes(tempPath) | FileAttributes.Hidden);
            }
            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [GeneratedRegex("^[A-Z0-9]{4} - .")]
    private static partial Regex AirfieldDirectoryRegex();

    // Unter Windows reservierte Gerätenamen, auch mit Endung ("CON.png")
    [GeneratedRegex(@"^(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])(\..*)?$", RegexOptions.IgnoreCase)]
    private static partial Regex ReservedNameRegex();
}
