using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using MT.PhotoViewer.Helpers;
using MT.PhotoViewer.Models;
using MT.PhotoViewer.Services;
using MT.PhotoViewer.ViewModels;

namespace MT.PhotoViewer;

public partial class App : Application
{
    /// <summary>
    /// Kurulum/kaldırma sırasında uygulamanın açık olduğunu tespit etmek için
    /// (Inno Setup AppMutex). Süreç boyunca canlı kalmalıdır.
    /// </summary>
    private static Mutex? _instanceMutex;

    private const string InstanceMutexName = "MT.PhotoViewer.SingleInstance";

    public static IServiceProvider Services { get; private set; } = null!;

    public static void CreateInstanceMutex() =>
        _instanceMutex ??= new Mutex(initiallyOwned: false, InstanceMutexName);

    /// <summary>
    /// Güncelleme kurulumu başlatılmadan hemen önce çağrılır: kurulum mutex'i görürse
    /// "uygulama açık" diyerek durur.
    /// </summary>
    public static void ReleaseInstanceMutex()
    {
        _instanceMutex?.Dispose();
        _instanceMutex = null;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        CreateInstanceMutex();

        DispatcherUnhandledException += OnUnhandledException;

        Services = BuildServices();

        var settings = Services.GetRequiredService<SettingsService>();
        AppSettings loaded = settings.Load();

        Services.GetRequiredService<ThemeService>().Apply(loaded.Theme);

        var window = Services.GetRequiredService<MainWindow>();
        MainWindow = window;

        var ui = (UiService)Services.GetRequiredService<IUiService>();
        ui.Owner = window;

        window.Show();

        // Guide §43 — komut satırı argümanıyla açılan resim.
        string? startupFile = e.Args.FirstOrDefault(a => !a.StartsWith('-'));
        if (!string.IsNullOrWhiteSpace(startupFile))
            _ = window.OpenStartupPathAsync(startupFile);

        // Güncelleme sonrası "Yenilikler" + arka planda yeni sürüm kontrolü.
        _ = Services.GetRequiredService<MainViewModel>().RunStartupChecksAsync();
    }

    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<SettingsService>();
        services.AddSingleton<ThemeService>();
        services.AddSingleton<ImageLoaderService>();
        services.AddSingleton<FolderScannerService>();
        services.AddSingleton<MetadataService>();
        services.AddSingleton<ClipboardService>();
        services.AddSingleton<PrintService>();
        services.AddSingleton<FileService>();
        services.AddSingleton<EmailService>();
        services.AddSingleton<WallpaperService>();
        services.AddSingleton<UpdateService>();
        services.AddSingleton<IUiService, UiService>();

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }

    /// <summary>Guide §50 — kullanıcıya stack trace gösterilmez.</summary>
    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        CommandGuard.Report(e.Exception, "Beklenmeyen bir hata oluştu. İşlem tamamlanamadı.");
        e.Handled = true;
    }
}
