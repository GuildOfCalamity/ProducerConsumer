using System.Runtime.InteropServices;

namespace ProducerConsumer;

/// <summary>
/// Here's a bunch of juicy extras for all of your console desires.
/// </summary>
public static class ConsoleHelper
{
    public const int SW_HIDE = 0;
    public const int SW_MAXIMIZE = 3;
    public const int SW_MINIMIZE = 6;
    public const int SW_RESTORE = 9;
    public const int SWP_NOZORDER = 0x4;
    public const int SWP_NOACTIVATE = 0x10;

    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct COORD
    {
        public short X;
        public short Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct SMALL_RECT
    {
        public short Left;
        public short Top;
        public short Right;
        public short Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct CONSOLE_SCREEN_BUFFER_INFO
    {
        public COORD dwSize;
        public COORD dwCursorPosition;
        public ushort wAttributes;
        public SMALL_RECT srWindow;
        public COORD dwMaximumWindowSize;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct FontInfo
    {
        internal int cbSize;
        internal int FontIndex;
        internal short FontWidth;
        public short FontSize;
        public int FontFamily;
        public int FontWeight;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        //[MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.wc, SizeConst = 32)]
        public string FontName;
    }

    [Flags]
    public enum EXECUTION_STATE : uint
    {
        ES_AWAYMODE_REQUIRED = 0x00000040,
        ES_CONTINUOUS = 0x80000000,
        ES_DISPLAY_REQUIRED = 0x00000002,
        ES_SYSTEM_REQUIRED = 0x00000001
        //ES_USER_PRESENT = 0x00000004 // <-- Legacy flag, do not use.
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GetConsoleScreenBufferInfo(IntPtr hConsoleOutput, out CONSOLE_SCREEN_BUFFER_INFO lpConsoleScreenBufferInfo);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(System.IntPtr hWnd, int cmdShow);

    [DllImport("user32.dll", EntryPoint = "FindWindow", SetLastError = true)]
    public static extern IntPtr FindWindowByCaption(IntPtr zeroOnly, string lpWindowName);

    [DllImport("kernel32")]
    public static extern IntPtr GetConsoleWindow();

    [DllImport("user32")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, int flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

    private const int FixedWidthTrueType = 54;
    private const int StandardOutputHandle = -11;

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr GetStdHandle(int nStdHandle);

    [return: MarshalAs(UnmanagedType.Bool)]
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern bool SetCurrentConsoleFontEx(IntPtr hConsoleOutput, bool MaximumWindow, ref FontInfo ConsoleCurrentFontEx);

    [return: MarshalAs(UnmanagedType.Bool)]
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern bool GetCurrentConsoleFontEx(IntPtr hConsoleOutput, bool MaximumWindow, ref FontInfo ConsoleCurrentFontEx);

    private static readonly IntPtr ConsoleOutputHandle = GetStdHandle(StandardOutputHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool Beep(int frequency, int duration);

    public static FontInfo[] SetCurrentFont(string font, short fontSize = 0)
    {
        Debug.WriteLine("Set Current Font: " + font);
        FontInfo tmp = new FontInfo();
        FontInfo before = new FontInfo { cbSize = Marshal.SizeOf(tmp) };
        if (GetCurrentConsoleFontEx(ConsoleOutputHandle, false, ref before))
        {
            FontInfo set = new FontInfo
            {
                cbSize = Marshal.SizeOf(tmp),
                FontIndex = 0,
                FontFamily = FixedWidthTrueType,
                FontName = font,
                FontWeight = 400,
                FontSize = fontSize > 0 ? fontSize : before.FontSize
            };
            // Get some settings from current font.
            if (!SetCurrentConsoleFontEx(ConsoleOutputHandle, false, ref set))
            {
                var ex = Marshal.GetLastWin32Error();
                Console.WriteLine("[SetCurrentFont]: " + ex);
                throw new System.ComponentModel.Win32Exception(ex);
            }
            FontInfo after = new FontInfo { cbSize = Marshal.SizeOf(tmp) };
            GetCurrentConsoleFontEx(ConsoleOutputHandle, false, ref after);
            return new[] { before, set, after };
        }
        else
        {
            var er = Marshal.GetLastWin32Error();
            Console.WriteLine("[SetCurrentFont]: " + er);
            throw new System.ComponentModel.Win32Exception(er);
        }
    }

    /// <summary>
    /// Sets the console window location and size in pixels
    /// </summary>
    public static void SetWindowPosition(IntPtr handle, int x, int y, int width, int height)
    {
        SetWindowPos(handle, IntPtr.Zero, x, y, width, height, SWP_NOZORDER | SWP_NOACTIVATE);
    }

    /// <summary>
    /// Gets the console window buffer size
    /// </summary>
    public static (int width, int height) GetConsoleSize()
    {
        if (GetConsoleScreenBufferInfo(GetStdHandle(-11), out CONSOLE_SCREEN_BUFFER_INFO csbi))
        {
            var width = csbi.srWindow.Right - csbi.srWindow.Left + 1;
            var height = csbi.srWindow.Bottom - csbi.srWindow.Top + 1;
            Debug.WriteLine($"Width: {width}, Height: {height}");
            return (width, height);
        }
        else
        {
            Debug.WriteLine("Failed to get console buffer info.");
            return (0, 0);
        }
    }
}