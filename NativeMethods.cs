using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;

namespace QuickUp;

internal enum TaskbarEdge
{
    Left = 0,
    Top = 1,
    Right = 2,
    Bottom = 3
}

/// <summary>
/// Win32 P/Invoke wrappers for DWM window chrome, taskbar geometry, DPI scaling, focus, and WebView2 loader discovery.
/// </summary>
internal static class NativeMethods
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_BORDER_COLOR = 34;
    private const int DWMWCP_ROUND = 2;

    private const uint ABM_GETTASKBARPOS = 5;
    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const int MDT_EFFECTIVE_DPI = 0;

    private static bool _loaderPathConfigured;

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public Rectangle ToRectangle() => Rectangle.FromLTRB(Left, Top, Right, Bottom);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct APPBARDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uCallbackMessage;
        public uint uEdge;
        public RECT rc;
        public IntPtr lParam;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("shell32.dll")]
    private static extern IntPtr SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const ushort VK_CONTROL = 0x11;

    /// <summary>
    /// Synthesizes Ctrl+&lt;key&gt; to the currently focused control (e.g. WebView2 child window).
    /// </summary>
    public static void SendCtrlShortcut(ushort virtualKey)
    {
        var inputs = new INPUT[4];
        inputs[0] = CreateKeyInput(VK_CONTROL, 0);
        inputs[1] = CreateKeyInput(virtualKey, 0);
        inputs[2] = CreateKeyInput(virtualKey, KEYEVENTF_KEYUP);
        inputs[3] = CreateKeyInput(VK_CONTROL, KEYEVENTF_KEYUP);
        _ = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    private static INPUT CreateKeyInput(ushort vk, uint flags) => new()
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion
        {
            ki = new KEYBDINPUT { wVk = vk, dwFlags = flags }
        }
    };

    /// <summary>
    /// Enables or disables the Windows 10/11 dark title bar on the given window handle.
    /// </summary>
    public static void ApplyDarkTitleBar(IntPtr hwnd, bool dark)
    {
        if (hwnd == IntPtr.Zero) return;
        try
        {
            int value = dark ? 1 : 0;
            _ = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
        }
        catch
        {
            // Ignore on older Windows versions without this attribute.
        }
    }

    /// <summary>
    /// Applies Windows 11 rounded corners to a top-level or frameless window.
    /// </summary>
    public static void ApplyRoundedCorners(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        try
        {
            int preference = DWMWCP_ROUND;
            _ = DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
        }
        catch
        {
            // Ignore on Windows 10 where DWMWA_WINDOW_CORNER_PREFERENCE is unsupported.
        }
    }

    /// <summary>
    /// Sets the DWM 1px outer border color on Windows 11.
    /// </summary>
    public static void ApplyBorderColor(IntPtr hwnd, Color color)
    {
        if (hwnd == IntPtr.Zero) return;
        try
        {
            int colorRef = (color.B << 16) | (color.G << 8) | color.R;
            _ = DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref colorRef, sizeof(int));
        }
        catch
        {
            // Ignore on Windows 10 where DWMWA_BORDER_COLOR is unsupported.
        }
    }

    /// <summary>
    /// Queries the current primary taskbar edge and screen bounds via SHAppBarMessage.
    /// </summary>
    public static bool GetTaskbarPosition(out TaskbarEdge edge, out Rectangle bounds)
    {
        var data = new APPBARDATA
        {
            cbSize = (uint)Marshal.SizeOf<APPBARDATA>()
        };

        try
        {
            if (SHAppBarMessage(ABM_GETTASKBARPOS, ref data) != IntPtr.Zero)
            {
                edge = data.uEdge <= 3 ? (TaskbarEdge)data.uEdge : TaskbarEdge.Bottom;
                bounds = data.rc.ToRectangle();
                return true;
            }
        }
        catch
        {
            // Fall through to screen-based fallback.
        }

        var primary = Screen.PrimaryScreen ?? Screen.AllScreens[0];
        var total = primary.Bounds;
        var work = primary.WorkingArea;

        if (work.Top > total.Top)
        {
            edge = TaskbarEdge.Top;
            bounds = new Rectangle(total.Left, total.Top, total.Width, work.Top - total.Top);
        }
        else if (work.Left > total.Left)
        {
            edge = TaskbarEdge.Left;
            bounds = new Rectangle(total.Left, total.Top, work.Left - total.Left, total.Height);
        }
        else if (work.Right < total.Right)
        {
            edge = TaskbarEdge.Right;
            bounds = new Rectangle(work.Right, total.Top, total.Right - work.Right, total.Height);
        }
        else
        {
            edge = TaskbarEdge.Bottom;
            bounds = new Rectangle(total.Left, work.Bottom, total.Width, Math.Max(40, total.Bottom - work.Bottom));
        }

        return false;
    }

    /// <summary>
    /// Returns the effective horizontal DPI for the monitor nearest the given screen point.
    /// </summary>
    public static int GetDpiForPoint(Point point)
    {
        try
        {
            var pt = new POINT { X = point.X, Y = point.Y };
            IntPtr monitor = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);
            if (monitor != IntPtr.Zero && GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0 && dpiX >= 72)
            {
                return (int)dpiX;
            }
        }
        catch
        {
            // Fall back to standard 96 DPI.
        }

        return 96;
    }

    /// <summary>
    /// Locates WebView2Loader.dll across unpackaged directories and .NET single-file extraction directories.
    /// Must run before CoreWebView2Environment.CreateAsync.
    /// </summary>
    public static void ConfigureWebView2LoaderPath()
    {
        if (_loaderPathConfigured) return;

        string archFolder = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "win-arm64",
            Architecture.X86 => "win-x86",
            _ => "win-x64"
        };

        var candidates = new List<string>
        {
            AppContext.BaseDirectory,
            Path.Combine(AppContext.BaseDirectory, "runtimes", archFolder, "native")
        };

        if (AppContext.GetData("NATIVE_DLL_SEARCH_DIRECTORIES") is string nativeDirs &&
            !string.IsNullOrWhiteSpace(nativeDirs))
        {
            foreach (string rawDir in nativeDirs.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                candidates.Add(rawDir);
                candidates.Add(Path.Combine(rawDir, "runtimes", archFolder, "native"));
            }
        }

        foreach (string dir in candidates)
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            string loaderPath = Path.Combine(dir, "WebView2Loader.dll");
            if (File.Exists(loaderPath))
            {
                CoreWebView2Environment.SetLoaderDllFolderPath(dir);
                _loaderPathConfigured = true;
                return;
            }
        }
    }
}
