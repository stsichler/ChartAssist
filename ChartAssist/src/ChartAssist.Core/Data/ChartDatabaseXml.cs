using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace ChartAssist.Core.Data;

/// <summary>
/// Liest und schreibt <c>.ChartButler.xml</c> exakt im Format von <c>DataSet.WriteXml</c> aus ChartButlerCS
/// (TECHNISCHE-BASIS 4.3): ohne Namespace, Tabellen in der Reihenfolge AFCharts, Airfields, AIP, Updates,
/// ChartButler, fehlende Werte als fehlendes Element, 2 Leerzeichen Einrückung, immer CRLF, UTF-8 ohne BOM.
/// </summary>
public static class ChartDatabaseXml
{
    public const string FileName = ".ChartButler.xml";

    private const string Declaration = "<?xml version=\"1.0\" standalone=\"yes\"?>";

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Liest die Datenbank. Akzeptiert auch LF (von ChartButlerCS unter Mono geschrieben).</summary>
    /// <exception cref="InvalidDataException">Die Datei ist kein gültiges .ChartButler.xml.</exception>
    public static ChartDatabase Read(Stream stream)
    {
        XDocument document;
        try
        {
            document = XDocument.Load(stream);
        }
        catch (XmlException e)
        {
            throw new InvalidDataException("Die Karten-Datenbank ist kein gültiges XML: " + e.Message, e);
        }

        XElement root = document.Root!;
        if (root.Name.LocalName != "ChartButlerDataSet")
        {
            throw new InvalidDataException("Unbekanntes Wurzelelement: " + root.Name);
        }

        var database = new ChartDatabase();
        var charts = new List<Chart>();
        foreach (XElement row in root.Elements())
        {
            switch (row.Name.LocalName)
            {
                case "AFCharts":
                    charts.Add(ReadChart(row));
                    break;
                case "Airfields":
                    database.Airfields.Add(new Airfield
                    {
                        Icao = Required(row, "ICAO"),
                        Name = Required(row, "AFname"),
                        LastUpdate = OptionalDate(row, "LastUpdate"),
                    });
                    break;
                case "AIP":
                    database.AipLastUpdate = OptionalDate(row, "LastUpdate");
                    break;
                case "Updates":
                    database.Updates.Add(ParseDate(Required(row, "Date")));
                    break;
                case "ChartButler":
                    database.Version = Optional(row, "Version");
                    database.DataSource = Optional(row, "DataSource");
                    break;
                default:
                    // Das Schema von ChartButlerCS enthält keine weiteren Tabellen
                    break;
            }
        }

        CheckUnique(database.Airfields.Select(a => a.Icao), "Flugplatz");
        CheckUnique(charts.Select(c => c.Name), "Karte");
        CheckUnique(database.Updates, "Aktualisierung");

        foreach (Chart chart in charts)
        {
            if (database.FindAirfield(chart.Icao) != null)
            {
                database.Charts.Add(chart);
            }
            else
            {
                Debug.WriteLine($"Karte ohne Flugplatz ignoriert: {chart.Icao} {chart.Name}");
            }
        }
        return database;
    }

    public static ChartDatabase Read(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Read(stream);
    }

