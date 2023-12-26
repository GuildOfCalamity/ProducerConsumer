using System.ComponentModel;
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
    public const int GWLP_WNDPROC = -4;
    public const int GWL_EXSTYLE = -20;
    public const int SM_CXSCREEN = 0;
    public const int SM_CYSCREEN = 1;

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
    
    [StructLayout(LayoutKind.Sequential)]
    internal struct SYSTEM_INFO // only used for GetNativeSystemInfo calls
    {
        internal ushort wProcessorArchitecture;
        internal ushort wReserved;
        internal uint dwPageSize;
        internal IntPtr lpMinimumApplicationAddress;
        internal IntPtr lpMaximumApplicationAddress;
        internal IntPtr dwActiveProcessorMask;
        internal uint dwNumberOfProcessors;
        internal uint dwProcessorType;
        internal uint dwAllocationGranularity;
        internal ushort wProcessorLevel;
        internal ushort wProcessorRevision;
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

    [DllImport("gdi32.dll", EntryPoint = "AddFontResourceW", SetLastError = true)]
    public static extern int AddFontResource([In][MarshalAs(UnmanagedType.LPWStr)] string lpFileName);

    [DllImport("User32.dll")]
    public static extern int GetSystemMetrics(int nIndex);

    [DllImport("kernel32.dll")]
    internal static extern void GetNativeSystemInfo(out SYSTEM_INFO lpSystemInfo);

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
    /// Installs the font <paramref name="path"/> on the user's machine.
    /// </summary>
    /// <param name="path">"C:\Fonts\NewFont.ttf"</param>
    public static void InstallFont(string path)
    {
        if (string.IsNullOrEmpty(path))
            return;

        if (!File.Exists(path))
        {
            Console.WriteLine($"[InstallFont]: File not found! \"{path}\"");
            return;
        }

        var result = AddFontResource(path + '\0');
        var errCode = Marshal.GetLastWin32Error();
        if (errCode == 122)
            Console.WriteLine($"This may appear the first time and then will not happen again: {new Win32Exception(errCode).Message}");
        else if (errCode != 0)
            Console.WriteLine($"[InstallFont]: {new Win32Exception(errCode).Message}");
        else
            Debug.WriteLine((result == 0) ? "[InstallFont]: Font is already installed." : "[InstallFont]: Font installed successfully.");

        #region [To have the font persist after reboot a registry entry must be created]
        /*
        Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts");
        key.SetValue("Font Description", "FontFileName.tff");
        key.Close();
        */
        #endregion
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
    /// P/Invoke style desktop dimension getter.
    /// </summary>
    /// <returns>tuple</returns>
    public static (int width, int height) GetScreenDimensions()
    {
        try
        {
            int sWidth = GetSystemMetrics(SM_CXSCREEN);
            int sHeight = GetSystemMetrics(SM_CYSCREEN);
            return (sWidth, sHeight);
        }
        catch (Exception)
        {
            return (-1, -1);
        }
    }

    /// <summary>
    /// P/Invoke style Recycle Bin checker.
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

#region Windows TaskBar Progress
/// <summary>
/// Helper class to set taskbar progress on Windows 7+ systems.
/// </summary>
public static class TaskbarProgress
{
    /// <summary>
    /// Available taskbar progress states
    /// </summary>
    public enum TaskbarStates
    {
        /// <summary>No progress displayed</summary>
        NoProgress = 0,
        /// <summary>Indeterminate</summary>
        Indeterminate = 0x1,
        /// <summary>Normal</summary>
        Normal = 0x2,
        /// <summary>Error</summary>
        Error = 0x4,
        /// <summary>Paused</summary>
        Paused = 0x8
    }

    [ComImportAttribute()]
    [GuidAttribute("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf")]
    [InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITaskbarList3
    {
        // ITaskbarList
        [PreserveSig]
        void HrInit();
        [PreserveSig]
        void AddTab(IntPtr hwnd);
        [PreserveSig]
        void DeleteTab(IntPtr hwnd);
        [PreserveSig]
        void ActivateTab(IntPtr hwnd);
        [PreserveSig]
        void SetActiveAlt(IntPtr hwnd);

        // ITaskbarList2
        [PreserveSig]
        void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fFullscreen);

        // ITaskbarList3
        [PreserveSig]
        void SetProgressValue(IntPtr hwnd, UInt64 ullCompleted, UInt64 ullTotal);
        [PreserveSig]
        void SetProgressState(IntPtr hwnd, TaskbarStates state);
    }

    [GuidAttribute("56FDF344-FD6D-11d0-958A-006097C9A090")]
    [ClassInterfaceAttribute(ClassInterfaceType.None)]
    [ComImportAttribute()]
    private class TaskbarInstance
    {
    }

    private static readonly bool taskbarSupported = IsWindows7OrLater;
    private static readonly ITaskbarList3? taskbarInstance = taskbarSupported ? (ITaskbarList3)new TaskbarInstance() : null;

    /// <summary>
    /// Sets the state of the taskbar progress.
    /// </summary>
    /// <param name="windowHandle">current form handle</param>
    /// <param name="taskbarState">desired state</param>
    public static void SetState(IntPtr windowHandle, TaskbarStates taskbarState)
    {
        if (taskbarSupported)
        {
            taskbarInstance?.SetProgressState(windowHandle, taskbarState);
        }
    }

    /// <summary>
    /// Sets the value of the taskbar progress.
    /// </summary>
    /// <param name="windowHandle">current form handle</param>
    /// <param name="progressValue">desired progress value</param>
    /// <param name="progressMax">maximum progress value</param>
    public static void SetValue(IntPtr windowHandle, double progressValue, double progressMax)
    {
        if (taskbarSupported)
        {
            taskbarInstance?.SetProgressValue(windowHandle, (ulong)progressValue, (ulong)progressMax);
        }
    }

    /// <summary>
    /// Determines if current operating system is Windows 7+.
    /// </summary>
    public static bool IsWindows7OrLater => Environment.OSVersion.Version >= new Version(6, 1);

    /// <summary>
    /// Determines if current operating system is Vista+.
    /// </summary>
    public static bool IsWindowsVistaOrLater => Environment.OSVersion.Platform == PlatformID.Win32NT && Environment.OSVersion.Version >= new Version(6, 0, 6000);
}
#endregion
