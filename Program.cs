using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Json;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using Microsoft.VisualBasic;

namespace ProducerConsumer;

/// <summary>
/// To be able to use Icon assets in this project, the NuGet package "System.Drawing.Common" was added.
/// To be able to use a Resources.resx file in this project, the NuGet package "System.Resources.Extensions" was added.
/// NOTE: If you make changes to any embedded resource assets, it is recommended that you rebuild instead of build.
/// The System.Drawing.Icon type is only supported on Windows 7 (2009) and higher.
/// My github repos are here <see href="https://github.com/GuildOfCalamity?tab=repositories"/>.
/// </summary>
public class Program
{
    #region [Properties]
    static bool _clearFlag = false;
    static bool _addingFlag = false;
    static bool _shutdown = false;
    static int _memberCount = 0;
    static int _currentIndex = 0;
    static int _maxDesired = 20;
    static Icon? _iconNormal = null;
    static Icon? _iconWarning = null;
    static Version _winVersion = new();
    static ConsoleKey _conKey = ConsoleKey.Process;
    static Settings _settings = new();
    static IntPtr _conHwnd = IntPtr.Zero;
    static Thread? _adder = null;
    static ScheduleManager _schedman = new(true);
    static ChannelManager _chanman = new(true);
    static ConcurrentManager _queueman = new(true);

    /// <summary>
    /// Macro for <see cref="Exception"/> objects.
    /// </summary>
    public static Func<Exception, string> FormatException = new Func<Exception, string>(ex =>
    {
        StringBuilder sb = new();
        sb.AppendLine("Error Source....: " + ex.Source);
        sb.AppendLine("Error Message...: " + ex.Message);
        sb.AppendLine("Stack Trace.....: " + ex.StackTrace);
        sb.AppendLine("InnerEx(Source).: " + ex.InnerException?.Source);
        sb.AppendLine("InnerEx(Message): " + ex.InnerException?.Message);
        sb.AppendLine("InnerEx(Trace)..: " + ex.InnerException?.StackTrace);
        return sb.ToString();
    });

    /// <value>
    /// The <c>Title</c> property represents the name of the application.
    /// </value>
    /// <remarks>
    /// The <see cref="Title"/> is a <see langword="string"/>.
    /// <para>
    /// Note that there isn't a way to provide a "cref" to each accessor, only to the property itself.
    /// </para>
    /// </remarks>
    public static string Title { get; set; } = AppDomain.CurrentDomain.FriendlyName.Replace(".exe", "").SeparateCamelCase();
    #endregion

