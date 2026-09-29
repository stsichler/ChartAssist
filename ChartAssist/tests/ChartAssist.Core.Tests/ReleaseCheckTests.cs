using System.Net;
using System.Text;

namespace ChartAssist.Core.Tests;

public class ReleaseCheckTests
{
    private static readonly Version Current = new(1, 0, 0, 0);

    [Theory]
    [InlineData("v1.0.0.1", "1.0.0.1")]
    [InlineData("v2.0.0.0", "2.0.0.0")]
    [InlineData("v1.0.0.0", null)] // gleiche Version
    [InlineData("v0.9.0.0", null)] // ältere Version, z. B. bei einem Vorab-Build
    [InlineData("release-1", null)]
    public async Task FindNewerRelease_VergleichtVersionen(string tag, string? expected)
    {
        using HttpClient client = ClientReturning(HttpStatusCode.OK, $$"""{"tag_name":"{{tag}}","name":"x"}""");

        Version? newer = await ReleaseCheck.FindNewerReleaseAsync(client, Current, TestContext.Current.CancellationToken);

        Assert.Equal(expected, newer?.ToString());
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "{}")]
    [InlineData(HttpStatusCode.OK, "kein json")]
    [InlineData(HttpStatusCode.OK, "{}")]
    public async Task GetLatestTag_FehlerErgebenNull(HttpStatusCode status, string body)
    {
        using HttpClient client = ClientReturning(status, body);

        Assert.Null(await ReleaseCheck.GetLatestTagAsync(client, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetLatestTag_OfflineErgibtNull()
    {
        using var client = new HttpClient(new StubHandler(_ => throw new HttpRequestException("offline")));

        Assert.Null(await ReleaseCheck.GetLatestTagAsync(client, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetLatestTag_FragtNurGitHubAb()
    {
        Uri? requested = null;
        using var client = new HttpClient(new StubHandler(request =>
        {
            requested = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"tag_name\":\"v1.0.0.0\"}") };
        }));

        Assert.Equal("v1.0.0.0", await ReleaseCheck.GetLatestTagAsync(client, TestContext.Current.CancellationToken));
        Assert.Equal("api.github.com", requested?.Host);
    }

    private static HttpClient ClientReturning(HttpStatusCode status, string body) =>
        new(new StubHandler(_ => new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") }));

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
