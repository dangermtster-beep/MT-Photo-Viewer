using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MT.PhotoViewer.Helpers;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    /// <summary>"Invert" parametresi verilirse mantık ters çevrilir.</summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool flag = value is bool b && b;
        if (parameter is string s && s.Equals("Invert", StringComparison.OrdinalIgnoreCase))
            flag = !flag;

        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Visibility v && v == Visibility.Visible;
}

/// <summary>Boş/null metinleri gizler — bilgi panelinde kullanılır (Guide §24).</summary>
public sealed class EmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool hasValue = value is not null;
        if (parameter is string s && s.Equals("Invert", StringComparison.OrdinalIgnoreCase))
            hasValue = !hasValue;

        return hasValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Enum değerini RadioButton/ComboBox seçimiyle eşler.</summary>
public sealed class EnumMatchConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value?.ToString() == parameter?.ToString();

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool b && b && parameter is not null
            ? Enum.Parse(targetType, parameter.ToString()!)
            : Binding.DoNothing;
}

/// <summary>Enum ve sabit değerler için Türkçe görünen ad.</summary>
public sealed class DisplayNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value?.ToString() switch
        {
            "Dark" => "Koyu",
            "Light" => "Açık",
            "System" => "Windows Sistem Teması",
            "Theme" => "Tema rengi",
            "Black" => "Siyah",
            "White" => "Beyaz",
            "Checker" => "Dama tahtası",
            "FitToScreen" => "Ekrana sığdır",
            "ActualSize" => "%100 gerçek boyut",
            _ => value?.ToString() ?? string.Empty
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
