using ChartAssist.Core.Data;
using ChartAssist.Core.Dfs;
using ChartAssist.Core.Import;
using ChartAssist.Core.Tests.Dfs;
using ChartAssist.Core.TripKit;
using SkiaSharp;

namespace ChartAssist.Core.Tests.Import;

/// <summary>Abgleich-Sitzungen in einem temporären Kartenverzeichnis (TECHNISCHE-BASIS 11, Phase 3).</summary>
public sealed class ChartImportTests : IDisposable
{
    private const string Version = "1.0.0.0";
    private const string Permalink = "C0AAA1.html";

    private static readonly DateOnly LastEdition = new(2026, 8, 20);
    private static readonly DateOnly NewEdition = new(2026, 9, 17);

    private static readonly byte[] Preview1 = DfsTestPages.Png(SKColors.Red);
    private static readonly byte[] Preview2 = DfsTestPages.Png(SKColors.Green);
    private static readonly byte[] Chart1 = DfsTestPages.Png(SKColors.Red, 148, 210);
    private static readonly byte[] Chart2 = DfsTestPages.Png(SKColors.Green, 148, 210);

    private readonly string _root = Directory.CreateTempSubdirectory("ChartAssistTest").FullName;
    private readonly ChartFolder _folder;
    private readonly ChartDatabase _database = new();
    private readonly Airfield _airfield = new() { Icao = "EDXA", Name = "Musterstadt", LastUpdate = LastEdition };

