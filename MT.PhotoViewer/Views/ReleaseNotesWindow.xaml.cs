using System.IO;
using System.Windows;
using MT.PhotoViewer.Models;
using MT.PhotoViewer.Services;

namespace MT.PhotoViewer.Views;

/// <summary>
/// İki modlu pencere:
/// <list type="bullet">
/// <item>Yenilikler — sürüm notlarını gösterir.</item>
/// <item>Güncelleme — yeni sürümün notlarını gösterir, onaylanırsa kurulumu indirip doğrular.
/// İndirilen dosyanın yolu <see cref="DownloadedInstaller"/>'dadır.</item>
/// </list>
/// </summary>
public partial class ReleaseNotesWindow : Window
{
    private readonly UpdateInfo? _update;
    private readonly UpdateService? _service;
    private CancellationTokenSource? _downloadCts;

    /// <summary>Yenilikler modu.</summary>
    public ReleaseNotesWindow(string header, string subtitle, IEnumerable<ReleaseNote> notes)
    {
        InitializeComponent();

        Title = "Yenilikler";
        HeaderText.Text = header;
        SubtitleText.Text = subtitle;
        NotesList.ItemsSource = notes.ToList();
    }

    /// <summary>Güncelleme modu.</summary>
    public ReleaseNotesWindow(UpdateInfo update, UpdateService service)
        : this(
            $"Yeni sürüm hazır: v{update.Version.ToString(3)}",
            $"Kurulu sürüm: v{service.CurrentVersionText}" +
                (update.Size > 0 ? $"   •   İndirme boyutu: {MetadataService.FormatBytes(update.Size)}" : ""),
            update.NewNotes)
    {
        _update = update;
        _service = service;

        Title = "Güncelleme Mevcut";
        SecondaryButton.Content = "Daha Sonra";
        PrimaryButton.Visibility = Visibility.Visible;
    }

    /// <summary>Güncelleme modunda indirilip doğrulanan kurulum dosyası.</summary>
    public string? DownloadedInstaller { get; private set; }

    private async void PrimaryButton_Click(object sender, RoutedEventArgs e)
    {
        if (_update is null || _service is null) return;

        PrimaryButton.IsEnabled = false;
        SecondaryButton.Content = "İptal";
        ProgressPanel.Visibility = Visibility.Visible;
        DownloadProgress.IsIndeterminate = _update.Size <= 0;
        DownloadProgress.Value = 0;
        StatusText.Text = "Güncelleme indiriliyor…";

        _downloadCts = new CancellationTokenSource();
        var progress = new Progress<double>(p =>
        {
            DownloadProgress.Value = p;
            StatusText.Text = $"Güncelleme indiriliyor…  %{p * 100:0}";
        });

        try
        {
            DownloadedInstaller = await _service.DownloadAsync(_update, progress, _downloadCts.Token);
            StatusText.Text = "İndirme tamamlandı. Kurulum başlatılıyor…";
            DialogResult = true;
        }
        catch (OperationCanceledException)
        {
            ResetAfterFailure("İndirme iptal edildi.");
        }
        catch (Exception ex)
        {
            ResetAfterFailure(ex is InvalidDataException
                ? ex.Message
                : "Güncelleme indirilemedi. İnternet bağlantınızı kontrol edip tekrar deneyin.");
        }
        finally
        {
            _downloadCts?.Dispose();
            _downloadCts = null;
        }
    }

    private void ResetAfterFailure(string message)
    {
        StatusText.Text = message;
        DownloadProgress.IsIndeterminate = false;
        DownloadProgress.Value = 0;
        PrimaryButton.IsEnabled = true;
        PrimaryButton.Content = "Tekrar Dene";
        SecondaryButton.Content = "Daha Sonra";
    }

    private void SecondaryButton_Click(object sender, RoutedEventArgs e)
    {
        // İndirme sürerken bu düğme "İptal" işlevi görür.
        if (_downloadCts is not null)
        {
            _downloadCts.Cancel();
            return;
        }

        DialogResult = false;
    }

    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == System.Windows.Input.Key.Escape)
        {
            SecondaryButton_Click(this, e);
            e.Handled = true;
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _downloadCts?.Cancel();
        base.OnClosing(e);
    }
}
