using ChartAssist.Core.Import;

namespace ChartAssist.Core.Tests.Import;

public sealed class ImportFolderScannerTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ChartAssistTest").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Scan_LiefertDateiErstWennGroesseStabilIst()
    {
        var scanner = new ImportFolderScanner(Path.Combine(_root, "Import"));
        string file = Path.Combine(scanner.Folder, "AIP VFR Germany.html");

        Assert.Empty(scanner.Scan()); // legt den Ordner an
        File.WriteAllText(file, "<html>");
        Assert.Empty(scanner.Scan());

        File.AppendAllText(file, "<body>"); // wird noch geschrieben
        Assert.Empty(scanner.Scan());

        Assert.Equal([file], scanner.Scan());
    }

    [Fact]
    public void Scan_IgnoriertTemporaereUndFremdeDateien()
    {
        var scanner = new ImportFolderScanner(_root);
        foreach (string name in new[] { "a.html.part", "b.crdownload", "c.tmp", "d.txt", "leer.html" })
        {
            File.WriteAllText(Path.Combine(_root, name), name == "leer.html" ? "" : "x");
        }
        Directory.CreateDirectory(Path.Combine(_root, ImportFolderScanner.ErrorFolderName));
        File.WriteAllText(Path.Combine(_root, ImportFolderScanner.ErrorFolderName, "alt.html"), "x");

        scanner.Scan();
        Assert.Empty(scanner.Scan());
    }

    [Fact]
    public void Scan_AeltesteDateiZuerst()
    {
        var scanner = new ImportFolderScanner(_root);
        string newer = Path.Combine(_root, "AIP VFR Germany.html");
        string older = Path.Combine(_root, "AIP VFR Germany(1).htm");
        File.WriteAllText(newer, "x");
        File.WriteAllText(older, "x");
        File.SetLastWriteTimeUtc(newer, new DateTime(2026, 9, 29, 10, 0, 5, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(older, new DateTime(2026, 9, 29, 10, 0, 1, DateTimeKind.Utc));

        scanner.Scan();
        Assert.Equal([older, newer], scanner.Scan());
    }

    [Fact]
    public void Remove_LoeschtDateiUndRessourcenordner()
    {
        var scanner = new ImportFolderScanner(_root);
        string file = Path.Combine(_root, "AIP VFR Germany.html");
        File.WriteAllText(file, "x");
        Directory.CreateDirectory(Path.Combine(_root, "AIP VFR Germany_files"));
        File.WriteAllText(Path.Combine(_root, "AIP VFR Germany_files", "logo.svg"), "x");
        Assert.True(ImportFolderScanner.IsSavedComplete(file));

        scanner.Remove(file);

        Assert.Empty(Directory.GetFileSystemEntries(_root));
    }

    [Fact]
    public void MoveToErrorFolder_VergibtEindeutigeNamen()
    {
        var scanner = new ImportFolderScanner(_root);
        string file = Path.Combine(_root, "AIP VFR Germany.html");

        File.WriteAllText(file, "1");
        string first = scanner.MoveToErrorFolder(file);
        File.WriteAllText(file, "2");
        string second = scanner.MoveToErrorFolder(file);

        Assert.Equal("AIP VFR Germany.html", Path.GetFileName(first));
        Assert.Equal("AIP VFR Germany (1).html", Path.GetFileName(second));
        Assert.Equal("2", File.ReadAllText(second));
        Assert.False(File.Exists(file));
        Assert.False(ImportFolderScanner.IsSavedComplete(file));
    }
}
