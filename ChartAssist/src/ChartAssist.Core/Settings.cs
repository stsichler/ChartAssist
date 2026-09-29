using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;
using System.Xml.Linq;

namespace ChartAssist.Core;

/// <summary>
/// Benutzereinstellungen, gespeichert als JSON unter <see cref="Environment.SpecialFolder.ApplicationData"/>
/// (Linux und macOS: <c>~/.config</c>, Windows: <c>%APPDATA%</c>).
/// </summary>
public sealed class Settings
{
    /// <summary>Kartenverzeichnis, leer, solange keines gewählt ist.</summary>
    public string ChartFolder { get; set; } = "";

    /// <summary>Import-Ordner für gespeicherte DFS-Seiten; leer bedeutet <c>&lt;Kartenverzeichnis&gt;/Import</c> (IMPORT-MODUS 5.3).</summary>
    public string ImportFolder { get; set; } = "";

    /// <summary>Version des rechtlichen Hinweises, die der Benutzer bestätigt hat.</summary>
    public string LegalNoticeAccepted { get; set; } = "";

    /// <summary>Unter Linux wurde bereits gefragt, ob Startmenü-Eintrag und Desktop-Verknüpfung angelegt werden sollen.</summary>
    public bool DesktopIntegrationOffered { get; set; }

    public static string DefaultPath { get; } = Path.Combine(ApplicationData, "ChartAssist", "Settings.json");

    /// <summary>Einstellungsdatei von ChartButlerCS, aus der beim ersten Start das Kartenverzeichnis übernommen wird.</summary>
    public static string ChartButlerCSPath { get; } = Path.Combine(ApplicationData, "ChartButlerCS.config");

    private static string ApplicationData => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    /// <summary>Import-Ordner, der tatsächlich gilt.</summary>
    public string EffectiveImportFolder =>
        ImportFolder.Length > 0 || ChartFolder.Length == 0 ? ImportFolder : Path.Combine(ChartFolder, "Import");

    /// <summary>Lädt die Einstellungen. Fehlt die Datei oder ist sie defekt, gelten die Standardwerte.</summary>
    public static Settings Load(string path)
    {
        try
        {
            using FileStream stream = File.OpenRead(path);
            return JsonSerializer.Deserialize(stream, SettingsJsonContext.Default.Settings) ?? new Settings();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return new Settings();
        }
    }

    /// <summary>
    /// Lädt die Einstellungen. Gibt es noch keine, wird das Kartenverzeichnis aus ChartButlerCS übernommen,
    /// falls vorhanden (TECHNISCHE-BASIS 10). Den rechtlichen Hinweis bestätigt der Benutzer neu.
    /// </summary>
    public static Settings LoadOrImport(string path, string chartButlerCSPath)
    {
        if (File.Exists(path))
        {
            return Load(path);
        }
        return new Settings { ChartFolder = ReadChartButlerCSChartFolder(chartButlerCSPath) ?? "" };
    }

    /// <summary>
    /// Liest <c>ChartFolder</c> aus <c>ChartButlerCS.config</c>. Das Format ist
    /// <c>&lt;configuration&gt;&lt;setting name="…"&gt;&lt;value&gt;…&lt;/value&gt;</c>, mit oder ohne BOM,
    /// ein leerer Wert kann Zeilenumbruch und Einrückung enthalten.
    /// </summary>
    public static string? ReadChartButlerCSChartFolder(string path)
    {
        try
        {
            XElement root = XDocument.Load(path).Root!;
            string? value = root.Elements("setting")
                .FirstOrDefault(s => (string?)s.Attribute("name") == "ChartFolder")?
                .Element("value")?.Value.Trim();
            return string.IsNullOrEmpty(value) ? null : value;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or XmlException)
        {
            return null;
        }
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string json = JsonSerializer.Serialize(this, SettingsJsonContext.Default.Settings);
        Utility.WriteFileAtomic(path, Encoding.UTF8.GetBytes(json));
    }
}

// Source-Generator statt Reflection, damit Trimming möglich bleibt (TECHNISCHE-BASIS 8)
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Settings))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext
{
}
