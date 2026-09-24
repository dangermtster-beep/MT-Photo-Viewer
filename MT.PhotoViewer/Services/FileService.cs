using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace MT.PhotoViewer.Services;

/// <summary>
/// Dosya işlemleri (Guide §7, §10, §11, §50).
/// UI katmanı doğrudan dosya işlemi yapmaz; her şey buradan geçer.
/// </summary>
public sealed class FileService
{
    #region Shell interop

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        [MarshalAs(UnmanagedType.LPWStr)] public string pFrom;
        [MarshalAs(UnmanagedType.LPWStr)] public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszProgressTitle;
    }

    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_WANTNUKEWARNING = 0x4000;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT fileOp);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHELLEXECUTEINFO
    {
        public int cbSize;
        public uint fMask;
        public IntPtr hwnd;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpVerb;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpFile;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpParameters;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpDirectory;
        public int nShow;
        public IntPtr hInstApp;
        public IntPtr lpIDList;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpClass;
        public IntPtr hkeyClass;
        public uint dwHotKey;
        public IntPtr hIcon;
        public IntPtr hProcess;
    }

    private const uint SEE_MASK_INVOKEIDLIST = 0x0000000C;
    private const int SW_SHOW = 5;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO info);

    #endregion

    /// <summary>Dosyayı Geri Dönüşüm Kutusu'na taşır (Guide §7.1). Kalıcı silme yapılmaz.</summary>
    public bool MoveToRecycleBin(string path)
    {
        if (!File.Exists(path)) return false;

        var op = new SHFILEOPSTRUCT
        {
            wFunc = FO_DELETE,
            pFrom = path + "\0\0",
            fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_WANTNUKEWARNING
        };

        int result = SHFileOperation(ref op);
        return result == 0 && !op.fAnyOperationsAborted;
    }

    /// <summary>Windows dosya özellikleri penceresini açar (Guide §7.4).</summary>
    public bool ShowFileProperties(string path)
    {
        var info = new SHELLEXECUTEINFO
        {
            lpVerb = "properties",
            lpFile = path,
            lpDirectory = Path.GetDirectoryName(path),
            nShow = SW_SHOW,
            fMask = SEE_MASK_INVOKEIDLIST
        };
        info.cbSize = Marshal.SizeOf(info);

        return ShellExecuteEx(ref info);
    }

    /// <summary>Dosyayı Explorer'da seçili olarak açar (Guide §11).</summary>
    public void OpenFileLocation(string path)
    {
        if (!File.Exists(path)) return;

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{path}\"",
            UseShellExecute = true
        });
    }

    /// <summary>Varsayılan uygulamayla açar.</summary>
    public void OpenWithDefault(string path)
    {
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    /// <summary>"Birlikte aç" diyalogunu gösterir.</summary>
    public void OpenWithDialog(string path)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "rundll32.exe",
            Arguments = $"shell32.dll,OpenAs_RunDLL \"{path}\"",
            UseShellExecute = true
        });
    }

    /// <summary>Paint ile açar (Guide §11).</summary>
    public void OpenWithPaint(string path)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "mspaint.exe",
            Arguments = $"\"{path}\"",
            UseShellExecute = true
        });
    }

    /// <summary>Dosyayı hedef klasöre kopyalar; ad çakışırsa "(2)" eki verir (Guide §10).</summary>
    public string CopyToFolder(string sourcePath, string targetFolder)
    {
        System.IO.Directory.CreateDirectory(targetFolder);

        string target = Path.Combine(targetFolder, Path.GetFileName(sourcePath));
        target = MakeUnique(target);

        File.Copy(sourcePath, target);
        return target;
    }

    public void SaveCopy(string sourcePath, string targetPath)
    {
        string? dir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(dir))
            System.IO.Directory.CreateDirectory(dir);

        File.Copy(sourcePath, targetPath, overwrite: true);
    }

    /// <summary>Varsayılan kopya adı önerisi: Road_001.jpg → Road_001_Copy.jpg (Guide §7.2).</summary>
    public static string SuggestCopyName(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        string ext = Path.GetExtension(path);
        return $"{name}_Copy{ext}";
    }

    public static string MakeUnique(string path)
    {
        if (!File.Exists(path)) return path;

        string? dir = Path.GetDirectoryName(path) ?? string.Empty;
        string name = Path.GetFileNameWithoutExtension(path);
        string ext = Path.GetExtension(path);

        for (int i = 2; i < 10000; i++)
        {
            string candidate = Path.Combine(dir, $"{name} ({i}){ext}");
            if (!File.Exists(candidate))
                return candidate;
        }

        return Path.Combine(dir, $"{name}_{Guid.NewGuid():N}{ext}");
    }

    /// <summary>Çıkarılabilir sürücüleri listeler (Guide §10 — USB Belleğe Kopyala).</summary>
    public static IReadOnlyList<DriveInfo> GetRemovableDrives()
    {
        try
        {
            return DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Removable)
                .ToList();
        }
        catch
        {
            return Array.Empty<DriveInfo>();
        }
    }
}
