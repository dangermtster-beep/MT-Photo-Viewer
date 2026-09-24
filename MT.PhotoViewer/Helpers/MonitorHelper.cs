using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MT.PhotoViewer.Helpers;

/// <summary>
/// WindowChrome ile custom title bar kullanıldığında pencere ekranı kapladığında
/// görev çubuğunun üstüne taşar ve kenarlardan taşar. Bu yardımcı sınıf
/// maksimum boyutu çalışma alanına sabitler ve gerçek tam ekran için
/// monitörün tam sınırlarını verir (Guide §21).
/// </summary>
public static class MonitorHelper
{
    private const int WM_GETMINMAXINFO = 0x0024;
    private const int MONITOR_DEFAULTTONEAREST = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X, Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

    private delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, ref RECT rect, IntPtr data);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);

    /// <summary>Bağlı monitörler içindeki en uzun kenar (fiziksel piksel).</summary>
    public static int GetLargestMonitorEdge()
    {
        int largest = 0;

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr _, IntPtr _, ref RECT r, IntPtr _) =>
        {
            largest = Math.Max(largest, Math.Max(r.Right - r.Left, r.Bottom - r.Top));
            return true;
        }, IntPtr.Zero);

        return largest > 0 ? largest : 1920;
    }

    /// <summary>
    /// Pencereye "ekranı kapla" sınırlandırmasını ekler.
    /// <paramref name="bypass"/> true döndüğünde (tam ekran modu) sınırlandırma uygulanmaz;
    /// aksi halde ptMaxTrackSize pencerenin görev çubuğunu kaplamasını da engellerdi.
    /// </summary>
    public static void HookMaximizeFix(Window window, Func<bool>? bypass = null)
    {
        if (PresentationSource.FromVisual(window) is HwndSource existing)
        {
            existing.AddHook(Hook);
            return;
        }

        window.SourceInitialized += (_, _) =>
        {
            var source = (HwndSource)PresentationSource.FromVisual(window)!;
            source.AddHook(Hook);
        };

        IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (bypass?.Invoke() == true) return IntPtr.Zero;
            return WndProc(hwnd, msg, wParam, lParam, ref handled);
        }
    }

    private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WM_GETMINMAXINFO) return IntPtr.Zero;

        IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (monitor == IntPtr.Zero) return IntPtr.Zero;

        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(monitor, ref info)) return IntPtr.Zero;

        var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);

        mmi.ptMaxPosition.X = info.rcWork.Left - info.rcMonitor.Left;
        mmi.ptMaxPosition.Y = info.rcWork.Top - info.rcMonitor.Top;
        mmi.ptMaxSize.X = info.rcWork.Right - info.rcWork.Left;
        mmi.ptMaxSize.Y = info.rcWork.Bottom - info.rcWork.Top;
        mmi.ptMaxTrackSize = mmi.ptMaxSize;

        Marshal.StructureToPtr(mmi, lParam, true);
        handled = true;
        return IntPtr.Zero;
    }

    /// <summary>Pencerenin bulunduğu monitörün çalışma alanı (görev çubuğu hariç), WPF birimlerinde.</summary>
    public static Rect GetWorkAreaBounds(Window window) => GetBounds(window, work: true);

    /// <summary>Pencerenin bulunduğu monitörün tam sınırları (görev çubuğu dahil), WPF birimlerinde.</summary>
    public static Rect GetFullScreenBounds(Window window) => GetBounds(window, work: false);

    private static Rect GetBounds(Window window, bool work)
    {
        var fallback = new Rect(
            SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);

        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return fallback;

        IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (monitor == IntPtr.Zero) return fallback;

        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(monitor, ref info)) return fallback;

        RECT r = work ? info.rcWork : info.rcMonitor;
        var deviceRect = new Rect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);

        // Fiziksel pikselleri WPF birimlerine çevir.
        if (PresentationSource.FromVisual(window)?.CompositionTarget is { } target)
        {
            Point topLeft = target.TransformFromDevice.Transform(deviceRect.TopLeft);
            Point bottomRight = target.TransformFromDevice.Transform(deviceRect.BottomRight);
            return new Rect(topLeft, bottomRight);
        }

        return deviceRect;
    }
}
