using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using MT.PhotoViewer.Models;

namespace MT.PhotoViewer.Services;

/// <summary>
/// Sürüm bilgisi, sürüm notları ve güncelleme kontrolü.
///
/// Akış: version.json okunur → sürüm kurulu olandan yeniyse <see cref="UpdateInfo"/> döner →
/// kullanıcı onaylarsa kurulum dosyası indirilir, SHA-256 ile doğrulanır ve
/// sessiz modda çalıştırılır (kurulum bitince uygulamayı yeniden açar).
/// </summary>
public sealed class UpdateService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly HttpClient Http = CreateHttpClient();

    private IReadOnlyList<ReleaseNote>? _releaseNotes;

    /// <summary>Kurulu sürüm (csproj &lt;Version&gt;).</summary>
    public Version CurrentVersion { get; } = Normalize(
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0));

    public string CurrentVersionText => CurrentVersion.ToString(3);

    /// <summary>version.json adresi (csproj &lt;UpdateFeedUrl&gt;). Boşsa kontrol kapalıdır.</summary>
    public string FeedUrl { get; } = Assembly.GetExecutingAssembly()
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(a => a.Key == "UpdateFeedUrl")?.Value?.Trim() ?? "";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(FeedUrl);

    /// <summary>Uygulamaya gömülü sürüm notları (yeniden eskiye).</summary>
    public IReadOnlyList<ReleaseNote> ReleaseNotes => _releaseNotes ??= LoadEmbeddedNotes();

    /// <summary>
    /// Yeni sürüm varsa bilgisini, yoksa null döner.
    /// Sunucuya ulaşılamazsa veya dosya bozuksa istisna fırlatır.
    /// </summary>
    public async Task<UpdateInfo?> CheckAsync(CancellationToken token = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Güncelleme adresi yapılandırılmamış.");

        Uri feed = ToUri(FeedUrl);
        string json = await ReadTextAsync(feed, token).ConfigureAwait(false);

        UpdateManifest manifest = JsonSerializer.Deserialize<UpdateManifest>(json, JsonOptions)
            ?? throw new InvalidDataException("version.json okunamadı.");

        if (!System.Version.TryParse(manifest.Version, out Version? latest))
            throw new InvalidDataException($"Geçersiz sürüm: \"{manifest.Version}\"");

        latest = Normalize(latest);
        if (latest <= CurrentVersion)
            return null;

        if (string.IsNullOrWhiteSpace(manifest.File))
            throw new InvalidDataException("version.json kurulum dosyasını belirtmiyor.");

        // "file" göreli olabilir: version.json ile aynı yerdeki kurulum dosyası.
        var download = new Uri(feed, manifest.File);

        List<ReleaseNote> newNotes = manifest.Notes
            .Where(n => Normalize(n.ParsedVersion) > CurrentVersion)
            .OrderByDescending(n => n.ParsedVersion)
            .ToList();

        return new UpdateInfo(latest, download, manifest.Sha256, manifest.Size, newNotes);
    }

    /// <summary>Kurulum dosyasını indirir ve SHA-256 ile doğrular. Dosya yolunu döner.</summary>
    public async Task<string> DownloadAsync(UpdateInfo update, IProgress<double>? progress, CancellationToken token)
    {
        string dir = Path.Combine(Path.GetTempPath(), "MT.PhotoViewer", "Update");
        Directory.CreateDirectory(dir);

        string fileName = Path.GetFileName(update.DownloadUri.LocalPath);
        if (string.IsNullOrWhiteSpace(fileName) || !fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            fileName = $"MT_Photo_Viewer_Setup_{update.Version.ToString(3)}.exe";

        string target = Path.Combine(dir, fileName);
        string partial = target + ".part";

        await using (Stream source = await OpenReadAsync(update.DownloadUri, token).ConfigureAwait(false))
        await using (var output = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            long total = update.Size;
            long done = 0;
            var buffer = new byte[81920];
            int read;

            while ((read = await source.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                done += read;
                if (total > 0) progress?.Report(Math.Min(1.0, (double)done / total));
            }
        }

        if (!string.IsNullOrWhiteSpace(update.Sha256))
        {
            string actual = await ComputeSha256Async(partial, token).ConfigureAwait(false);
            if (!actual.Equals(update.Sha256.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(partial);
                throw new InvalidDataException(
                    "İndirilen kurulum dosyası doğrulanamadı (SHA-256 eşleşmedi). Dosya bozuk olabilir.");
            }
        }

        File.Move(partial, target, overwrite: true);
        progress?.Report(1.0);
        return target;
    }

    /// <summary>
    /// Kurulumu sessiz modda başlatır. /UPDATE=1 kurulumun bitince uygulamayı
    /// yeniden açmasını sağlar (bkz. setup\MT.PhotoViewer.iss [Run]).
    /// Çağırandan sonra uygulama kapanmalıdır.
    /// </summary>
    public static void LaunchInstaller(string installerPath)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = installerPath,
            Arguments = "/SILENT /SP- /NOCANCEL /NORESTART /UPDATE=1",
            UseShellExecute = true   // yönetici izni (UAC) istemi için gerekli
        });
    }

    #region Yardımcılar

    private static IReadOnlyList<ReleaseNote> LoadEmbeddedNotes()
    {
        try
        {
            using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ReleaseNotes.json");
            if (stream is null) return Array.Empty<ReleaseNote>();

            List<ReleaseNote> notes = JsonSerializer.Deserialize<List<ReleaseNote>>(stream, JsonOptions) ?? new();
            return notes.OrderByDescending(n => n.ParsedVersion).ToList();
        }
        catch
        {
            return Array.Empty<ReleaseNote>();
        }
    }

    /// <summary>"1.0.1" gibi bir metni karşılaştırılabilir sürüme çevirir; geçersizse null.</summary>
    public static Version? TryParseVersion(string? text) =>
        System.Version.TryParse(text, out Version? v) ? Normalize(v) : null;

    /// <summary>1.0.1 ile 1.0.1.0 eşit sayılsın diye eksik alanlar 0 kabul edilir.</summary>
    private static Version Normalize(Version v) =>
        new(v.Major, v.Minor, Math.Max(0, v.Build), Math.Max(0, v.Revision));

    private static Uri ToUri(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) ? uri : new Uri(Path.GetFullPath(value));

    private static async Task<string> ReadTextAsync(Uri uri, CancellationToken token)
    {
        if (uri.IsFile)
            return await File.ReadAllTextAsync(uri.LocalPath, token).ConfigureAwait(false);

        // Önbelleğe alınmış eski bir version.json dönmesin.
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };

        using HttpResponseMessage response = await Http.SendAsync(request, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
    }

    private static Task<Stream> OpenReadAsync(Uri uri, CancellationToken token) =>
        uri.IsFile
            ? Task.FromResult<Stream>(new FileStream(uri.LocalPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            : Http.GetStreamAsync(uri, token);

    private static async Task<string> ComputeSha256Async(string path, CancellationToken token)
    {
        await using var stream = File.OpenRead(path);
        byte[] hash = await SHA256.HashDataAsync(stream, token).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        // GitHub ve bazı sunucular User-Agent olmadan isteği reddeder.
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MT.PhotoViewer-Updater");
        return client;
    }

    #endregion
}
