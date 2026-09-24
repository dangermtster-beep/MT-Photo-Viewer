using System.Windows;
using System.Windows.Controls;
using MT.PhotoViewer.Services;

namespace MT.PhotoViewer.Views;

public partial class EmailDialog : Window
{
    private readonly string _sourcePath;

    public EmailDialog(string sourcePath)
    {
        InitializeComponent();
        _sourcePath = sourcePath;

        foreach (RadioButton option in new[] { OriginalOption, LargeOption, MediumOption, SmallOption })
            option.Checked += (_, _) => UpdateEstimate();

        CreateButton.Click += (_, _) =>
        {
            SelectedSize = CurrentSelection();
            DialogResult = true;
        };

        UpdateEstimate();
    }

    public EmailPhotoSize? SelectedSize { get; private set; }

    private EmailPhotoSize CurrentSelection()
    {
        if (LargeOption.IsChecked == true) return EmailPhotoSize.Large;
        if (MediumOption.IsChecked == true) return EmailPhotoSize.Medium;
        if (SmallOption.IsChecked == true) return EmailPhotoSize.Small;
        return EmailPhotoSize.Original;
    }

    private void UpdateEstimate()
    {
        long bytes = EmailService.EstimateSize(_sourcePath, CurrentSelection());
        EstimateText.Text = $"Tahmini boyut: {MetadataService.FormatBytes(bytes)}";
    }
}
