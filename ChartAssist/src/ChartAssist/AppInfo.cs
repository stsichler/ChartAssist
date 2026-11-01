namespace ChartAssist;

internal static class AppInfo
{
    /// <summary>Vierstellige Programmversion; landet auch in der Karten-Datenbank.</summary>
    public static Version Version { get; } = typeof(AppInfo).Assembly.GetName().Version ?? new Version(0, 0, 0, 0);

    public static string VersionText => Version.ToString();

    /// <summary>
    /// Kennung für das Feld <c>ChartButler.Version</c> in <c>.ChartButler.xml</c>, z. B. "ChartAssist 1.0.0.0".
    /// Weicht sie ab, erzwingen ChartAssist und ChartButlerCS einen vollständigen Abgleich. Der Programmname
    /// stellt sicher, dass beide Programme die Datenbank des jeweils anderen erkennen, auch bei gleicher Nummer.
    /// </summary>
    public static string DatabaseVersion => "ChartAssist " + VersionText;

    /// <summary>Version des rechtlichen Hinweises; eine neue Version wird beim Start erneut angezeigt.</summary>
    public const string LegalNoticeVersion = "v1";

    /// <summary>Webseite für Anwender (GitHub Pages, die README im Git-Root) mit den Downloads.</summary>
    public const string WebsiteUrl = "https://stsichler.github.io/ChartAssist/";
}
