namespace ChartAssist.Core.Dfs;

/// <summary>
/// Adressen der BasicVFR AIP. Sie dienen ausschließlich als Links, die der Benutzer
/// einzeln im Browser öffnet (IMPORT-MODUS 2, Leitplanken 1 und 2).
/// ChartAssist ruft diese Adressen nie selbst ab.
/// </summary>
public static class DfsUrls
{
    public const string BaseUrl = "https://aip.dfs.de/BasicVFR/";
    public const string PermalinkBaseUrl = "https://aip.dfs.de/BasicVFR/pages/";
    public const string PermalinkMain = "C00001.html";
    public const string PermalinkAirfields = "C0004A.html";

    /// <summary>Startseite der BasicVFR.</summary>
    public static Uri MainPage { get; } = new(PermalinkBaseUrl + PermalinkMain);

    /// <summary>Flugplatzverzeichnis, Ausgangspunkt für "Neuer Flugplatz" (IMPORT-MODUS 3.3).</summary>
    public static Uri AirfieldDirectory { get; } = new(PermalinkBaseUrl + PermalinkAirfields);

    /// <summary>Flugplatzseite über ihren Permalink, wie er in <c>AFCharts.Crypt</c> steht (z. B. "C01A45.html").</summary>
    public static Uri AirfieldPage(string permalink) => new(PermalinkBaseUrl + permalink);
}
