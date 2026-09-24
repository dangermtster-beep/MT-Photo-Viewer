using System.IO;
using MT.PhotoViewer.Helpers;

namespace MT.PhotoViewer.Services;

/// <summary>
/// Klasör indexleme (Guide §14) ve klasör değişikliklerini izleme (Guide §41).
/// </summary>
public sealed class FolderScannerService : IDisposable
{
    private FileSystemWatcher? _watcher;

    /// <summary>Klasör içeriği değiştiğinde tetiklenir (UI thread'e marshal edilmez).</summary>
    public event Action? FolderChanged;

    public IReadOnlyList<string> Scan(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            return Array.Empty<string>();

        try
        {
            List<string> files = Directory
                .EnumerateFiles(folderPath, "*", SearchOption.TopDirectoryOnly)
                .Where(ImageExtensions.IsKnown)
                .ToList();

            files.Sort(NaturalFileNameComparer.Instance);
            return files;
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
        catch (IOException)
        {
            return Array.Empty<string>();
        }
    }

    public void Watch(string folderPath)
    {
        StopWatching();

        if (!Directory.Exists(folderPath))
            return;

        try
        {
            _watcher = new FileSystemWatcher(folderPath)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName,
                IncludeSubdirectories = false,
                EnableRaisingEvents = true
            };

            _watcher.Created += OnChanged;
            _watcher.Deleted += OnChanged;
            _watcher.Renamed += OnChanged;
        }
        catch
        {
            // İzleme opsiyoneldir; başarısız olursa uygulama çalışmaya devam eder.
            _watcher = null;
        }
    }

    public void StopWatching()
    {
        if (_watcher is null) return;

        _watcher.EnableRaisingEvents = false;
        _watcher.Created -= OnChanged;
        _watcher.Deleted -= OnChanged;
        _watcher.Renamed -= OnChanged;
        _watcher.Dispose();
        _watcher = null;
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        string ext = Path.GetExtension(e.FullPath);
        if (!string.IsNullOrEmpty(ext) && !ImageExtensions.All.Contains(ext))
            return;

        FolderChanged?.Invoke();
    }

    public void Dispose() => StopWatching();
}
