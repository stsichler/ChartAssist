using ChartAssist.Core.Dfs;
using SkiaSharp;

namespace ChartAssist.Core.Tests.Dfs;

public class DfsPageParserTests
{
    private static readonly DateOnly Effective = new(2026, 9, 17);
    private static readonly byte[] PreviewA = DfsTestPages.Png(SKColors.Red);
    private static readonly byte[] PreviewB = DfsTestPages.Png(SKColors.Blue);

    private static readonly TestChart[] Charts =
    [
        new("EDXA Musterstadt 1", "0a1b2c3d4e5f", PreviewA),
        new("AD 2-1 & Umgebung", "FFEE0011", PreviewB),
    ];

    [Theory]
    [InlineData(SaveFormat.HtmlOnly)]
    [InlineData(SaveFormat.Complete)]
    public void DetectKind_ErkenntSeitenartAmInhalt(SaveFormat format)
    {
        Assert.Equal(DfsPageKind.OtherDfsPage, DfsPageParser.DetectKind(DfsTestPages.StartPage(Effective, format)));
        Assert.Equal(DfsPageKind.AirfieldPage, DfsPageParser.DetectKind(AirfieldPage(format)));
        Assert.Equal(DfsPageKind.ChartPage, DfsPageParser.DetectKind(ChartPage(format)));
        Assert.Equal(DfsPageKind.Unknown, DfsPageParser.DetectKind("<html><body>Irgendeine Seite</body></html>"));
    }

