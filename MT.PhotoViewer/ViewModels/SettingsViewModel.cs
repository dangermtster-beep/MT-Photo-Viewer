using CommunityToolkit.Mvvm.ComponentModel;
using MT.PhotoViewer.Models;
using MT.PhotoViewer.Services;

namespace MT.PhotoViewer.ViewModels;

/// <summary>Ayarlar penceresi (Guide §31).</summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _service;

    public SettingsViewModel(SettingsService service)
    {
        _service = service;
        AppSettings s = service.Current;

        theme = s.Theme;
        photoBackground = s.PhotoBackground;
        autoHideControls = s.AutoHideControls;
        startupZoom = s.StartupZoom;
        mouseWheelZoom = s.MouseWheelZoom;
        smoothZoom = s.SmoothZoom;
        highQualityInterpolation = s.HighQualityInterpolation;
        slideshowIntervalSeconds = s.SlideshowIntervalSeconds;
        preloadNeighbors = s.PreloadNeighbors;
        cacheSize = s.CacheSize;
        rememberWindowPosition = s.RememberWindowPosition;
        checkForUpdates = s.CheckForUpdates;
    }

    public IReadOnlyList<AppTheme> Themes { get; } =
        new[] { AppTheme.Dark, AppTheme.Light, AppTheme.System };

    public IReadOnlyList<string> Backgrounds { get; } =
        new[] { "Theme", "Black", "White", "Checker" };

    public IReadOnlyList<StartupZoomMode> StartupZoomModes { get; } =
        new[] { StartupZoomMode.FitToScreen, StartupZoomMode.ActualSize };

    [ObservableProperty] private AppTheme theme;
    [ObservableProperty] private string photoBackground;
    [ObservableProperty] private bool autoHideControls;
    [ObservableProperty] private StartupZoomMode startupZoom;
    [ObservableProperty] private bool mouseWheelZoom;
    [ObservableProperty] private bool smoothZoom;
    [ObservableProperty] private bool highQualityInterpolation;
    [ObservableProperty] private int slideshowIntervalSeconds;
    [ObservableProperty] private bool preloadNeighbors;
    [ObservableProperty] private int cacheSize;
    [ObservableProperty] private bool rememberWindowPosition;
    [ObservableProperty] private bool checkForUpdates;

    public string SupportedFormatsText =>
        string.Join("  •  ", Helpers.ImageExtensions.Primary.Select(e => e.TrimStart('.').ToUpperInvariant()));

    /// <summary>Değişiklikleri kaydeder ve uygulamaya duyurur.</summary>
    public void Commit()
    {
        AppSettings s = _service.Current;

        s.Theme = Theme;
        s.PhotoBackground = PhotoBackground;
        s.AutoHideControls = AutoHideControls;
        s.StartupZoom = StartupZoom;
        s.MouseWheelZoom = MouseWheelZoom;
        s.SmoothZoom = SmoothZoom;
        s.HighQualityInterpolation = HighQualityInterpolation;
        s.SlideshowIntervalSeconds = Math.Clamp(SlideshowIntervalSeconds, 1, 60);
        s.PreloadNeighbors = PreloadNeighbors;
        s.CacheSize = Math.Clamp(CacheSize, 2, 32);
        s.RememberWindowPosition = RememberWindowPosition;
        s.CheckForUpdates = CheckForUpdates;

        _service.NotifyChanged();
    }
}
