using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MT.PhotoViewer.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace MT.PhotoViewer.Services;

/// <summary>
/// Görsel yükleme (Guide §37–39).
/// - Dosya kilitlenmez (bayt olarak okunur, bitmap'ler donmuştur).
/// - Decode, EXIF dönüşü ve önizleme üretimi tamamen UI thread dışında yapılır;
///   UI thread'ine yalnızca çizilmeye hazır pikseller gelir.
/// - Komşu görseller arka planda ön yüklenir; aynı dosya iki kez çözülmez.
/// </summary>
public sealed class ImageLoaderService
{
    private readonly ConcurrentDictionary<string, LoadedImage> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Lazy<Task<LoadedImage>>> _inflight = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<string> _lru = new();
    private readonly object _lruLock = new();

    public int CacheSize { get; set; } = 6;

    /// <summary>
    /// Önizlemenin uzun kenarı (fiziksel piksel). En büyük monitörün uzun kenarına
    /// eşitlenir; kare kutu kullanıldığı için döndürülmüş görünüm de keskin kalır.
    /// </summary>
    public int PreviewMaxEdge { get; set; } = 1920;

    public LoadedImage? TryGetCached(string path) =>
        _cache.TryGetValue(path, out var img) ? img : null;

    public async Task<LoadedImage> LoadAsync(string path, CancellationToken token = default)
    {
        if (_cache.TryGetValue(path, out var cached))
        {
            Touch(path);
            return cached;
        }

        // Ön yükleme bu dosyayı zaten çözüyorsa onu bekle; baştan başlama.
        return await GetOrStartDecode(path).WaitAsync(token).ConfigureAwait(false);
    }

    /// <summary>Önceki/sonraki görselleri sessizce cache'e alır (Guide §39).</summary>
    public void Preload(IEnumerable<string> paths)
    {
        foreach (string path in paths)
        {
            if (string.IsNullOrEmpty(path) || _cache.ContainsKey(path))
                continue;

            // Ön yükleme hatası kullanıcıyı ilgilendirmez; yalnızca gözlemlenir.
            _ = GetOrStartDecode(path).ContinueWith(
                t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
        }
    }

    public void ClearCache()
    {
        _cache.Clear();
        lock (_lruLock) _lru.Clear();
    }

    public void Remove(string path)
    {
        _cache.TryRemove(path, out _);
        lock (_lruLock) _lru.Remove(path);
    }

    private Task<LoadedImage> GetOrStartDecode(string path) =>
        _inflight.GetOrAdd(path, p => new Lazy<Task<LoadedImage>>(() => DecodeAndCacheAsync(p))).Value;

    private async Task<LoadedImage> DecodeAndCacheAsync(string path)
    {
        try
        {
            int maxEdge = PreviewMaxEdge;
            LoadedImage image = await Task.Run(() => Decode(path, maxEdge)).ConfigureAwait(false);

            _cache[path] = image;
            Touch(path);
            Trim();

            return image;
        }
        finally
        {
            _inflight.TryRemove(path, out _);
        }
    }

    private static LoadedImage Decode(string path, int maxEdge)
    {
        string ext = Path.GetExtension(path);

        if (Helpers.ImageExtensions.RequiresImageSharp.Contains(ext))
            return DecodeWithImageSharp(path, maxEdge);

        try
        {
            return DecodeWithWpf(path, maxEdge);
        }
        catch
        {
            // WPF codec'i yoksa ImageSharp'a düş (webp, bazı tiff varyantları vb.).
            return DecodeWithImageSharp(path, maxEdge);
        }
    }

    private static LoadedImage DecodeWithWpf(string path, int maxEdge)
    {
        byte[] data = ReadAllBytesShared(path);

        // Yalnızca başlık: boyut + EXIF Orientation (pikseller çözülmez).
        BitmapFrame header = BitmapDecoder.Create(
            new MemoryStream(data, writable: false),
            BitmapCreateOptions.DelayCreation,
            BitmapCacheOption.None).Frames[0];

        int orientation = ReadOrientation(header);
        int storedWidth = header.PixelWidth;
        double scale = PreviewScale(header.PixelWidth, header.PixelHeight, maxEdge);

        // Tam çözünürlük ve önizleme paralel çözülür. Önizleme, JPEG'in DCT
        // ölçeklemesiyle doğrudan küçük boyutta çözüldüğü için hem hızlı hem keskindir.
        Task<BitmapSource> full = Task.Run(() => Orient(DecodeFrame(data, 0), orientation));

        BitmapSource? preview = scale < 1
            ? Orient(DecodeFrame(data, (int)Math.Round(storedWidth * scale)), orientation)
            : null;

        BitmapSource fullImage = full.GetAwaiter().GetResult();
        return new LoadedImage(fullImage, preview ?? fullImage);
    }

    private static BitmapSource DecodeFrame(byte[] data, int decodeWidth)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        if (decodeWidth > 0)
            bitmap.DecodePixelWidth = decodeWidth;   // yükseklik oranla hesaplanır
        bitmap.StreamSource = new MemoryStream(data, writable: false);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static LoadedImage DecodeWithImageSharp(string path, int maxEdge)
    {
        using var image = SixLabors.ImageSharp.Image.Load<Bgra32>(path);

        // EXIF Orientation'ı piksellere uygula (etiket sonrasında sıfırlanır,
        // codec zaten uygulamışsa işlem no-op'tur).
        image.Mutate(ctx => ctx.AutoOrient());

        BitmapSource full = ToBitmapSource(image);

        double scale = PreviewScale(image.Width, image.Height, maxEdge);
        if (scale >= 1)
            return new LoadedImage(full, full);

        using var small = image.Clone(ctx => ctx.Resize(
            Math.Max(1, (int)Math.Round(image.Width * scale)),
            Math.Max(1, (int)Math.Round(image.Height * scale))));

        return new LoadedImage(full, ToBitmapSource(small));
    }

    private static BitmapSource ToBitmapSource(Image<Bgra32> image)
    {
        int stride = image.Width * 4;
        var buffer = new byte[stride * image.Height];
        image.CopyPixelDataTo(buffer);

        BitmapSource source = BitmapSource.Create(
            image.Width, image.Height, 96, 96, PixelFormats.Bgra32, null, buffer, stride);

        source.Freeze();
        return source;
    }

    /// <summary>
    /// Önizleme ölçeği (≤ 1). Tam çözünürlüğe yakın görsellerde ayrı önizleme
    /// üretmek kazandırmaz; o durumda 1 döner ve tam çözünürlük kullanılır.
    /// </summary>
    private static double PreviewScale(int width, int height, int maxEdge)
    {
        int longEdge = Math.Max(width, height);
        if (longEdge <= 0 || maxEdge <= 0) return 1;

        double scale = (double)maxEdge / longEdge;
        return scale < 0.8 ? scale : 1;
    }

    private static byte[] ReadAllBytesShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var data = new byte[stream.Length];
        stream.ReadExactly(data);
        return data;
    }

