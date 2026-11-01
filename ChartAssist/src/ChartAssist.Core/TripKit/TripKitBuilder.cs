using ChartAssist.Core.Data;
using SkiaSharp;

namespace ChartAssist.Core.TripKit;

/// <summary>Das erzeugte TripKit: PDF, JPEG-Vorschau der ersten Seite und Seitenzahl.</summary>
public sealed record TripKitDocument(byte[] Pdf, byte[] PreviewJpeg, int PageCount);

/// <summary>
/// Erzeugt das TripKit eines Flugplatzes (bisher <c>DFS_UpdateTripKitCharts</c>): alle Karten in einem PDF
/// aus A4-Querformat-Seiten. Hochformat-Karten füllen je eine Seitenhälfte, Querformat-Karten eine ganze Seite.
/// Die Karten werden nur skaliert und angeordnet, nicht verändert: nichts wird zugeschnitten, übermalt
/// oder eingeblendet.
/// </summary>
public static class TripKitBuilder
{
    public const int PreviewWidth = 840;
    public const int PreviewHeight = 594;

    private const int PreviewJpegQuality = 75;

    // A4 quer in Punkt (1/72 Zoll)
    private const float PageWidth = 297 / 25.4f * 72;
    private const float PageHeight = 210 / 25.4f * 72;

    private static readonly SKSamplingOptions Sampling = new(SKFilterMode.Linear, SKMipmapMode.Linear);

    public static string FileName(string icao) => icao + "_TripKit_Charts.pdf";

    /// <summary>Baut das TripKit aus den Kartenbildern (PNG) in der angegebenen Reihenfolge.</summary>
    /// <exception cref="ArgumentException">Keine Karten angegeben.</exception>
    /// <exception cref="InvalidDataException">Ein Kartenbild lässt sich nicht lesen.</exception>
    public static TripKitDocument Build(IReadOnlyList<byte[]> chartImages)
    {
        if (chartImages.Count == 0)
        {
            throw new ArgumentException("Ein TripKit braucht mindestens eine Karte.", nameof(chartImages));
        }

        var images = new List<SKImage>();
        try
        {
            foreach (byte[] imageData in chartImages)
            {
                images.Add(SKImage.FromEncodedData(imageData)
                    ?? throw new InvalidDataException("Eine Karte ist kein lesbares Bild."));
            }

            List<List<(SKImage Image, int Half)>> pages = Arrange(images);
            return new TripKitDocument(WritePdf(pages), WritePreview(pages[0]), pages.Count);
        }
        finally
        {
            images.ForEach(image => image.Dispose());
        }
    }

    /// <summary>
    /// Verteilt die Karten auf Seiten. Half: 0 = linke Hälfte, 1 = rechte Hälfte, -1 = ganze Seite (Querformat).
    /// </summary>
    private static List<List<(SKImage Image, int Half)>> Arrange(List<SKImage> images)
    {
        var pages = new List<List<(SKImage, int)>>();
        int halfPage = 0; // zählt A5-Hälften in A4-Querformat-Seiten
        foreach (SKImage image in images)
        {
            bool isLandscape = image.Width > image.Height;

            // Querformat beginnt immer auf einer neuen Seite, die rechte Hälfte bleibt dann leer
            if (isLandscape && halfPage % 2 != 0)
            {
                halfPage++;
            }
            if (halfPage % 2 == 0)
            {
                pages.Add([]);
            }

            pages[^1].Add((image, isLandscape ? -1 : halfPage % 2));
            halfPage += isLandscape ? 2 : 1;
        }
        return pages;
    }

