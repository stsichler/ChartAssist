using System.Text;
using System.Text.RegularExpressions;
using ChartAssist.Core.Data;
using ChartAssist.Core.TripKit;
using SkiaSharp;

namespace ChartAssist.Core.Tests.TripKit;

public sealed partial class TripKitBuilderTests : IDisposable
{
    private static readonly byte[] Portrait = CreatePng(148, 210, SKColors.SteelBlue);
    private static readonly byte[] Landscape = CreatePng(297, 210, SKColors.IndianRed);

    private readonly string _root = Directory.CreateTempSubdirectory("ChartAssistTest").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Theory]
    [InlineData("P", 1)]
    [InlineData("PP", 1)]
    [InlineData("PPP", 2)]
    [InlineData("L", 1)]
    [InlineData("PL", 2)] // Querformat beginnt auf neuer Seite
    [InlineData("PLP", 3)]
    [InlineData("LPP", 2)]
    [InlineData("PPLPPP", 4)]
    public void Build_OrdnetKartenAufA4QuerSeitenAn(string layout, int expectedPages)
    {
        byte[][] images = layout.Select(c => c == 'L' ? Landscape : Portrait).ToArray();

        TripKitDocument tripKit = TripKitBuilder.Build(images);

        Assert.Equal(expectedPages, tripKit.PageCount);
        Assert.Equal(expectedPages, CountPdfPages(tripKit.Pdf));
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(tripKit.Pdf, 0, 5));
    }

    [Fact]
    public void Build_VorschauIstJpegInFesterGroesse()
    {
        TripKitDocument tripKit = TripKitBuilder.Build([Portrait, Portrait]);

        using SKBitmap preview = SKBitmap.Decode(tripKit.PreviewJpeg);
        Assert.Equal(SKEncodedImageFormat.Jpeg, SKCodec.Create(new MemoryStream(tripKit.PreviewJpeg)).EncodedFormat);
        Assert.Equal(TripKitBuilder.PreviewWidth, preview.Width);
        Assert.Equal(TripKitBuilder.PreviewHeight, preview.Height);
        AssertColorNear(SKColors.SteelBlue, preview.GetPixel(200, 300));
        AssertColorNear(SKColors.SteelBlue, preview.GetPixel(640, 300));
    }

    [Fact]
    public void Build_OhneKarten_WirftArgumentException()
    {
        Assert.Throws<ArgumentException>(() => TripKitBuilder.Build([]));
    }

    [Fact]
    public void Update_SchreibtPdfVorschauUndEintrag()
    {
        (ChartFolder folder, ChartDatabase database, Airfield airfield) = CreateAirfield(
            ("EDXA Musterstadt 1.png", Portrait, new DateOnly(2026, 3, 19)),
            ("EDXA AD 2-1.png", Landscape, new DateOnly(2026, 8, 20)));

        Chart? tripKit = TripKitBuilder.Update(folder, database, airfield);

        Assert.NotNull(tripKit);
        Assert.Equal("EDXA_TripKit_Charts.pdf", tripKit.Name);
        Assert.True(tripKit.IsTripKit);
        Assert.Equal(new DateOnly(2026, 8, 20), tripKit.LastUpdate);
        Assert.Same(tripKit, database.FindChart(tripKit.Name));
        Assert.Equal(2, CountPdfPages(File.ReadAllBytes(Utility.BuildChartPath(folder.Path, airfield, tripKit))));
        Assert.True(File.Exists(Utility.BuildChartPreviewPath(folder.Path, airfield, tripKit, "jpg")));

        // Zweiter Aufruf aktualisiert denselben Eintrag
        Assert.Same(tripKit, TripKitBuilder.Update(folder, database, airfield));
        Assert.Equal(3, database.Charts.Count);
    }

    [Fact]
    public void Update_OhneKarten_EntferntTripKit()
    {
        (ChartFolder folder, ChartDatabase database, Airfield airfield) = CreateAirfield(
            ("EDXA Musterstadt 1.png", Portrait, new DateOnly(2026, 3, 19)));
        Chart tripKit = TripKitBuilder.Update(folder, database, airfield)!;
        string pdfPath = Utility.BuildChartPath(folder.Path, airfield, tripKit);

        database.Charts.RemoveAll(c => !c.IsTripKit);
        Assert.Null(TripKitBuilder.Update(folder, database, airfield));

        Assert.Empty(database.Charts);
        Assert.False(File.Exists(pdfPath));
    }

    [Fact]
    public void EchteKarten_GleicheSeitenzahlWieChartButlerCS()
    {
        string copy = Path.Combine(_root, "testcharts");
        TestData.CopyDirectory(TestData.RequireLocal("testcharts"), copy);
        var folder = new ChartFolder(copy);
        ChartDatabase database = folder.Load().Database;

        foreach (Airfield airfield in database.Airfields)
        {
            string oldPdf = Utility.BuildChartPath(folder.Path, airfield, database.FindChart(TripKitBuilder.FileName(airfield.Icao))!);
            int expectedPages = CountPdfPages(File.ReadAllBytes(oldPdf));

            Chart tripKit = TripKitBuilder.Update(folder, database, airfield)!;

            Assert.Equal(expectedPages, CountPdfPages(File.ReadAllBytes(Utility.BuildChartPath(folder.Path, airfield, tripKit))));
        }
    }

    private (ChartFolder, ChartDatabase, Airfield) CreateAirfield(params (string Name, byte[] Png, DateOnly LastUpdate)[] charts)
    {
        var folder = new ChartFolder(_root);
        var database = new ChartDatabase();
        var airfield = new Airfield { Icao = "EDXA", Name = "Musterstadt" };
        database.Airfields.Add(airfield);
        Directory.CreateDirectory(Path.Combine(_root, Utility.AirfieldDirectoryName(airfield)));
        foreach ((string name, byte[] png, DateOnly lastUpdate) in charts)
        {
            var chart = new Chart { Icao = "EDXA", Name = name, LastUpdate = lastUpdate, AirfieldPermalink = "C0AAA1.html" };
            database.Charts.Add(chart);
            File.WriteAllBytes(Utility.BuildChartPath(_root, airfield, chart), png);
        }
        return (folder, database, airfield);
    }

    private static byte[] CreatePng(int width, int height, SKColor color)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(color);
        using SKData data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>Zählt die Seitenobjekte, funktioniert für PDFsharp 1.32 (ChartButlerCS) und 6.x.</summary>
    private static int CountPdfPages(byte[] pdf) => PageObjectRegex().Count(Encoding.Latin1.GetString(pdf));

    private static void AssertColorNear(SKColor expected, SKColor actual)
    {
        Assert.InRange(Math.Abs(expected.Red - actual.Red), 0, 8);
        Assert.InRange(Math.Abs(expected.Green - actual.Green), 0, 8);
        Assert.InRange(Math.Abs(expected.Blue - actual.Blue), 0, 8);
    }

    [GeneratedRegex(@"/Type\s*/Page(?![s\w])")]
    private static partial Regex PageObjectRegex();
}
