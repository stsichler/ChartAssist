using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChartAssist.Core;

/// <summary>
/// Benutzereinstellungen, gespeichert als JSON unter <see cref="Environment.SpecialFolder.ApplicationData"/>.
/// Wird in Phase 2 um Kartenverzeichnis, Import-Ordner usw. erweitert (TECHNISCHE-BASIS 4.4, 10).
/// </summary>
public sealed class Settings
{
    /// <summary>Unter Linux wurde bereits gefragt, ob Startmenü-Eintrag und Desktop-Verknüpfung angelegt werden sollen.</summary>
    public bool DesktopIntegrationOffered { get; set; }

    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ChartAssist", "Settings.json");

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

    /// <summary>Speichert über eine Temp-Datei im selben Verzeichnis, damit die Datei nie halb geschrieben ist.</summary>
    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string tempPath = path + ".tmp";
        using (FileStream stream = File.Create(tempPath))
        {
            JsonSerializer.Serialize(stream, this, SettingsJsonContext.Default.Settings);
        }
        File.Move(tempPath, path, overwrite: true);
    }
}

// Source-Generator statt Reflection, damit Trimming möglich bleibt (TECHNISCHE-BASIS 8)
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Settings))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext
{
}
