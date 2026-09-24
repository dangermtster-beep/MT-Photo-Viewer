using System.Text.Json.Serialization;

namespace MT.PhotoViewer.Models;

/// <summary>Bir sürümün kullanıcıya gösterilen değişiklik listesi.</summary>
public sealed class ReleaseNote
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("date")]
    public string Date { get; set; } = "";

    [JsonPropertyName("changes")]
    public List<string> Changes { get; set; } = new();

    [JsonIgnore]
    public Version ParsedVersion => System.Version.TryParse(Version, out var v) ? v : new Version(0, 0);

    /// <summary>Pencerede başlık olarak gösterilir: "v1.0.1 — 24.09.2026".</summary>
    [JsonIgnore]
    public string Title =>
        DateTime.TryParse(Date, System.Globalization.CultureInfo.InvariantCulture,
                          System.Globalization.DateTimeStyles.None, out var d)
            ? $"v{Version}  —  {d:dd.MM.yyyy}"
            : $"v{Version}";
}

/// <summary>
/// Güncelleme sunucusundaki <c>version.json</c>. <c>tools\build-installer.ps1</c> üretir.
/// </summary>
public sealed class UpdateManifest
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("date")]
    public string Date { get; set; } = "";

    /// <summary>Kurulum dosyası; mutlak URL ya da version.json'a göre göreli ad.</summary>
    [JsonPropertyName("file")]
    public string File { get; set; } = "";

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = "";

    [JsonPropertyName("size")]
    public long Size { get; set; }

    /// <summary>Tüm sürüm geçmişi (yeniden eskiye).</summary>
    [JsonPropertyName("notes")]
    public List<ReleaseNote> Notes { get; set; } = new();
}

/// <summary>Kurulu sürümden yeni bir sürüm bulunduğunda oluşur.</summary>
public sealed record UpdateInfo(
    Version Version,
    Uri DownloadUri,
    string Sha256,
    long Size,
    IReadOnlyList<ReleaseNote> NewNotes);
