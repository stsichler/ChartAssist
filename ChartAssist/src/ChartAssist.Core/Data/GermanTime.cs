namespace ChartAssist.Core.Data;

/// <summary>Deutsche Zeitzone für die Datumswerte der Karten-Datenbank.</summary>
internal static class GermanTime
{
    public static TimeZoneInfo Zone { get; } = Find();

    private static TimeZoneInfo Find()
    {
        // IANA-Name unter Linux und macOS; unter Windows ohne ICU (InvariantGlobalization)
        // funktioniert nur der Windows-Name
        foreach (string id in new[] { "Europe/Berlin", "W. Europe Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
            }
        }
        return TimeZoneInfo.Local;
    }
}
