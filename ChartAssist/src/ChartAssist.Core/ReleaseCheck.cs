using System.Net.Http.Headers;
using System.Text.Json;

namespace ChartAssist.Core;

/// <summary>
/// Fragt das neueste Release auf GitHub ab. Das ist die einzige Netzwerkverbindung von ChartAssist
/// (IMPORT-MODUS 2, Leitplanke 1). Normale Zertifikatsprüfung, keine eigene TLS-Konfiguration.
/// </summary>
public static class ReleaseCheck
{
    public const string LatestReleaseUrl = "https://api.github.com/repos/stsichler/ChartAssist/releases/latest";

    public const string ReleasesPageUrl = "https://github.com/stsichler/ChartAssist/releases/latest";

    public static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ChartAssist", null));
        return client;
    }

    /// <summary>
    /// Liefert die Version des neuesten Releases, wenn sie neuer ist als <paramref name="currentVersion"/>.
    /// Offline, bei Fehlern oder ohne neuere Version: null, ohne Meldung.
    /// </summary>
    public static async Task<Version?> FindNewerReleaseAsync(HttpClient client, Version currentVersion, CancellationToken cancellationToken = default)
    {
        string? tag = await GetLatestTagAsync(client, cancellationToken);
        return ParseTag(tag) is Version latest && latest > currentVersion ? latest : null;
    }

    /// <summary>Tag des neuesten Releases, z. B. "v1.0.0.0", oder null.</summary>
    public static async Task<string?> GetLatestTagAsync(HttpClient client, CancellationToken cancellationToken = default)
    {
        try
        {
            using HttpResponseMessage response = await client.GetAsync(LatestReleaseUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }
            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using JsonDocument json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return json.RootElement.TryGetProperty("tag_name", out JsonElement tag) ? tag.GetString() : null;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>"v1.2.3.4" → 1.2.3.4. Andere Formate ergeben null.</summary>
    public static Version? ParseTag(string? tag) =>
        tag != null && tag.StartsWith('v') && Version.TryParse(tag[1..], out Version? version) ? version : null;
}
