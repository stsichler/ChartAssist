using ChartAssist.Core.Data;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using SkiaSharp;

namespace ChartAssist.Core.TripKit;

/// <summary>Das erzeugte TripKit: PDF, JPEG-Vorschau der ersten Seite und Seitenzahl.</summary>
public sealed record TripKitDocument(byte[] Pdf, byte[] PreviewJpeg, int PageCount);

/// <summary>
/// Erzeugt das TripKit eines Flugplatzes (bisher <c>DFS_UpdateTripKitCharts</c>): alle Karten in einem PDF
/// aus A4-Querformat-Seiten. Hochformat-Karten füllen je eine Seitenhälfte, Querformat-Karten eine ganze Seite.
/// Die Karten werden nur skaliert und angeordnet, nicht verändert (IMPORT-MODUS 2, Leitplanke 7).
/// </summary>
public static class TripKitBuilder
{
    public const int PreviewWidth = 840;
    public const int PreviewHeight = 594;

    private const int PreviewJpegQuality = 75;

    private static readonly XUnit PageWidth = XUnit.FromMillimeter(297);
    private static readonly XUnit PageHeight = XUnit.FromMillimeter(210);

    public static string FileName(string icao) => icao + "_TripKit_Charts.pdf";

    /// <summary>Baut das TripKit aus den Kartenbildern (PNG) in der angegebenen Reihenfolge.</summary>
    /// <exception cref="ArgumentException">Keine Karten angegeben.</exception>
    public static TripKitDocument Build(IReadOnlyList<byte[]> chartImages)
    {
        if (chartImages.Count == 0)
        {
            throw new ArgumentException("Ein TripKit braucht mindestens eine Karte.", nameof(chartImages));
        }

        using var document = new PdfDocument();
        using var preview = new SKBitmap(new SKImageInfo(PreviewWidth, PreviewHeight, SKImageInfo.PlatformColorType, SKAlphaType.Opaque));
        using var previewCanvas = new SKCanvas(preview);
        previewCanvas.Clear(SKColors.White);

        int halfPage = 0; // zählt A5-Hälften in A4-Querformat-Seiten
        foreach (byte[] imageData in chartImages)
        {
            using XImage image = XImage.FromStream(new MemoryStream(imageData, writable: false));
            bool isLandscape = image.PixelWidth > image.PixelHeight;

            // Querformat beginnt immer auf einer neuen Seite, die rechte Hälfte bleibt dann leer
            if (isLandscape && halfPage % 2 != 0)
            {
                halfPage++;
            }

            PdfPage page;
            if (halfPage % 2 == 0)
            {
                page = document.AddPage();
                page.Width = PageWidth;
                page.Height = PageHeight;
            }
            else
            {
                page = document.Pages[halfPage / 2];
            }

            double width = page.Width.Point;
            double height = page.Height.Point;
            using (XGraphics graphics = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append))
            {
                if (isLandscape)
                {
                    graphics.DrawImage(image, 0, 0, width, height);
                }
                else
                {
                    graphics.DrawImage(image, halfPage % 2 * width / 2, 0, width / 2, height);
                }
            }

            if (halfPage < 2)
            {
                DrawPreview(previewCanvas, imageData, isLandscape, halfPage % 2);
            }

            halfPage += isLandscape ? 2 : 1;
        }

        int pageCount = document.PageCount; // nach dem Speichern nicht mehr abrufbar
        using var pdf = new MemoryStream();
        document.Save(pdf, closeStream: false);

        using SKImage previewImage = SKImage.FromBitmap(preview);
        using SKData jpeg = previewImage.Encode(SKEncodedImageFormat.Jpeg, PreviewJpegQuality)
            ?? throw new InvalidOperationException("Die TripKit-Vorschau konnte nicht als JPEG gespeichert werden.");

        return new TripKitDocument(pdf.ToArray(), jpeg.ToArray(), pageCount);
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
            File.Delete(pdfPath);
            File.Delete(previewPath);
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

    private static void DrawPreview(SKCanvas canvas, byte[] imageData, bool isLandscape, int half)
    {
        using SKImage? image = SKImage.FromEncodedData(imageData);
        if (image == null)
        {
            return;
        }

        SKRect target = isLandscape
            ? new SKRect(0, 0, PreviewWidth, PreviewHeight)
            : SKRect.Create(half * PreviewWidth / 2f, 0, PreviewWidth / 2f, PreviewHeight);
        canvas.DrawImage(image, target, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
    }
}
