using System.Runtime.Versioning;
using ChartAssist.Core.Data;

namespace ChartAssist.Core.Tests;

public sealed class ChartFolderTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ChartAssistTest").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Load_LiestVorhandeneDatenbank()
    {
        File.Copy(TestData.FixturePath("ChartButler.xml"), Path.Combine(_root, ChartDatabaseXml.FileName));

        ChartFolderLoadResult result = new ChartFolder(_root).Load();

        Assert.False(result.Rebuilt);
        Assert.False(result.ReadFailed);
        Assert.Equal(6, result.Database.Charts.Count);
    }

    [Fact]
    public void Load_DefekteDatenbank_StelltAusVerzeichnisWiederHer()
    {
        CreateAirfield("EDXA - Musterstadt", "EDXA Musterstadt 1.png");
        File.WriteAllText(Path.Combine(_root, ChartDatabaseXml.FileName), "<kaputt");

        ChartFolderLoadResult result = new ChartFolder(_root).Load();

        Assert.True(result.ReadFailed);
        Assert.True(result.Rebuilt);
        Assert.Equal("EDXA Musterstadt 1.png", Assert.Single(result.Database.Charts).Name);
    }

    [Fact]
    public void Rebuild_ErkenntKartenTripKitUndFehlendeVorschauen()
    {
        CreateAirfield("EDXA - Musterstadt", "EDXA Musterstadt 1.png", ".EDXA Musterstadt 1.png_preview.png",
            "EDXA_TripKit_Charts.pdf", ".EDXA_TripKit_Charts.pdf_preview.jpg", "notiz.txt");
        CreateAirfield("EDXB - Beispielfeld", "EDXB AD 2-1.png");
        CreateAirfield("Import", "AIP VFR Germany.html");
        CreateAirfield("Fehler", "x.png");

        ChartDatabase database = new ChartFolder(_root).Rebuild(out bool previewsMissing);

        Assert.Equal(["EDXA", "EDXB"], database.Airfields.Select(a => a.Icao));
        Assert.Equal("Musterstadt", database.Airfields[0].Name);
        Assert.Equal(["EDXA Musterstadt 1.png", "EDXA_TripKit_Charts.pdf", "EDXB AD 2-1.png"], database.Charts.Select(c => c.Name));
        Assert.True(previewsMissing); // für EDXB AD 2-1.png
    }

    [Fact]
    public void Rebuild_OhneFehlendeVorschauen()
    {
        CreateAirfield("EDXA - Musterstadt", "EDXA Musterstadt 1.png", ".EDXA Musterstadt 1.png_preview.png");

        new ChartFolder(_root).Rebuild(out bool previewsMissing);

        Assert.False(previewsMissing);
    }

    [Fact]
    public void Save_SchreibtNurBeiAenderung()
    {
        var folder = new ChartFolder(_root);
        ChartDatabase database = ChartDatabaseXml.Read(TestData.FixturePath("ChartButler.xml"));

        Assert.True(folder.Save(database));
        Assert.False(folder.Save(database));

        database.Version = "1.0.0.0";
        Assert.True(folder.Save(database));
        Assert.Equal(ChartDatabaseXml.ToBytes(database), File.ReadAllBytes(folder.DatabasePath));
        Assert.Single(Directory.GetFileSystemEntries(_root));
    }

    [Fact]
    public void Save_OhneKarten_LoeschtDatenbank()
    {
        var folder = new ChartFolder(_root);
        ChartDatabase database = ChartDatabaseXml.Read(TestData.FixturePath("ChartButler.xml"));
        folder.Save(database);

        database.Charts.Clear();

        Assert.True(folder.Save(database));
        Assert.False(File.Exists(folder.DatabasePath));
    }

    [Fact]
    [SupportedOSPlatform("linux")]
    public void Save_VerwendetNormaleDateirechte()
    {
        // ChartButlerCS hat über Path.GetTempFileName() Dateien mit 0600 hinterlassen (TECHNISCHE-BASIS 10)
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Nur unter Linux");
        var folder = new ChartFolder(_root);

        folder.Save(ChartDatabaseXml.Read(TestData.FixturePath("ChartButler.xml")));

        UnixFileMode mode = File.GetUnixFileMode(folder.DatabasePath);
        Assert.True(mode.HasFlag(UnixFileMode.GroupRead) || mode.HasFlag(UnixFileMode.OtherRead), mode.ToString());
    }

    [Fact]
    public void EchtesKartenverzeichnis_LadenUndWiederherstellen()
    {
        string copy = Path.Combine(_root, "testcharts");
        TestData.CopyDirectory(TestData.RequireLocal("testcharts"), copy);
        var folder = new ChartFolder(copy);

        ChartDatabase loaded = folder.Load().Database;
        ChartDatabase rebuilt = folder.Rebuild(out bool previewsMissing);

        Assert.False(previewsMissing);
        Assert.Equal(loaded.Airfields.Select(a => a.Icao + " - " + a.Name), rebuilt.Airfields.Select(a => a.Icao + " - " + a.Name));
        Assert.Equal(loaded.Charts.Select(c => c.Name).Order(StringComparer.Ordinal), rebuilt.Charts.Select(c => c.Name));
        Assert.False(folder.Save(loaded)); // unverändert geladen: Datei bleibt unberührt
    }

    private void CreateAirfield(string directory, params string[] files)
    {
        string path = Directory.CreateDirectory(Path.Combine(_root, directory)).FullName;
        foreach (string file in files)
        {
            File.WriteAllBytes(Path.Combine(path, file), [1, 2, 3]);
        }
    }
}
