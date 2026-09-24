using System.Windows;
using Microsoft.Win32;
using MT.PhotoViewer.Models;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace MT.PhotoViewer.Services;

/// <summary>Dark / Light / Windows sistem teması yönetimi (Guide §28).</summary>
public sealed class ThemeService
{
    private const string PersonalizeKey =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private ResourceDictionary? _active;

    public AppTheme CurrentTheme { get; private set; } = AppTheme.Dark;

    public bool IsDarkActive { get; private set; } = true;

    public event Action? ThemeApplied;

    public void Apply(AppTheme theme)
    {
        CurrentTheme = theme;
        IsDarkActive = theme switch
        {
            AppTheme.Dark => true,
            AppTheme.Light => false,
            _ => IsSystemDark()
        };

        // WPF-UI kontrollerini (ComboBox, Slider, CheckBox…) aynı temaya al.
        ApplicationThemeManager.Apply(
            IsDarkActive ? ApplicationTheme.Dark : ApplicationTheme.Light,
            WindowBackdropType.None,
            updateAccent: true);

        var dictionary = new ResourceDictionary
        {
            Source = new Uri(
                IsDarkActive
                    ? "pack://application:,,,/Themes/DarkTheme.xaml"
                    : "pack://application:,,,/Themes/LightTheme.xaml",
                UriKind.Absolute)
        };

        var merged = Application.Current.Resources.MergedDictionaries;

        // Rehberdeki renkler WPF-UI paletini gölgelemeli; bu yüzden her zaman en sonda durur.
        if (_active is not null)
        {
            int index = merged.IndexOf(_active);
            if (index >= 0)
                merged[index] = dictionary;
            else
                merged.Add(dictionary);
        }
        else
        {
            merged.Add(dictionary);
        }

        _active = dictionary;
        ThemeApplied?.Invoke();
    }

    public static bool IsSystemDark()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            object? value = key?.GetValue("AppsUseLightTheme");
            if (value is int i)
                return i == 0;
        }
        catch
        {
            // Kayıt defteri okunamazsa koyu tema varsayılır.
        }

        return true;
    }
}
