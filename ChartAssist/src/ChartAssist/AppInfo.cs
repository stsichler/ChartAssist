namespace ChartAssist;

internal static class AppInfo
{
    /// <summary>Vierstellige Programmversion; landet auch in der Karten-Datenbank.</summary>
    public static Version Version { get; } = typeof(AppInfo).Assembly.GetName().Version ?? new Version(0, 0, 0, 0);

    public static string VersionText => Version.ToString();

    /// <summary>Version des rechtlichen Hinweises; eine neue Version wird beim Start erneut angezeigt.</summary>
    public const string LegalNoticeVersion = "v1";

    public const string ProjectUrl = "https://github.com/stsichler/ChartAssist";
}
