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

    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 0, CharSet = CharSet.Auto)]
    public struct SHQUERYRBINFO
    {
        public int cbSize;
        public long i64Size;
        public long i64NumItems;
    }

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

    #region [Recycle Bin]
    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    internal static extern int SHQueryRecycleBin(string pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern IntPtr FindFirstFile(string lpFileName, out WIN32_FIND_DATA lpFindFileData);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern bool FindNextFile(IntPtr hFindFile, out WIN32_FIND_DATA lpFindFileData);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool FindClose(IntPtr hFindFile);

    [StructLayout(LayoutKind.Sequential)]
    public struct FILETIME
    {
        public uint dwLowDateTime;  // The low-order part of the file time. This represents the less significant bits of the file time.
        public uint dwHighDateTime; // The high-order part of the file time. This represents the more significant bits of the file time.
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct WIN32_FIND_DATA
    {
        public uint dwFileAttributes;
        public FILETIME ftCreationTime;
        public FILETIME ftLastAccessTime;
        public FILETIME ftLastWriteTime;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        public uint dwReserved0;
        public uint dwReserved1;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string cFileName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
        public string cAlternateFileName;
    }
    #endregion
    
    public static FontInfo[] SetCurrentFont(string font, short fontSize = 0)
    {
        Debug.WriteLine("[SetCurrentFont]: " + font);
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
                if (ex != 0)
                {
                    Console.WriteLine("[SetCurrentFont]: " + ex);
                    throw new System.ComponentModel.Win32Exception(ex);
                }
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
    /// Sets the console window location and size in pixels.
    /// </summary>
    public static void SetWindowPosition(IntPtr handle, int x, int y, int width, int height)
    {
        SetWindowPos(handle, IntPtr.Zero, x, y, width, height, SWP_NOZORDER | SWP_NOACTIVATE);
    }

    /// <summary>
    /// Gets the console buffer size.
    /// </summary>
    public static (int width, int height) GetConsoleSize()
    {
        if (GetConsoleScreenBufferInfo(GetStdHandle(-11), out CONSOLE_SCREEN_BUFFER_INFO csbi))
        {
            var width = csbi.srWindow.Right - csbi.srWindow.Left + 1;
            var height = csbi.srWindow.Bottom - csbi.srWindow.Top + 1;
            Debug.WriteLine($"WindowWidth: {width}, WindowHeight: {height}");
            Debug.WriteLine($"MaximumWindowSize.X: {csbi.dwMaximumWindowSize.X}, MaximumWindowSize.Y: {csbi.dwMaximumWindowSize.Y}");
            // dwSize.Y will typically be large, like 9000 or similar.
            Debug.WriteLine($"BufferSize.X: {csbi.dwSize.X}, BufferSize.Y: {csbi.dwSize.Y}");
            return (width, height);
        }
        else
        {
            Debug.WriteLine("[GetConsoleSize]: Failed to get console buffer info.");
            return (0, 0);
        }
    }

    /// <summary>
    /// Gets the console window size.
    /// </summary>
    public static (int width, int height) GetWindowSize(IntPtr conHwnd)
    {
        if (GetWindowRect(conHwnd, out RECT rect))
        {
            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;
            Debug.WriteLine($"Console window size: {width} pixels x {height} pixels");
            return (width, height);
        }
        else
        {
            Debug.WriteLine("[GetWindowSize]: Failed to get window size info.");
            return (0, 0);
        }
    }

    /// <summary>
    /// A pinvoke style Recycle Bin checker.
    /// </summary>
    public static void CheckRecycleBin()
    {
        int S_OK = 0;
        SHQUERYRBINFO sqrbi = new SHQUERYRBINFO();
        sqrbi.cbSize = Marshal.SizeOf(typeof(SHQUERYRBINFO));
        int hresult = SHQueryRecycleBin(null, ref sqrbi);
        if (hresult == S_OK)
        {
            Console.WriteLine("RecycleBin size: " + sqrbi.i64Size);
            Console.WriteLine("Number of items: " + sqrbi.i64NumItems);
        }
        else
        {
            Console.WriteLine("Error querying recycle bin using PInvoke.");
            var ex = Marshal.GetLastWin32Error();
            if (ex != 0)
            {
                Console.WriteLine("[CheckRecycleBin]: ErrCode " + ex);
                throw new System.ComponentModel.Win32Exception(ex);
            }

            // A typical recycle bin format is "S-1-5-21-1689413186-4051262083-785059725-1003".
            string[] entries = Directory.GetFileSystemEntries(@"C:\$Recycle.bin", "?-?-?-??*");
            if (entries.Length > 0)
            {
                Console.WriteLine($"Number of hidden files: {entries.Length}");
                foreach (var hf in entries)
                    Console.WriteLine($"{hf}");
            }
            else
            {
                Console.WriteLine($"There are no hidden files.");
            }
        }
    }

    /// <summary>
    /// A pinvoke style Recycle Bin checker using an alternative method.
    /// </summary>
    public static void CheckRecycleBinAlternative()
    {
        uint FILE_ATTRIBUTE_READONLY = 0x0001;   // The file is read-only. Applications can read the file, but cannot write to it or delete it.
        uint FILE_ATTRIBUTE_HIDDEN = 0x0002;     // The file is hidden. It is not included in an ordinary directory listing.
        uint FILE_ATTRIBUTE_SYSTEM = 0x0004;     // The file is part of, or used exclusively by, the operating system.
        uint FILE_ATTRIBUTE_DIRECTORY = 0x0010;  // The file is a directory.
        uint FILE_ATTRIBUTE_ARCHIVE = 0x0020;    // The file has been archived. Applications use this attribute to mark files for backup or removal.
        uint FILE_ATTRIBUTE_NORMAL = 0x0080;     // The file does not have other attributes set. This attribute is valid only if used alone.
        uint FILE_ATTRIBUTE_TEMPORARY = 0x0400;  // The file is being used for temporary storage.
        uint FILE_ATTRIBUTE_COMPRESSED = 0x0800; // The file is compressed.
        uint FILE_ATTRIBUTE_OFFLINE = 0x1000;    // The data of the file is not immediately available.
        uint FILE_ATTRIBUTE_ENCRYPTED = 0x2000;  // The file or directory is encrypted.

        WIN32_FIND_DATA findData;
        IntPtr findHandle = FindFirstFile(@"C:\$Recycle.Bin\*", out findData);
        if (findHandle != IntPtr.Zero)
        {
            do
            {
                Console.WriteLine($"RecycleBin: {findData.cFileName}");
                if ((findData.dwFileAttributes & FILE_ATTRIBUTE_READONLY) != 0) Console.WriteLine("FILE_ATTRIBUTE_READONLY");
                if ((findData.dwFileAttributes & FILE_ATTRIBUTE_HIDDEN) != 0) Console.WriteLine("FILE_ATTRIBUTE_HIDDEN");
                if ((findData.dwFileAttributes & FILE_ATTRIBUTE_SYSTEM) != 0) Console.WriteLine("FILE_ATTRIBUTE_SYSTEM");
                if ((findData.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) != 0) Console.WriteLine("FILE_ATTRIBUTE_DIRECTORY");
                if ((findData.dwFileAttributes & FILE_ATTRIBUTE_ARCHIVE) != 0) Console.WriteLine("FILE_ATTRIBUTE_ARCHIVE");
                if ((findData.dwFileAttributes & FILE_ATTRIBUTE_NORMAL) != 0) Console.WriteLine("FILE_ATTRIBUTE_NORMAL");
                if ((findData.dwFileAttributes & FILE_ATTRIBUTE_TEMPORARY) != 0) Console.WriteLine("FILE_ATTRIBUTE_TEMPORARY");
                if ((findData.dwFileAttributes & FILE_ATTRIBUTE_COMPRESSED) != 0) Console.WriteLine("FILE_ATTRIBUTE_COMPRESSED");
                if ((findData.dwFileAttributes & FILE_ATTRIBUTE_OFFLINE) != 0) Console.WriteLine("FILE_ATTRIBUTE_OFFLINE");
                if ((findData.dwFileAttributes & FILE_ATTRIBUTE_ENCRYPTED) != 0) Console.WriteLine("FILE_ATTRIBUTE_ENCRYPTED");
            }
            while (FindNextFile(findHandle, out findData));
            FindClose(findHandle);
        }
    }
}
