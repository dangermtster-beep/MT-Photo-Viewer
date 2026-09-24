using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using MT.PhotoViewer.Helpers;
using MT.PhotoViewer.Services;

namespace MT.PhotoViewer.Views;

public partial class PrintWindow : Window
{
    /// <summary>Kağıt boyutları 96 DPI cinsinden (önizleme için).</summary>
    private static readonly Dictionary<string, Size> PaperSizes = new()
    {
        ["A4"] = new Size(794, 1123),
        ["A3"] = new Size(1123, 1587),
        ["Letter"] = new Size(816, 1056)
    };

    private readonly BitmapSource _image;
    private readonly PrintService _print;
    private readonly string _fileName;

    public PrintWindow(BitmapSource image, string fileName, PrintService print)
    {
        InitializeComponent();

        _image = image;
        _fileName = fileName;
        _print = print;

        PreviewImage.Source = image;
        Title = $"Yazdır — {fileName}";

        LoadPrinters();

        PaperCombo.SelectionChanged += (_, _) => UpdatePreview();
        PortraitOption.Checked += (_, _) => UpdatePreview();
        LandscapeOption.Checked += (_, _) => UpdatePreview();
        FitOption.Checked += (_, _) => UpdatePreview();
        ActualOption.Checked += (_, _) => UpdatePreview();
        CenterOption.Checked += (_, _) => UpdatePreview();
        CenterOption.Unchecked += (_, _) => UpdatePreview();
        MarginSlider.ValueChanged += (_, _) => UpdatePreview();

        PrintButton.Click += OnPrint;

        Loaded += (_, _) => UpdatePreview();
    }

    private void LoadPrinters()
    {
        IReadOnlyList<string> printers = PrintService.GetPrinters();

        foreach (string printer in printers)
            PrinterCombo.Items.Add(printer);

        string? defaultPrinter = PrintService.GetDefaultPrinter();

        PrinterCombo.SelectedItem = defaultPrinter is not null && printers.Contains(defaultPrinter)
            ? defaultPrinter
            : printers.FirstOrDefault();

        if (printers.Count == 0)
        {
            PrinterCombo.IsEnabled = false;
            PrintButton.IsEnabled = false;
        }
    }

    private PrintOptions BuildOptions() => new()
    {
        PrinterName = PrinterCombo.SelectedItem as string,
        PaperSize = (PaperCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "A4",
        Landscape = LandscapeOption.IsChecked == true,
        ScaleMode = ActualOption.IsChecked == true ? PrintScaleMode.ActualSize : PrintScaleMode.FitToPage,
        Center = CenterOption.IsChecked == true,
        MarginMm = MarginSlider.Value,
        Copies = (int)CopiesSlider.Value
    };

    private void UpdatePreview()
    {
        PrintOptions options = BuildOptions();

        Size page = PaperSizes.TryGetValue(options.PaperSize, out Size size) ? size : PaperSizes["A4"];
        if (options.Landscape)
            page = new Size(page.Height, page.Width);

        PreviewCanvas.Width = page.Width;
        PreviewCanvas.Height = page.Height;

        Rect target = PrintService.ComputeLayout(
            new Size(_image.PixelWidth, _image.PixelHeight), page, options);

        Canvas.SetLeft(PreviewImage, target.X);
        Canvas.SetTop(PreviewImage, target.Y);
        PreviewImage.Width = target.Width;
        PreviewImage.Height = target.Height;
    }

    private void OnPrint(object sender, RoutedEventArgs e)
    {
        CommandGuard.Run(() =>
        {
            _print.Print(_image, BuildOptions());
            DialogResult = true;
        }, "Yazdırma tamamlanamadı.");
    }
}
