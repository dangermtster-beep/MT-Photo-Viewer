using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using MT.PhotoViewer.Helpers;
using MT.PhotoViewer.Views;

namespace MT.PhotoViewer.Services;

public sealed record CopyRequest(string FileName, string Folder)
{
    public string FullPath => Path.Combine(Folder, FileName);
}

/// <summary>
/// ViewModel'in ihtiyaç duyduğu tüm pencere/diyalog etkileşimleri.
/// ViewModel doğrudan WPF penceresi açmaz; her şey bu arayüz üzerinden gider.
/// </summary>
public interface IUiService
{
    Window? Owner { get; set; }

    string? PickImageFile();
    string? PickFolder(string? title = null);
    string? PickSaveAs(string suggestedName);

    bool Confirm(string message, string title = "MT Photo Viewer");
    void Info(string message);

    CopyRequest? ShowCopyDialog(string sourcePath);
    EmailPhotoSize? ShowEmailDialog(string sourcePath);
    void ShowPrintDialog(BitmapSource image, string fileName);
    void ShowSettings();
    AboutAction ShowAbout();
    void ShowMetadata(Models.ImageMetadata metadata);

    void ShowReleaseNotes(string header, string subtitle, IEnumerable<Models.ReleaseNote> notes);

    /// <summary>Güncelleme penceresi. Kullanıcı onaylayıp indirme başarılı olursa kurulum dosyasının yolu döner.</summary>
    string? ShowUpdate(Models.UpdateInfo update);
}

public sealed class UiService : IUiService
{
    private readonly SettingsService _settings;
    private readonly PrintService _print;
    private readonly UpdateService _updates;

    public UiService(SettingsService settings, PrintService print, UpdateService updates)
    {
        _settings = settings;
        _print = print;
        _updates = updates;
    }

    public Window? Owner { get; set; }

    public string? PickImageFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Fotoğraf Aç",
            Filter = ImageExtensions.OpenDialogFilter,
            CheckFileExists = true
        };

        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    public string? PickFolder(string? title = null)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title ?? "Klasör Aç",
            Multiselect = false
        };

        return dialog.ShowDialog(Owner) == true ? dialog.FolderName : null;
    }

    public string? PickSaveAs(string suggestedName)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Farklı Kaydet",
            FileName = suggestedName,
            Filter = ImageExtensions.OpenDialogFilter,
            AddExtension = true
        };

        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    public bool Confirm(string message, string title = "MT Photo Viewer") =>
        MessageBox.Show(Owner!, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question)
            == MessageBoxResult.Yes;

    public void Info(string message) =>
        MessageBox.Show(Owner!, message, "MT Photo Viewer", MessageBoxButton.OK, MessageBoxImage.Information);

    public CopyRequest? ShowCopyDialog(string sourcePath)
    {
        var window = new CopyDialog(sourcePath) { Owner = Owner };
        return window.ShowDialog() == true ? window.Result : null;
    }

    public EmailPhotoSize? ShowEmailDialog(string sourcePath)
    {
        var window = new EmailDialog(sourcePath) { Owner = Owner };
        return window.ShowDialog() == true ? window.SelectedSize : null;
    }

    public void ShowPrintDialog(BitmapSource image, string fileName)
    {
        var window = new PrintWindow(image, fileName, _print) { Owner = Owner };
        window.ShowDialog();
    }

    public void ShowSettings()
    {
        var window = new SettingsWindow(_settings) { Owner = Owner };
        window.ShowDialog();
    }

    public AboutAction ShowAbout()
    {
        var window = new AboutWindow(_updates.CurrentVersionText, _updates.IsConfigured) { Owner = Owner };
        window.ShowDialog();
        return window.RequestedAction;
    }

    public void ShowReleaseNotes(string header, string subtitle, IEnumerable<Models.ReleaseNote> notes)
    {
        var window = new ReleaseNotesWindow(header, subtitle, notes) { Owner = Owner };
        window.ShowDialog();
    }

    public string? ShowUpdate(Models.UpdateInfo update)
    {
        var window = new ReleaseNotesWindow(update, _updates) { Owner = Owner };
        return window.ShowDialog() == true ? window.DownloadedInstaller : null;
    }

    public void ShowMetadata(Models.ImageMetadata metadata)
    {
        var window = new PropertiesWindow(metadata) { Owner = Owner };
        window.ShowDialog();
    }
}
