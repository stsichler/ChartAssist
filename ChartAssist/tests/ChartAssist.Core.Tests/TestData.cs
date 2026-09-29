namespace ChartAssist.Core.Tests;

/// <summary>
/// Zugriff auf Testdaten. Die echten DFS-Daten in <c>testdata/</c> und <c>testcharts/</c> liegen nur lokal
/// (nicht im Repository, §11 der DFS-Nutzungsbedingungen); Tests damit werden sonst übersprungen.
/// </summary>
internal static class TestData
{
    public static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    /// <summary>Pfad zu <c>testcharts/</c> bzw. <c>testdata/</c>; überspringt den Test, wenn der Ordner fehlt.</summary>
    public static string RequireLocal(string folder)
    {
        string? path = FindLocal(folder);
        Assert.SkipWhen(path == null, $"Lokale Testdaten '{folder}/' nicht vorhanden");
        return path!;
    }

    /// <summary>Kopiert ein Verzeichnis, damit Tests die echten Testdaten nicht verändern.</summary>
    public static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (string file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        }
        foreach (string directory in Directory.GetDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
        }
    }

    private static string? FindLocal(string folder)
    {
        // Vom Ausgabeverzeichnis aufwärts bis zum Verzeichnis mit der Solution
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ChartAssist.slnx")))
            {
                string path = Path.Combine(dir.FullName, folder);
                return Directory.Exists(path) ? path : null;
            }
        }
        return null;
    }
}