    [Theory]
    [InlineData(SaveFormat.HtmlOnly)]
    [InlineData(SaveFormat.Complete)]
    public void ParseAirfieldPage_LiefertFlugplatzUndKarten(SaveFormat format)
    {
        DfsAirfieldPage page = DfsPageParser.ParseAirfieldPage(AirfieldPage(format));

        Assert.Equal(Effective, page.Effective);
        Assert.Equal("C0AAA1.html", page.Permalink);
        Assert.Equal("EDXA", page.Icao);
        Assert.Equal("Musterstadt am See", page.Name);
        Assert.Equal(["EDXA Musterstadt 1", "AD 2-1 & Umgebung"], page.Charts.Select(c => c.Name));
        Assert.Equal(["0a1b2c3d4e5f", "FFEE0011"], page.Charts.Select(c => c.Hash));
        Assert.Equal(PreviewA, page.Charts[0].PreviewPng);
        Assert.Equal(PreviewB, page.Charts[1].PreviewPng);
        Assert.Equal(format == SaveFormat.Complete, page.Charts[0].Href.StartsWith("https://aip.dfs.de/", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(SaveFormat.HtmlOnly)]
    [InlineData(SaveFormat.Complete)]
    public void ParseChartPage_LiefertKarte(SaveFormat format)
    {
        DfsChartPage page = DfsPageParser.ParseChartPage(ChartPage(format));

        Assert.Equal(Effective, page.Effective);
        Assert.Equal("0a1b2c3d4e5f", page.Hash);
        Assert.Equal("EDXA Musterstadt 1", page.Name);
        Assert.Equal(new DateOnly(2017, 2, 16), page.Date);
        Assert.Equal(PreviewB, page.Png);
    }

    [Fact]
    public void ParseAirfieldPage_OhneKarten_IstFehler()
    {
        string html = DfsTestPages.AirfieldPage(Effective, "C0AAA1.html", "Musterstadt EDXA", [], SaveFormat.HtmlOnly)
            .Replace("<ul>", "<ul><li class=\"document-item\"></li>", StringComparison.Ordinal);

        Assert.Equal(DfsPageKind.AirfieldPage, DfsPageParser.DetectKind(html));
        DfsPageException e = Assert.Throws<DfsPageException>(() => DfsPageParser.ParseAirfieldPage(html));
        Assert.Contains("keine Karten", e.Message);
    }

    [Fact]
    public void ParseChartPage_AbgeschnittenesBild_IstFehler()
    {
        string html = ChartPage(SaveFormat.HtmlOnly);
        html = html.Replace(Convert.ToBase64String(PreviewB), Convert.ToBase64String(PreviewB)[..7], StringComparison.Ordinal);

        Assert.Throws<DfsPageException>(() => DfsPageParser.ParseChartPage(html));
    }

    [Theory]
    [InlineData("17 SEP 2026", 2026, 9, 17)]
    [InlineData("02 FEB 2023", 2023, 2, 2)]
    [InlineData("Effective: 05 MAR 2026 ", 2026, 3, 5)]
    public void CreateDateFromString_ErkenntDfsDatum(string text, int year, int month, int day)
    {
        Assert.Equal(new DateOnly(year, month, day), DfsPageParser.CreateDateFromString(text));
    }

    [Theory]
    [InlineData(2026, 9, 17, "17 SEP 2026")]
    [InlineData(2026, 3, 5, "05 MAR 2026")]
    public void FormatDate_SchreibweiseDerDfs(int year, int month, int day, string expected)
    {
        Assert.Equal(expected, DfsPageParser.FormatDate(new DateOnly(year, month, day)));
        Assert.Equal(new DateOnly(year, month, day), DfsPageParser.CreateDateFromString(expected));
    }

    [Theory]
    [InlineData("")]
    [InlineData("17 Sep 2026")]
    [InlineData("31 FEB 2026")]
    public void CreateDateFromString_UngueltigErgibtNull(string text)
    {
        Assert.Null(DfsPageParser.CreateDateFromString(text));
    }

    // Lokale Tests gegen echte, von Hand gespeicherte Seiten (IMPORT-MODUS 4)

    [Fact]
    public void EchteSeiten_SeitenartUndEffective()
    {
        string testdata = TestData.RequireLocal("testdata");
        foreach ((string file, DfsPageKind kind) in new[]
        {
            ("AIP VFR Germany.html", DfsPageKind.OtherDfsPage),
            ("AIP VFR Germany_nur_HTML.html", DfsPageKind.OtherDfsPage),
            ("AIP VFR Germany_Mannheim.html", DfsPageKind.AirfieldPage),
            ("AIP VFR Germany_Mannheim_nur_HTML.html", DfsPageKind.AirfieldPage),
            ("AIP VFR Germany_Mannheim_Page1.html", DfsPageKind.ChartPage),
            ("AIP VFR Germany_Mannheim_Page1_nur_HTML.html", DfsPageKind.ChartPage),
            ("AIP VFR Germany_Muenchen.html", DfsPageKind.AirfieldPage),
            ("AIP VFR Germany_Muenchen_Page1.html", DfsPageKind.ChartPage),
        })
        {
            string html = File.ReadAllText(Path.Combine(testdata, file));
            Assert.Equal(kind, DfsPageParser.DetectKind(html));
            Assert.Equal(Effective, DfsPageParser.ParseEffectiveDate(html));
        }
    }

    [Fact]
    public void EchteFlugplatzseite_BeideFormateGleich()
    {
        string testdata = TestData.RequireLocal("testdata");

        DfsAirfieldPage complete = DfsPageParser.ParseAirfieldPage(File.ReadAllText(Path.Combine(testdata, "AIP VFR Germany_Mannheim.html")));
        DfsAirfieldPage htmlOnly = DfsPageParser.ParseAirfieldPage(File.ReadAllText(Path.Combine(testdata, "AIP VFR Germany_Mannheim_nur_HTML.html")));
        DfsAirfieldPage munich = DfsPageParser.ParseAirfieldPage(File.ReadAllText(Path.Combine(testdata, "AIP VFR Germany_Muenchen.html")));

        Assert.Equal(("EDFM", "Mannheim City", "C01A45.html"), (complete.Icao, complete.Name, complete.Permalink));
        Assert.Equal(7, complete.Charts.Count);
        Assert.Equal(complete.Charts.Select(c => (c.Name, c.Hash)), htmlOnly.Charts.Select(c => (c.Name, c.Hash)));
        Assert.All(complete.Charts.Zip(htmlOnly.Charts), pair => Assert.Equal(pair.First.PreviewPng, pair.Second.PreviewPng));
        Assert.All(complete.Charts, c => Assert.NotEmpty(c.PreviewPng));
        Assert.Equal(("EDDM", "Muenchen", 9), (munich.Icao, munich.Name, munich.Charts.Count));
    }

    [Fact]
    public void EchteKartenseite_PngIdentischMitKartenverzeichnis()
    {
        string testdata = TestData.RequireLocal("testdata");
        string localChart = Path.Combine(TestData.RequireLocal("testcharts"), "EDFM - Mannheim City", "EDFM Mannheim City 1.png");

        foreach (string file in new[] { "AIP VFR Germany_Mannheim_Page1.html", "AIP VFR Germany_Mannheim_Page1_nur_HTML.html" })
        {
            DfsChartPage page = DfsPageParser.ParseChartPage(File.ReadAllText(Path.Combine(testdata, file)));
            Assert.Equal("758fd4e22947d0611582f1e9e80013fb", page.Hash);
            Assert.Equal("EDFM Mannheim City 1", page.Name);
            Assert.Equal(new DateOnly(2017, 2, 16), page.Date);
            Assert.Equal(File.ReadAllBytes(localChart), page.Png);
        }
    }

    private static string AirfieldPage(SaveFormat format) =>
        DfsTestPages.AirfieldPage(Effective, "C0AAA1.html", "Musterstadt am See EDXA", Charts, format);

    private static string ChartPage(SaveFormat format) =>
        DfsTestPages.ChartPage(Effective, "0a1b2c3d4e5f", "EDXA Musterstadt 1", "16 FEB 2017", PreviewB, format);
}
