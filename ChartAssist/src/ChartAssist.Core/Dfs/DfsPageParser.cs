using System.Net;
using System.Text.RegularExpressions;

namespace ChartAssist.Core.Dfs;

public enum DfsPageKind
{
    /// <summary>Keine DFS-Seite.</summary>
    Unknown,

    /// <summary>Sonstige DFS-Seite, z. B. Startseite oder Flugplatzverzeichnis.</summary>
    OtherDfsPage,

    AirfieldPage,

    ChartPage,
}

/// <summary>Flugplatzseite der BasicVFR mit ihren Karten.</summary>
/// <param name="Permalink">Letztes Segment von <c>myPermalink</c>, z. B. "C01A45.html", wie in <c>Crypt</c>.</param>
/// <param name="Name">Flugplatzname aus der Überschrift, noch nicht als Dateiname aufbereitet.</param>
public sealed record DfsAirfieldPage(DateOnly Effective, string Permalink, string Icao, string Name, IReadOnlyList<DfsChartEntry> Charts);

/// <summary>Eintrag einer Karte auf der Flugplatzseite.</summary>
/// <param name="Name">Kartenname der DFS, noch ohne vorangestellten ICAO-Code.</param>
/// <param name="Hash">Dateiname der Kartenseite ohne ".html"; ordnet die gespeicherte Kartenseite zu.</param>
/// <param name="Href">Link auf die Kartenseite: absolut ("komplett") oder relativ ("nur HTML").</param>
/// <param name="PreviewPng">Vorschaubild; leer, wenn die Seite keines enthält.</param>
public sealed record DfsChartEntry(string Name, string Hash, string Href, byte[] PreviewPng);

/// <summary>Kartenseite der BasicVFR.</summary>
/// <param name="Hash">Mittleres Segment von <c>const myURL = "AD/&lt;Hash&gt;/&lt;Name&gt;"</c>.</param>
/// <param name="Date">Datum der Karte aus der Überschrift, falls vorhanden.</param>
public sealed record DfsChartPage(DateOnly Effective, string Hash, string Name, DateOnly? Date, byte[] Png);

/// <summary>Eine gespeicherte Seite ließ sich nicht auswerten.</summary>
public sealed class DfsPageException(string message) : Exception(message);

/// <summary>
/// Wertet vom Benutzer gespeicherte Seiten der BasicVFR aus (IMPORT-MODUS 5.2), in beiden Speicherformaten
/// von Firefox: "nur HTML" (Originalquelltext) und "komplett" (vom Browser serialisiertes DOM, z. B. mit nicht
/// geschlossenem <c>&lt;img&gt;</c>). Deshalb durchgehend tolerante Regexe statt XML-Parser, unabhängig von
/// Attributreihenfolge und zusätzlichen Attributen.
/// </summary>
public static partial class DfsPageParser
{
    private const string PngDataPrefix = "data:image/png;base64,";

    /// <summary>Erkennt die Seitenart am Inhalt, nie am Dateinamen.</summary>
    public static DfsPageKind DetectKind(string html)
    {
        if (!EffectiveRegex().IsMatch(html))
        {
            return DfsPageKind.Unknown;
        }
        if (ChartImageTagRegex().IsMatch(html))
        {
            return DfsPageKind.ChartPage;
        }
        if (DocumentItemRegex().IsMatch(html))
        {
            return DfsPageKind.AirfieldPage;
        }
        return DfsPageKind.OtherDfsPage;
    }

    /// <summary>Effective-Datum der Ausgabe, das auf jeder DFS-Seite steht.</summary>
    public static DateOnly ParseEffectiveDate(string html)
    {
        Match match = EffectiveRegex().Match(html);
        if (match.Success && CreateDateFromString(match.Groups["date"].Value) is DateOnly date)
        {
            return date;
        }
        throw new DfsPageException("Die Seite enthält kein Effective-Datum.");
    }

