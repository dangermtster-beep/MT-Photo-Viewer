using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace MT.PhotoViewer.Services;

public enum EmailPhotoSize
{
    Original,
    Large,
    Medium,
    Small
}

/// <summary>
/// E-posta ile gönderme (Guide §9).
/// Fotoğraf seçilen boyuta küçültülür ve varsayılan e-posta istemcisinde
/// yeni bir ileti eki olarak açılır. İleti otomatik gönderilmez.
/// </summary>
public sealed class EmailService
{
    public static int LongEdgeFor(EmailPhotoSize size) => size switch
    {
        EmailPhotoSize.Large => 1920,
        EmailPhotoSize.Medium => 1280,
        EmailPhotoSize.Small => 800,
        _ => 0
    };

    /// <summary>Seçilen boyut için tahmini dosya boyutu (Guide §9).</summary>
    public static long EstimateSize(string path, EmailPhotoSize size)
    {
        var info = new FileInfo(path);
        if (!info.Exists) return 0;

        if (size == EmailPhotoSize.Original)
            return info.Length;

        try
        {
            ImageInfo src = SixLabors.ImageSharp.Image.Identify(path);
            int longEdge = Math.Max(src.Width, src.Height);
            int target = LongEdgeFor(size);
            if (longEdge <= target) return info.Length;

            double ratio = (double)target / longEdge;
            // JPEG yeniden sıkıştırma piksel oranından daha iyi sonuç verir.
            return (long)(info.Length * ratio * ratio * 1.15);
        }
        catch
        {
            return info.Length;
        }
    }

    /// <summary>Gerekiyorsa küçültülmüş bir kopya üretir ve yolunu döndürür.</summary>
    public string PrepareAttachment(string path, EmailPhotoSize size)
    {
        if (size == EmailPhotoSize.Original)
            return path;

        int target = LongEdgeFor(size);

        using var image = SixLabors.ImageSharp.Image.Load(path);
        image.Mutate(x => x.AutoOrient());   // alıcıda yan yatmasın

        int longEdge = Math.Max(image.Width, image.Height);

        if (longEdge <= target)
            return path;

        double ratio = (double)target / longEdge;
        int w = Math.Max(1, (int)Math.Round(image.Width * ratio));
        int h = Math.Max(1, (int)Math.Round(image.Height * ratio));

        image.Mutate(x => x.Resize(w, h));

        string dir = Path.Combine(Path.GetTempPath(), "MT.PhotoViewer", "Email");
        Directory.CreateDirectory(dir);

        string outPath = Path.Combine(dir, Path.GetFileNameWithoutExtension(path) + ".jpg");
        image.Save(outPath, new JpegEncoder { Quality = 88 });

        return outPath;
    }

    /// <summary>
    /// Varsayılan e-posta istemcisinde eki hazır bir taslak açar.
    /// Simple MAPI desteklenmiyorsa mailto ile boş bir taslak açıp
    /// eki Explorer'da gösterir.
    /// </summary>
    public bool CreateDraft(string attachmentPath, string subject)
    {
        if (TryMapi(attachmentPath, subject))
            return true;

        try
        {
            Process.Start(new ProcessStartInfo(
                $"mailto:?subject={Uri.EscapeDataString(subject)}")
            {
                UseShellExecute = true
            });

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{attachmentPath}\"",
                UseShellExecute = true
            });

            return true;
        }
        catch
        {
            return false;
        }
    }

    #region Simple MAPI

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private class MapiMessage
    {
        public int reserved;
        public string? subject;
        public string? noteText;
        public string? messageType;
        public string? dateReceived;
        public string? conversationID;
        public int flags;
        public IntPtr originator;
        public int recipCount;
        public IntPtr recips;
        public int fileCount;
        public IntPtr files;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private class MapiFileDesc
    {
        public int reserved;
        public int flags;
        public int position = -1;
        public string? path;
        public string? name;
        public IntPtr type;
    }

    private const int MAPI_LOGON_UI = 0x00000001;
    private const int MAPI_DIALOG = 0x00000008;

    [DllImport("MAPI32.DLL", CharSet = CharSet.Ansi, EntryPoint = "MAPISendMail")]
    private static extern int MapiSendMail(
        IntPtr session, IntPtr uiParam, MapiMessage message, int flags, int reserved);

    private static bool TryMapi(string attachmentPath, string subject)
    {
        IntPtr fileBuffer = IntPtr.Zero;

        try
        {
            var file = new MapiFileDesc
            {
                path = attachmentPath,
                name = Path.GetFileName(attachmentPath)
            };

            int size = Marshal.SizeOf<MapiFileDesc>();
            fileBuffer = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(file, fileBuffer, false);

            var message = new MapiMessage
            {
                subject = subject,
                noteText = string.Empty,
                fileCount = 1,
                files = fileBuffer
            };

            int result = MapiSendMail(IntPtr.Zero, IntPtr.Zero, message,
                MAPI_LOGON_UI | MAPI_DIALOG, 0);

            // 0 = gönderildi, 1 = kullanıcı iptal etti; her ikisi de "istemci açıldı" demektir.
            return result is 0 or 1;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (fileBuffer != IntPtr.Zero)
            {
                Marshal.DestroyStructure<MapiFileDesc>(fileBuffer);
                Marshal.FreeHGlobal(fileBuffer);
            }
        }
    }

    #endregion
}
