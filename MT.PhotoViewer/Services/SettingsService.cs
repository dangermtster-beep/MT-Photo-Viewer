using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using MT.PhotoViewer.Models;

namespace MT.PhotoViewer.Services;

/// <summary>Ayarların %AppData%\MT.PhotoViewer\settings.json içinde saklanması (Guide §31).</summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _path;

    public SettingsService()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MT.PhotoViewer");

        System.IO.Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "settings.json");
    }

    public AppSettings Current { get; private set; } = new();

    /// <summary>Ayar dosyası hiç yoktu — uygulama bu kullanıcıda ilk kez açılıyor.</summary>
    public bool IsFirstRun { get; private set; }

    public event Action<AppSettings>? SettingsChanged;

    public AppSettings Load()
    {
        IsFirstRun = !File.Exists(_path);

        try
        {
            if (!IsFirstRun)
            {
                string json = File.ReadAllText(_path);
                Current = JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
            }
        }
        catch
        {
            Current = new AppSettings();
        }

        return Current;
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(Current, Options));
        }
        catch
        {
            // Ayar yazılamazsa uygulama çalışmaya devam eder.
        }
    }

    public void NotifyChanged()
    {
        Save();
        SettingsChanged?.Invoke(Current);
    }
}