    public static DfsAirfieldPage ParseAirfieldPage(string html)
    {
        DateOnly effective = ParseEffectiveDate(html);
        string permalink = ParsePermalink(html);

        // Überschrift "Name ICAO", z. B. "Mannheim City EDFM"
        string headline = GermanSpanText(HeadlineLeftRegex().Match(html).Groups["content"].Value)
            ?? throw new DfsPageException("Die Flugplatzseite enthält keine Überschrift mit dem Flugplatznamen.");
        Match nameAndIcao = NameAndIcaoRegex().Match(headline);
        if (!nameAndIcao.Success)
        {
            throw new DfsPageException($"Die Überschrift \"{headline}\" enthält keinen ICAO-Code.");
        }

        var charts = new List<DfsChartEntry>();
        foreach (Match item in DocumentItemRegex().Matches(html))
        {
            if (ParseDocumentItem(item.Groups["content"].Value) is DfsChartEntry chart)
            {
                charts.Add(chart);
            }
        }

        // Eine leere Liste wäre fatal: Beim Abgleich würden alle Karten als entfallen gelöscht
        if (charts.Count == 0)
        {
            throw new DfsPageException("Auf der Flugplatzseite wurden keine Karten erkannt.");
        }

        return new DfsAirfieldPage(effective, permalink, nameAndIcao.Groups["icao"].Value,
            nameAndIcao.Groups["name"].Value.Trim(), charts);
    }

    public static DfsChartPage ParseChartPage(string html)
    {
        DateOnly effective = ParseEffectiveDate(html);

        Match url = MyUrlRegex().Match(html);
        if (!url.Success)
        {
            throw new DfsPageException("Die Kartenseite enthält keine Kennung (myURL).");
        }

        string name = WebUtility.HtmlDecode(HeadlineStartRegex().Match(html).Groups["text"].Value).Trim();
        Match dateMatch = HeadlineEndRegex().Match(html);
        DateOnly? date = dateMatch.Success ? CreateDateFromString(dateMatch.Groups["text"].Value) : null;

        Match image = ChartImageTagRegex().Match(html);
        byte[]? png = image.Success ? DecodePng(Attribute(image.Value, "src")) : null;
        if (png == null || png.Length == 0)
        {
            throw new DfsPageException("Die Kartenseite enthält kein Kartenbild.");
        }

        return new DfsChartPage(effective, url.Groups["hash"].Value, name, date, png);
    }

    /// <summary>Datum der Form "TT MMM JJJJ", z. B. "17 SEP 2026"; null, wenn keins erkannt wird.</summary>
    public static DateOnly? CreateDateFromString(string dateString)
    {
        Match match = DateRegex().Match(dateString);
        if (!match.Success)
        {
            return null;
        }
        int month = Array.IndexOf(Months, match.Groups["month"].Value) + 1;
        int day = int.Parse(match.Groups["day"].Value);
        int year = int.Parse(match.Groups["year"].Value);
        return day <= DateTime.DaysInMonth(year, month) && day >= 1 ? new DateOnly(year, month, day) : null;
    }

    /// <summary>Datum in der Schreibweise der DFS, z. B. "17 SEP 2026" wie in "Effective: 17 SEP 2026".</summary>
    public static string FormatDate(DateOnly date) => $"{date.Day:00} {Months[date.Month - 1]} {date.Year}";

    private static readonly string[] Months = ["JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC"];

    private static string ParsePermalink(string html)
    {
        Match match = PermalinkRegex().Match(html);
        if (!match.Success)
        {
            throw new DfsPageException("Die Seite enthält keinen Permalink.");
        }
        string permalink = match.Groups["permalink"].Value;
        return permalink[(permalink.LastIndexOf('/') + 1)..];
    }

    private static DfsChartEntry? ParseDocumentItem(string item)
    {
        string? href = AnchorTagRegex().Matches(item)
            .Select(tag => tag.Value)
            .Where(tag => HasClass(tag, "document-link"))
            .Select(tag => Attribute(tag, "href"))
            .FirstOrDefault(value => value != null);

        string? name = null;
        foreach (Match span in SpanRegex().Matches(item))
        {
            string attributes = span.Groups["attributes"].Value;
            if (HasClass(attributes, "document-name") && Attribute(attributes, "lang") == "de")
            {
                name = WebUtility.HtmlDecode(span.Groups["text"].Value).Trim();
                break;
            }
        }

        if (href == null || string.IsNullOrEmpty(name))
        {
            return null;
        }

        string fileName = href[(href.LastIndexOfAny(['/', '\\']) + 1)..];
        string hash = fileName.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ? fileName[..^5] : fileName;

        byte[] preview = ImageTagRegex().Matches(item)
            .Select(tag => DecodePng(Attribute(tag.Value, "src")))
            .FirstOrDefault(data => data != null) ?? [];

        return new DfsChartEntry(name, hash, href, preview);
    }

