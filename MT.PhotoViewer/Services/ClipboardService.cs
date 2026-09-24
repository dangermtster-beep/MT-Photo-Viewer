using System.Collections.Specialized;
using System.Windows;
using System.Windows.Media.Imaging;

namespace MT.PhotoViewer.Services;

/// <summary>Panoya kopyalama (Guide §7.3).</summary>
public sealed class ClipboardService
{
    /// <summary>
    /// Hem bitmap hem de dosya listesi olarak kopyalar; böylece
    /// hem görsel editörlere hem de Explorer'a yapıştırılabilir.
    /// </summary>
    public void CopyImage(BitmapSource image, string? filePath)
    {
        var data = new DataObject();
        data.SetImage(image);

        if (!string.IsNullOrEmpty(filePath) && System.IO.File.Exists(filePath))
        {
            var files = new StringCollection { filePath };
            data.SetFileDropList(files);
        }

        Clipboard.SetDataObject(data, copy: true);
    }

    public void CopyText(string text) => Clipboard.SetText(text);
}
