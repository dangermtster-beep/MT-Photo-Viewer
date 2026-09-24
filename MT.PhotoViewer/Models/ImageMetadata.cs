namespace MT.PhotoViewer.Models;

/// <summary>Bilgi panelinde gösterilen EXIF / dosya bilgileri (Guide §24, §25).</summary>
public sealed class ImageMetadata
{
    public string FileName { get; set; } = string.Empty;
    public string? Dimensions { get; set; }
    public string? Format { get; set; }
    public string? FileSize { get; set; }

    public string? CameraMake { get; set; }
    public string? CameraModel { get; set; }
    public string? Iso { get; set; }
    public string? Exposure { get; set; }
    public string? Aperture { get; set; }
    public string? FocalLength { get; set; }
    public string? DateTaken { get; set; }
    public string? Gps { get; set; }
    public string? Dpi { get; set; }
    public string? ColorProfile { get; set; }
    public int Orientation { get; set; } = 1;

    public string? Camera =>
        string.IsNullOrWhiteSpace(CameraMake) && string.IsNullOrWhiteSpace(CameraModel)
            ? null
            : $"{CameraMake} {CameraModel}".Trim();
}