    static void Main(string[] args)
    {
        #region [Initialization and Extras]
        // Keep watch for any errant wrong-doing.
        AppDomain.CurrentDomain.UnhandledException += new System.UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
        Console.OutputEncoding = Encoding.UTF8;

        #region [Set console window font and location]
        _conHwnd = ConsoleHelper.GetForegroundWindow();
        var winSize = ConsoleHelper.GetWindowSize(_conHwnd);
        var buffSize = ConsoleHelper.GetConsoleSize();
        //Log.Instance.WriteToConsole($"⇒ WindowSize:{winSize.width},{winSize.height} ─ BufferSize:{buffSize.width},{buffSize.height}", LogLevel.Debug);
        #endregion

        #region [Load the app settings]
        var config = _settings.GetSettings("Settings.json");
        // Do we have any settings?
        if (config != null)
        {
            #region [Show current settings using Reflection]
            //foreach (var val in _settings.ListSettings())
            //{ 
            //    if (val != null && val.GetType() == typeof(Settings))
            //    {
            //        var s = val as Settings;
            //        Console.WriteLine($"FontName ⇒ \"{s?.FontName}\"");
            //        Console.WriteLine($"FontSize ⇒ \"{s?.FontSize}\"");
            //        Console.WriteLine($"TestNumber ⇒ \"{s?.TestNumber}\"");
            //    }
            //}
            #endregion

            try
            {
                // Configure our custom font.
                var fontLocation = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fonts", "3270-Regular.ttf");
                ConsoleHelper.InstallFont(fontLocation);
                // NOTE: Setting the font may change the size of the console window.
                // To account for this you could move the GetWindowSize call AFTER the SetCurrentFont call.
                ConsoleHelper.SetCurrentFont(config.FontName, Convert.ToInt16(config.FontSize));

                // Attempt to center the console window.
                var dims = ConsoleHelper.GetScreenDimensions();
                if (dims.width > 0 && dims.height > 0)
                {
                    var x = (dims.width - (winSize.width + 10)) / 2;
                    var y = (dims.height - (winSize.height + 10)) / 2;
                    ConsoleHelper.SetWindowPosition(_conHwnd, x, y, winSize.width, winSize.height);
                }
                else
                {
                    ConsoleHelper.SetWindowPosition(_conHwnd, 1, 1, winSize.width, winSize.height);
                }
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                Log.Instance.WriteConsole($"⇒ SetCurrentFont {ex.Message}", LogLevel.Error);
            }
        }
        #endregion

        // Show the current runtime info.
        Assembly assembly = typeof(Program).Assembly;
        var frameAttr = (TargetFrameworkAttribute)assembly.GetCustomAttributes(typeof(TargetFrameworkAttribute), false)[0];
        Log.Instance.WriteConsole(string.Format("⇒ {0} ─ User \"{1}\"", string.IsNullOrEmpty(frameAttr.FrameworkDisplayName) ? frameAttr.FrameworkName : frameAttr.FrameworkDisplayName, Environment.UserName), LogLevel.Info);
        Log.Instance.WriteConsole($"⇒ Windows version {Environment.OSVersion.Version} is being reported from the environment.", LogLevel.Info);
        Log.Instance.WriteConsole($"⇒ Runtime is here \"{RuntimeEnvironment.GetRuntimeDirectory()}\"", LogLevel.Info);
        Log.Instance.WriteConsole($"⇒ Current process is \"{Process.GetCurrentProcess().MainModule?.FileName}\"", LogLevel.Info);
        //_winVersion = Utils.GetOSMajorAndMinor();
        if (Utils.IsWindowsCompatible())
        {   // Configure embedded resources.
            _iconNormal = Resources.ResourceManager.GetObject("Logo") as Icon;
            _iconWarning = Resources.ResourceManager.GetObject("Warning") as Icon;
            // Update the console window icon.
            IconUpdater.SetConsoleIconAtRuntime(_iconNormal);
        }
        #endregion

        CheckMaximumThreads();
        DumpAllEmbeddedResources();
        ShowLogo(leftPad: 1, addPause: false);

        //var result = await TaskTimer.Start(async () => { await RunThreadTest(); });
        //Console.WriteLine($"Task took {result.Duration.TotalSeconds} seconds (no return value)");

        #region [Setup the ChannelManager's delegates]
        _chanman.OnBeginInvoke += (item, msg) => { $"••BEGIN••••• {msg}".Announcement(); /* var ci = item as ChannelItem; */ };
        _chanman.OnEndInvoke += (item, msg) => { $"••END••••••• {msg}".Announcement(); };
        _chanman.OnCancel += (item, msg) => { $"••CANCEL•••• {msg}".Announcement(); };
        _chanman.OnError += (item, msg) => { $"••ERROR••••• {msg}".Announcement(); };
        _chanman.OnWarning += (item, msg) => { $"••WARNING••• {msg}".Announcement(); };
        _chanman.OnShutdown += (msg) => { $"••SHUTDOWN•• {msg}".Announcement(); };
        _chanman.ChangeResolution(1000);
        #endregion

        #region [Setup the ConcurrentlManager's delegates]
        _queueman.OnBeginInvoke += (item, msg) => { $"••BEGIN••••• {msg}".Announcement(); /* var ci = item as QueueItem; */ };
        _queueman.OnEndInvoke += (item, msg) => { $"••END••••••• {msg}".Announcement(); };
        _queueman.OnCancel += (item, msg) => { $"••CANCEL•••• {msg}".Announcement(); };
        _queueman.OnError += (item, msg) => { $"••ERROR••••• {msg}".Announcement(); };
        _queueman.OnWarning += (item, msg) => { $"••WARNING••• {msg}".Announcement(); };
        _queueman.OnShutdown += (msg) => { $"••SHUTDOWN•• {msg}".Announcement(); };
        _queueman.OnExhausted += (msg) =>
        {
            $"••EXHAUSTED•• {msg}".Announcement();
            TestConcurrentManager(Random.Shared.Next(10, _maxDesired * 10));
        };
        _queueman.ChangeResolution(1000);
        #endregion

        #region [Setup the ScheduleManager's delegates]
        _schedman.OnInvoke += (item, msg) => { $"••INVOKE•••• {msg}".Announcement(); /* var ai = item as ActionItem; */ };
        _schedman.OnCancel += (item, msg) => { $"••CANCEL•••• {msg}".Announcement(); };
        _schedman.OnError += (item, msg) => { $"••ERROR••••• {msg}".Announcement(); };
        _schedman.OnWarning += (item, msg) => { $"••WARNING••• {msg}".Announcement(); };
        _schedman.OnShutdown += (msg) => { $"••SHUTDOWN•• {msg}".Announcement(); };
        _schedman.OnExhausted += (msg) =>
        {
            $"••EXHAUSTED•• {msg}".Announcement();
            if (!_clearFlag)
            {
                #region [Test reusing the Scheduler]
                Thread.Sleep(2000);
                ShowLogo(leftPad: 1, addPause: true);
                for (int idx = _currentIndex; idx < (_currentIndex + _maxDesired); idx++)
                {
                    int trapped = idx;
                    string title = Utils.GetRandomName();
                    int secDelay = Random.Shared.Next(1, 31);
                    DateTime runTime = DateTime.Now.AddSeconds(secDelay);
                    $"⇒ {title} will run {runTime.ToLongTimeString()}".Announcement();
                    CancellationTokenSource aiCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                    _schedman.ScheduleItem(new ActionItem(
                        trapped,
                        $"{title} #{trapped}",
                        delegate ()
                        {
                            var vsw = ValueStopwatch.StartNew();
                            Console.WriteLine($"{title} #{trapped} scheduled for {runTime.ToLongTimeString()} started");
                            Thread.Sleep(Random.Shared.Next(100, 3001));
                            Console.WriteLine($"{title} #{trapped} ran for {vsw.GetElapsedTime().ToReadableString()}");
                        },
                        DateTime.Now.AddSeconds(secDelay), // Set some time in the future to run.
                        aiCts.Token)
                    );
                    Thread.Sleep(10);
                }
                _currentIndex += _maxDesired;
                #endregion
            }
        };
        #endregion

        #region [Setup the ThreadPool delegate]
        OnUserWorkItemComplete += (obj, msg) => 
        { 
            Log.Instance.WriteConsole($"{msg} ({(obj != null ? obj : "null")})", LogLevel.Event); 
        };
        #endregion

        #region [Select test to run based on config file]
        //Console.WriteLine($"⇒ The #{config?.TestNumber} test will be run.");
        switch (config?.TestNumber)
        {
            case 1:
                TestSequentialThreadingChannel();
                break;
            case 2:
                Task.Run(async () => await TestParallelThreadingChannel()).GetAwaiter().GetResult();
                break;
            case 3:
                Console.WriteLine($"⇒ Press 'A' to add {nameof(ChannelItem)}s.");
                break;
            case 4:
                TestScheduleManager();
                break;
            case 5:
                TestConcurrentManager(Random.Shared.Next(10, _maxDesired * 10));
                break;
            case 6:
                using (var sm = new StackManager())
                {
                    sm.Start(2, 3, 100);
                }
                break;
            case 7:
                if (Utils.IsWindowsCompatible())
                    TestWMIC();
                break;
            default:
                ShowMenu();
                break;
        }
        #endregion

        #region [Monitor user keypress]
        while ((_conKey = Console.ReadKey(true).Key) != ConsoleKey.Escape)
        {
            Log.Instance.WriteConsole($"⇒ \"{_conKey}\" keypress detected.", LogLevel.Info);
            if (_conKey == ConsoleKey.D1)
            {
                // Signal the taskbar that we're working.
                TaskbarProgress.SetState(_conHwnd, TaskbarProgress.TaskbarStates.Indeterminate);

                config!.TestNumber = 1;
                Log.Instance.WriteConsole($"⇒ Test #{config.TestNumber} selected.", LogLevel.Info);
                TestSequentialThreadingChannel();

                // Signal the taskbar that we're done.
                TaskbarProgress.SetState(_conHwnd, TaskbarProgress.TaskbarStates.NoProgress);
            }
            else if (_conKey == ConsoleKey.D2)
            {
                config!.TestNumber = 2;
                Log.Instance.WriteConsole($"⇒ Test #{config.TestNumber} selected.", LogLevel.Info);
                //Task.Run(async () => await TestParallelThreadingChannel()).GetAwaiter().GetResult();
                var rez = TaskTimer.Start(async () => 
                {
                    // Signal the taskbar that we're working.
                    TaskbarProgress.SetState(_conHwnd, TaskbarProgress.TaskbarStates.Indeterminate);

                    await TestParallelThreadingChannel();

                    // Signal the taskbar that we're done.
                    TaskbarProgress.SetState(_conHwnd, TaskbarProgress.TaskbarStates.NoProgress);

                }).GetAwaiter().GetResult();

                Log.Instance.WriteConsole($"⇒ Waited {rez.Duration.ToReadableString()}", LogLevel.Info);
            }
            else if (_conKey == ConsoleKey.D3)
            {
                config!.TestNumber = 3;
                Log.Instance.WriteConsole($"⇒ Test #{config.TestNumber} selected.", LogLevel.Info);
                //Log.Instance.WriteToConsole($"⇒ Press 'A' to add {nameof(ChannelItem)}s.", LogLevel.Info);
                if (_adder == null)
                {
                    // Signal the taskbar that we're working.
                    TaskbarProgress.SetState(_conHwnd, TaskbarProgress.TaskbarStates.Indeterminate);

                    _adder = new Thread(AddingLoop)
                    {
                        IsBackground = true,
                        Name = $"ChannelAdder_{DateTime.Now.ToString("dddMMMdd")}",
                        Priority = ThreadPriority.BelowNormal
                    };
                    _adder.Start();
                }
                else
                {
                    //AddChannelItems(Random.Shared.Next(10, _maxDesired * 10));
                    //TestWaitToWriteAsync(Random.Shared.Next(10, _maxDesired * 20));
                    TestAddItemValueTask(Random.Shared.Next(10, _maxDesired * 20));
                }
            }
            else if (_conKey == ConsoleKey.D4)
            {
                // Signal the taskbar that we're working.
                TaskbarProgress.SetState(_conHwnd, TaskbarProgress.TaskbarStates.Indeterminate);

                config!.TestNumber = 4;
                Log.Instance.WriteConsole($"⇒ Test #{config.TestNumber} selected.", LogLevel.Info);
                TestScheduleManager();
            }
            else if (_conKey == ConsoleKey.D5)
            {
                // Signal the taskbar that we're working.
                TaskbarProgress.SetState(_conHwnd, TaskbarProgress.TaskbarStates.Indeterminate);

                config!.TestNumber = 5;
                Log.Instance.WriteConsole($"⇒ Test #{config.TestNumber} selected.", LogLevel.Info);
                TestConcurrentManager(Random.Shared.Next(10, _maxDesired * 10));
            }
            else if (_conKey == ConsoleKey.D6)
            {
                // Signal the taskbar that we're working.
                TaskbarProgress.SetState(_conHwnd, TaskbarProgress.TaskbarStates.Indeterminate);

                config!.TestNumber = 6;
                using (var sm = new StackManager())
                {
                    sm.Start(1, 1, 20);
                }

                // Signal the taskbar that we're done.
                TaskbarProgress.SetState(_conHwnd, TaskbarProgress.TaskbarStates.NoProgress);
            }
            else if (_conKey == ConsoleKey.D7)
            {
                config!.TestNumber = 7;
                if (Utils.IsWindowsCompatible())
                {
                    // Signal the taskbar that we're working.
                    TaskbarProgress.SetState(_conHwnd, TaskbarProgress.TaskbarStates.Indeterminate);

                    Log.Instance.WriteConsole($"⇒ Test #{config.TestNumber} selected.", LogLevel.Info);
                    #region [Gathering system data from cmdline]
                    var lines = CallWMIC();
                    if (lines.Count > 1)
                    {
                        Console.ForegroundColor = ConsoleColor.Blue;
                        int index = 0;
                        string data = lines[1];
                        Dictionary<string, int> distances = GetDistances(lines[0]);
                        foreach (var kvp in distances)
                        {
                            if (!string.IsNullOrEmpty(kvp.Key))
                            {
                                string element = $"{kvp.Key}" + new string('.', kvp.Value);
                                if (data.Length >= index + element.Length)
                                {
                                    string value = $"{data.Substring(index, element.Length)}";
                                    Console.WriteLine(string.Format("{0,-38}{1,-42}", kvp.Key, value));
                                    if (value.StartsWith("{"))
                                    {
                                        var group = value.ExtractWMICSubItems();
                                        foreach (var item in group)
                                        {
                                            if (!string.IsNullOrEmpty(item))
                                                Console.WriteLine($"  • {item}");
                                        }
                                    }
                                }
                                index += element.Length;
                            }
                        }
                    }
                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.WriteLine();

                    // Signal the taskbar that we're done.
                    TaskbarProgress.SetState(_conHwnd, TaskbarProgress.TaskbarStates.NoProgress);
                }
                else
                {
                    Log.Instance.WriteConsole($"⇒ Test #{config.TestNumber} is only available on Windows.", LogLevel.Warning);
                }
                #endregion
            }
            else if (_conKey == ConsoleKey.D8)
            {
                config!.TestNumber = 8;

                if (Utils.IsWindowsCompatible())
                {
                    // Signal the taskbar that we're working.
                    TaskbarProgress.SetState(_conHwnd, TaskbarProgress.TaskbarStates.Indeterminate);

                    Log.Instance.WriteConsole($"⇒ Collecting services...", LogLevel.Info);
                    var services = GetWindowsServices();
                    foreach (var serv in services)
                    {
                        try
                        {
                            Log.Instance.WriteConsole(string.Format("{0,-71}{1,-20}{2,-20}", serv["Caption"], serv["State"], serv["StartMode"]), LogLevel.Info);
                        }
                        catch (KeyNotFoundException) { }
                    }

                    // Signal the taskbar that we're done.
                    TaskbarProgress.SetState(_conHwnd, TaskbarProgress.TaskbarStates.NoProgress);
                }
            }
            else if (_conKey == ConsoleKey.D9)
            {
                config!.TestNumber = 9;

                if (Utils.IsWindowsCompatible())
                {
                    // Signal the taskbar that we're working.
                    TaskbarProgress.SetState(_conHwnd, TaskbarProgress.TaskbarStates.Indeterminate);

                    Log.Instance.WriteConsole($"⇒ Collecting shares...", LogLevel.Debug);
                    var shares = GetWindowsShares();
                    foreach (var share in shares)
                    {
                        try
                        {
                            Log.Instance.WriteConsole(string.Format("{0,-40}{1,-20}{2,-30}{3,-10}", share["Description"], share["Name"], share["Path"], share["Status"]), LogLevel.Info);
                        }
                        catch (KeyNotFoundException) { }
                    }
                    Console.WriteLine();

                    //Log.Instance.WriteToConsole($"⇒ Collecting processes...", LogLevel.Debug);
                    //var procs = GetWindowsProcesses();
                    //foreach (var proc in procs)
                    //{
                    //    try
                    //    {
                    //        Log.Instance.WriteToConsole(string.Format("{0,-60}{1,-30}{2,-10}", proc["Description"], WMICOffsetConversion(proc["CreationDate"]), proc["ProcessId"]), LogLevel.Info);
                    //    }
                    //    catch (KeyNotFoundException) { }
                    //} Console.WriteLine();

                    Log.Instance.WriteConsole($"⇒ Collecting printers...", LogLevel.Debug);
                    var printers = GetWindowsPrinters();
                    foreach (var p in printers)
                    {
                        try
                        {
                            Log.Instance.WriteConsole(string.Format("{0,-40}default={1,-10} local={2,-10} port={3,-30}", p["Caption"], p["Default"], p["Local"], p["PortName"]), LogLevel.Info);
                        }
                        catch (KeyNotFoundException) { }
                    }
                    Console.WriteLine();


                    Log.Instance.WriteConsole($"⇒ Collecting alternate file streams...", LogLevel.Debug);

                    var dlist = Log.Instance.GetAllDrives();
                    var path = Utils.NavigateUpFolders(Directory.GetCurrentDirectory(), 3);
                    var rez = RunPowerShellCommand(path, "Get-ChildItem -Recurse | Get-Item -Stream Zone.Identifier -ErrorAction SilentlyContinue | Select-Object FileName");
                    foreach (var line in rez)
                    {
                        Log.Instance.WriteConsole($"{line}", LogLevel.Info);
                    }
                    Log.Instance.WriteConsole($"⇒ Collection finished.", LogLevel.Debug);

                    // Signal the taskbar that we're done.
                    TaskbarProgress.SetState(_conHwnd, TaskbarProgress.TaskbarStates.NoProgress);
                }
            }
            else if (_conKey == ConsoleKey.D0) 
            {
                // Signal the taskbar that we're working.
                TaskbarProgress.SetState(_conHwnd, TaskbarProgress.TaskbarStates.Indeterminate);

                var serial = new SerialEmulator();

                //TestThreadPoolQueue(Random.Shared.Next(5,30));
                //TestThreadPoolWaitRegister();

                //var rwom = new RegisterWaitObjectManager();
                //rwom.TestThreadPoolWaitRegisterWithARE();

                #region [Using a basic trigger]
                //using (var rwom = new RegisterWaitObjectManager(100))
                //{
                //    rwom.TestThreadPoolWaitRegister(TimeSpan.FromSeconds(30));
                //    while (rwom.AreAnyWaitObjectsNotTriggered())
                //    {
                //        // Small delay before attempting a trigger.
                //        Thread.Sleep(Random.Shared.Next(100, 501));
                //        rwom.TriggerWaitObject(Random.Shared.Next(0, rwom.GetWaitObjectCount()));
                //    }
                //    Log.Instance.WriteConsole($"⇒ No more {nameof(TriggerObject)}s remaining.", LogLevel.Info);
                //}
                #endregion

                #region [Using an advanced trigger]
                List<Action> actions = new();
                for (int t = 0; t < 10; t++) 
                {
                    actions.Add(new Action(() => {
                        Log.Instance.WriteConsole($"I'm an action on tid {Thread.CurrentThread.ManagedThreadId}.", LogLevel.Debug);
                        if (serial.Connect())
                        {
                            Log.Instance.WriteConsole($"I was able to connect to the serial device \"{serial.Device}\".", LogLevel.Success);
                            serial.SendData(Utils.GetRandomName());
                        }
                        else
                        {
                            Log.Instance.WriteConsole($"I was unable to connect to the serial device \"{serial.Device}\".", LogLevel.Warning);
                        }
                        Thread.Sleep(Random.Shared.Next(1000, 10000));
                    }));
                }
                var maxTimeToWait = TimeSpan.FromSeconds(2);
                using (var rwom = new RegisterWaitObjectManager()) 
                {
                    bool running = true;
                    rwom.OnTimedOut += (msg) => 
                    {
                        Log.Instance.WriteConsole($"{msg}", LogLevel.Error);
                        running = false;
                    };
                    rwom.TestThreadPoolWaitRegister(maxTimeToWait, actions, true);
                    
                    // We could fire all objects at once...
                    //rwom.TriggeredAllWaitObjects();

                    // Or, fire each object inside a loop until complete.
                    while (rwom.AreAnyWaitObjectsNotTriggered() && running) 
                    {
                        Thread.Sleep(250);
                        
                        // Try and guess the next one (not very practical)...
                        //rwom.TriggerWaitObject(Random.Shared.Next(0, rwom.GetWaitObjectCount()));

                        // Or, find the next available and trigger it.
                        var to = rwom.GetNextUntriggeredWaitObject();
                        if (to != null) { rwom.TriggerWaitObject(to.Id); }
                    }

                    Log.Instance.WriteConsole($"⇒ No more {nameof(TriggerObject)}s remaining.", LogLevel.Info);
                    Thread.Sleep(2000);
                    
                    //Log.Instance.WriteConsole($"⇒ Resetting all {nameof(TriggerObject)}s.", LogLevel.Info);
                    //rwom.ResetAllWaitObjects();
                    //Log.Instance.WriteConsole($"⇒ Running {nameof(TriggerObject)} again.", LogLevel.Info);
                    //var tobj = rwom.GetNextUntriggeredWaitObject();
                    //if (tobj != null) { rwom.TriggerWaitObject(tobj.Id); }
                    //Thread.Sleep(2000);
                }
                #endregion

                // Signal the taskbar that we're working.
                TaskbarProgress.SetState(_conHwnd, TaskbarProgress.TaskbarStates.NoProgress);
            }
            else if (_conKey == ConsoleKey.C)
            {
                if (config?.TestNumber == 3)
                {
                    Log.Instance.WriteConsole($"⇒ Clearing {_chanman.GetItemCount()} from the Channel.", LogLevel.Info);
                    _chanman.ClearItems();
                }
                else if (config?.TestNumber == 4)
                {
                    Log.Instance.WriteConsole($"⇒ Clearing {_schedman.GetInactivatedCount()} items from the scheduler!", LogLevel.Info);
                    _schedman.ClearSchedule();
                    _clearFlag = true;
                }
                else if (config?.TestNumber == 5)
                {
                    Log.Instance.WriteConsole($"⇒ Clearing {_queueman.GetItemCount()} from the Queue.", LogLevel.Info);
                    _queueman.ClearItems();
                }
            }
            else if (_conKey == ConsoleKey.B)
            {
                if (config?.TestNumber == 3)
                {
                    Log.Instance.WriteConsole($"IsBusy? ⇒ {_chanman.IsBusy()}", LogLevel.Info);
                }
                else if (config?.TestNumber == 4)
                {
                    Log.Instance.WriteConsole($"⇒ Still waiting to be activated: {_schedman.GetInactivatedCount()}", LogLevel.Info);
                }
            }
            else if (_conKey == ConsoleKey.T)
            {
                if (config?.TestNumber == 3)
                {
                    // This will not stop execution of the items if we're already inside the while loop.
                    _chanman.Toggle();
                    Log.Instance.WriteConsole($"Is Channel Thread Suspended? ⇒ {_chanman.IsThreadSuspended()}", LogLevel.Info);
                }
                else if (config?.TestNumber == 4)
                {
                    // This will not stop execution of the items if we're already inside the while loop.
                    _schedman.Toggle();
                    Log.Instance.WriteConsole($"Is Scheduler Thread Suspended? ⇒ {_schedman.IsThreadSuspended()}", LogLevel.Info);
                }
            }
            else
            {
                ShowMenu();
            }
        }
        #endregion

        #region [Cleanup]
        Console.ForegroundColor = ConsoleColor.White;
        if (config?.TestNumber == 3)
        {
            Console.WriteLine($"⇒ {_chanman.GetItemCount()} items remain in the manager. ");
        }
        else if (config?.TestNumber == 4)
        {
            Console.WriteLine($"⇒ {_schedman.GetInactivatedCount()} items remain in the scheduler. ");
        }
        else if (config?.TestNumber == 5)
        {
            Console.WriteLine($"⇒ {_queueman.GetItemCount()} items remain in the queue. ");
        }

        // Inform the agents to close shop.
        _schedman.Dispose();
        _queueman.Dispose();
        _chanman.Dispose();

        // Signal any local thread loops.
        _shutdown = true;

        // Signal the taskbar that we're done.
        TaskbarProgress.SetState(_conHwnd, TaskbarProgress.TaskbarStates.NoProgress);
        #endregion

        Log.Instance.WriteConsole("⇒ Closing... ", LogLevel.Info);
        Thread.Sleep(1800);
    }

