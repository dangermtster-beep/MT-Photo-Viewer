using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Processing;

namespace MT.PhotoViewer.Services;

/// <summary>"Duvar Kağıdı Yap" (Guide §22).</summary>
public sealed class WallpaperService
{
    private const int SPI_SETDESKWALLPAPER = 0x0014;
    private const int SPIF_UPDATEINIFILE = 0x01;
    private const int SPIF_SENDCHANGE = 0x02;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SystemParametersInfo(int action, int param, string lpvParam, int fuWinIni);

    public void SetWallpaper(string imagePath)
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MT.PhotoViewer");

        Directory.CreateDirectory(dir);
        string bmpPath = Path.Combine(dir, "wallpaper.bmp");

        using (var image = SixLabors.ImageSharp.Image.Load(imagePath))
        {
            // BMP EXIF taşımaz; dönüşü piksellere yazmazsak duvar kağıdı yan yatar.
            image.Mutate(ctx => ctx.AutoOrient());
            image.Save(bmpPath, new BmpEncoder());
        }

        using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", writable: true))
        {
            key?.SetValue("WallpaperStyle", "10"); // Fill
            key?.SetValue("TileWallpaper", "0");
        }

        SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, bmpPath, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
    }
}
