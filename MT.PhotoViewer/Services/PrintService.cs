using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MT.PhotoViewer.Services;

public enum PrintScaleMode
{
    /// <summary>Sayfaya sığdır.</summary>
    FitToPage,

    /// <summary>Gerçek boyut (DPI'ya göre).</summary>
    ActualSize
}

public sealed class PrintOptions
{
    public string? PrinterName { get; set; }
    public string PaperSize { get; set; } = "A4";
    public bool Landscape { get; set; }
    public PrintScaleMode ScaleMode { get; set; } = PrintScaleMode.FitToPage;
    public bool Center { get; set; } = true;
    public double MarginMm { get; set; } = 10;
    public int Copies { get; set; } = 1;
}

/// <summary>Yazdırma (Guide §8).</summary>
public sealed class PrintService
{
    private const double MmToDip = 96.0 / 25.4;

    public static IReadOnlyList<string> GetPrinters()
    {
        try
        {
            using var server = new LocalPrintServer();
            return server.GetPrintQueues(new[]
                {
                    EnumeratedPrintQueueTypes.Local,
                    EnumeratedPrintQueueTypes.Connections
                })
                .Select(q => q.FullName)
                .Distinct()
                .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public static string? GetDefaultPrinter()
    {
        try
        {
            using var server = new LocalPrintServer();
            return server.DefaultPrintQueue?.FullName;
        }
        catch
        {
            return null;
        }
    }

    public static PageMediaSizeName ToMediaSize(string paperSize) => paperSize switch
    {
        "A3" => PageMediaSizeName.ISOA3,
        "Letter" => PageMediaSizeName.NorthAmericaLetter,
        _ => PageMediaSizeName.ISOA4
    };

    /// <summary>Verilen sayfa alanı için görselin yerleşimini hesaplar.</summary>
    public static Rect ComputeLayout(Size imageSize, Size pageSize, PrintOptions options)
    {
        double margin = options.MarginMm * MmToDip;
        double availableW = Math.Max(1, pageSize.Width - margin * 2);
        double availableH = Math.Max(1, pageSize.Height - margin * 2);

        double w, h;

        if (options.ScaleMode == PrintScaleMode.ActualSize)
        {
            w = imageSize.Width;
            h = imageSize.Height;

            // Gerçek boyut sayfayı aşıyorsa yine de kırpmadan küçült.
            double shrink = Math.Min(availableW / w, availableH / h);
            if (shrink < 1)
            {
                w *= shrink;
                h *= shrink;
            }
        }
        else
        {
            double scale = Math.Min(availableW / imageSize.Width, availableH / imageSize.Height);
            w = imageSize.Width * scale;
            h = imageSize.Height * scale;
        }

        double x = options.Center ? margin + (availableW - w) / 2 : margin;
        double y = options.Center ? margin + (availableH - h) / 2 : margin;

        return new Rect(x, y, w, h);
    }

    public void Print(BitmapSource image, PrintOptions options)
    {
        var dialog = new PrintDialog();

        if (!string.IsNullOrEmpty(options.PrinterName))
        {
            try
            {
                using var server = new LocalPrintServer();
                dialog.PrintQueue = server.GetPrintQueue(options.PrinterName);
            }
            catch
            {
                // Seçilen yazıcı bulunamazsa varsayılan kullanılır.
            }
        }

        PrintTicket ticket = dialog.PrintTicket ?? new PrintTicket();
        ticket.PageOrientation = options.Landscape ? PageOrientation.Landscape : PageOrientation.Portrait;
        ticket.PageMediaSize = new PageMediaSize(ToMediaSize(options.PaperSize));
        ticket.CopyCount = Math.Max(1, options.Copies);
        dialog.PrintTicket = ticket;

        var pageSize = new Size(dialog.PrintableAreaWidth, dialog.PrintableAreaHeight);
        Rect target = ComputeLayout(new Size(image.PixelWidth, image.PixelHeight), pageSize, options);

        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            dc.DrawImage(image, target);
        }

        dialog.PrintVisual(visual, "MT Photo Viewer");
    }

    /// <summary>Hızlı yazdır: varsayılan yazıcı, A4, sayfaya sığdır (Guide §8).</summary>
    public void QuickPrint(BitmapSource image)
    {
        Print(image, new PrintOptions
        {
            PrinterName = GetDefaultPrinter(),
            ScaleMode = PrintScaleMode.FitToPage,
            Center = true
        });
    }
}
