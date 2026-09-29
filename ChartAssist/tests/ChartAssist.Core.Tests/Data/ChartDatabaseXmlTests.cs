using System.Text;
using ChartAssist.Core.Data;

namespace ChartAssist.Core.Tests.Data;

public class ChartDatabaseXmlTests
{
    [Fact]
    public void Fixture_RoundTrip_IstByteweiseIdentisch()
    {
        byte[] original = File.ReadAllBytes(TestData.FixturePath("ChartButler.xml"));

        ChartDatabase database = ChartDatabaseXml.Read(new MemoryStream(original));

        Assert.Equal(original, ChartDatabaseXml.ToBytes(database));
    }

    [Fact]
    public void EchteDatenbank_RoundTrip_IstByteweiseIdentisch()
    {
        string path = Path.Combine(TestData.RequireLocal("testcharts"), ChartDatabaseXml.FileName);
        byte[] original = File.ReadAllBytes(path);

        ChartDatabase database = ChartDatabaseXml.Read(path);

        Assert.Equal(8, database.Airfields.Count);
        Assert.Equal(37, database.Charts.Count);
        Assert.Equal(original, ChartDatabaseXml.ToBytes(database));
    }

    [Fact]
    public void Fixture_WirdVollstaendigGelesen()
    {
        ChartDatabase database = ChartDatabaseXml.Read(TestData.FixturePath("ChartButler.xml"));

        Assert.Equal(["EDXA", "EDXB"], database.Airfields.Select(a => a.Icao));
        Assert.Equal("Beispielfeld & Umgebung", database.Airfields[1].Name);
        Assert.Null(database.Airfields[1].LastUpdate);

        // Reihenfolge der Datei bleibt erhalten, auch wenn sich die Flugplätze abwechseln
        Assert.Equal("EDXA Musterstadt-2B.png", database.Charts[^1].Name);

        Chart chart = database.Charts[0];
        Assert.Equal(new DateOnly(2024, 4, 14), chart.CreationDate);
        Assert.Equal(new DateOnly(2017, 2, 16), chart.LastUpdate);
        Assert.Equal("C0AAA1.html", chart.AirfieldPermalink);
        Assert.Equal("EDXA Musterstadt 1", chart.ServerName);
        Assert.False(chart.IsTripKit);

        Chart tripKit = database.Charts[4];
        Assert.True(tripKit.IsTripKit);
        Assert.Null(tripKit.LastUpdate);

        Assert.Equal(new DateOnly(2026, 3, 19), database.AipLastUpdate);
        Assert.Equal([new DateOnly(2025, 9, 18), new DateOnly(2025, 12, 25), new DateOnly(2026, 3, 19)], database.Updates);
        Assert.Equal("2.0.1.1", database.Version);
        Assert.Equal("DFS", database.DataSource);
    }

    [Fact]
    public void Lesen_AkzeptiertLF_SchreibenErzeugtCRLF()
    {
        byte[] original = File.ReadAllBytes(TestData.FixturePath("ChartButler.xml"));
        string withLf = Encoding.UTF8.GetString(original).Replace("\r\n", "\n");

        ChartDatabase database = ChartDatabaseXml.Read(new MemoryStream(Encoding.UTF8.GetBytes(withLf)));

        Assert.Equal(original, ChartDatabaseXml.ToBytes(database));
    }

    [Fact]
    public void Schreiben_OhneBomUndOhneEncodingAngabe()
    {
        byte[] bytes = ChartDatabaseXml.ToBytes(new ChartDatabase { Version = "1.0.0.0" });

        Assert.Equal((byte)'<', bytes[0]);
        Assert.StartsWith("<?xml version=\"1.0\" standalone=\"yes\"?>\r\n<ChartButlerDataSet>\r\n", Encoding.UTF8.GetString(bytes));
    }

    [Theory]
    [InlineData(2026, 1, 15, "2026-01-15T00:00:00+01:00")]
    [InlineData(2026, 7, 23, "2026-07-23T00:00:00+02:00")]
    [InlineData(2026, 3, 29, "2026-03-29T00:00:00+01:00")] // Umstellung auf Sommerzeit erst um 2 Uhr
    [InlineData(2026, 10, 25, "2026-10-25T00:00:00+02:00")] // Umstellung auf Winterzeit erst um 3 Uhr
    public void FormatDate_VerwendetDeutschenOffsetDesTages(int year, int month, int day, string expected)
    {
        Assert.Equal(expected, ChartDatabaseXml.FormatDate(new DateOnly(year, month, day)));
    }

    [Theory]
    [InlineData("2024-04-14T00:00:00+02:00")]
    [InlineData("2024-04-13T22:00:00+00:00")] // von ChartButlerCS auf einem Rechner in UTC umgeschrieben
    [InlineData("2024-04-13T22:00:00Z")]
    public void ParseDate_LiefertDeutschenKalendertag(string value)
    {
        Assert.Equal(new DateOnly(2024, 4, 14), ChartDatabaseXml.ParseDate(value));
    }

    [Theory]
    [InlineData("<kaputt")]
    [InlineData("<?xml version=\"1.0\"?><Anderes />")]
    [InlineData("<ChartButlerDataSet><Airfields><ICAO>EDXA</ICAO></Airfields></ChartButlerDataSet>")]
    [InlineData("<ChartButlerDataSet><Updates><Date>gestern</Date></Updates></ChartButlerDataSet>")]
    [InlineData("<ChartButlerDataSet><Airfields><ICAO>EDXA</ICAO><AFname>A</AFname></Airfields>"
        + "<Airfields><ICAO>EDXA</ICAO><AFname>B</AFname></Airfields></ChartButlerDataSet>")]
    public void Lesen_UngueltigeDatei_WirftInvalidDataException(string xml)
    {
        Assert.Throws<InvalidDataException>(() => ChartDatabaseXml.Read(new MemoryStream(Encoding.UTF8.GetBytes(xml))));
    }

    [Fact]
    public void Lesen_IgnoriertKartenOhneFlugplatz()
    {
        const string xml = "<ChartButlerDataSet>"
            + "<AFCharts><ICAO>EDXZ</ICAO><Cname>EDXZ 1.png</Cname><CreationDate>2026-01-15T00:00:00+01:00</CreationDate></AFCharts>"
            + "<Airfields><ICAO>EDXA</ICAO><AFname>A</AFname></Airfields></ChartButlerDataSet>";

        ChartDatabase database = ChartDatabaseXml.Read(new MemoryStream(Encoding.UTF8.GetBytes(xml)));

        Assert.Empty(database.Charts);
    }

    [Fact]
    public void AddUpdate_BehaeltDieLetztenFuenf()
    {
        var database = new ChartDatabase();
        for (int month = 1; month <= 7; month++)
        {
            database.AddUpdate(new DateOnly(2026, month, 1));
        }
        database.AddUpdate(new DateOnly(2026, 7, 1));

        Assert.Equal(Enumerable.Range(3, 5).Select(m => new DateOnly(2026, m, 1)), database.Updates);
    }
}
