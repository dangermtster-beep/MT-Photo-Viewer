using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MT.PhotoViewer.Helpers;
using MT.PhotoViewer.Models;
using MT.PhotoViewer.Services;

namespace MT.PhotoViewer.ViewModels;

/// <summary>Ana ViewModel (Guide §35, §36).</summary>
public partial class MainViewModel : ObservableObject
{
    private readonly ImageLoaderService _loader;
    private readonly FolderScannerService _scanner;
    private readonly MetadataService _metadata;
    private readonly ClipboardService _clipboard;
    private readonly PrintService _print;
    private readonly FileService _files;
    private readonly EmailService _email;
    private readonly WallpaperService _wallpaper;
    private readonly SettingsService _settings;
    private readonly IUiService _ui;
    private readonly UpdateService _updates;

    private readonly DispatcherTimer _slideshowTimer;

    private CancellationTokenSource? _loadCts;
    private bool _slideshowOwnsFullscreen;

    public MainViewModel(
        ImageLoaderService loader,
        FolderScannerService scanner,
        MetadataService metadata,
        ClipboardService clipboard,
        PrintService print,
        FileService files,
        EmailService email,
        WallpaperService wallpaper,
        SettingsService settings,
        IUiService ui,
        UpdateService updates)
    {
        _updates = updates;
        _loader = loader;
        _scanner = scanner;
        _metadata = metadata;
        _clipboard = clipboard;
        _print = print;
        _files = files;
        _email = email;
        _wallpaper = wallpaper;
        _settings = settings;
        _ui = ui;

        InfoPanel = new InfoPanelViewModel();
        _loader.CacheSize = settings.Current.CacheSize;
        _loader.PreviewMaxEdge = MonitorHelper.GetLargestMonitorEdge();

        _slideshowTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(Math.Clamp(settings.Current.SlideshowIntervalSeconds, 1, 60))
        };
        _slideshowTimer.Tick += OnSlideshowTick;

