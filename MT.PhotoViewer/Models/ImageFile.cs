using System.IO;

namespace MT.PhotoViewer.Models;

/// <summary>Klasör indexinde tutulan tek bir görsel dosya.</summary>
public sealed class ImageFile
{
    public ImageFile(string path)
    {
        FullPath = path;
    }

    public string FullPath { get; }

    public string FileName => Path.GetFileName(FullPath);

    public string? DirectoryPath => Path.GetDirectoryName(FullPath);

    public string Extension => Path.GetExtension(FullPath).ToLowerInvariant();

    public override string ToString() => FullPath;
}