    /// <summary>EXIF Orientation (TIFF tag 274). Okunamazsa 1 (dönüş yok).</summary>
    private static int ReadOrientation(BitmapSource frame)
    {
        if (frame.Metadata is not BitmapMetadata meta)
            return 1;

        // JPEG etiketi APP1 altında, TIFF ise doğrudan IFD içinde taşır.
        foreach (string query in new[] { "/app1/ifd/{ushort=274}", "/ifd/{ushort=274}" })
        {
            try
            {
                object? value = meta.GetQuery(query);
                if (value is ushort u) return u;
                if (value is int i) return i;
            }
            catch
            {
                // Bu sorguyu desteklemeyen format — sıradakine bak.
            }
        }

        return 1;
    }

    /// <summary>
    /// WPF/WIC codec'i EXIF Orientation etiketini kendiliğinden uygulamaz; uygulamazsak
    /// telefonla çekilen dikey fotoğraflar yan yatmış görünür. Dönüş burada, arka planda
    /// gerçek piksellere yazılır — TransformedBitmap tembel olduğu için olduğu gibi
    /// bırakılırsa döndürme ekrana çizilirken UI thread'inde yapılır ve geçişi dondurur.
    /// </summary>
    private static BitmapSource Orient(BitmapSource source, int orientation)
    {
        (double angle, bool mirror) = orientation switch
        {
            2 => (0d, true),
            3 => (180d, false),
            4 => (180d, true),
            5 => (90d, true),
            6 => (90d, false),
            7 => (270d, true),
            8 => (270d, false),
            _ => (0d, false)
        };

        if (angle == 0 && !mirror)
            return source;

        var transform = new TransformGroup();
        if (mirror) transform.Children.Add(new ScaleTransform(-1, 1));
        if (angle != 0) transform.Children.Add(new RotateTransform(angle));
        transform.Freeze();

        return Materialize(new TransformedBitmap(source, transform));
    }

    private static BitmapSource Materialize(BitmapSource source)
    {
        int stride = (source.PixelWidth * source.Format.BitsPerPixel + 7) / 8;
        var buffer = new byte[stride * source.PixelHeight];
        source.CopyPixels(buffer, stride, 0);

        BitmapSource result = BitmapSource.Create(
            source.PixelWidth, source.PixelHeight, source.DpiX, source.DpiY,
            source.Format, source.Palette, buffer, stride);

        result.Freeze();
        return result;
    }

    private void Touch(string path)
    {
        lock (_lruLock)
        {
            _lru.Remove(path);
            _lru.AddFirst(path);
        }
    }

    private void Trim()
    {
        lock (_lruLock)
        {
            while (_lru.Count > Math.Max(2, CacheSize))
            {
                LinkedListNode<string>? last = _lru.Last;
                if (last is null) break;
                _lru.RemoveLast();
                _cache.TryRemove(last.Value, out _);
            }
        }
    }
}
