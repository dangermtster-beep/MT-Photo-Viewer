using System.IO;

namespace MT.PhotoViewer.Helpers;

/// <summary>Desteklenen dosya uzantıları (Guide §2).</summary>
public static class ImageExtensions
{
    /// <summary>Faz 1 — WPF / ImageSharp ile doğrudan açılabilen formatlar.</summary>
    public static readonly string[] Primary =
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tif", ".tiff", ".ico"
    };

    /// <summary>Faz 2 — Windows codec'i kuruluysa açılabilen formatlar.</summary>
    public static readonly string[] Extended =
    {
        ".heic", ".heif", ".avif", ".cr2", ".cr3", ".nef", ".arw", ".dng"
    };

    public static readonly HashSet<string> All =
        new(Primary.Concat(Extended), StringComparer.OrdinalIgnoreCase);

    public static readonly HashSet<string> Supported =
        new(Primary, StringComparer.OrdinalIgnoreCase);

    /// <summary>ImageSharp ile decode edilmesi gereken formatlar (WPF bunları bilmez).</summary>
    public static readonly HashSet<string> RequiresImageSharp =
        new(new[] { ".webp" }, StringComparer.OrdinalIgnoreCase);

    public static bool IsSupported(string path) =>
        Supported.Contains(Path.GetExtension(path));

    public static bool IsKnown(string path) =>
        All.Contains(Path.GetExtension(path));

    /// <summary>OpenFileDialog filtresi (Guide §11).</summary>
    public static string OpenDialogFilter
    {
        get
        {
            string primary = string.Join(";", Primary.Select(e => "*" + e));
            string all = string.Join(";", All.Select(e => "*" + e));
            return $"Resim Dosyaları|{primary}|Tüm Desteklenenler|{all}|Tüm Dosyalar|*.*";
        }
    }
}