    public static void Write(ChartDatabase database, Stream stream)
    {
        var settings = new XmlWriterSettings
        {
            Encoding = Utf8NoBom,
            Indent = true,
            IndentChars = "  ",
            NewLineChars = "\r\n",
            NewLineHandling = NewLineHandling.Replace,
            OmitXmlDeclaration = true,
        };

        // Die Deklaration von DataSet.WriteXml hat keine encoding-Angabe, XmlWriter würde eine schreiben
        byte[] declaration = Utf8NoBom.GetBytes(Declaration + "\r\n");
        stream.Write(declaration);

        using XmlWriter writer = XmlWriter.Create(stream, settings);
        writer.WriteStartElement("ChartButlerDataSet");

        foreach (Chart chart in database.Charts)
        {
            writer.WriteStartElement("AFCharts");
            writer.WriteElementString("ICAO", chart.Icao);
            writer.WriteElementString("Cname", chart.Name);
            writer.WriteElementString("CreationDate", FormatDate(chart.CreationDate));
            if (chart.AirfieldPermalink != null)
            {
                string crypt = chart.ServerName == null ? chart.AirfieldPermalink : chart.AirfieldPermalink + "#" + chart.ServerName;
                writer.WriteElementString("Crypt", crypt);
            }
            WriteOptionalDate(writer, "LastUpdate", chart.LastUpdate);
            writer.WriteEndElement();
        }

        foreach (Airfield airfield in database.Airfields)
        {
            writer.WriteStartElement("Airfields");
            writer.WriteElementString("ICAO", airfield.Icao);
            writer.WriteElementString("AFname", airfield.Name);
            WriteOptionalDate(writer, "LastUpdate", airfield.LastUpdate);
            writer.WriteEndElement();
        }

        if (database.AipLastUpdate != null)
        {
            writer.WriteStartElement("AIP");
            WriteOptionalDate(writer, "LastUpdate", database.AipLastUpdate);
            writer.WriteEndElement();
        }

        foreach (DateOnly update in database.Updates)
        {
            writer.WriteStartElement("Updates");
            writer.WriteElementString("Date", FormatDate(update));
            writer.WriteEndElement();
        }

        if (database.Version != null || database.DataSource != null)
        {
            writer.WriteStartElement("ChartButler");
            if (database.Version != null)
            {
                writer.WriteElementString("Version", database.Version);
            }
            if (database.DataSource != null)
            {
                writer.WriteElementString("DataSource", database.DataSource);
            }
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    }

    public static byte[] ToBytes(ChartDatabase database)
    {
        using var stream = new MemoryStream();
        Write(database, stream);
        return stream.ToArray();
    }

    /// <summary>
    /// Kalendertag als xs:dateTime um Mitternacht mit dem deutschen Offset dieses Tages, z. B.
    /// "2026-08-20T00:00:00+02:00". So schreibt ChartButlerCS auf einem deutschen Rechner. Die feste
    /// Zeitzone macht die Datei unabhängig von der Zeitzone des Rechners.
    /// </summary>
    public static string FormatDate(DateOnly date)
    {
        DateTime midnight = date.ToDateTime(TimeOnly.MinValue);
        TimeSpan offset = GermanTime.Zone.GetUtcOffset(midnight);
        char sign = offset < TimeSpan.Zero ? '-' : '+';
        return midnight.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture)
            + sign + offset.Duration().ToString("hh\\:mm", CultureInfo.InvariantCulture);
    }

    /// <summary>Liest ein xs:dateTime und liefert den Kalendertag in deutscher Zeit.</summary>
    public static DateOnly ParseDate(string value)
    {
        DateTimeOffset parsed;
        try
        {
            parsed = XmlConvert.ToDateTimeOffset(value.Trim());
        }
        catch (FormatException e)
        {
            throw new InvalidDataException("Ungültiges Datum in der Karten-Datenbank: " + value, e);
        }
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(parsed, GermanTime.Zone).DateTime);
    }

    private static Chart ReadChart(XElement row)
    {
        var chart = new Chart
        {
            Icao = Required(row, "ICAO"),
            Name = Required(row, "Cname"),
            CreationDate = ParseDate(Required(row, "CreationDate")),
            LastUpdate = OptionalDate(row, "LastUpdate"),
        };
        if (Optional(row, "Crypt") is string crypt)
        {
            int separator = crypt.IndexOf('#', StringComparison.Ordinal);
            chart.AirfieldPermalink = separator < 0 ? crypt : crypt[..separator];
            chart.ServerName = separator < 0 ? null : crypt[(separator + 1)..];
        }
        return chart;
    }

    private static string Required(XElement row, string name) =>
        Optional(row, name) ?? throw new InvalidDataException($"In {row.Name.LocalName} fehlt {name}.");

    private static string? Optional(XElement row, string name) => row.Element(name)?.Value;

    private static DateOnly? OptionalDate(XElement row, string name) =>
        Optional(row, name) is string value ? ParseDate(value) : null;

    private static void WriteOptionalDate(XmlWriter writer, string name, DateOnly? date)
    {
        if (date is DateOnly value)
        {
            writer.WriteElementString(name, FormatDate(value));
        }
    }

    private static void CheckUnique<T>(IEnumerable<T> keys, string what)
    {
        var seen = new HashSet<T>();
        foreach (T key in keys)
        {
            if (!seen.Add(key))
            {
                throw new InvalidDataException($"{what} doppelt in der Karten-Datenbank: {key}");
            }
        }
    }
}