    public ChartImportTests()
    {
        _folder = new ChartFolder(_root);
        _database.Airfields.Add(_airfield);
        _database.AipLastUpdate = LastEdition;
        _database.Version = Version;
        _database.DataSource = "DFS";
        AddLocalChart("EDXA Musterstadt 1", Chart1, Preview1);
        AddLocalChart("EDXA AD 2-1", Chart2, Preview2);
        TripKitBuilder.Update(_folder, _database, _airfield);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void GleicheAusgabe_KeineAktualisierungNotwendig()
    {
        ChartImport import = StartUpdate();
        string before = Snapshot();

        ImportResult result = import.Import(AirfieldPage(LastEdition, ("EDXA Musterstadt 1", "h1", Preview1), ("AD 2-1", "h2", Preview2)));

        Assert.Equal(ImportSessionState.NothingToDo, import.State);
        Assert.Contains("Keine Aktualisierung notwendig", result.Message);
        Assert.Equal(before, Snapshot());
    }

    [Fact]
    public void AndereProgrammversion_ErzwingtAbgleich()
    {
        _database.Version = "2.0.1.1";
        ChartImport import = StartUpdate();

        import.Import(AirfieldPage(LastEdition, ("EDXA Musterstadt 1", "h1", Preview1), ("AD 2-1", "h2", Preview2)));

        Assert.Equal(ImportSessionState.Completed, import.State);
        Assert.Equal(Version, _database.Version);
    }

    [Fact]
    public void NeueAusgabe_AllesAktuell_AendertKeineDateien()
    {
        ChartImport import = StartUpdate();
        string before = SnapshotOf(_root);

        ImportResult result = import.Import(AirfieldPage(NewEdition, ("EDXA Musterstadt 1", "h1", Preview1), ("AD 2-1", "h2", Preview2)));

        Assert.Equal(ImportOutcome.Processed, result.Outcome);
        Assert.Contains("alle Karten aktuell", result.Message);
        Assert.Equal(AirfieldTaskStatus.Done, import.Airfields.Single().Status);
        Assert.Equal(ImportSessionState.Completed, import.State);
        Assert.Equal(NewEdition, _database.AipLastUpdate);
        Assert.Equal(before, SnapshotOf(_root));
        Assert.Empty(import.UpdatedCharts);
    }

    [Theory]
    [InlineData(SaveFormat.HtmlOnly)]
    [InlineData(SaveFormat.Complete)]
    public void GeaenderteKarte_WirdAngefordertUndUebernommen(SaveFormat format)
    {
        byte[] newPreview = DfsTestPages.Png(SKColors.Orange);
        byte[] newChart = DfsTestPages.Png(SKColors.Orange, 148, 210);
        ChartImport import = StartUpdate();

        import.Import(AirfieldPage(NewEdition, format, ("EDXA Musterstadt 1", "h1", newPreview), ("AD 2-1", "h2", Preview2)));

        PendingChart pending = Assert.Single(import.PendingCharts);
        Assert.Equal("EDXA Musterstadt 1.png", pending.ChartName);
        Assert.Equal("https://aip.dfs.de/BasicVFR/2026SEP17/pages/h1.html", pending.Link.AbsoluteUri);
        Assert.Equal(AirfieldTaskStatus.WaitingForCharts, import.Airfields.Single().Status);
        Assert.Equal(Preview1, File.ReadAllBytes(PreviewPath("EDXA Musterstadt 1.png"))); // Vorschau erst mit der Karte

        ImportResult result = import.Import(DfsTestPages.ChartPage(NewEdition, "H1", "EDXA Musterstadt 1", "10 SEP 2026", newChart, format));

        Assert.Contains("EDXA ist aktuell", result.Message);
        Assert.Equal(newChart, File.ReadAllBytes(ChartPath("EDXA Musterstadt 1.png")));
        Assert.Equal(newPreview, File.ReadAllBytes(PreviewPath("EDXA Musterstadt 1.png")));
        Chart chart = _database.FindChart("EDXA Musterstadt 1.png")!;
        Assert.Equal(new DateOnly(2026, 9, 10), chart.LastUpdate);
        Assert.Equal(new DateOnly(2026, 9, 10), _airfield.LastUpdate);
        Assert.Contains(new DateOnly(2026, 9, 10), _database.Updates);
        Assert.Equal(["EDXA Musterstadt 1.png", "EDXA_TripKit_Charts.pdf"], import.UpdatedCharts.Select(c => c.Name));
        Assert.Equal(new DateOnly(2026, 9, 10), _database.FindChart("EDXA_TripKit_Charts.pdf")!.LastUpdate);
        Assert.Equal(ImportSessionState.Completed, import.State);
        Assert.Equal(NewEdition, _database.AipLastUpdate);
    }

    [Fact]
    public void KarteOhneDatum_BekommtEffectiveDatum()
    {
        ChartImport import = StartUpdate();
        import.Import(AirfieldPage(NewEdition, ("EDXA Musterstadt 1", "h1", Preview2), ("AD 2-1", "h2", Preview2)));

        import.Import(DfsTestPages.ChartPage(NewEdition, "h1", "EDXA Musterstadt 1", null, Chart2, SaveFormat.HtmlOnly));

        Assert.Equal(NewEdition, _database.FindChart("EDXA Musterstadt 1.png")!.LastUpdate);
    }

    [Fact]
    public void FehlendeKartendatei_WirdAngefordert()
    {
        File.Delete(ChartPath("EDXA AD 2-1.png"));
        ChartImport import = StartUpdate();

        import.Import(AirfieldPage(NewEdition, ("EDXA Musterstadt 1", "h1", Preview1), ("AD 2-1", "h2", Preview2)));

        Assert.Equal("EDXA AD 2-1.png", Assert.Single(import.PendingCharts).ChartName);
    }

    [Fact]
    public void EntfalleneKarte_WirdGeloeschtUndTripKitNeuErzeugt()
    {
        ChartImport import = StartUpdate();

        ImportResult result = import.Import(AirfieldPage(NewEdition, ("EDXA Musterstadt 1", "h1", Preview1)));

        Assert.Contains("1 entfallene Karte", result.Message);
        Assert.Null(_database.FindChart("EDXA AD 2-1.png"));
        Assert.False(File.Exists(ChartPath("EDXA AD 2-1.png")));
        Assert.False(File.Exists(PreviewPath("EDXA AD 2-1.png")));
        Assert.Equal(["EDXA_TripKit_Charts.pdf"], import.UpdatedCharts.Select(c => c.Name));
        Assert.Equal(ImportSessionState.Completed, import.State);
    }

    [Fact]
    public void NeueKarte_WirdAngefordertUndHinzugefuegt()
    {
        byte[] preview = DfsTestPages.Png(SKColors.Purple);
        ChartImport import = StartUpdate();

        import.Import(AirfieldPage(NewEdition, ("EDXA Musterstadt 1", "h1", Preview1), ("AD 2-1", "h2", Preview2), ("Musterstadt/2B", "h3", preview)));
        import.Import(DfsTestPages.ChartPage(NewEdition, "h3", "EDXA Musterstadt/2B", "10 SEP 2026", Chart1, SaveFormat.HtmlOnly));

        Chart chart = _database.FindChart("EDXA Musterstadt-2B.png")!;
        Assert.Equal((Permalink, "EDXA Musterstadt/2B"), (chart.AirfieldPermalink, chart.ServerName));
        Assert.Same(chart, _database.Charts[^1]); // neue Karten stehen am Ende, wie bei ChartButlerCS
        Assert.True(File.Exists(ChartPath("EDXA Musterstadt-2B.png")));
    }

    [Fact]
    public void MehrereKarten_ZuordnungUeberHashInBeliebigerReihenfolge()
    {
        byte[] previewA = DfsTestPages.Png(SKColors.Orange);
        byte[] previewB = DfsTestPages.Png(SKColors.Purple);
        ChartImport import = StartUpdate();
        import.Import(AirfieldPage(NewEdition, ("EDXA Musterstadt 1", "h1", previewA), ("AD 2-1", "h2", previewB)));

        import.Import(DfsTestPages.ChartPage(NewEdition, "h2", "EDXA AD 2-1", null, Chart1, SaveFormat.Complete));
        Assert.Equal(AirfieldTaskStatus.WaitingForCharts, import.Airfields.Single().Status);
        import.Import(DfsTestPages.ChartPage(NewEdition, "h1", "EDXA Musterstadt 1", null, Chart2, SaveFormat.HtmlOnly));

        Assert.Equal(Chart1, File.ReadAllBytes(ChartPath("EDXA AD 2-1.png")));
        Assert.Equal(Chart2, File.ReadAllBytes(ChartPath("EDXA Musterstadt 1.png")));
        Assert.Equal(ImportSessionState.Completed, import.State);
        Assert.Empty(import.PendingCharts);
        Assert.Equal(["EDXA AD 2-1.png", "EDXA Musterstadt 1.png"], import.CompletedCharts.Select(c => c.ChartName));
        Assert.Equal([1, 0], import.CompletedCharts.Select(c => c.Position)); // Stelle auf der Flugplatzseite
    }

    [Fact]
    public void KarteOhneAufgabe_WirdUebergangen()
    {
        ChartImport import = StartUpdate();
        import.Import(AirfieldPage(NewEdition, ("EDXA Musterstadt 1", "h1", Preview2), ("AD 2-1", "h2", Preview2)));
        string before = Snapshot();

        ImportResult result = import.Import(DfsTestPages.ChartPage(NewEdition, "h9", "EDXA Irgendwas", null, Chart1, SaveFormat.HtmlOnly));

        Assert.Equal(ImportOutcome.Processed, result.Outcome);
        Assert.Contains("nicht angefordert", result.Message);
        Assert.Equal(before, Snapshot());
    }

    [Fact]
    public void NeueAusgabeWaehrendDerSitzung_BrichtAb()
    {
        ChartImport import = StartUpdate();
        import.Import(AirfieldPage(NewEdition, ("EDXA Musterstadt 1", "h1", Preview2), ("AD 2-1", "h2", Preview2)));

        ImportResult result = import.Import(DfsTestPages.ChartPage(new DateOnly(2026, 10, 15), "h1", "EDXA Musterstadt 1", null, Chart2, SaveFormat.HtmlOnly));

        Assert.Equal(ImportSessionState.Aborted, import.State);
        Assert.Contains("neu starten", result.Message);
        Assert.Equal(Chart1, File.ReadAllBytes(ChartPath("EDXA Musterstadt 1.png")));
        Assert.Equal(LastEdition, _database.AipLastUpdate);
    }

    [Fact]
    public void Startseite_SetztNurDasEffectiveDatum()
    {
        ChartImport import = StartUpdate();

        ImportResult result = import.Import(DfsTestPages.StartPage(NewEdition, SaveFormat.HtmlOnly));

        Assert.Equal(ImportOutcome.Processed, result.Outcome);
        Assert.Equal(NewEdition, import.Effective);
        Assert.Equal(AirfieldTaskStatus.Open, import.Airfields.Single().Status);
    }

    [Fact]
    public void GeaenderterPermalink_WarnungUndZuordnungUeberIcao()
    {
        ChartImport import = StartUpdate();

        ImportResult result = import.Import(DfsTestPages.AirfieldPage(NewEdition, "C0NEU9.html", "Musterstadt EDXA",
            [new("EDXA Musterstadt 1", "h1", Preview1), new("AD 2-1", "h2", Preview2)], SaveFormat.HtmlOnly));

        Assert.Contains("Permalink", result.Message);
        Assert.All(_database.ChartsOf(_airfield).Where(c => !c.IsTripKit), c => Assert.Equal("C0NEU9.html", c.AirfieldPermalink));
        Assert.Equal(ImportSessionState.Completed, import.State);
    }

    [Fact]
    public void UnvollstaendigerAbgleich_AendertAipLastUpdateNicht()
    {
        var other = new Airfield { Icao = "EDXB", Name = "Beispielfeld" };
        _database.Airfields.Add(other);
        ChartImport import = StartUpdate();

        import.Import(AirfieldPage(NewEdition, ("EDXA Musterstadt 1", "h1", Preview1), ("AD 2-1", "h2", Preview2)));

        Assert.Equal(ImportSessionState.Running, import.State);
        Assert.Equal(LastEdition, _database.AipLastUpdate);
    }

    [Fact]
    public void NeuerFlugplatz_NachRueckfrageAlleKartenUebernehmen()
    {
        var import = new ChartImport(_folder, _database, Version, ImportMode.AddAirfield);
        string page = DfsTestPages.AirfieldPage(NewEdition, "C0BBB2.html", "Beispiel-Feld EDXB",
            [new("EDXB Beispiel 1", "b1", Preview1), new("AD 2-5", "b2", Preview2)], SaveFormat.Complete);

        ImportResult result = import.Import(page);
        Assert.Equal(ImportOutcome.NewAirfieldFound, result.Outcome);
        Assert.Null(_database.FindAirfield("EDXB"));

        import.AddAirfield(result.NewAirfield!);
        Assert.Equal(["EDXB Beispiel 1.png", "EDXB AD 2-5.png"], import.PendingCharts.Select(p => p.ChartName));
        Assert.Null(_database.FindAirfield("EDXB")); // erst mit der ersten Karte in der Datenbank

        import.Import(DfsTestPages.ChartPage(NewEdition, "b1", "EDXB Beispiel 1", "01 JAN 2026", Chart1, SaveFormat.Complete));
        import.Import(DfsTestPages.ChartPage(NewEdition, "b2", "EDXB AD 2-5", "01 JAN 2026", Chart2, SaveFormat.Complete));

        Airfield added = _database.FindAirfield("EDXB")!;
        Assert.Equal("Beispiel-Feld", added.Name);
        Assert.Equal(3, _database.ChartsOf(added).Count()); // zwei Karten und das TripKit
        Assert.True(File.Exists(Path.Combine(_root, "EDXB - Beispiel-Feld", "EDXB_TripKit_Charts.pdf")));
        Assert.Empty(_database.Updates); // neu abonniert: keine Einträge unter "Aktualisierungen"
        Assert.Equal(LastEdition, _database.AipLastUpdate); // war schon gesetzt
        Assert.Equal(AirfieldTaskStatus.Done, import.Airfields.Single().Status);
    }

    [Fact]
    public void NeuerFlugplatz_InLeererDatenbank_SetztAusgabe()
    {
        var database = new ChartDatabase();
        var import = new ChartImport(_folder, database, Version, ImportMode.AddAirfield);
        ImportResult result = import.Import(DfsTestPages.AirfieldPage(NewEdition, "C0BBB2.html", "Beispiel EDXB",
            [new("EDXB Beispiel 1", "b1", Preview1)], SaveFormat.HtmlOnly));
        import.AddAirfield(result.NewAirfield!);

        import.Import(DfsTestPages.ChartPage(NewEdition, "b1", "EDXB Beispiel 1", null, Chart1, SaveFormat.HtmlOnly));

        Assert.Equal((NewEdition, Version, "DFS"), (database.AipLastUpdate, database.Version, database.DataSource));
    }

    [Fact]
    public void FremdeOderDefekteSeite_Fehlschlag()
    {
        ChartImport import = StartUpdate();

        Assert.Equal(ImportOutcome.Failed, import.Import("<html>Einkaufsliste</html>").Outcome);
        Assert.Equal(ImportOutcome.Failed, import.Import(
            DfsTestPages.StartPage(NewEdition, SaveFormat.HtmlOnly).Replace("<ul>", "<ul><li class=\"document-item\"></li>", StringComparison.Ordinal)).Outcome);
        Assert.Equal(2, _database.ChartsOf(_airfield).Count(c => !c.IsTripKit));
    }

    [Fact]
    public void GleicheSeitenMehrfach_Abgleich()
    {
        byte[] newPreview = DfsTestPages.Png(SKColors.Orange);
        string airfieldPage = AirfieldPage(NewEdition, ("EDXA Musterstadt 1", "h1", newPreview), ("AD 2-1", "h2", Preview2));
        string chartPage = DfsTestPages.ChartPage(NewEdition, "h1", "EDXA Musterstadt 1", null, Chart2, SaveFormat.HtmlOnly);
        ChartImport import = StartUpdate();

        import.Import(airfieldPage);
        import.Import(airfieldPage); // Flugplatzseite doppelt: dieselbe Karte bleibt angefordert, nicht doppelt
        Assert.Single(import.PendingCharts);

        import.Import(chartPage);
        string before = Snapshot();
        ImportResult again = import.Import(chartPage); // Kartenseite doppelt

        Assert.Contains("bereits beendet", again.Message);
        Assert.Equal(before, Snapshot());
    }

    [Fact]
    public void GleicheSeitenMehrfach_NeuerFlugplatz()
    {
        var import = new ChartImport(_folder, _database, Version, ImportMode.AddAirfield);
        string airfieldPage = DfsTestPages.AirfieldPage(NewEdition, "C0BBB2.html", "Beispiel EDXB",
            [new("EDXB Beispiel 1", "b1", Preview1), new("AD 2-5", "b2", Preview2)], SaveFormat.HtmlOnly);
        string chartPage = DfsTestPages.ChartPage(NewEdition, "b1", "EDXB Beispiel 1", null, Chart1, SaveFormat.HtmlOnly);
        import.AddAirfield(import.Import(airfieldPage).NewAirfield!);
        import.Import(chartPage);

        import.Import(airfieldPage); // Flugplatzseite erneut: die übernommene Karte bleibt erledigt
        Assert.Equal(["EDXB AD 2-5.png"], import.PendingCharts.Select(p => p.ChartName));
        Assert.Equal(["EDXB Beispiel 1.png"], import.CompletedCharts.Select(p => p.ChartName));

        string before = Snapshot();
        ImportResult again = import.Import(chartPage); // Kartenseite doppelt
        Assert.Contains("bereits übernommen", again.Message);
        Assert.Equal(before, Snapshot());
    }

    // Lokale Tests gegen echte Daten (IMPORT-MODUS 5.2)

    [Theory]
    [InlineData("AIP VFR Germany_Mannheim.html")]
    [InlineData("AIP VFR Germany_Mannheim_nur_HTML.html")]
    public void EchteDaten_EdfmIstAktuell(string file)
    {
        (ChartFolder folder, ChartDatabase database) = CopyTestCharts();
        var import = new ChartImport(folder, database, Version, ImportMode.UpdateCharts);
        string before = SnapshotOf(folder.Path);

        ImportResult result = import.Import(File.ReadAllText(Path.Combine(TestData.RequireLocal("testdata"), file)));

        Assert.Contains("alle Karten aktuell", result.Message);
        Assert.Equal(AirfieldTaskStatus.Done, import.Airfields.Single(t => t.Icao == "EDFM").Status);
        Assert.Equal(before, SnapshotOf(folder.Path));
    }

    [Fact]
    public void EchteDaten_FehlendeVorschau_KarteWirdNeuUebernommen()
    {
        string testdata = TestData.RequireLocal("testdata");
        (ChartFolder folder, ChartDatabase database) = CopyTestCharts();
        string directory = Path.Combine(folder.Path, "EDFM - Mannheim City");
        byte[] chartBefore = File.ReadAllBytes(Path.Combine(directory, "EDFM Mannheim City 1.png"));
        byte[] previewBefore = File.ReadAllBytes(Path.Combine(directory, ".EDFM Mannheim City 1.png_preview.png"));
        File.Delete(Path.Combine(directory, ".EDFM Mannheim City 1.png_preview.png"));
        var import = new ChartImport(folder, database, Version, ImportMode.UpdateCharts);

        import.Import(File.ReadAllText(Path.Combine(testdata, "AIP VFR Germany_Mannheim_nur_HTML.html")));
        Assert.Equal("EDFM Mannheim City 1.png", Assert.Single(import.PendingCharts).ChartName);

        ImportResult result = import.Import(File.ReadAllText(Path.Combine(testdata, "AIP VFR Germany_Mannheim_Page1.html")));

        Assert.Contains("EDFM ist aktuell", result.Message);
        Assert.Equal(chartBefore, File.ReadAllBytes(Path.Combine(directory, "EDFM Mannheim City 1.png")));
        Assert.Equal(previewBefore, File.ReadAllBytes(Path.Combine(directory, ".EDFM Mannheim City 1.png_preview.png")));
        Assert.Contains(import.UpdatedCharts, c => c.Name == "EDFM_TripKit_Charts.pdf");
    }

    private ChartImport StartUpdate() => new(_folder, _database, Version, ImportMode.UpdateCharts);

    private static string AirfieldPage(DateOnly effective, params (string Name, string Hash, byte[] Preview)[] charts) =>
        AirfieldPage(effective, SaveFormat.HtmlOnly, charts);

    private static string AirfieldPage(DateOnly effective, SaveFormat format, params (string Name, string Hash, byte[] Preview)[] charts) =>
        DfsTestPages.AirfieldPage(effective, Permalink, "Musterstadt EDXA", charts.Select(c => new TestChart(c.Name, c.Hash, c.Preview)), format);

    private void AddLocalChart(string serverName, byte[] png, byte[] preview)
    {
        var chart = new Chart
        {
            Icao = "EDXA",
            Name = serverName + ".png",
            CreationDate = LastEdition,
            LastUpdate = LastEdition,
            AirfieldPermalink = Permalink,
            ServerName = serverName,
        };
        _database.Charts.Add(chart);
        Directory.CreateDirectory(Path.Combine(_root, Utility.AirfieldDirectoryName(_airfield)));
        File.WriteAllBytes(ChartPath(chart.Name), png);
        File.WriteAllBytes(PreviewPath(chart.Name), preview);
    }

    private string ChartPath(string name) => Path.Combine(_root, "EDXA - Musterstadt", name);

    private string PreviewPath(string name) => Path.Combine(_root, "EDXA - Musterstadt", "." + name + "_preview.png");

    /// <summary>Dateien und Datenbank, um "keine Änderung" festzustellen.</summary>
    private string Snapshot() => SnapshotOf(_root) + Convert.ToHexString(ChartDatabaseXml.ToBytes(_database));

    /// <summary>Alle Dateien mit Prüfsumme ihres Inhalts.</summary>
    private static string SnapshotOf(string root) => string.Join("\n", Directory
        .GetFiles(root, "*", SearchOption.AllDirectories)
        .Order(StringComparer.Ordinal)
        .Select(f => Path.GetRelativePath(root, f) + ":" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(f)))));

    private (ChartFolder, ChartDatabase) CopyTestCharts()
    {
        string copy = Path.Combine(_root, "testcharts");
        TestData.CopyDirectory(TestData.RequireLocal("testcharts"), copy);
        var folder = new ChartFolder(copy);
        return (folder, folder.Load().Database);
    }
}
