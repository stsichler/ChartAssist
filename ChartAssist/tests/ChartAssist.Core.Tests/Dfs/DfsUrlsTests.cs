using ChartAssist.Core.Dfs;

namespace ChartAssist.Core.Tests.Dfs;

public class DfsUrlsTests
{
    [Theory]
    [InlineData(2026, 9, 17, "2026SEP17")]
    [InlineData(2026, 3, 5, "2026MAR05")]
    [InlineData(2026, 5, 28, "2026MAY28")]
    public void EditionFolder_EntsprichtOrdnernamenDerDfs(int year, int month, int day, string expected)
    {
        Assert.Equal(expected, DfsUrls.EditionFolder(new DateTime(year, month, day)));
    }

    [Fact]
    public void ChartPage_BildetAbsolutenLinkAusEffectiveUndHash()
    {
        Uri url = DfsUrls.ChartPage(new DateTime(2026, 9, 17), "758fd4e2");

        Assert.Equal("https://aip.dfs.de/BasicVFR/2026SEP17/pages/758fd4e2.html", url.AbsoluteUri);
    }

    [Fact]
    public void AirfieldPage_HaengtPermalinkAn()
    {
        Assert.Equal("https://aip.dfs.de/BasicVFR/pages/C01A45.html", DfsUrls.AirfieldPage("C01A45.html").AbsoluteUri);
    }
}
