using ChartAssist.Core.Dfs;

namespace ChartAssist.Core.Tests.Dfs;

public class DfsUrlsTests
{
    [Fact]
    public void AirfieldPage_HaengtPermalinkAn()
    {
        Assert.Equal("https://aip.dfs.de/BasicVFR/pages/C01A45.html", DfsUrls.AirfieldPage("C01A45.html").AbsoluteUri);
    }
}
