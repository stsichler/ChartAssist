namespace ChartAssist.Core.Import;

/// <summary>
/// Findet fertig gespeicherte Seiten im Import-Ordner (IMPORT-MODUS 5.3). Die UI ruft <see cref="Scan"/> etwa
/// alle 1–2 Sekunden auf, solange das Abgleichfenster offen ist. Polling statt FileSystemWatcher: einfach und
/// auf allen Plattformen zuverlässig.
/// </summary>
public sealed class ImportFolderScanner(string folder)
{
    /// <summary>Unterordner für Dateien, die nicht verarbeitet werden konnten.</summary>
    public const string ErrorFolderName = "Fehler";

    private Dictionary<string, long> _lastSizes = [];

    public string Folder { get; } = folder;

    /// <summary>
    /// Liefert die HTML-Dateien, die fertig geschrieben sind, älteste zuerst. Fertig ist eine Datei, wenn ihre
    /// Größe seit der letzten Abfrage gleich geblieben ist. Temporäre Dateien der Browser (<c>*.part</c>,
    /// <c>*.crdownload</c>, <c>*.tmp</c>) haben eine andere Endung und werden so ohnehin übergangen.
    /// </summary>
    public IReadOnlyList<string> Scan()
    {
        Directory.CreateDirectory(Folder);

        var sizes = new Dictionary<string, long>();
        var ready = new List<FileInfo>();
        foreach (FileInfo file in new DirectoryInfo(Folder).EnumerateFiles())
        {
            if (!IsHtml(file.Name))
            {
                continue;
            }
            sizes[file.FullName] = file.Length;
            if (file.Length > 0 && _lastSizes.TryGetValue(file.FullName, out long lastSize) && lastSize == file.Length)
            {
                ready.Add(file);
            }
        }
        _lastSizes = sizes;

        return ready.OrderBy(f => f.LastWriteTimeUtc).ThenBy(f => f.Name, StringComparer.Ordinal).Select(f => f.FullName).ToList();
    }

    /// <summary>
    /// Löscht eine verarbeitete Datei samt Ressourcenordner (<c>&lt;Name&gt;_files</c> bei "Webseite, komplett"),
    /// damit keine DFS-Seiten liegen bleiben (IMPORT-MODUS 2, Leitplanke 5).
    /// </summary>
    public void Remove(string file)
    {
        File.Delete(file);
        DeleteResourceFolder(file);
        _lastSizes.Remove(file);
    }

    /// <summary>Verschiebt eine Datei, die nicht verarbeitet werden konnte, nach <c>Fehler/</c>.</summary>
    /// <returns>Der neue Pfad.</returns>
    public string MoveToErrorFolder(string file)
    {
        string errorFolder = Directory.CreateDirectory(Path.Combine(Folder, ErrorFolderName)).FullName;
        string name = Path.GetFileNameWithoutExtension(file);
        string extension = Path.GetExtension(file);
        string target = Path.Combine(errorFolder, name + extension);
        for (int i = 1; File.Exists(target); i++)
        {
            target = Path.Combine(errorFolder, $"{name} ({i}){extension}");
        }
        File.Move(file, target);
        DeleteResourceFolder(file);
        _lastSizes.Remove(file);
        return target;
    }

    /// <summary>
    /// Wurde die Seite als "Webseite, komplett" gespeichert? Erkennbar am Ressourcenordner. Dann ist sie das vom
    /// Browser dargestellte DOM, das Erweiterungen verändert haben können; "nur HTML" ist der Originalquelltext.
    /// </summary>
    public static bool IsSavedComplete(string file) => Directory.Exists(ResourceFolder(file));

    private static bool IsHtml(string name) =>
        name.EndsWith(".html", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".htm", StringComparison.OrdinalIgnoreCase);

    private static string ResourceFolder(string file) =>
        Path.Combine(Path.GetDirectoryName(file)!, Path.GetFileNameWithoutExtension(file) + "_files");

    private static void DeleteResourceFolder(string file)
    {
        string resources = ResourceFolder(file);
        if (Directory.Exists(resources))
        {
            Directory.Delete(resources, recursive: true);
        }
    }
}