        _scanner.FolderChanged += OnFolderChanged;
    }

    #region State (Guide §35)

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    [NotifyPropertyChangedFor(nameof(HasImage))]
    private string? currentFilePath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImage))]
    private BitmapSource? currentImage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUpdateAvailable))]
    [NotifyPropertyChangedFor(nameof(UpdateBadgeText))]
    private UpdateInfo? availableUpdate;

    /// <summary>Ekrana sığdırılmış görünümde çizilen, ekran boyutundaki kopya.</summary>
    [ObservableProperty]
    private BitmapSource? currentPreview;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZoomText))]
    private double zoom = 1.0;

    [ObservableProperty]
    private int rotation;

    [ObservableProperty]
    private bool isFullscreen;

    [ObservableProperty]
    private bool isSlideshowRunning;

    [ObservableProperty]
    private bool isInfoPanelOpen;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PositionText))]
    private int currentIndex = -1;

    public ObservableCollection<string> FolderImages { get; } = new();

    public InfoPanelViewModel InfoPanel { get; }

    public AppSettings Settings => _settings.Current;

    public bool HasImage => CurrentImage is not null && CurrentFilePath is not null;

    /// <summary>Guide §42 — başlıkta yalnızca dosya adı yer alır.</summary>
    public string WindowTitle => string.IsNullOrEmpty(CurrentFilePath)
        ? "MT Photo Viewer"
        : $"{Path.GetFileName(CurrentFilePath)} — MT Photo Viewer";

    public string ZoomText => (Zoom * 100).ToString("0", CultureInfo.CurrentCulture) + "%";

    public string PositionText => FolderImages.Count > 1 && CurrentIndex >= 0
        ? $"{CurrentIndex + 1} / {FolderImages.Count}"
        : string.Empty;

    public bool CanGoPrevious => FolderImages.Count > 1;
    public bool CanGoNext => FolderImages.Count > 1;

    #endregion

    #region View bridge

    /// <summary>ZoomableImage üzerinde işlem isteyen olaylar — MainWindow bunlara abone olur.</summary>
    public event Action? FitRequested;
    public event Action? ActualSizeRequested;
    public event Action? ZoomInRequested;
    public event Action? ZoomOutRequested;
    public event Action? ResetViewRequested;
    public event Action? ExitRequested;

    /// <summary>
    /// Zoom değeri ZoomableImage'dan geldiğinde ViewModel'i günceller.
    /// Zoom tek yönlü akar (kontrol → ViewModel), bu yüzden geri besleme oluşmaz.
    /// </summary>
    public void ReportZoom(double value) => Zoom = value;

    #endregion

    #region Opening

    public async Task OpenPathAsync(string path)
    {
        try
        {
            if (System.IO.Directory.Exists(path))
            {
                await OpenFolderPathAsync(path).ConfigureAwait(true);
                return;
            }

            if (!File.Exists(path))
            {
                _ui.Info("Dosya bulunamadı.");
                return;
            }

            IndexFolderOf(path);
            await ShowAsync(path).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            CommandGuard.Report(ex, "Dosya açılamadı.");
        }
    }

    private async Task OpenFolderPathAsync(string folder)
    {
        IndexFolder(folder);

        if (FolderImages.Count == 0)
        {
            _ui.Info("Bu klasörde desteklenen bir görsel bulunamadı.");
            return;
        }

        await ShowAsync(FolderImages[0]).ConfigureAwait(true);
    }

    /// <summary>Guide §14 — açılan resmin klasörünü indexler.</summary>
    private void IndexFolderOf(string filePath)
    {
        string? folder = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(folder)) return;

        IndexFolder(folder);
    }

    private void IndexFolder(string folder)
    {
        IReadOnlyList<string> files = _scanner.Scan(folder);

        FolderImages.Clear();
        foreach (string file in files)
            FolderImages.Add(file);

        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(PositionText));

        _scanner.Watch(folder);
    }

    /// <summary>Guide §38 — yükleme UI thread'ini bloke etmez.</summary>
    private async Task ShowAsync(string path)
    {
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        CancellationToken token = _loadCts.Token;

        int index = FolderImages.IndexOf(path);
        if (index < 0)
        {
            FolderImages.Add(path);
            index = FolderImages.Count - 1;
        }

        CurrentIndex = index;

        LoadedImage? cached = _loader.TryGetCached(path);
        if (cached is null)
            IsLoading = true;

        try
        {
            LoadedImage loaded = cached ?? await _loader.LoadAsync(path, token).ConfigureAwait(true);
            if (token.IsCancellationRequested) return;

            BitmapSource image = loaded.Full;

            // Önizleme önce atanır: görüntüleyici Source değiştiğinde hangisini
            // çizeceğine karar verirken yeni önizlemeyi görmelidir.
            CurrentPreview = loaded.Preview;
            CurrentImage = image;
            CurrentFilePath = path;
            Rotation = 0;

            ResetViewRequested?.Invoke();
            UpdateCommandStates();

            _ = LoadMetadataAsync(path, image);
            PreloadNeighbors();
        }
        catch (OperationCanceledException)
        {
            // Yeni bir yükleme başladı.
        }
        catch (Exception ex)
        {
            CommandGuard.Report(ex, $"\"{Path.GetFileName(path)}\" açılamadı. Dosya bozuk veya format desteklenmiyor olabilir.");
        }
        finally
        {
            if (!token.IsCancellationRequested)
                IsLoading = false;
        }
    }

    private async Task LoadMetadataAsync(string path, BitmapSource image)
    {
        try
        {
            ImageMetadata meta = await _metadata
                .ReadAsync(path, image.PixelWidth, image.PixelHeight)
                .ConfigureAwait(true);

            if (CurrentFilePath == path)
                InfoPanel.Load(meta);
        }
        catch
        {
            // Metadata okunamazsa panel boş kalır.
        }
    }

    /// <summary>Guide §39 — önceki/sonraki görselleri arka planda hazırlar.</summary>
    private void PreloadNeighbors()
    {
        if (!_settings.Current.PreloadNeighbors || FolderImages.Count < 2) return;

        // İleri yönde iki, geri yönde bir görsel: art arda → basıldığında da
        // sıradaki resim hazır bekler.
        var targets = new List<string>(3);
        if (CurrentIndex + 1 < FolderImages.Count) targets.Add(FolderImages[CurrentIndex + 1]);
        if (CurrentIndex - 1 >= 0) targets.Add(FolderImages[CurrentIndex - 1]);
        if (CurrentIndex + 2 < FolderImages.Count) targets.Add(FolderImages[CurrentIndex + 2]);

        _loader.Preload(targets);
    }

    private void OnFolderChanged()
    {
        // FileSystemWatcher olayları UI thread dışında gelir.
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            string? current = CurrentFilePath;
            if (string.IsNullOrEmpty(current)) return;

            string? folder = Path.GetDirectoryName(current);
            if (string.IsNullOrEmpty(folder)) return;

            IReadOnlyList<string> files = _scanner.Scan(folder);

            FolderImages.Clear();
            foreach (string file in files)
                FolderImages.Add(file);

            CurrentIndex = FolderImages.IndexOf(current);
            OnPropertyChanged(nameof(PositionText));
            OnPropertyChanged(nameof(CanGoPrevious));
            OnPropertyChanged(nameof(CanGoNext));
            UpdateCommandStates();
        });
    }

    private void UpdateCommandStates()
    {
        PreviousCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
        CopyCommand.NotifyCanExecuteChanged();
        SaveCopyCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
        PrintCommand.NotifyCanExecuteChanged();
        QuickPrintCommand.NotifyCanExecuteChanged();
        EmailCommand.NotifyCanExecuteChanged();
        RotateLeftCommand.NotifyCanExecuteChanged();
        RotateRightCommand.NotifyCanExecuteChanged();
        ShowPropertiesCommand.NotifyCanExecuteChanged();
        OpenFileLocationCommand.NotifyCanExecuteChanged();
        OpenWithPaintCommand.NotifyCanExecuteChanged();
        OpenWithDefaultCommand.NotifyCanExecuteChanged();
        OpenWithDialogCommand.NotifyCanExecuteChanged();
        SetWallpaperCommand.NotifyCanExecuteChanged();
        CopyToFolderCommand.NotifyCanExecuteChanged();
        ToggleInfoPanelCommand.NotifyCanExecuteChanged();
        ToggleSlideshowCommand.NotifyCanExecuteChanged();
    }

    #endregion

    #region Commands — Aç (Guide §11)

    [RelayCommand]
    private async Task OpenFileAsync()
    {
        string? path = _ui.PickImageFile();
        if (path is null) return;

        await OpenPathAsync(path).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task OpenFolderAsync()
    {
        string? folder = _ui.PickFolder();
        if (folder is null) return;

        await OpenFolderPathAsync(folder).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(HasImage))]
    private void OpenWithPaint() =>
        CommandGuard.Run(() => _files.OpenWithPaint(CurrentFilePath!), "Paint açılamadı.");

    [RelayCommand(CanExecute = nameof(HasImage))]
    private void OpenWithDefault() =>
        CommandGuard.Run(() => _files.OpenWithDefault(CurrentFilePath!), "Varsayılan uygulama açılamadı.");

    [RelayCommand(CanExecute = nameof(HasImage))]
    private void OpenWithDialog() =>
        CommandGuard.Run(() => _files.OpenWithDialog(CurrentFilePath!), "Uygulama seçme penceresi açılamadı.");

    [RelayCommand(CanExecute = nameof(HasImage))]
    private void OpenFileLocation() =>
        CommandGuard.Run(() => _files.OpenFileLocation(CurrentFilePath!), "Dosya konumu açılamadı.");

    #endregion

    #region Commands — Navigasyon (Guide §13)

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private async Task PreviousAsync()
    {
        if (FolderImages.Count == 0) return;

        int index = CurrentIndex - 1;
        if (index < 0) index = FolderImages.Count - 1;   // başa dönerken sona sar

        await ShowAsync(FolderImages[index]).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private async Task NextAsync()
    {
        if (FolderImages.Count == 0) return;

        int index = CurrentIndex + 1;
        if (index >= FolderImages.Count) index = 0;

        await ShowAsync(FolderImages[index]).ConfigureAwait(true);
    }

    #endregion

    #region Commands — Zoom / görünüm (Guide §16–§21)

    [RelayCommand] private void ZoomIn() => ZoomInRequested?.Invoke();
    [RelayCommand] private void ZoomOut() => ZoomOutRequested?.Invoke();
    [RelayCommand] private void FitToScreen() => FitRequested?.Invoke();
    [RelayCommand] private void ActualSize() => ActualSizeRequested?.Invoke();

    [RelayCommand(CanExecute = nameof(HasImage))]
    private void RotateLeft() => Rotation = ((Rotation - 90) % 360 + 360) % 360;

    [RelayCommand(CanExecute = nameof(HasImage))]
    private void RotateRight() => Rotation = (Rotation + 90) % 360;

    [RelayCommand]
    private void ToggleFullscreen() => IsFullscreen = !IsFullscreen;

    [RelayCommand]
    private void ExitFullscreen() => IsFullscreen = false;

    /// <summary>Klasördeki fotoğrafları otomatik olarak sırayla gösterir.</summary>
    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void ToggleSlideshow()
    {
        if (IsSlideshowRunning) StopSlideshow();
        else StartSlideshow();
    }

    private void StartSlideshow()
    {
        if (!HasImage || FolderImages.Count < 2) return;

        _slideshowTimer.Interval = TimeSpan.FromSeconds(
            Math.Clamp(_settings.Current.SlideshowIntervalSeconds, 1, 60));

        _slideshowTimer.Start();
        IsSlideshowRunning = true;

        // Windows Fotoğraf Görüntüleyicisi'ndeki gibi tam ekranda başlar.
        if (!IsFullscreen)
        {
            _slideshowOwnsFullscreen = true;
            IsFullscreen = true;
        }
    }

    private void StopSlideshow()
    {
        _slideshowTimer.Stop();
        IsSlideshowRunning = false;

        if (!_slideshowOwnsFullscreen) return;

        _slideshowOwnsFullscreen = false;
        IsFullscreen = false;
    }

    private void OnSlideshowTick(object? sender, EventArgs e)
    {
        if (FolderImages.Count < 2)
        {
            StopSlideshow();
            return;
        }

        if (NextCommand.CanExecute(null))
            NextCommand.Execute(null);
    }

    /// <summary>Tam ekrandan çıkmak (Esc) slayt gösterisini de durdurur.</summary>
    partial void OnIsFullscreenChanged(bool value)
    {
        if (value || !IsSlideshowRunning) return;

        _slideshowOwnsFullscreen = false;
        StopSlideshow();
    }

    [RelayCommand(CanExecute = nameof(HasImage))]
    private void ToggleInfoPanel() => IsInfoPanelOpen = !IsInfoPanelOpen;

    #endregion

    #region Commands — Dosya (Guide §7)

    [RelayCommand(CanExecute = nameof(HasImage))]
    private void Copy() =>
        CommandGuard.Run(
            () => _clipboard.CopyImage(CurrentImage!, CurrentFilePath),
            "Fotoğraf panoya kopyalanamadı.");

    /// <summary>Guide §7.2 — Kopya Oluştur.</summary>
    [RelayCommand(CanExecute = nameof(HasImage))]
    private void SaveCopy()
    {
        CopyRequest? request = _ui.ShowCopyDialog(CurrentFilePath!);
        if (request is null) return;

        CommandGuard.Run(() =>
        {
            _files.SaveCopy(CurrentFilePath!, request.FullPath);
            OnFolderChanged();
        }, "Kopya oluşturulamadı.");
    }

    /// <summary>Guide §7.1, §40 — Geri Dönüşüm Kutusu'na taşır ve komşu resme geçer.</summary>
    [RelayCommand(CanExecute = nameof(HasImage))]
    private async Task DeleteAsync()
    {
        string path = CurrentFilePath!;

        if (!_ui.Confirm(
                $"\"{Path.GetFileName(path)}\" dosyasını Geri Dönüşüm Kutusu'na taşımak istiyor musunuz?",
                "Sil"))
            return;

        bool deleted = false;
        CommandGuard.Run(() => deleted = _files.MoveToRecycleBin(path), "Dosya silinemedi.");
        if (!deleted) return;

        _loader.Remove(path);

        int index = FolderImages.IndexOf(path);
        if (index >= 0) FolderImages.Remove(path);

        OnPropertyChanged(nameof(PositionText));
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));

        if (FolderImages.Count == 0)
        {
            CloseCurrent();
            return;
        }

        int next = Math.Min(index < 0 ? 0 : index, FolderImages.Count - 1);
        await ShowAsync(FolderImages[next]).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(HasImage))]
    private void ShowProperties()
    {
        // Windows'un kendi özellikler penceresi; açılamazsa uygulamanın bilgi penceresi.
        bool shown = false;
        CommandGuard.Run(() => shown = _files.ShowFileProperties(CurrentFilePath!), "Özellikler açılamadı.");

        if (!shown && InfoPanel.Metadata is not null)
            _ui.ShowMetadata(InfoPanel.Metadata);
    }

    [RelayCommand(CanExecute = nameof(HasImage))]
    private void SaveAs()
    {
        string? target = _ui.PickSaveAs(Path.GetFileName(CurrentFilePath!));
        if (target is null) return;

        CommandGuard.Run(() => _files.SaveCopy(CurrentFilePath!, target), "Dosya kaydedilemedi.");
    }

    [RelayCommand]
    private void Exit() => ExitRequested?.Invoke();

    #endregion

    #region Commands — Yazdır / E-posta / Yaz (Guide §8, §9, §10)

    [RelayCommand(CanExecute = nameof(HasImage))]
    private void Print() => _ui.ShowPrintDialog(CurrentImage!, Path.GetFileName(CurrentFilePath!));

    [RelayCommand(CanExecute = nameof(HasImage))]
    private void QuickPrint() =>
        CommandGuard.Run(() => _print.QuickPrint(CurrentImage!), "Yazdırma başlatılamadı.");

    [RelayCommand(CanExecute = nameof(HasImage))]
    private void Email()
    {
        EmailPhotoSize? size = _ui.ShowEmailDialog(CurrentFilePath!);
        if (size is null) return;

        CommandGuard.Run(() =>
        {
            string attachment = _email.PrepareAttachment(CurrentFilePath!, size.Value);
            if (!_email.CreateDraft(attachment, Path.GetFileName(CurrentFilePath!)))
                _ui.Info("Varsayılan e-posta istemcisi bulunamadı.");
        }, "E-posta oluşturulamadı.");
    }

    /// <summary>Guide §10 — Klasöre / USB belleğe kopyala.</summary>
    [RelayCommand(CanExecute = nameof(HasImage))]
    private void CopyToFolder(string? targetFolder)
    {
        targetFolder ??= _ui.PickFolder("Hedef Klasörü Seçin");
        if (string.IsNullOrEmpty(targetFolder)) return;

        CommandGuard.Run(() =>
        {
            string result = _files.CopyToFolder(CurrentFilePath!, targetFolder);
            _ui.Info($"Kopyalandı:\n{result}");
        }, "Dosya kopyalanamadı.");
    }

    [RelayCommand(CanExecute = nameof(HasImage))]
    private void SetWallpaper() =>
        CommandGuard.Run(() => _wallpaper.SetWallpaper(CurrentFilePath!), "Duvar kağıdı ayarlanamadı.");

    #endregion

    #region Commands — Ayarlar / Hakkında

    [RelayCommand]
    private void ShowSettings()
    {
        _ui.ShowSettings();
        _loader.CacheSize = _settings.Current.CacheSize;
        OnPropertyChanged(nameof(Settings));
    }

    [RelayCommand]
    private async Task ShowAbout()
    {
        switch (_ui.ShowAbout())
        {
            case Views.AboutAction.ShowReleaseNotes:
                ShowReleaseNotes();
                break;
            case Views.AboutAction.CheckForUpdates:
                await CheckForUpdatesAsync();
                break;
        }
    }

    #endregion

    #region Sürüm ve güncelleme

    public string CurrentVersionText => _updates.CurrentVersionText;

    public bool UpdatesConfigured => _updates.IsConfigured;

    public bool IsUpdateAvailable => AvailableUpdate is not null;

    public string UpdateBadgeText =>
        AvailableUpdate is null ? "" : $"Güncelleme var · v{AvailableUpdate.Version.ToString(3)}";

    [RelayCommand]
    private void ShowReleaseNotes() =>
        _ui.ShowReleaseNotes("Sürüm Notları", $"Kurulu sürüm: v{CurrentVersionText}", _updates.ReleaseNotes);

    /// <summary>Menüden / Hakkında'dan elle kontrol: sonucu her durumda kullanıcıya bildirir.</summary>
    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        if (!_updates.IsConfigured)
        {
            _ui.Info("Güncelleme sunucusu yapılandırılmamış.");
            return;
        }

        try
        {
            AvailableUpdate = await _updates.CheckAsync().ConfigureAwait(true);
        }
        catch
        {
            _ui.Info("Güncelleme sunucusuna ulaşılamadı. İnternet bağlantınızı kontrol edip tekrar deneyin.");
            return;
        }

        if (AvailableUpdate is null)
            _ui.Info($"MT Photo Viewer güncel.\n\nKurulu sürüm: v{CurrentVersionText}");
        else
            InstallUpdate();
    }

    /// <summary>Başlık çubuğundaki "Güncelleme var" düğmesi.</summary>
    [RelayCommand]
    private void InstallUpdate()
    {
        if (AvailableUpdate is null) return;

        string? installer = _ui.ShowUpdate(AvailableUpdate);
        if (installer is null) return;

        // Kurulum, uygulama açıkken çalışmayı reddeder (AppMutex); kapanmadan önce bırak.
        App.ReleaseInstanceMutex();

        try
        {
            UpdateService.LaunchInstaller(installer);
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // Kullanıcı yönetici izni (UAC) istemini reddetti.
            App.CreateInstanceMutex();
            _ui.Info("Güncelleme için yönetici izni gerekiyor. Kurulum başlatılmadı.");
            return;
        }

        Application.Current.Shutdown();
    }

    /// <summary>
    /// Pencere açıldıktan sonra bir kez çalışır: güncelleme sonrası ilk açılışta
    /// "Yenilikler"i gösterir, ayar açıksa arka planda sessizce yeni sürüm arar.
    /// </summary>
    public async Task RunStartupChecksAsync()
    {
        // Pencere ve varsa açılan fotoğraf önce ekrana gelsin.
        await Task.Delay(700).ConfigureAwait(true);
        ShowWhatsNewIfUpdated();

        if (!_settings.Current.CheckForUpdates || !_updates.IsConfigured)
            return;

        await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(true);

        try
        {
            AvailableUpdate = await _updates.CheckAsync().ConfigureAwait(true);
        }
        catch
        {
            // Açılıştaki kontrol sessizdir: çevrimdışıyken kullanıcıyı rahatsız etme.
        }
    }

    private void ShowWhatsNewIfUpdated()
    {
        AppSettings s = _settings.Current;
        Version current = _updates.CurrentVersion;
        Version? seen = UpdateService.TryParseVersion(s.LastSeenVersion);

        // LastSeenVersion 1.0.1'de eklendi: ayar dosyası var ama alan yoksa 1.0.0'dan yükseltilmiştir.
        bool upgraded = seen is not null ? seen < current : !_settings.IsFirstRun;

        if (s.LastSeenVersion is null && !_settings.IsFirstRun)
        {
            // 1.0.0'da bu seçenek işlevsizdi ve kapalı kaydedilmişti; artık varsayılan açık.
            s.CheckForUpdates = true;
        }

        if (s.LastSeenVersion != CurrentVersionText)
        {
            s.LastSeenVersion = CurrentVersionText;
            _settings.Save();
        }

        if (!upgraded) return;

        List<ReleaseNote> notes = _updates.ReleaseNotes
            .Where(n => UpdateService.TryParseVersion(n.Version) is { } v
                        && v <= current
                        && (seen is null ? v == current : v > seen))
            .ToList();

        if (notes.Count == 0) return;

        _ui.ShowReleaseNotes(
            $"v{CurrentVersionText} sürümüne güncellendi",
            "MT Photo Viewer'da bu sürümle gelen yenilikler:",
            notes);
    }

    #endregion

    /// <summary>Guide §40 — klasörde resim kalmadıysa başlangıç ekranına dön.</summary>
    private void CloseCurrent()
    {
        StopSlideshow();
        CurrentImage = null;
        CurrentPreview = null;
        CurrentFilePath = null;
        CurrentIndex = -1;
        IsInfoPanelOpen = false;
        InfoPanel.Clear();
        UpdateCommandStates();
    }

    public void Shutdown()
    {
        _slideshowTimer.Stop();
        _slideshowTimer.Tick -= OnSlideshowTick;
        _loadCts?.Cancel();
        _scanner.FolderChanged -= OnFolderChanged;
        _scanner.Dispose();
        _loader.ClearCache();
    }
}