    private static byte[] WritePdf(List<List<(SKImage Image, int Half)>> pages)
    {
        // Von Default ausgehen: Es setzt EncodingQuality auf 101 (verlustfrei). Ein leeres Metadaten-Objekt hätte 0,
        // dann würden die Karten als JPEG mit niedrigster Qualität eingebettet.
        SKDocumentPdfMetadata metadata = SKDocumentPdfMetadata.Default;
        metadata.Creator = "ChartAssist";

        using var pdf = new MemoryStream();
        using (SKDocument document = SKDocument.CreatePdf(pdf, metadata)
            ?? throw new InvalidOperationException("Das TripKit-PDF konnte nicht angelegt werden."))
        {
            foreach (List<(SKImage Image, int Half)> page in pages)
            {
                SKCanvas canvas = document.BeginPage(PageWidth, PageHeight);
                DrawPage(canvas, page, PageWidth, PageHeight);
                document.EndPage();
            }
            document.Close();
        }
        return pdf.ToArray();
    }

    private static byte[] WritePreview(List<(SKImage Image, int Half)> firstPage)
    {
        using var preview = new SKBitmap(new SKImageInfo(PreviewWidth, PreviewHeight, SKImageInfo.PlatformColorType, SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(preview))
        {
            canvas.Clear(SKColors.White);
            DrawPage(canvas, firstPage, PreviewWidth, PreviewHeight);
        }

        using SKImage previewImage = SKImage.FromBitmap(preview);
        using SKData jpeg = previewImage.Encode(SKEncodedImageFormat.Jpeg, PreviewJpegQuality)
            ?? throw new InvalidOperationException("Die TripKit-Vorschau konnte nicht als JPEG gespeichert werden.");
        return jpeg.ToArray();
    }

    /// <summary>Zeichnet die Karten einer Seite in ihre Hälfte bzw. auf die ganze Seite.</summary>
    private static void DrawPage(SKCanvas canvas, List<(SKImage Image, int Half)> page, float width, float height)
    {
        foreach ((SKImage image, int half) in page)
        {
            SKRect target = half < 0
                ? SKRect.Create(0, 0, width, height)
                : SKRect.Create(half * width / 2, 0, width / 2, height);
            canvas.DrawImage(image, target, Sampling);
        }
    }

    /// <summary>
    /// Erzeugt das TripKit eines Flugplatzes neu, schreibt PDF und Vorschau ins Kartenverzeichnis und pflegt den
    /// Eintrag in der Datenbank. <see cref="ChartDatabase.Updates"/> pflegt der Aufrufer.
    /// Gibt es keine Karten mehr, werden TripKit und Eintrag entfernt.
    /// </summary>
    /// <returns>Der TripKit-Eintrag oder null, wenn es keine Karten gibt.</returns>
    public static Chart? Update(ChartFolder folder, ChartDatabase database, Airfield airfield)
    {
        string name = FileName(airfield.Icao);
        Chart? tripKit = database.FindChart(name);

        var images = new List<byte[]>();
        DateOnly? lastUpdate = null;
        foreach (Chart chart in database.ChartsOf(airfield))
        {
            if (chart.Name == name || !chart.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (chart.LastUpdate is DateOnly date && (lastUpdate == null || date > lastUpdate))
            {
                lastUpdate = date;
            }
            string path = Utility.BuildChartPath(folder.Path, airfield, chart);
            if (File.Exists(path))
            {
                images.Add(File.ReadAllBytes(path));
            }
        }

        var entry = tripKit ?? new Chart { Icao = airfield.Icao, Name = name };
        string pdfPath = Utility.BuildChartPath(folder.Path, airfield, entry);
        string previewPath = Utility.BuildChartPreviewPath(folder.Path, airfield, entry, "jpg");

        if (images.Count == 0)
        {
            Utility.DeleteFile(pdfPath);
            Utility.DeleteFile(previewPath);
            if (tripKit != null)
            {
                database.Charts.Remove(tripKit);
            }
            return null;
        }

        TripKitDocument document = Build(images);
        Utility.WriteFileAtomic(pdfPath, document.Pdf);
        Utility.WriteFileAtomic(previewPath, document.PreviewJpeg, hidden: true);

        entry.CreationDate = DateOnly.FromDateTime(DateTime.Now);
        entry.LastUpdate = lastUpdate;
        if (tripKit == null)
        {
            database.Charts.Add(entry);
        }
        return entry;
    }
}
