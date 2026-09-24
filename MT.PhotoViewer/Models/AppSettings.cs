namespace MT.PhotoViewer.Models;

public enum AppTheme
{
    Dark,
    Light,
    System
}

public enum StartupZoomMode
{
    /// <summary>Her zaman ekrana sığdır.</summary>
    FitToScreen,

    /// <summary>%100, ancak resim pencereden büyükse ekrana sığdır.</summary>
    ActualSize
}

/// <summary>Kullanıcı ayarları (Guide §31). %AppData%\MT.PhotoViewer\settings.json içinde saklanır.</summary>
public sealed class AppSettings
{
    // Görünüm
    public AppTheme Theme { get; set; } = AppTheme.Dark;
    public string PhotoBackground { get; set; } = "Theme"; // Theme | Black | White | Checker
    public bool AutoHideControls { get; set; } = true;

    // Görüntüleme
    public StartupZoomMode StartupZoom { get; set; } = StartupZoomMode.FitToScreen;
    public bool MouseWheelZoom { get; set; } = true;
    public bool SmoothZoom { get; set; } = true;
    public bool HighQualityInterpolation { get; set; } = true;

    /// <summary>Slayt gösterisinde iki fotoğraf arasındaki bekleme (saniye).</summary>
    public int SlideshowIntervalSeconds { get; set; } = 4;

    // Performans
    public bool PreloadNeighbors { get; set; } = true;
    public int CacheSize { get; set; } = 6;

    // Genel
    public bool RememberWindowPosition { get; set; } = true;
    public bool CheckForUpdates { get; set; } = true;

    /// <summary>
    /// "Yenilikler" penceresinin en son gösterildiği sürüm. Kurulu sürüm bundan
    /// yeniyse uygulama güncellenmiştir ve yenilikler bir kez gösterilir.
    /// </summary>
    public string? LastSeenVersion { get; set; }

    public double WindowLeft { get; set; } = double.NaN;
    public double WindowTop { get; set; } = double.NaN;
    public double WindowWidth { get; set; } = 1280;
    public double WindowHeight { get; set; } = 800;
    public bool WindowMaximized { get; set; }
}
