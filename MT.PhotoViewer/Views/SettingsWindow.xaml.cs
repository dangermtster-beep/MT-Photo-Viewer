using System.Diagnostics;
using System.Windows;
using MT.PhotoViewer.Helpers;
using MT.PhotoViewer.Services;
using MT.PhotoViewer.ViewModels;

namespace MT.PhotoViewer.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _vm;

    public SettingsWindow(SettingsService settings)
    {
        InitializeComponent();

        _vm = new SettingsViewModel(settings);
        DataContext = _vm;

        OkButton.Click += (_, _) =>
        {
            _vm.Commit();
            DialogResult = true;
        };

        // Guide §43 — dosya ilişkilendirme Windows'un kendi ayarlarından yapılır.
        FileAssociationButton.Click += (_, _) => CommandGuard.Run(() =>
        {
            Process.Start(new ProcessStartInfo("ms-settings:defaultapps") { UseShellExecute = true });
        }, "Windows varsayılan uygulama ayarları açılamadı.");
    }
}
