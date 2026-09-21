using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace SanchesTV.Desktop;

internal static class Windows11Backdrop
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
    private const int WM_GETMINMAXINFO = 0x0024;
    private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

    public static void Apply(Window window)
    {
        try
        {
            var helper = new WindowInteropHelper(window);
            var hwnd = helper.Handle;
            if (hwnd == IntPtr.Zero)
                return;

            var enabled = 1;
            var rounded = 2;
            var mica = 2;

            _ = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref enabled, sizeof(int));
            _ = DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref rounded, sizeof(int));
            _ = DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref mica, sizeof(int));

            if (HwndSource.FromHwnd(hwnd) is { } source)
                source.AddHook(WindowProc);

            window.Background = new SolidColorBrush(Color.FromArgb(1, 12, 14, 19));
        }
        catch
        {
            window.Background = new SolidColorBrush(Color.FromRgb(12, 14, 19));
        }
    }

    private static IntPtr WindowProc(
        IntPtr hwnd,
        int msg,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (msg != WM_GETMINMAXINFO)
            return IntPtr.Zero;

        try
        {
            var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
            var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);

            if (monitor != IntPtr.Zero)
            {
                var monitorInfo = new MONITORINFO
                {
                    cbSize = Marshal.SizeOf<MONITORINFO>()
                };

                if (GetMonitorInfo(monitor, ref monitorInfo))
                {
                    var work = monitorInfo.rcWork;
                    var area = monitorInfo.rcMonitor;

                    mmi.ptMaxPosition.x = Math.Abs(work.left - area.left);
                    mmi.ptMaxPosition.y = Math.Abs(work.top - area.top);
                    mmi.ptMaxSize.x = Math.Abs(work.right - work.left);
                    mmi.ptMaxSize.y = Math.Abs(work.bottom - work.top);
                    mmi.ptMaxTrackSize.x = mmi.ptMaxSize.x;
                    mmi.ptMaxTrackSize.y = mmi.ptMaxSize.y;

                    Marshal.StructureToPtr(mmi, lParam, true);
                    handled = true;
                }
            }
        }
        catch
        {
            // Se o Windows não fornecer os dados do monitor, o comportamento padrão é mantido.
        }

        return IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
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

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int dwAttribute,
        ref int pvAttribute,
        int cbAttribute);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
}