    static void SomeCallbackMethod(object? data, bool timedOut)
    {
        if (!timedOut)
        {
            Log.Instance.WriteConsole($"Started: {data}", LogLevel.Info);
            Thread.Sleep(Random.Shared.Next(10, 5001));
            Log.Instance.WriteConsole($"Ended: {data}", LogLevel.Success);
        }
        else
        {
            Log.Instance.WriteConsole($"TimedOut: {data}", LogLevel.Warning);
        }
    }

    /// <summary>
    /// Display options menu to user.
    /// </summary>
    public static void ShowMenu()
    {
        string leftSide = $"{Title}";
        string rightSide = $"v{Assembly.GetExecutingAssembly().GetName().Version}";
        Console.WriteLine();
        Console.WriteLine($"───────────────────────────────────────────────────");
        Console.WriteLine(string.Format(" {0,-30}{1,20}", leftSide, rightSide));
        Console.WriteLine($"───────────────────────────────────────────────────");
        Console.WriteLine($"   1) Test SequentialThreading (Threading.Channels)");
        Console.WriteLine($"   2) Test ParallelThreading   (Threading.Channels)");
        Console.WriteLine($"   3) Test ChannelManager      (Threading.Channels)");
        Console.WriteLine($"   4) Test ScheduleManager     (BlockingCollection)");
        Console.WriteLine($"   5) Test QueueManager        (ConcurrentQueue)   ");
        Console.WriteLine($"   6) Test StackManager        (ConcurrentStack)   ");
        if (Utils.IsWindowsCompatible())
            Console.WriteLine($"   7) Test WMIC                                    ");
        Console.WriteLine($"   B) Check if agent is busy                       ");
        Console.WriteLine($"   C) Clear items                                  ");
        Console.WriteLine($"   T) Toggle agent thread                          ");
        Console.WriteLine($" Esc) Exit application                             ");
        Console.WriteLine($"───────────────────────────────────────────────────");
    }

