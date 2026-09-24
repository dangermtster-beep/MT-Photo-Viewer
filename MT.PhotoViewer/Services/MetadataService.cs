using System.Globalization;
using System.IO;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MT.PhotoViewer.Models;
using Directory = MetadataExtractor.Directory;

namespace MT.PhotoViewer.Services;

/// <summary>EXIF / dosya metadata okuma (Guide §25).</summary>
public sealed class MetadataService
{
    public Task<ImageMetadata> ReadAsync(string path, int pixelWidth, int pixelHeight) =>
        Task.Run(() => Read(path, pixelWidth, pixelHeight));

    public ImageMetadata Read(string path, int pixelWidth, int pixelHeight)
    {
        var meta = new ImageMetadata
        {
            FileName = Path.GetFileName(path),
            Format = Path.GetExtension(path).TrimStart('.').ToUpperInvariant(),
            Dimensions = pixelWidth > 0 ? $"{pixelWidth} × {pixelHeight}" : null
        };

        try
        {
            var info = new FileInfo(path);
            if (info.Exists)
                meta.FileSize = FormatBytes(info.Length);
        }
        catch
        {
            // Dosya bilgisi okunamazsa boş bırakılır.
        }

        try
        {
            IReadOnlyList<Directory> directories = ImageMetadataReader.ReadMetadata(path);

            var ifd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
            var subIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            var gps = directories.OfType<GpsDirectory>().FirstOrDefault();

            meta.CameraMake = ifd0?.GetDescription(ExifDirectoryBase.TagMake);
            meta.CameraModel = ifd0?.GetDescription(ExifDirectoryBase.TagModel);

            if (ifd0 is not null && ifd0.TryGetInt32(ExifDirectoryBase.TagOrientation, out int orientation))
                meta.Orientation = orientation;

            meta.Iso = subIfd?.GetDescription(ExifDirectoryBase.TagIsoEquivalent);
            meta.Exposure = subIfd?.GetDescription(ExifDirectoryBase.TagExposureTime);
            meta.Aperture = subIfd?.GetDescription(ExifDirectoryBase.TagFNumber);
            meta.FocalLength = subIfd?.GetDescription(ExifDirectoryBase.TagFocalLength);
            meta.ColorProfile = subIfd?.GetDescription(ExifDirectoryBase.TagColorSpace);

            if (subIfd is not null &&
                subIfd.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out DateTime taken))
            {
                meta.DateTaken = taken.ToString("dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture);
            }
            else if (ifd0 is not null && ifd0.TryGetDateTime(ExifDirectoryBase.TagDateTime, out DateTime dt))
            {
                meta.DateTaken = dt.ToString("dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture);
            }

            string? xRes = ifd0?.GetDescription(ExifDirectoryBase.TagXResolution);
            if (!string.IsNullOrWhiteSpace(xRes))
                meta.Dpi = xRes;

            var location = gps?.GetGeoLocation();
            if (location is not null && !location.IsZero)
                meta.Gps = $"{location.Latitude:F6}, {location.Longitude:F6}";
        }
        catch
        {
            // EXIF taşımayan formatlar (PNG/BMP/WEBP) için normaldir.
        }

        if (string.IsNullOrWhiteSpace(meta.DateTaken))
        {
            try
            {
                meta.DateTaken = File.GetLastWriteTime(path)
                    .ToString("dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture);
            }
            catch
            {
                // yoksay
            }
        }

        return meta;
    }

    public static string FormatBytes(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB" };
        double value = bytes;
        int unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0
            ? $"{bytes} B"
            : $"{value.ToString("0.#", CultureInfo.CurrentCulture)} {units[unit]}";
    }
}
