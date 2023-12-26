using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ProducerConsumer;

/// <summary>
/// Global enum for logging support.
/// </summary>
public enum LogLevel
{
    None = 0,
    Debug = 1,
    Info = 2,
    Warning = 3,
    Error = 4,
    Success = 5,
    Important = 6,
    Event = 7,
}

// Our delegate for the event methods.
public delegate void LoggingEventHandler(string message);

/// <summary>
/// This is a logging helper that takes uses a singleton for access.</br>
/// There is no need for a constructor.</br>
/// </summary>
public class Log
{
    private static Log? _instance = null;
    private static DateTime? _date = null;
    private static string _logPath = string.Empty;
    private static string _fileName = string.Empty;

    // Events for hooking in external modules.
    public event LoggingEventHandler? OnInfo;
    public event LoggingEventHandler? OnDebug;
    public event LoggingEventHandler? OnWarning;
    public event LoggingEventHandler? OnError;
    public event LoggingEventHandler? OnSuccess;
    public event LoggingEventHandler? OnImportant;
    public event LoggingEventHandler? OnEvent;

    /// <summary>
    /// Introduces a way to call the class once without worrying about creating a working object.
    /// </summary>
    public static Log Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new Log();
                _date = DateTime.Now;
            }

            return _instance;
        }
    }

    public string LogPath
    {
        get
        {
            var title = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name ?? "Application";

            // Determine if our file name needs to be updated.
            if (string.IsNullOrEmpty(_fileName) || _date?.Day != DateTime.Now.Day)
                _fileName = $"{DateTime.Now.ToString("yyyy-MM-dd")}_{title}.log";

            // The logging path should only need to be updated once.
            if (string.IsNullOrEmpty(_logPath))
            {
                string prefix = string.Empty;

                try
                {
                    IOrderedEnumerable<DirectoryInfo>? logPaths = DriveInfo.GetDrives().Where(di => (di.DriveType == DriveType.Fixed) && di.IsReady).Select(di => di.RootDirectory).OrderByDescending(di => di.FullName);
                    var letter = logPaths.FirstOrDefault();
                    if (letter != null)
                        prefix = letter.FullName;
                    else
                        prefix = GetLastDriveLetterUsingPInvoke();

                    string root = Path.Combine(prefix, "Logs", $"{title}");
                    Directory.CreateDirectory(root);
                    _logPath = Path.Combine(root, _fileName);
                }
                catch (Exception)
                {
                    _logPath = Path.Combine(Environment.CurrentDirectory, _fileName);
                }
            }

            return _logPath;
        }
    }

    #region [Event-based Methods]
    /// <summary>
    /// Signal the OnDebug <see cref="LoggingEventHandler"/>.
    /// </summary>
    /// <param name="message">The text to write to the file.</param>
    public void Debug(string message)
    {
        if (!string.IsNullOrEmpty(message))
            OnDebug?.Invoke(message);
    }

    /// <summary>
    /// Signal the OnInfo <see cref="LoggingEventHandler"/>.
    /// </summary>
    /// <param name="message">The text to write to the file.</param>
    public void Info(string message)
    {
        if (!string.IsNullOrEmpty(message))
            OnInfo?.Invoke(message);
    }

    /// <summary>
    /// Signal the OnError <see cref="LoggingEventHandler"/>.
    /// </summary>
    /// <param name="message">The text to write to the file.</param>
    public void Error(string message)
    {
        if (!string.IsNullOrEmpty(message))
            OnError?.Invoke(message);
    }

    /// <summary>
    /// Signal the OnWarning <see cref="LoggingEventHandler"/>.
    /// </summary>
    /// <param name="message">The text to write to the file.</param>
    public void Warning(string message)
    {
        if (!string.IsNullOrEmpty(message))
            OnWarning?.Invoke(message);
    }

    /// <summary>
    /// Signal the OnSuccess <see cref="LoggingEventHandler"/>.
    /// </summary>
    /// <param name="message">The text to write to the file.</param>
    public void Success(string message)
    {
        if (!string.IsNullOrEmpty(message))
            OnSuccess?.Invoke(message);
    }

    /// <summary>
    /// Signal the OnImportant <see cref="LoggingEventHandler"/>.
    /// </summary>
    /// <param name="message">The text to write to the file.</param>
    public void Important(string message)
    {
        if (!string.IsNullOrEmpty(message))
            OnImportant?.Invoke(message);
    }
    #endregion [Event-based Methods]

    /// <summary>
    /// <see cref="Log.Instance"/> callback for console output.
    /// </summary>
    public void WriteConsole(string message, LogLevel level = LogLevel.None)
    {
        if (Console.OutputEncoding != Encoding.UTF8)
            Console.OutputEncoding = Encoding.UTF8;

        ConsoleColor tmpfgnd = Console.ForegroundColor;
        ConsoleColor tmpbgnd = Console.BackgroundColor;

        switch (level)
        {
            case LogLevel.None:
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine(new string('─', 82));
                Console.ForegroundColor = tmpfgnd;
                return;
            case LogLevel.Debug:
                OnDebug?.Invoke(message);
                Console.ForegroundColor = ConsoleColor.Gray;
                break;
            case LogLevel.Info:
                OnInfo?.Invoke(message);
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                break;
            case LogLevel.Success:
                OnSuccess?.Invoke(message);
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                break;
            case LogLevel.Warning:
                OnWarning?.Invoke(message);
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                break;
            case LogLevel.Error:
                OnError?.Invoke(message);
                Console.ForegroundColor = ConsoleColor.DarkRed;
                break;
            case LogLevel.Event:
                OnEvent?.Invoke(message);
                Console.ForegroundColor = ConsoleColor.Magenta;
                break;
            case LogLevel.Important:
                OnImportant?.Invoke(message);
                Console.ForegroundColor = ConsoleColor.DarkBlue;
                Console.BackgroundColor = ConsoleColor.White;
                Console.WriteLine($" {message} ");
                Console.BackgroundColor = tmpbgnd;
                Console.ForegroundColor = tmpfgnd;
                return;
            default:
                Console.ForegroundColor = ConsoleColor.Gray;
                break;
        }
        Console.WriteLine($"[{level}] {message}");
        Console.ForegroundColor = tmpfgnd;
    }

    /// <summary>
    /// Core logging method with <see cref="System.IO.StreamWriter"/>.</br>
    /// This is usually called from the event delegate handler, but you may call it directly.</br>
    /// </summary>
    /// <param name="message">The text to write to the file.</param>
    /// <param name="level"><see cref="LogLevel"/></param>
    public void WriteFile(string message, LogLevel level = LogLevel.Info, [System.Runtime.CompilerServices.CallerMemberName] string origin = "", [System.Runtime.CompilerServices.CallerFilePath] string filePath = "", [System.Runtime.CompilerServices.CallerLineNumber] int lineNumber = 0)
    {
        // Format the message.
        message = $"[{DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss.fff tt")} -> {System.IO.Path.GetFileName(filePath)} -> {origin}(line {lineNumber})] {message}";

        switch (level)
        {
            case LogLevel.None:
                // Skip file write if LogLevel=None
                return;
            case LogLevel.Debug:
                OnDebug?.Invoke(message);
                break;
            case LogLevel.Info:
                OnInfo?.Invoke(message);
                break;
            case LogLevel.Warning:
                OnWarning?.Invoke(message);
                break;
            case LogLevel.Error:
                OnError?.Invoke(message);
                break;
            case LogLevel.Success:
                OnSuccess?.Invoke(message);
                break;
            case LogLevel.Important:
                OnImportant?.Invoke(message);
                break;
            case LogLevel.Event:
                OnEvent?.Invoke(message);
                break;
            default:
                break;
        }

        using (var fileStream = new StreamWriter(File.OpenWrite(LogPath), System.Text.Encoding.UTF8))
        {
            // Jump to the end of the file before writing (same as append).
            fileStream.BaseStream.Seek(0, SeekOrigin.End);
            // Write the text to the file (adds CRLF automatically).
            fileStream.WriteLine(message);
        }
    }

    /// <summary>
    /// Core logging method with <see cref="System.IO.StreamWriter"/>.</br>
    /// This is usually called from the event delegate handler, but you may call it directly.</br>
    /// </summary>
    /// <param name="message">The text to write to the file.</param>
    /// <param name="level"><see cref="LogLevel"/></param>
    /// <param name="formatParams">additional objects to log</param>
    public void WriteFile(string message, LogLevel level = LogLevel.Info, [System.Runtime.CompilerServices.CallerMemberName] string origin = "", [System.Runtime.CompilerServices.CallerFilePath] string filePath = "", [System.Runtime.CompilerServices.CallerLineNumber] int lineNumber = 0, params object[] formatParams)
    {
        if (formatParams != null)
        {
            try { message = String.Format(message, formatParams); }
            catch (Exception ex) { Console.WriteLine($"[Logger.Write()]: {ex.Message}"); }
        }

        // Format the message
        message = $"[{DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss.fff tt")} -> {System.IO.Path.GetFileName(filePath)} -> {origin}(line {lineNumber})] {message}";

        switch (level)
        {
            case LogLevel.None:
                // Skip file write if LogLevel=None
                return;
            case LogLevel.Debug:
                OnDebug?.Invoke(message);
                break;
            case LogLevel.Info:
                OnInfo?.Invoke(message);
                break;
            case LogLevel.Warning:
                OnWarning?.Invoke(message);
                break;
            case LogLevel.Error:
                OnError?.Invoke(message);
                break;
            case LogLevel.Success:
                OnSuccess?.Invoke(message);
                break;
            case LogLevel.Important:
                OnImportant?.Invoke(message);
                break;
            case LogLevel.Event:
                OnEvent?.Invoke(message);
                break;
            default:
                break;
        }

        using (var fileStream = new StreamWriter(File.OpenWrite(LogPath), System.Text.Encoding.UTF8))
        {
            // Jump to the end of the file before writing (same as append).
            fileStream.BaseStream.Seek(0, SeekOrigin.End);
            // Write the text to the file (adds CRLF automatically).
            fileStream.WriteLine(message);
        }
    }

    /// <summary>
    /// Logging test method extension.
    /// </summary>
    /// <remarks>asynchronous</remarks>
    public async Task<bool> LogLocalAsync(string message, string logName = null)
    {
        try
        {
            string name = logName ?? $"{DateTime.Now.ToString("yyyy-MM-dd")}.log";
            //string path1 = System.IO.Directory.GetParent(Assembly.GetExecutingAssembly().Location)?.ToString() ?? Environment.CurrentDirectory;
            string path2 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{name}");
            await Task.Run(() => { File.AppendAllText(path2, $"[{DateTime.Now.ToString("hh:mm:ss.fff tt")}] {message}{Environment.NewLine}", System.Text.Encoding.UTF8); });
            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"> LogLocalAsync: {ex.Message}");
            return await Task.FromResult(false);
        }
    }

    /// <summary>
    /// Debug method (can be removed)
    /// </summary>
    public void TestDateChanged() => _date = DateTime.Now.Subtract(new TimeSpan(1, 0, 0, 0)); // Set to yesterday.


    /// <summary>
    /// Returns a list of all logical drives on the system.</br>
    /// NOTE: This includes network mapped drives.</br>
    /// <returns><see cref="List{T}"/>TResult</returns>
    public List<string> GetAllDrives()
    {
        List<string> drives = new();
        try
        {
            string[] lds = System.IO.Directory.GetLogicalDrives();
            foreach (var str in lds) { drives.Add(str); }
        }
        catch (System.IO.IOException)
        {
            System.Diagnostics.Debug.WriteLine("> An I/O error occurred.");
        }
        catch (System.Security.SecurityException)
        {
            System.Diagnostics.Debug.WriteLine("> The caller does not have the required permission.");
        }
        return drives;
    }

    /// <summary>
    /// Fetches the last drive letter using the ManagementObjectSearcher.
    /// </summary>
    //public string GetLastDriveLetterUsingWMI()
    //{
    //    char lastDriveLetter = 'C'; // Start with the first drive letter
    //    try
    //    {
    //        ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_LogicalDisk WHERE DriveType=3");
    //        ManagementObjectCollection drives = searcher.Get();
    //        foreach (ManagementObject drive in drives)
    //        {
    //            string driveLetter = drive["DeviceID"].ToString();
    //            char currentLetter = driveLetter[0];
    //            if (currentLetter > lastDriveLetter)
    //            {
    //                lastDriveLetter = currentLetter;
    //            }
    //        }
    //        Console.WriteLine($"The last fixed drive letter is: {lastDriveLetter}");
    //    }
    //    catch (Exception ex)
    //    {
    //        Console.WriteLine($"Error: {ex.Message}");
    //    }
    //    return $"{lastDriveLetter}:";
    //}

    /// <summary>
    /// Fetches the last drive letter using the kernel32.dll.
    /// </summary>
    public string GetLastDriveLetterUsingPInvoke()
    {
        char lastDriveLetter = 'Z';
        try
        {
            uint drives = GetLogicalDrives();

            // Start with Z and iterate backward to find the last fixed drive
            for (char driveLetter = 'Z'; driveLetter >= 'A'; driveLetter--)
            {
                uint mask = 1u << (driveLetter - 'A');
                if ((drives & mask) != 0)
                {   // Check if the drive is a fixed drive
                    uint driveType = GetDriveType(driveLetter + @":\");
                    if (driveType == 3) // 3 represents a fixed drive
                    {
                        lastDriveLetter = driveLetter;
                        Console.WriteLine($"The last fixed drive letter is: {driveLetter}");
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }

        return $"{lastDriveLetter}:";
    }

    [DllImport("kernel32.dll")]
    public static extern uint GetLogicalDrives();

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern uint GetDriveType(string lpRootPathName);
}