    /// <summary>Text des <c>&lt;span lang="de"&gt;</c> innerhalb eines Elements.</summary>
    private static string? GermanSpanText(string content)
    {
        foreach (Match span in SpanRegex().Matches(content))
        {
            if (Attribute(span.Groups["attributes"].Value, "lang") == "de")
            {
                return WebUtility.HtmlDecode(span.Groups["text"].Value).Trim();
            }
        }
        return null;
    }

    private static byte[]? DecodePng(string? src)
    {
        if (src == null || !src.StartsWith(PngDataPrefix, StringComparison.Ordinal))
        {
            return null;
        }
        try
        {
            return Convert.FromBase64String(src[PngDataPrefix.Length..]);
        }
        catch (FormatException)
        {
            throw new DfsPageException("Ein Bild in der Seite ist beschädigt. Wurde die Seite vollständig gespeichert?");
        }
    }

    private static bool HasClass(string tag, string className) =>
        Attribute(tag, "class")?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(className) == true;

    /// <summary>Wert eines Attributs in einem Tag, in doppelten oder einfachen Anführungszeichen.</summary>
    private static string? Attribute(string tag, string name)
    {
        Match match = Regex.Match(tag, @"\s" + Regex.Escape(name) + @"\s*=\s*(?:""(?<value>[^""]*)""|'(?<value>[^']*)')", RegexOptions.IgnoreCase);
        return match.Success ? WebUtility.HtmlDecode(match.Groups["value"].Value) : null;
    }

    [GeneratedRegex(@"<a\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex AnchorTagRegex();

    [GeneratedRegex(@"<img\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ImageTagRegex();

    [GeneratedRegex(@"<span\b[^>]*\bclass\s*=\s*""expand-header""[^>]*>\s*Effective:\s*</span>\s*(?<date>\d{1,2}\s+[A-Z]{3}\s+\d{4})")]
    private static partial Regex EffectiveRegex();

    [GeneratedRegex(@"<img\b[^>]*\bid\s*=\s*""imgAIP""[^>]*>")]
    private static partial Regex ChartImageTagRegex();

    [GeneratedRegex(@"<li\b[^>]*\bclass\s*=\s*""document-item""[^>]*>(?<content>.*?)</li>", RegexOptions.Singleline)]
    private static partial Regex DocumentItemRegex();

    [GeneratedRegex(@"const\s+myPermalink\s*=\s*[""'](?<permalink>[^""']+)[""']")]
    private static partial Regex PermalinkRegex();

    [GeneratedRegex(@"const\s+myURL\s*=\s*[""'][^/""']*/(?<hash>[^/""']+)/")]
    private static partial Regex MyUrlRegex();

    [GeneratedRegex(@"<div\b[^>]*\bclass\s*=\s*""headlineText left""[^>]*>(?<content>.*?)</div>", RegexOptions.Singleline)]
    private static partial Regex HeadlineLeftRegex();

    [GeneratedRegex(@"<div\b[^>]*\bclass\s*=\s*""headlineText float-start""[^>]*>(?<text>[^<]*)</div>")]
    private static partial Regex HeadlineStartRegex();

    [GeneratedRegex(@"<div\b[^>]*\bclass\s*=\s*""headlineText float-end""[^>]*>(?<text>[^<]*)</div>")]
    private static partial Regex HeadlineEndRegex();

    [GeneratedRegex(@"<span\b(?<attributes>[^>]*)>(?<text>[^<]*)</span>")]
    private static partial Regex SpanRegex();

    [GeneratedRegex(@"^(?<name>.+)\s(?<icao>[A-Z0-9]{4})$")]
    private static partial Regex NameAndIcaoRegex();

    [GeneratedRegex(@"(?<day>\d{1,2})\s+(?<month>JAN|FEB|MAR|APR|MAY|JUN|JUL|AUG|SEP|OCT|NOV|DEC)\s+(?<year>\d{4})")]
    private static partial Regex DateRegex();
}