    /// <summary>
    /// Add method for the <see cref="ChannelManager"/>.
    /// </summary>
    /// <param name="maxItems">default is 250 items</param>
    static void AddChannelItems(int maxItems = 250)
    {
        List<ChannelItem> list = new();
        var vsw = ValueStopwatch.StartNew();
        for (int i = 0; i < maxItems; i++)
        {
            var timeout = Random.Shared.Next(10, 181);
            var ciCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));
            list.Add(new ChannelItem(i + 1, Utils.GetRandomName(), () =>
            {
                if (Random.Shared.Next(1, 100) >= 99)
                    throw new Exception("******* I'm not a real error. *******");
                else
                    Thread.Sleep(Random.Shared.Next(50, 601));
            }, ciCts.Token));
        }
        Log.Instance.WriteConsole($"⇒ Writing {maxItems} items to the Channel...", LogLevel.Info);
        _chanman.AddItems(list);
        Log.Instance.WriteConsole($"⇒ Generation took {vsw.GetElapsedTime().ToReadableString()}", LogLevel.Info);
    }

    /// <summary>
    /// Test for <see cref="ChannelManager.AddItemValueTask(ChannelItem, CancellationToken)"/>.
    /// </summary>
    static async void TestAddItemValueTask(int maxItems = 250)
    {
        var vsw = ValueStopwatch.StartNew();
        for (int i = 0; i < maxItems; i++)
        {
            var timeout = Random.Shared.Next(10, 181);
            var ciCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));
            var vt = await _chanman.AddItemValueTask(new ChannelItem(i + 1, Utils.GetRandomName(), () =>
            {
                if (Random.Shared.Next(1, 100) >= 99)
                    throw new Exception("******* I'm not a real error. *******");
                else
                    Thread.Sleep(Random.Shared.Next(50, 601));
            }, ciCts.Token), ciCts.Token);
        }
        Log.Instance.WriteConsole($"⇒ AddItemValueTask process took {vsw.GetElapsedTime().ToReadableString()}", LogLevel.Success);
    }

    /// <summary>
    /// Test for <see cref="ChannelManager.WaitToWriteAsync(ChannelItem, CancellationToken)"/>.
    /// </summary>
    static async void TestWaitToWriteAsync(int maxItems = 250)
    {
        var vsw = ValueStopwatch.StartNew();
        for (int i = 0; i < maxItems; i++)
        {
            var timeout = Random.Shared.Next(10, 181);
            var ciCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));
            await _chanman.WaitToWriteAsync(new ChannelItem(i + 1, Utils.GetRandomName(), () =>
            {
                if (Random.Shared.Next(1, 100) >= 99)
                    throw new Exception("******* I'm not a real error. *******");
                else
                    Thread.Sleep(Random.Shared.Next(50, 601));
            }, ciCts.Token), ciCts.Token);
        }
        Log.Instance.WriteConsole($"⇒ WaitToWriteAsync process took {vsw.GetElapsedTime().ToReadableString()}", LogLevel.Success);
    }

    /// <summary>
    /// Monitor thread for adding items once content is exhausted.
    /// </summary>
    static void AddingLoop()
    {
        while (!_shutdown)
        {
            Thread.Sleep(100);
            int current = _chanman.GetItemCount();
            if (current <= 1)
            {
                ShowLogo(leftPad: 1, addPause: true);
                AddChannelItems(Random.Shared.Next(10, _maxDesired * 10));
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Log.Instance.WriteConsole($"⇒ Still working on existing items, please wait.", LogLevel.Info);
                Console.ForegroundColor = ConsoleColor.Gray;
                switch (current)
                {
                    case int n when (n > 5000):
                        Thread.Sleep(60000);
                        break;
                    case int n when (n > 2000 && n <= 5000):
                        Thread.Sleep(30000);
                        break;
                    case int n when (n > 1000 && n <= 2000):
                        Thread.Sleep(20000);
                        break;
                    case int n when (n > 500 && n <= 1000):
                        Thread.Sleep(15000);
                        break;
                    case int n when (n > 250 && n <= 500):
                        Thread.Sleep(10000);
                        break;
                    case int n when (n > 10 && n <= 250):
                        Thread.Sleep(5000);
                        break;
                    default:
                        Thread.Sleep(1000);
                        break;
                }
            }
        }
    }

    /// <summary>
    /// Testing the <see cref="System.Threading.Channels.Channel{T}"/> class.
    /// </summary>
    static void TestSequentialThreadingChannel(int max = 10)
    {
        int itemCount = 0;
        var ciCts = new CancellationTokenSource(TimeSpan.FromSeconds(6));
        Channel<ChannelItem>? items = Channel.CreateUnbounded<ChannelItem>();

        #region [Write]
        Log.Instance.WriteConsole($"⇒ Attempting to write all to the Channel...", LogLevel.Info);
        for (int i = 0; i < max; i++)
        {
            var ci = new ChannelItem(
                i + 1,
                Utils.GetRandomName(),
                () =>
                {
                    Thread.Sleep(Random.Shared.Next(50, 501));
                },
                ciCts.Token);
            items?.Writer.TryWrite(ci);
            itemCount++;
            Log.Instance.WriteConsole($"Produced: \"{ci.Title}\" ID#{ci.Id}.  Item Count: {itemCount}", LogLevel.Info);
            Thread.Sleep(20);
        }
        // This step is only for parallel threads.
        items?.Writer.Complete();
        #endregion

        #region [Read]
        Log.Instance.WriteConsole($"⇒ Attempting to read all from the Channel...", LogLevel.Info);
        while (items?.Reader.Count > 0)
        {
            if (items.Reader.TryRead(out ChannelItem? item))
            {
                itemCount--;
                if (!item.Token.IsCancellationRequested)
                {
                    Log.Instance.WriteConsole($"Consumed: \"{item.Title}\" ID#{item.Id}.  Item Count: {itemCount}", LogLevel.Info);
                    item.ToRun?.Invoke();
                }
                else
                {
                    Log.Instance.WriteConsole($"Skipping \"{item.Title}\" ID#{item.Id}, since token has expired.  Item Count: {itemCount}", LogLevel.Info);
                }
            }
            else
            {
                Log.Instance.WriteConsole($"Could not read from the Channel.", LogLevel.Warning);
                Thread.Sleep(100); // wait a little before retry
            }
        }
        #endregion

        Log.Instance.WriteConsole($"⇒ Sequential Channel Test Complete.", LogLevel.Success);
    }

    /// <summary>
    /// Testing the <see cref="System.Threading.Channels.Channel{T}"/> class.
    /// </summary>
    static async Task TestParallelThreadingChannel(int max = 10)
    {
        int itemCount = 0;
        var ciCts = new CancellationTokenSource(TimeSpan.FromSeconds(6));
        Channel<ChannelItem>? items = Channel.CreateUnbounded<ChannelItem>();

        #region [Write]
        var producer = Task.Run(async () =>
        {
            Log.Instance.WriteConsole($"⇒ Attempting to write all to the channel.", LogLevel.Debug);
            for (int i = 0; i < max; i++)
            {
                var ci = new ChannelItem(
                    i + 1,
                    Utils.GetRandomName(),
                    async () =>
                    {
                        await Task.Delay(Random.Shared.Next(50, 501));
                    },
                    ciCts.Token);
                await items.Writer.WriteAsync(ci);
                itemCount++;
                Log.Instance.WriteConsole($"Produced: {ci.Title}, Item Count: {itemCount}", LogLevel.Info);
                await Task.Delay(20);
            }
            // Signal any waiting threads.
            items.Writer.Complete();
        });
        #endregion

        #region [Read]
        var consumer = Task.Run(async () =>
        {
            Log.Instance.WriteConsole($"⇒ Attempting to read all from the channel.", LogLevel.Debug);
            await foreach (var item in items.Reader.ReadAllAsync())
            {
                itemCount--;
                Log.Instance.WriteConsole($"Consumed: \"{item.Title}\", Item Count: {itemCount}", LogLevel.Info);
                item.ToRun?.Invoke();
            }
        });
        #endregion

        await Task.WhenAll(producer, consumer);

        Log.Instance.WriteConsole($"⇒ Parallel Channel Test Complete.", LogLevel.Success);
    }

    /// <summary>
    /// Testing the <see cref="ConcurrentManager"/> class.
    /// </summary>
    static void TestConcurrentManager(int maxItems)
    {
        #region [ConcurrentQueue]
        for (int idx = 0; idx < maxItems; idx++)
        {
            int trapped = idx + 1;
            string title = Utils.GetRandomName();
            int secDelay = Random.Shared.Next(1, 11);
            $"⇒ Adding '{title}' to the queue.".Announcement();
            CancellationTokenSource qiCts = new CancellationTokenSource(TimeSpan.FromSeconds(Random.Shared.Next(5, 61)));
            _queueman.AddItem(new QueueItem(trapped, $"{title}", () =>
            {
                if (Random.Shared.Next(1, 100) >= 99)
                    throw new Exception("******* I'm not a real error. *******");
                else
                    Thread.Sleep(Random.Shared.Next(50, 601));
            }, qiCts.Token));
        }
        #endregion
    }

    /// <summary>
    /// There are two times to consider:
    ///  1. How long will the action run for?
    ///  2. What time (from now) will the action start?
    /// </summary>
    static void TestScheduleManager()
    {
        #region [Add Items To Scheduler]
        for (int idx = 0; idx < _maxDesired; idx++)
        {
            int trapped = idx;
            string title = Utils.GetRandomName();
            int secDelay = Random.Shared.Next(1, 11);
            DateTime runTime = DateTime.Now.AddSeconds(secDelay);
            $"⇒ {title} will run {runTime.ToLongTimeString()}".Announcement();
            CancellationTokenSource aiCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            _schedman.ScheduleItem(new ActionItem(
                trapped,
           $"{title} #{trapped}",
                delegate ()
                {
                    var vsw = ValueStopwatch.StartNew();
                    Log.Instance.WriteConsole($"{title} #{trapped} scheduled for {runTime.ToLongTimeString()} started", LogLevel.Info);
                    Thread.Sleep(Random.Shared.Next(100, 3001));
                    Log.Instance.WriteConsole($"{title} #{trapped} ran for {vsw.GetElapsedTime().ToReadableString()}", LogLevel.Info);
                },
                DateTime.Now.AddSeconds(secDelay), // Set some time in the future to run.
                aiCts.Token)
            );
            _currentIndex = idx + 1;
            Thread.Sleep(10);
        }
        #endregion
    }

    #region [Superfluous]
    /// <summary>
    /// Checks the managed thread pool. Mostly targeted at <see cref="Task.Run(Action)"/> calls.
    /// </summary>
    static void CheckMaximumThreads()
    {
        int workerThreads, completionPortThreads;
        ThreadPool.GetMaxThreads(out workerThreads, out completionPortThreads);
        Log.Instance.WriteConsole($"⇒ Max worker threads: {workerThreads}", LogLevel.Debug);
        Log.Instance.WriteConsole($"⇒ Max I/O completion threads: {completionPortThreads}", LogLevel.Debug);
    }

    /// <summary>
    /// A simple try/catch wrapper.
    /// </summary>
    static void Try(Action action)
    {
        var tmp = Console.ForegroundColor;
        try { action(); Console.WriteLine(); }
        catch (RuntimeWrappedException e)
        { // catch any non-CLS exception (e.g. from a C++ DLL or CLI)
            String wes = e.WrappedException as String;
            if (wes != null)
            {
                string[] typ = {
                "──[TYPE]────────────────────────────────────────────────",
                $"RuntimeWrappedException was thrown!",
                };
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(string.Join(Environment.NewLine, typ));

                string[] msg = {
                "──[DETAILS]─────────────────────────────────────────────",
                wes,
                };
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine(string.Join(Environment.NewLine, msg));
                Console.ForegroundColor = tmp;
            }
        }
        catch (Exception e)
        {
            string[] typ = {
                "──[TYPE]────────────────────────────────────────────────",
                $"{e.GetType()} was thrown!",
            };
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(string.Join(Environment.NewLine, typ));

            string[] msg = {
                "──[DETAILS]─────────────────────────────────────────────",
                e.Message,
                e.InnerException?.Message ?? "",
            };
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine(string.Join(Environment.NewLine, msg));

            string[] trc = {
                "──[STACK]───────────────────────────────────────────────",
                e.StackTrace ?? ""
            };
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine(string.Join(Environment.NewLine, trc));
            Console.ForegroundColor = tmp;


            if (Utils.IsWindowsCompatible())
                IconUpdater.SetConsoleIconAtRuntime(_iconWarning); // Update the console window icon.
        }
    }

    /// <summary>
    /// ASCII art logo from https://patorjk.com/software/taag/#p=display&h=0&v=0&c=bash&f=ANSI%20Shadow&t=ICS
    /// </summary>
    /// <param name="index">logos are in line groups of <paramref name="clusterSize"/></param>
    static void ShowLogo(int leftPad = 1, bool addPause = false, bool playTune = false)
    {
        char[] delims = new char[1] { '\n' };
        ConsoleColor tmp = Console.ForegroundColor;
        try
        {
            if (playTune)
            {
                // Console.Beep() causes the current thread to freeze for the
                // duration of the sound, so fire it in the ThreadPool...
                Task.Run(() =>
                {
                    // 440=A4, 494=B4, 523=C5, 587=D5, 659=E5, 698=F5, 784=G5
                    ConsoleHelper.Beep(440, 150);
                    Thread.Sleep(1);
                    ConsoleHelper.Beep(784, 150);
                });
            }

            string[] chunks = global::ProducerConsumer.Resources.ASCII_Logo.Split(delims);
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine();
            foreach (string str in chunks)
            {
                Console.WriteLine($"{new string(' ', leftPad)}{str}");
            }
        }
        catch { }
        finally
        {
            Console.ForegroundColor = tmp;
            if (addPause)
                Thread.Sleep(1200);
        }
    }

    /// <summary>
    /// Shows all embedded resources in the assembly.
    /// </summary>
    static void DumpAllEmbeddedResources()
    {
        var rset = Resources.ResourceManager.GetResourceSet(System.Globalization.CultureInfo.CurrentUICulture, true, true);
        if (rset != null)
        {
            Log.Instance.WriteConsole($"[Embedded resource keys]", LogLevel.Info);
            // Iterate through the ResourceSet
            foreach (DictionaryEntry entry in rset)
            {
                // Get the resource key and value
                string resourceKey = (string)entry.Key;
                object? resourceValue = entry.Value;

                //Log.Instance.WriteToConsole($"Key: \"{resourceKey}\"   Value: {resourceValue}");
                Log.Instance.WriteConsole($"Key: \"{resourceKey}\" ({resourceValue?.GetType()})", LogLevel.Info);

                if (Utils.IsWindowsCompatible())
                {
                    // NOTE: The System.Drawing.Icon type is only supported on Windows 7 (2009) and higher.
                    Dictionary<string, Icon> icons = new();
                    if (resourceValue?.GetType() == typeof(Icon))
                        icons.Add(resourceKey, resourceValue as Icon);
                }
            }
            Console.WriteLine();
        }
    }

    /// <summary>
    /// For use with "C:\>wmic computersystem get"
    /// The list-style output widths are determined by the header row.
    /// </summary>
    static Dictionary<string, int> GetDistances(string text)
    {
        int currentIndex = 0;
        string[] parts = text.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var distances = new Dictionary<string, int>();
        for (int i = 0; i < parts.Length - 1; i++)
        {
            currentIndex = text.IndexOf(parts[i], currentIndex) + parts[i].Length;
            int nextIndex = text.IndexOf(parts[i + 1], currentIndex);
            int gap = Math.Abs(nextIndex - currentIndex);
            distances[parts[i]] = gap;
        }
        return distances;
    }

    /// <summary>
    /// Helper to bring console to foreground, will handle minimized state also.
    /// </summary>
    static void FocusConsole()
    {
        if (_conHwnd == IntPtr.Zero)
            _conHwnd = ConsoleHelper.GetForegroundWindow();

        if (_conHwnd != IntPtr.Zero)
        {
            ConsoleHelper.ShowWindow(_conHwnd, ConsoleHelper.SW_RESTORE);
            Thread.Sleep(1);
            ConsoleHelper.SetForegroundWindow(_conHwnd);
        }
    }
    #endregion

    #region [Other Tests]
    /// <summary>
    /// Testing our <see cref="TaskRunner"/> class with Task based code.
    /// These will not start running immediately.
    /// </summary>
    static async Task TestFuncTaskRunner()
    {
        bool stopOnFault = true;
        bool wasThereAnyFailure = false;
        CancellationTokenSource cts = new CancellationTokenSource(10000);
        var taskRunner = new TaskRunner();

        // A Func<> in C# is a way to define a method in-line that has a return value.
        // The return value’s type is always the last generic parameter on the Func‘s definition.
        // There is a similar concept of an Action<> that doesn’t have a return value.
        Func<CancellationToken, Task>[] taskFactories = new Func<CancellationToken, Task>[]
        {
                async token =>
                {
                    await Task.Delay(1500, token); // Simulate an asynchronous operation
                    Log.Instance.WriteConsole("Task 1 completed!", LogLevel.Success);
                },
                async token =>
                {
                    await Task.Delay(2000, token); // Simulate a synchronous operation
                    Log.Instance.WriteConsole("Task 2 completed!", LogLevel.Success);
                },
                async token =>
                {
                    await Task.Delay(2500, token); // Simulate a third asynchronous operation
                    Log.Instance.WriteConsole("Task 3 completed!", LogLevel.Success);
                    throw new Exception("I'm a fake error, ignore me.");
                },
                async token =>
                {
                    await Task.Delay(3000, token); // Simulate a fourth asynchronous operation
                    Log.Instance.WriteConsole("Task 4 completed!", LogLevel.Success);
                },
                async token =>
                {
                    await Task.Delay(4000, token); // Simulate a fith asynchronous operation
                    Log.Instance.WriteConsole("Task 5 completed!", LogLevel.Success);
                }
        };

        taskRunner.TaskCompleted += (sender, eventArgs) =>
        {
            Log.Instance.WriteConsole($"Event fired: Task {eventArgs.CompletedTask.Id} completed!", LogLevel.Event);
            Log.Instance.WriteConsole($"Task Status: {eventArgs.CompletedTask.Status}", LogLevel.Info);
        };

        taskRunner.TaskCanceled += (sender, eventArgs) =>
        {
            Log.Instance.WriteConsole($"Event fired: Task {eventArgs.CanceledTask?.Id} canceled!", LogLevel.Event);
            Log.Instance.WriteConsole($"Task Status: {eventArgs.CanceledTask?.Status}", LogLevel.Info);
        };

        taskRunner.TaskFailed += (sender, eventArgs) =>
        {
            Log.Instance.WriteConsole($"Event fired: Task {eventArgs.FailedTask?.Id} faulted!", LogLevel.Event);
            Log.Instance.WriteConsole($"Task Error: {eventArgs.Exception.Message}", LogLevel.Info);
            wasThereAnyFailure = true;
        };

        // Passing true indicates to not proceed if an error happens.
        await taskRunner.RunTasksSequentially(taskFactories, cts.Token, stopOnFault);

        if (stopOnFault && !wasThereAnyFailure)
            Log.Instance.WriteConsole("All tasks completed!\r\n", LogLevel.Info);
        else if (stopOnFault && wasThereAnyFailure)
            Log.Instance.WriteConsole("Not all tasks were completed!\r\n", LogLevel.Info);
        else
            Log.Instance.WriteConsole("All tasks completed!\r\n", LogLevel.Info);
    }

    /// <summary>
    /// Testing our <see cref="TaskRunner"/> class with Action based code.
    /// These will not start running immediately.
    /// </summary>
    static void TestActionRunner2()
    {
        var actionRunner = new TaskRunner();
        CancellationTokenSource cts = new CancellationTokenSource();
        cts.CancelAfter(5000); // Cancel after 5 seconds (adjust as needed)

        actionRunner.ActionCompleted += (sender, args) =>
        {
            Log.Instance.WriteConsole($"Action completed: {args.Action.Method.Name}", LogLevel.Event);
        };

        actionRunner.ActionCanceled += (sender, args) =>
        {
            Log.Instance.WriteConsole($"Action canceled: {args.Action.Method.Name}", LogLevel.Event);
        };

        actionRunner.ActionFailed += (sender, args) =>
        {
            Log.Instance.WriteConsole($"Action failed: {args.Action.Method.Name}, Exception: {args.Exception?.Message}", LogLevel.Event);
        };

        List<Action> actions = new List<Action>
            {
                () => // 1st
                {
                    Thread.Sleep(1000);
                    Log.Instance.WriteConsole("Action 1 complete", LogLevel.Success);
                },
                () => // 2nd
                {
                    Thread.Sleep(1000);
                    Log.Instance.WriteConsole("Action 2 complete", LogLevel.Success);
                },
                () => // 3rd
                {
                    Thread.Sleep(2000);
                    throw new Exception("Error in Action 3");  // This will cause the sequence to stop
                },
                () => // 4th
                {
                    Thread.Sleep(3000);
                    Log.Instance.WriteConsole("Action 4 complete", LogLevel.Success);
                },
            };

        //Action act1 = new(() => { 
        //    Thread.Sleep(1000);
        //    Log.Instance.WriteToConsole("Action 1 complete", LogLevel.Info);
        //});
        //Action act2 = new(() => {
        //    Thread.Sleep(1000);
        //    Log.Instance.WriteToConsole("Action 2 complete", LogLevel.Info);
        //});
        //Action act3 = new(() => {
        //    Thread.Sleep(1000);
        //    throw new Exception("Error in Action 3");  // This will cause the sequence to stop
        //});
        //Action act4 = new(() => {
        //    Thread.Sleep(1000);
        //    Log.Instance.WriteToConsole("Action 4 complete", LogLevel.Info);
        //});
        //var actions = Utils.CreateList(act1, act2, act3, act4);

        actionRunner.RunActionsSequentially(actions, cts.Token, true);
    }

    /// <summary>
    /// Testing our <see cref="TaskRunner"/> class with Action based code.
    /// These will not start running immediately.
    /// </summary>
    static async Task TestActionRunner()
    {
        bool stopOnFault = true;
        bool wasThereAnyFailure = false;
        var taskRunner = new TaskRunner();

        Action[] actions = new Action[]
        {
            () =>
            {
                Log.Instance.WriteConsole("Action 1", LogLevel.Info);
                Thread.Sleep(1000);
            },
            () =>
            {
                Log.Instance.WriteConsole("Action 2", LogLevel.Info);
                Thread.Sleep(1000);
            },
            () =>
            {
                Thread.Sleep(1000);
                throw new Exception("Fake error in Action 3");
            },
            () =>
            {
                Log.Instance.WriteConsole("Action 4", LogLevel.Info);
                Thread.Sleep(1000);
            },
        };

        CancellationTokenSource cts = new CancellationTokenSource();
        cts.CancelAfter(5000); // Cancel after 5 seconds (adjust as needed)

        taskRunner.TaskCompleted += (sender, args) =>
        {
            Log.Instance.WriteConsole($"Action completed. Task {args.CompletedTask.Id}\r\n", LogLevel.Event);
        };

        taskRunner.TaskCanceled += (sender, eventArgs) =>
        {
            Log.Instance.WriteConsole($"Event fired: Action canceled! Task {eventArgs.CanceledTask?.Id}\r\n", LogLevel.Event);
        };

        taskRunner.TaskFailed += (sender, args) =>
        {
            Log.Instance.WriteConsole($"Action failed: {args.Exception.Message}", LogLevel.Event);
            wasThereAnyFailure = true;
        };

        // Passing true indicates to not proceed if an error happens.
        await taskRunner.RunActionsSequentially(actions, cts.Token, stopOnFault);

        if (stopOnFault && !wasThereAnyFailure)
            Log.Instance.WriteConsole("All actions completed!\r\n", LogLevel.Info);
        else if (stopOnFault && wasThereAnyFailure)
            Log.Instance.WriteConsole("Not all actions were completed!\r\n", LogLevel.Info);
        else
            Log.Instance.WriteConsole("All actions completed!\r\n", LogLevel.Info);
    }

    /// <summary>
    /// Testing our <see cref="TaskRunner"/> class. These will start running immediately.
    /// </summary>
    static async void TestTaskRunner()
    {
        CancellationTokenSource cts = new CancellationTokenSource(30000);
        var taskRunner = new TaskRunner();

        Task[] tasks = new Task[]
        {
                Task.Run(async () =>
                {
                    await Task.Delay(1000, cts.Token); // Simulate an asynchronous operation
                    Log.Instance.WriteConsole("Task 1 completed!", LogLevel.Info);
                }),
                Task.Run(async () =>
                {
                    await Task.Delay(1500, cts.Token); // Simulate another asynchronous operation
                    Log.Instance.WriteConsole("Task 2 completed!", LogLevel.Info);
                    throw new Exception("I'm a fake error, ignore me.");
                }),
                Task.Run(async () =>
                {
                    await Task.Delay(2000, cts.Token); // Simulate a third asynchronous operation
                    Log.Instance.WriteConsole("Task 3 completed!", LogLevel.Info);
                }),
                Task.Run(async () =>
                {
                    await Task.Delay(3000, cts.Token); // Simulate a third asynchronous operation
                    Log.Instance.WriteConsole("Task 4 completed!", LogLevel.Info);
                })
        };

        taskRunner.TaskCompleted += (sender, eventArgs) =>
        {
            Log.Instance.WriteConsole($"Event fired: Task {eventArgs.CompletedTask.Id} completed!", LogLevel.Event);
            Log.Instance.WriteConsole($"Task Status: {eventArgs.CompletedTask.Status}\r\n", LogLevel.Info);
        };

        taskRunner.TaskFailed += (sender, eventArgs) =>
        {
            Log.Instance.WriteConsole($"Event fired: Task {eventArgs.FailedTask?.Id} faulted!", LogLevel.Event);
            Log.Instance.WriteConsole($"Task Error: {eventArgs.Exception.Message}\r\n", LogLevel.Info);
        };

        await taskRunner.RunTasksSequentially(tasks, true);

        Log.Instance.WriteConsole("All tasks completed!", LogLevel.Success);
    }

    /// <summary>
    /// Testing <see cref="Func{TResult}"/> with <see cref="Task"/>.
    /// </summary>
    static async void TestTaskArray()
    {
        Func<Task>[] factory = new Func<Task>[]
        {
                async () =>
                {
                    Log.Instance.WriteConsole($" • Func<Task> 1: t{Thread.CurrentThread.ManagedThreadId}", LogLevel.Info);
                    await RunThreadTest();
                    Log.Instance.WriteConsole($"Completed Func<Task> 1!", LogLevel.Info);
                },
                async () =>
                {
                    Log.Instance.WriteConsole($" • Func<Task> 2: t{Thread.CurrentThread.ManagedThreadId}", LogLevel.Info);
                    await RunThreadTest();
                    Log.Instance.WriteConsole($"Completed Func<Task> 2!", LogLevel.Info);
                },
                async () =>
                {
                    Log.Instance.WriteConsole($" • Func<Task> 3: t{Thread.CurrentThread.ManagedThreadId}", LogLevel.Info);
                    await RunThreadTest();
                    Log.Instance.WriteConsole($"Completed Func<Task> 3!", LogLevel.Info);
                },
                async () =>
                {
                    Log.Instance.WriteConsole($" • Func<Task> 4: t{Thread.CurrentThread.ManagedThreadId}", LogLevel.Info);
                    await RunThreadTest();
                    Log.Instance.WriteConsole($"Completed Func<Task> 4!", LogLevel.Info);
                }
        };

        // Create a holder for the tasks.
        Task[] tasks = new Task[factory.Length];

        // Create tasks from the factory.
        for (int i = 0; i < factory.Length; i++)
        {
            tasks[i] = Task.Run(factory[i]);
        }

        // Wait for all tasks to finish (blocking call).
        await Task.WhenAll(tasks);

        Log.Instance.WriteConsole("All tasks completed!", LogLevel.Success);
    }

    static async Task RunThreadTest()
    {
        Log.Instance.WriteConsole($" • CallStackMethod.ConfigureAwait(true): t{Thread.CurrentThread.ManagedThreadId}", LogLevel.Info);
        // This is the default which tells the compiler that you want
        // to return to the calling thread after the await is finished.
        await CallStackMethod().ConfigureAwait(true);

        Log.Instance.WriteConsole($" • CallStackMethod.ConfigureAwait(false): t{Thread.CurrentThread.ManagedThreadId}", LogLevel.Info);
        // This tells the compiler that we do not care if we
        // return to the calling thread after the await is finished.
        await CallStackMethod().ConfigureAwait(false);

        Log.Instance.WriteConsole($" • RunThreadTest: t{Thread.CurrentThread.ManagedThreadId}", LogLevel.Info);
    }

    static async Task CallStackMethod()
    {
        Log.Instance.WriteConsole($" • SomeOtherMethod: t{Thread.CurrentThread.ManagedThreadId}", LogLevel.Info);
        await Task.Delay(1000);
    }
    #endregion

    #region [WMIC Stuff]
    /// <summary>
    /// Use the Windows Management Instrumentation to gather basic info about the machine.
    /// </summary>
    static void TestWMIC()
    {
        try
        {
            // For Windows only.
            var os = GetOSSettings();
            // CodePage 1252 character encoding is a superset of ISO 8859-1.
            Log.Instance.WriteConsole($"⇒ {os["Caption"]} • {os["OSArchitecture"]} • v{os["Version"]}", LogLevel.Info);
            Log.Instance.WriteConsole($"⇒ LastBoot {WMICOffsetConversion(os["LastBootUpTime"])} • InstallDate {WMICOffsetConversion(os["InstallDate"])}", LogLevel.Info);
            Log.Instance.WriteConsole($"⇒ CodePage {os["CodeSet"]} • {os["SystemDirectory"]} • {os["CSName"]} • Status {os["Status"]}", LogLevel.Info);
            Log.Instance.WriteConsole($"⇒ BootDevice \"{os["BootDevice"]}\" • SystemDevice \"{os["SystemDevice"]}\"", LogLevel.Info);
            Log.Instance.WriteConsole($"⇒ Organization \"{os["Organization"]}\" • SerialNumber \"{os["SerialNumber"]}\"", LogLevel.Info);
            //var cpu = GetCPUSettings();
            //Log.Instance.WriteToConsole($"⇒ Processor \"{cpu["Name"]}\" • Cores \"{cpu["NumberOfCores"]}\"", LogLevel.Info);
        }
        catch (KeyNotFoundException) { }
    }

    /// <summary>
    /// The timezone offset in WMIC's date string is "-300", this is not a standard timezone offset format.
    /// The standard format is "+HH:mm" or "-HH:mm". The "-300" from WMIC is known as the "bias" and represents
    /// the timezone offset in minutes.
    /// </summary>
    static string WMICOffsetConversion(string dateString)
    {
        string format = "yyyyMMddHHmmss.ffffffzzz"; // "20231106082826.500000-300"

        // Extract the timezone offset from the date string
        int timezoneOffsetInMinutes = Int32.Parse(dateString.Substring(dateString.Length - 4));

        // Convert the timezone offset from minutes to the format "+HH:mm" or "-HH:mm"
        TimeSpan offset = TimeSpan.FromMinutes(timezoneOffsetInMinutes);
        string newOffset = offset.ToString(@"hh\:mm");
        if (timezoneOffsetInMinutes < 0)
            newOffset = "-" + newOffset;
        else
            newOffset = "+" + newOffset;

        // Replace the old timezone offset with the new one in the date string
        dateString = dateString.Remove(dateString.Length - 4) + newOffset;

        // Perform a TryParseExact on our adjusted date string.
        if (DateTime.TryParseExact(dateString, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime result))
        {
            //Log.Instance.WriteToConsole($"Converted '{dateString}' to {result}.", LogLevel.Info);
            return $"{result}";
        }
        else
        {
            //Log.Instance.WriteToConsole($"Unable to convert '{dateString}' to a date.", LogLevel.Info);
            return $"{dateString}";
        }
    }

    /// <summary>
    /// Windows only.
    /// </summary>
    /// <returns><see cref="Dictionary{TKey, TValue}"/></returns>
    static Dictionary<string, string> GetOSSettings()
    {
        Dictionary<string, string> result = new();
        var lines = CallWMIC("OS");
        if (lines.Count > 1)
        {
            int index = 0;
            string data = lines[1];
            Dictionary<string, int> distances = GetDistances(lines[0]);
            foreach (var kvp in distances)
            {
                if (!string.IsNullOrEmpty(kvp.Key))
                {
                    string element = $"{kvp.Key}" + new string('.', kvp.Value);

                    if (data.Length >= index + element.Length)
                    {
                        string value = $"{data.Substring(index, element.Length)}";
                        result[$"{kvp.Key}"] = value.Trim();
                    }

                    index += element.Length;
                }
            }
        }
        return result;
    }

    /// <summary>
    /// Windows only.
    /// </summary>
    /// <returns><see cref="Dictionary{TKey, TValue}"/></returns>
    static List<Dictionary<string, string>> GetWindowsServices()
    {
        /* [Key Names]
           AcceptPause  
           AcceptStop  
           Caption                                                                             
           CheckPoint  
           CreationClassName  
           DelayedAutoStart  
           Description                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                          
           DesktopInteract  
           DisplayName                                                                         
           ErrorControl             
           ExitCode  
           InstallDate  
           Name                                                    
           PathName                                                                                                                                                                                                                                                                                                                                                                                                             
           ProcessId                                          
           ServiceSpecificExitCode  
           ServiceType    
           Started        
           StartMode  
           StartName                    
           State                        
           Status   
           SystemCreationClassName  
           SystemName            
           TagId          
           WaitHint  
        */
        List<Dictionary<string, string>> result = new();
        var lines = CallWMIC("SERVICE");
        // This will contain a line item for every service in the system.
        if (lines.Count > 1)
        {
            Dictionary<string, int> distances = GetDistances(lines[0]);
            for (int i = 1; i < lines.Count - 1; i++)
            {
                int index = 0;
                string data = lines[i];
                Dictionary<string, string> individual = new();
                foreach (var kvp in distances)
                {
                    if (!string.IsNullOrEmpty(kvp.Key))
                    {
                        string element = $"{kvp.Key}" + new string('.', kvp.Value);

                        if (data.Length >= index + element.Length)
                        {
                            string value = $"{data.Substring(index, element.Length)}";
                            individual[$"{kvp.Key}"] = value.Trim();
                        }
                        index += element.Length;
                    }
                }
                result.Add(individual);
            }
        }
        return result;
    }

    /// <summary>
    /// Windows only.
    /// </summary>
    /// <returns><see cref="Dictionary{TKey, TValue}"/></returns>
    static List<Dictionary<string, string>> GetWindowsProcesses()
    {
        /* [Key Names]
           Caption                                       
           CommandLine                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             
           CreationClassName  
           CreationDate               
           CSCreationClassName   
           CSName         
           Description                                   
           ExecutablePath                                                                                                                                              
           ExecutionState  
           Handle  
           HandleCount  
           InstallDate  
           KernelModeTime   
           MaximumWorkingSetSize  
           MinimumWorkingSetSize  
           Name                                          
           OSCreationClassName    
           OSName                                                                   
           OtherOperationCount  
           OtherTransferCount  
           PageFaults  
           PageFileUsage  
           ParentProcessId  
           PeakPageFileUsage  
           PeakVirtualSize  
           PeakWorkingSetSize  
           Priority  
           PrivatePageCount  
           ProcessId  
           QuotaNonPagedPoolUsage  
           QuotaPagedPoolUsage  
           QuotaPeakNonPagedPoolUsage  
           QuotaPeakPagedPoolUsage  
           ReadOperationCount  
           ReadTransferCount  
           SessionId  
           Status  
           TerminationDate  
           ThreadCount  
           UserModeTime  
           VirtualSize    
           WindowsVersion  
           WorkingSetSize  
           WriteOperationCount  
           WriteTransferCount          
        */
        List<Dictionary<string, string>> result = new();
        var lines = CallWMIC("PROCESS");
        // This will contain a line item for every process in the system.
        if (lines.Count > 1)
        {
            Dictionary<string, int> distances = GetDistances(lines[0]);
            for (int i = 1; i < lines.Count - 1; i++)
            {
                int index = 0;
                string data = lines[i];
                Dictionary<string, string> individual = new();
                foreach (var kvp in distances)
                {
                    if (!string.IsNullOrEmpty(kvp.Key))
                    {
                        string element = $"{kvp.Key}" + new string('.', kvp.Value);

                        if (data.Length >= index + element.Length)
                        {
                            string value = $"{data.Substring(index, element.Length)}";
                            individual[$"{kvp.Key}"] = value.Trim();
                        }
                        index += element.Length;
                    }
                }
                result.Add(individual);
            }
        }
        return result;
    }

    /// <summary>
    /// Windows only.
    /// </summary>
    /// <returns><see cref="Dictionary{TKey, TValue}"/></returns>
    static List<Dictionary<string, string>> GetWindowsShares()
    {
        List<Dictionary<string, string>> result = new();
        var lines = CallWMIC("SHARE");
        // This will contain a line item for every process in the system.
        if (lines.Count > 1)
        {
            Dictionary<string, int> distances = GetDistances(lines[0]);
            for (int i = 1; i < lines.Count - 1; i++)
            {
                int index = 0;
                string data = lines[i];
                Dictionary<string, string> individual = new();
                foreach (var kvp in distances)
                {
                    if (!string.IsNullOrEmpty(kvp.Key))
                    {
                        string element = $"{kvp.Key}" + new string('.', kvp.Value);

                        if (data.Length >= index + element.Length)
                        {
                            string value = $"{data.Substring(index, element.Length)}";
                            individual[$"{kvp.Key}"] = value.Trim();
                        }
                        index += element.Length;
                    }
                }
                result.Add(individual);
            }
        }
        return result;
    }

    /// <summary>
    /// Windows only.
    /// </summary>
    /// <returns><see cref="Dictionary{TKey, TValue}"/></returns>
    static List<Dictionary<string, string>> GetWindowsPrinters()
    {
        List<Dictionary<string, string>> result = new();
        var lines = CallWMIC("PRINTER");
        // This will contain a line item for every process in the system.
        if (lines.Count > 1)
        {
            Dictionary<string, int> distances = GetDistances(lines[0]);
            for (int i = 1; i < lines.Count - 1; i++)
            {
                int index = 0;
                string data = lines[i];
                Dictionary<string, string> individual = new();
                foreach (var kvp in distances)
                {
                    if (!string.IsNullOrEmpty(kvp.Key))
                    {
                        string element = $"{kvp.Key}" + new string('.', kvp.Value);

                        if (data.Length >= index + element.Length)
                        {
                            string value = $"{data.Substring(index, element.Length)}";
                            individual[$"{kvp.Key}"] = value.Trim();
                        }
                        index += element.Length;
                    }
                }
                result.Add(individual);
            }
        }
        return result;
    }

    /// <summary>
    /// Windows only.
    /// </summary>
    /// <returns><see cref="Dictionary{TKey, TValue}"/></returns>
    static Dictionary<string, string> GetCPUSettings()
    {
        Dictionary<string, string> result = new();
        var lines = CallWMIC("CPU");
        if (lines.Count > 1)
        {
            int index = 0;
            string data = lines[1];
            Dictionary<string, int> distances = GetDistances(lines[0]);
            foreach (var kvp in distances)
            {
                if (!string.IsNullOrEmpty(kvp.Key))
                {
                    string element = $"{kvp.Key}" + new string('.', kvp.Value);

                    if (data.Length >= index + element.Length)
                    {
                        string value = $"{data.Substring(index, element.Length)}";
                        result[$"{kvp.Key}"] = value.Trim();
                    }

                    index += element.Length;
                }
            }
        }
        return result;
    }

    /// <summary>
    /// If <paramref name="alias"/> is empty then a random category will be selected.
    /// </summary>
    static List<string> CallWMIC(string alias = "")
    {
        if (string.IsNullOrEmpty(alias))
        {
            var available = GetWMICOptions();
            alias = available[Random.Shared.Next(0, available.Length)].Trim();
            Console.ForegroundColor = ConsoleColor.White;
            Log.Instance.WriteConsole($"Showing all values for \"{alias}\"...", LogLevel.Info);
            Console.ForegroundColor = ConsoleColor.Gray;
        }

        var proc = new Process();
        // This needs to be set to the working dir / classpath dir as the library looks for this system property at runtime
        //proc.StartInfo.Environment["JAVA_TOOL_OPTIONS"] = $"-Dcom.android.sdkmanager.toolsdir=\"{toolPath}\"";
        proc.StartInfo.FileName = "wmic";
        //proc.StartInfo.Arguments = "computersystem get";
        proc.StartInfo.Arguments = $"{alias} get"; // The "get" or "list" modifier can be used for each category.
        //proc.StartInfo.WorkingDirectory = libPath;
        proc.StartInfo.UseShellExecute = false;
        proc.StartInfo.RedirectStandardOutput = true;
        proc.StartInfo.RedirectStandardError = true;
        // Buffer for holding app output
        var output = new List<string>();
        var error = new List<string>();
        proc.OutputDataReceived += (s, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                output.Add(e.Data);
        };
        proc.ErrorDataReceived += (s, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                error.Add(e.Data);
        };
        if (proc.Start())
        {
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            bool wait = proc.WaitForExit(TimeSpan.FromSeconds(60)); // blocking call
            if (!wait)
            {
                //var toKill = Process.GetProcessById(proc.Id);
                Log.Instance.WriteConsole($">> Got tired of waiting, killing it now...", LogLevel.Info);
                proc.Kill();
            }
        }

        if (error.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            foreach (var item in error)
                Log.Instance.WriteConsole($"{item}", LogLevel.Info);
            Console.ForegroundColor = ConsoleColor.Gray;
            return new List<string>();
        }
        return output;
    }

    static string[] GetWMICOptions()
    {
        return new string[]
        {
            "ALIAS              ", // Access to the aliases available on the local system
            "BASEBOARD          ", // Base board (also known as a motherboard or system board) management.
            "BIOS               ", // Basic input/output services (BIOS) management.
            "BOOTCONFIG         ", // Boot configuration management.
            "CDROM              ", // CD", //ROM management.
            "COMPUTERSYSTEM     ", // Computer system management.
            "CPU                ", // CPU management.
            "CSPRODUCT          ", // Computer system product information from SMBIOS. 
            "DATAFILE           ", // DataFile Management.  
            "DCOMAPP            ", // DCOM Application management.
            "DESKTOP            ", // User's Desktop management.
            "DESKTOPMONITOR     ", // Desktop Monitor management.
            "DEVICEMEMORYADDRESS", // Device memory addresses management.
            "DISKDRIVE          ", // Physical disk drive management. 
            "DISKQUOTA          ", // Disk space usage for NTFS volumes.
            "DMACHANNEL         ", // Direct memory access (DMA) channel management.
            "ENVIRONMENT        ", // System environment settings management.
            "FSDIR              ", // Filesystem directory entry management. 
            "GROUP              ", // Group account management. 
            "IDECONTROLLER      ", // IDE Controller management.  
            "IRQ                ", // Interrupt request line (IRQ) management. 
            "JOB                ", // Provides  access to the jobs scheduled using the schedule service. 
            "LOADORDER          ", // Management of system services that define execution dependencies. 
            "LOGICALDISK        ", // Local storage device management.
            "LOGON              ", // LOGON Sessions.  
            "MEMCACHE           ", // Cache memory management.
            "MEMORYCHIP         ", // Memory chip information.
            "MEMPHYSICAL        ", // Computer system's physical memory management. 
            "NETCLIENT          ", // Network Client management.
            "NETLOGIN           ", // Network login information (of a particular user) management. 
            "NETPROTOCOL        ", // Protocols (and their network characteristics) management.
            "NETUSE             ", // Active network connection management.
            "NIC                ", // Network Interface Controller (NIC) management.
            "NICCONFIG          ", // Network adapter management. 
            "NTDOMAIN           ", // NT Domain management.  
            "NTEVENT            ", // Entries in the NT Event Log.  
            "NTEVENTLOG         ", // NT eventlog file management. 
            "ONBOARDDEVICE      ", // Management of common adapter devices built into the motherboard (system board).
            "OS                 ", // Installed Operating System/s management. 
            "PAGEFILE           ", // Virtual memory file swapping management. 
            "PAGEFILESET        ", // Page file settings management. 
            "PARTITION          ", // Management of partitioned areas of a physical disk.
            "PORT               ", // I/O port management.
            "PORTCONNECTOR      ", // Physical connection ports management.
            "PRINTER            ", // Printer device management. 
            "PRINTERCONFIG      ", // Printer device configuration management.  
            "PRINTJOB           ", // Print job management. 
            "PROCESS            ", // Process management. 
            "PRODUCT            ", // Installation package task management. 
            "QFE                ", // Quick Fix Engineering.  
            "QUOTASETTING       ", // Setting information for disk quotas on a volume. 
            "RDACCOUNT          ", // Remote Desktop connection permission management.
            "RDNIC              ", // Remote Desktop connection management on a specific network adapter.
            "RDPERMISSIONS      ", // Permissions to a specific Remote Desktop connection.
            "RDTOGGLE           ", // Turning Remote Desktop listener on or off remotely.
            "RECOVEROS          ", // Information that will be gathered from memory when the operating system fails. 
            "REGISTRY           ", // Computer system registry management.
            "SCSICONTROLLER     ", // SCSI Controller management.  
            "SERVER             ", // Server information management. 
            "SERVICE            ", // Service application management. 
            "SHADOWCOPY         ", // Shadow copy management.
            "SHADOWSTORAGE      ", // Shadow copy storage area management.
            "SHARE              ", // Shared resource management. 
            "SOFTWAREELEMENT    ", // Management of the  elements of a software product installed on a system.
            "SOFTWAREFEATURE    ", // Management of software product subsets of SoftwareElement. 
            "SOUNDDEV           ", // Sound Device management.
            "STARTUP            ", // Management of commands that run automatically when users log onto the computer system.
            "SYSACCOUNT         ", // System account management.  
            "SYSDRIVER          ", // Management of the system driver for a base service.
            "SYSTEMENCLOSURE    ", // Physical system enclosure management.
            "SYSTEMSLOT         ", // Management of physical connection points including ports,  slots and peripherals, and proprietary connections points.
            "TAPEDRIVE          ", // Tape drive management.  
            "TEMPERATURE        ", // Data management of a temperature sensor (electronic thermometer).
            "TIMEZONE           ", // Time zone data management. 
            "UPS                ", // Uninterruptible power supply (UPS) management. 
            "USERACCOUNT        ", // User account management.
            "VOLTAGE            ", // Voltage sensor (electronic voltmeter) data management.
            "VOLUME             ", // Local storage volume management.
            "VOLUMEQUOTASETTING ", // Associates the disk quota setting with a specific disk volume. 
            "VOLUMEUSERQUOTA    ", // Per user storage volume quota management.
            "WMISET             ", // WMI service operational parameters management. 
        };
    }
    #endregion

    #region [PowerShell Stuff]
    /// <summary>
    /// For collecting files that have alternate data streams.
    /// command = "Get-ChildItem -Recurse | Get-Item -Stream Zone.Identifier -ErrorAction SilentlyContinue | Select-Object FileName"
    /// </summary>
    public static List<string> RunPowerShellCommand(string workingDirectory, string command)
    {
        var proc = new Process();
        proc.StartInfo.FileName = "powershell.exe";
        proc.StartInfo.Arguments = $"-Command \"{command}\"";
        proc.StartInfo.WorkingDirectory = string.IsNullOrEmpty(workingDirectory) ? Directory.GetCurrentDirectory() : workingDirectory;
        proc.StartInfo.UseShellExecute = false;
        proc.StartInfo.RedirectStandardOutput = true;
        proc.StartInfo.RedirectStandardOutput = true;
        proc.StartInfo.RedirectStandardError = true;
        // Buffer for holding app output
        var output = new List<string>();
        var error = new List<string>();
        proc.OutputDataReceived += (s, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                output.Add(e.Data);
        };
        proc.ErrorDataReceived += (s, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                error.Add(e.Data);
        };
        if (proc.Start())
        {
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            bool wait = proc.WaitForExit(TimeSpan.FromSeconds(60)); // blocking call
            if (!wait)
            {
                //var toKill = Process.GetProcessById(proc.Id);
                Log.Instance.WriteConsole($">> Got tired of waiting, killing it now...", LogLevel.Info);
                proc.Kill();
            }
        }

        if (error.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            foreach (var item in error)
                Log.Instance.WriteConsole($"{item}", LogLevel.Info);
            Console.ForegroundColor = ConsoleColor.Gray;
            return new List<string>();
        }
        return output;
    }
    #endregion

    #region [Superfluous]
    /// <summary>
    /// QueueUserWorkItem is a fire-n-forget mechanism, and monitoring its completion 
    /// directly is not part of its design. Consider using Task.Run or TaskFactory.StartNew 
    /// if you require more control over asynchronous operations or if you need to track 
    /// completion through tasks and their completion states.
    /// </summary>
    static void TestThreadPoolQueue(int numItems = 20)
    {
        double totalTime = 0d;
        int workItemCount = 0;
        object lockObject = new object();
        // Enqueue user work items.
        for (int i = 0; i < numItems; i++)
        {
            Log.Instance.WriteConsole($"Starting thread #{workItemCount}.", LogLevel.Info);
            Interlocked.Increment(ref workItemCount); // Increment counter
            ThreadPool.QueueUserWorkItem(state => {
                try
                {
                    var vsw = ValueStopwatch.StartNew();

                    // Perform some fake work.
                    if (Utils.CoinFlip())
                        Thread.Sleep(Random.Shared.Next(10, 5001));
                    else
                        Thread.Sleep(Random.Shared.Next(100, 2001));

                    // On completion, decrement counter.
                    lock (lockObject)
                    {
                        Interlocked.Decrement(ref workItemCount);
                        var ts = vsw.GetElapsedTime();
                        totalTime += ts.TotalSeconds;
                        Log.Instance.WriteConsole($"Thread #{workItemCount} finished ({vsw.GetElapsedTime().ToReadableString()})", LogLevel.Info);
                        if (workItemCount == 0)
                        {
                            state = $"Average was {(totalTime/(double)numItems):N3} seconds";
                            // The object state will typically be null, but we'll pass it anyways.
                            OnUserWorkItemComplete?.Invoke(state, "All work items are done.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    string details = ex?.InnerException != null ? $"{ex?.Message} ⇒ Inner Exception: {ex?.InnerException.Message}" : $"{ex?.Message}";
                    Log.Instance.WriteConsole($"ThreadPoolException: {details}", LogLevel.Error);
                }
            });
        }
    }
    public static event Action<object?, string> OnUserWorkItemComplete = (item, msg) => { };
    #endregion

    /// <summary>
    /// Domain exception handler.
    /// </summary>
    static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Console.CursorVisible = true;
        Log.Instance.WriteConsole("!!! Caught unhandled exception event on " + DateTime.Now.ToLongDateString() + " at " + DateTime.Now.ToLongTimeString() + " !!!", LogLevel.Error);
        var ex = e.ExceptionObject as Exception;
        if (ex != null) { Log.Instance.WriteConsole(FormatException(ex), LogLevel.Error); }
        // In the event that we're launched from a shortcut,
        // allow enough pause for the user to see the error.
        Thread.Sleep(5000);
    }
}
