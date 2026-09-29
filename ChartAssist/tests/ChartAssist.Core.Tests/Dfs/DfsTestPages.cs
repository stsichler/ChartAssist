using System.Globalization;
using System.Net;
using SkiaSharp;

namespace ChartAssist.Core.Tests.Dfs;

public enum SaveFormat
{
    /// <summary>Firefox "Webseite, nur HTML": Originalquelltext, relative Links, <c>&lt;img … /&gt;</c>.</summary>
    HtmlOnly,

    /// <summary>
    /// Firefox "Webseite, komplett": serialisiertes DOM mit absoluten Links, nicht geschlossenem <c>&lt;img&gt;</c>
    /// und Attributen von Erweiterungen (hier wie von Dark Reader).
    /// </summary>
    Complete,
}

/// <summary>Karteneintrag für eine synthetische Flugplatzseite.</summary>
public sealed record TestChart(string Name, string Hash, byte[] Preview);

/// <summary>
/// Synthetische Seiten mit derselben HTML-Struktur wie die BasicVFR (IMPORT-MODUS 5.2). Echte DFS-Seiten dürfen
/// nicht ins Repository (§11 der Nutzungsbedingungen); die Struktur ist aus den lokalen Testdaten abgeleitet.
/// </summary>
public static class DfsTestPages
{
    public static string StartPage(DateOnly effective, SaveFormat format) =>
        Page(effective, "pages/C00001.html", format, "", "<span lang=\"de\">Luftfahrthandbuch AIP VFR</span>",
            "<li class=\"folder-item\"><a class=\"folder-link\" href=\"../pages/C0004A.html\">Flugplätze</a></li>");

    public static string AirfieldPage(DateOnly effective, string permalink, string headline, IEnumerable<TestChart> charts, SaveFormat format)
    {
        string edition = DfsUrlsEdition(effective);
        string items = string.Concat(charts.Select(chart =>
        {
            string href = format == SaveFormat.Complete
                ? $"https://aip.dfs.de/BasicVFR/{edition}/pages/{chart.Hash}.html"
                : $"../pages/{chart.Hash}.html";
            string nameStyle = format == SaveFormat.Complete ? " style=\"--darkreader-inline-color: #e8e6e3;\" data-darkreader-inline-color=\"\"" : "";
            return $"""

                	<li class="document-item">
                	<a class="document-link" href="{href}">
                	<span lang="de" class="document-name"{nameStyle}>{WebUtility.HtmlEncode(chart.Name)}</span>
                	<span lang="en" class="document-name">{WebUtility.HtmlEncode(chart.Name)}</span>
                	<span class="document-icon">
                		{Image($"data:image/png;base64,{Convert.ToBase64String(chart.Preview)}", "PDF Logo", format)}
                	</span>
                	</a>
                   </li>
                """;
        }));
        string encoded = WebUtility.HtmlEncode(headline);
        return Page(effective, $"pages/{permalink}", format, "", $"<span lang=\"en\">{encoded}</span><span lang=\"de\">{encoded}</span>", items);
    }

    public static string ChartPage(DateOnly effective, string hash, string name, string? date, byte[] png, SaveFormat format)
    {
        string script = $"""
            		const myPrevURL = 'AAAA0000.html';
            		const myNextURL = 'BBBB1111.html';
            		const myURL = "AD/{hash}/{name}";
            """;
        string headline = $"""
            <div class="headlineText float-start">{WebUtility.HtmlEncode(name)}</div>
            				<div class="headlineText float-end">{date}</div>
            """;
        string image = Image($"data:image/png;base64,{Convert.ToBase64String(png)}", "Page not found", format)
            .Replace("<img ", "<img id=\"imgAIP\" class=\"pageImage\" ", StringComparison.Ordinal);
        return Page(effective, "pages/P00001.html", format, script, headline, image, isChartPage: true);
    }

    public static byte[] Png(SKColor color, int width = 12, int height = 17)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(color);
        using SKData data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static string Page(DateOnly effective, string permalink, SaveFormat format, string extraScript, string headline, string main, bool isChartPage = false)
    {
        bool complete = format == SaveFormat.Complete;
        string html = complete ? "<html data-darkreader-mode=\"dynamic\" data-darkreader-scheme=\"dark\" lang=\"de\">" : "<html lang=\"de\">";
        string style = complete ? "<style class=\"darkreader darkreader--sync\" media=\"screen\"></style>" : "";
        string logo = complete ? "<img class=\"logo\" src=\"AIP%20VFR%20Germany_files/DFS_Logo.svg\">" : "<img class=\"logo\" src=\"../../img/DFS_Logo.svg\"/>";
        string headlineDiv = isChartPage ? headline : $"<div class=\"headlineText left\">{headline}</div>";
        return $"""
            <!DOCTYPE html>
            {html}
            <head>
            	<meta charset="utf-8">
            	<title>AIP VFR Germany</title>{style}
            	<script>
            		const myPermalink = "{permalink}";
            {extraScript}
            	</script>
            </head>
            <body class="de" style="visibility:{(complete ? " visible" : "hidden")};">
            	<header class="bg-white sticky-top">
                    <div class="row g-0">
                        <div class="col float-start">
                            <div class="topHeader">
                                <span lang="en">AIP VFR<span class="expand-header">Effective: </span>{EffectiveText(effective)}
                            </div>
                        </div>
                        <div class="col-auto float-end">
                            {logo}
                        </div>
                    </div>
            		<div class="row align-items-center g-0">
            			<div class="container">
            			{headlineDiv}
            			</div>
            		</div>
            	</header>
            	<main role="main" class="container">
            		<ul>
            		{main}
            		</ul>
            	</main>
            </body>
            </html>
            """;
    }

    private static string Image(string src, string alt, SaveFormat format) =>
        format == SaveFormat.Complete ? $"<img src=\"{src}\" alt=\"{alt}\">" : $"<img src=\"{src}\" alt=\"{alt}\"/>";

    public static string EffectiveText(DateOnly date) =>
        date.ToString("dd MMM yyyy", CultureInfo.InvariantCulture).ToUpperInvariant();

    private static string DfsUrlsEdition(DateOnly date) => ChartAssist.Core.Dfs.DfsUrls.EditionFolder(date);
}
