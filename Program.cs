using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Json;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using System.Threading.Channels;

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

    /// <summary>
    /// Domain exception handler.
    /// </summary>
    static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Console.CursorVisible = true;
        Console.WriteLine("!!! Caught unhandled exception event on " + DateTime.Now.ToLongDateString() + " at " + DateTime.Now.ToLongTimeString() + " !!!");
        var ex = e.ExceptionObject as Exception;
        if (ex != null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(FormatException(ex));
            Console.ForegroundColor = ConsoleColor.Gray;
        }
        // In the event that we're launched from a shortcut,
        // allow enough pause for the user to see the error.
        Thread.Sleep(5000);
    }

    static void Main(string[] args)
    {
        #region [Initialization and Extras]
        // Keep watch for any errant wrong-doing.
        AppDomain.CurrentDomain.UnhandledException += new System.UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
        Console.OutputEncoding = Encoding.UTF8;

        #region [Get console window sizes]
        _conHwnd = ConsoleHelper.GetForegroundWindow();
        var winSize = ConsoleHelper.GetWindowSize(_conHwnd);
        var buffSize = ConsoleHelper.GetConsoleSize();
        Console.WriteLine($"⇒ WindowSize:{winSize.width},{winSize.height} ─ BufferSize:{buffSize.width},{buffSize.height}");
        //ConsoleHelper.SetWindowPosition(conHwnd, 1, 1, winSize.width, winSize.width);
        #endregion

        #region [Load the app settings]
        var config = _settings.GetSettings("Settings.cfg");
        // Do we have any settings?
        if (config != null)
        {
            // Show current settings using Reflection.
            //foreach (var val in settings.ListSettings()) { Console.WriteLine($"⇒ \"{val}\""); }

            try
            {   // It's best to do this after initializing the Window Sizes and Buffers.
                ConsoleHelper.SetCurrentFont(config.FontName, Convert.ToInt16(config.FontSize));
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                Console.WriteLine($"⇒ SetCurrentFont {ex.Message}");
            }
        }
        #endregion

        // Show the current runtime info.
        Assembly assembly = typeof(Program).Assembly;
        var frameAttr = (TargetFrameworkAttribute)assembly.GetCustomAttributes(typeof(TargetFrameworkAttribute), false)[0];
        Console.WriteLine(string.Format("⇒ {0} ─ User \"{1}\"", string.IsNullOrEmpty(frameAttr.FrameworkDisplayName) ? frameAttr.FrameworkName : frameAttr.FrameworkDisplayName, Environment.UserName));
        Console.WriteLine($"⇒ Windows version {Environment.OSVersion.Version} is being reported from the environment.");
        Console.WriteLine($"⇒ Runtime is here \"{RuntimeEnvironment.GetRuntimeDirectory()}\"");
        Console.WriteLine($"⇒ Current process is \"{Process.GetCurrentProcess().MainModule?.FileName}\"");
        //_winVersion = Utils.GetOSMajorAndMinor();
        if (Utils.IsWindowsCompatible())
        {   // Configure embedded resources.
            _iconNormal = Resources.ResourceManager.GetObject("Logo") as Icon;
            _iconWarning = Resources.ResourceManager.GetObject("Warning") as Icon;
            // Update the console window icon.
            IconUpdater.SetConsoleIconAtRuntime(_iconNormal);
        }
        #endregion

        DumpAllEmbeddedResources();

        ShowLogo(leftPad: 1, addPause: true);

        //var result = await TaskTimer.Start(async () => { await RunThreadTest(); });
        //Console.WriteLine($"Task took {result.Duration.TotalSeconds} seconds (no return value)");

        #region [Setup the ChannelManager's delegates]
        _chanman.OnBeginInvoke += (item, msg) => { $"••BEGIN••••• {msg}".Announcement(); /* var ci = item as ChannelItem; */ };
        _chanman.OnEndInvoke += (item, msg) =>   { $"••END••••••• {msg}".Announcement(); };
        _chanman.OnCancel += (item, msg) =>      { $"••CANCEL•••• {msg}".Announcement(); };
        _chanman.OnError += (item, msg) =>       { $"••ERROR••••• {msg}".Announcement(); };
        _chanman.OnWarning += (item, msg) =>     { $"••WARNING••• {msg}".Announcement(); };
        _chanman.OnShutdown += (msg) =>                { $"••SHUTDOWN•• {msg}".Announcement(); };
        _chanman.ChangeResolution(1000);
        #endregion

        #region [Setup the ConcurrentlManager's delegates]
        _queueman.OnBeginInvoke += (item, msg) => { $"••BEGIN••••• {msg}".Announcement(); /* var ci = item as QueueItem; */ };
        _queueman.OnEndInvoke += (item, msg) =>   { $"••END••••••• {msg}".Announcement(); };
        _queueman.OnCancel += (item, msg) =>      { $"••CANCEL•••• {msg}".Announcement(); };
        _queueman.OnError += (item, msg) =>       { $"••ERROR••••• {msg}".Announcement(); };
        _queueman.OnWarning += (item, msg) =>     { $"••WARNING••• {msg}".Announcement(); };
        _queueman.OnShutdown += (msg) =>                { $"••SHUTDOWN•• {msg}".Announcement(); };
        _queueman.OnExhausted += (msg) =>               { 
            $"••EXHAUSTED•• {msg}".Announcement();
            TestConcurrentManager(Random.Shared.Next(10, _maxDesired * 10));
        };
        _queueman.ChangeResolution(1000);
        #endregion

        #region [Setup the ScheduleManager's delegates]
        _schedman.OnInvoke += (item, msg) =>  { $"••INVOKE•••• {msg}".Announcement(); /* var ai = item as ActionItem; */ };
        _schedman.OnCancel += (item, msg) =>  { $"••CANCEL•••• {msg}".Announcement(); };
        _schedman.OnError += (item, msg) =>   { $"••ERROR••••• {msg}".Announcement(); };
        _schedman.OnWarning += (item, msg) => { $"••WARNING••• {msg}".Announcement(); };
        _schedman.OnShutdown += (msg) =>            { $"••SHUTDOWN•• {msg}".Announcement(); };
        _schedman.OnExhausted += (msg) =>           { $"••EXHAUSTED•• {msg}".Announcement();
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
                            Console.WriteLine($"{title} #{trapped} ran for {vsw.GetElapsedTime().GetReadableTime()}");
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
            Console.WriteLine($"⇒ \"{_conKey}\" keypress detected.");
            if (_conKey == ConsoleKey.D1)
            {
                config!.TestNumber = 1;
                Console.WriteLine($"⇒ Test #{config.TestNumber} selected.");
                TestSequentialThreadingChannel();
            }
            else if (_conKey == ConsoleKey.D2)
            {
                config!.TestNumber = 2;
                Console.WriteLine($"⇒ Test #{config.TestNumber} selected.");
                //Task.Run(async () => await TestParallelThreadingChannel()).GetAwaiter().GetResult();
                var rez = TaskTimer.Start(async () => { await TestParallelThreadingChannel(); }).GetAwaiter().GetResult();
                Console.WriteLine($"⇒ Waited {rez.Duration.GetReadableTime()}");
            }
            else if (_conKey == ConsoleKey.D3)
            {
                config!.TestNumber = 3;
                Console.WriteLine($"⇒ Test #{config.TestNumber} selected.");
                //Console.WriteLine($"⇒ Press 'A' to add {nameof(ChannelItem)}s.");
                if (_adder == null)
                {
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
                config!.TestNumber = 4;
                Console.WriteLine($"⇒ Test #{config.TestNumber} selected.");
                TestScheduleManager();
            }
            else if (_conKey == ConsoleKey.D5)
            {
                config!.TestNumber = 5;
                Console.WriteLine($"⇒ Test #{config.TestNumber} selected.");
                TestConcurrentManager(Random.Shared.Next(10, _maxDesired * 10));
            }
            else if (_conKey == ConsoleKey.D6)
            {
                config!.TestNumber = 5;
                if (Utils.IsWindowsCompatible())
                {
                    Console.WriteLine($"⇒ Test #{config.TestNumber} selected.");
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
                }
                else
                {
                    Console.WriteLine($"⇒ Test #{config.TestNumber} is only available on Windows.");
                }
                #endregion
            }
            else if (_conKey == ConsoleKey.D7)
            {
                Console.WriteLine($"⇒ Collecting services...");
                var services = GetWindowsServices();
                foreach (var serv in services)
                {
                    try
                    {
                        Console.WriteLine(string.Format("{0,-71}{1,-20}{2,-20}", serv["Caption"], serv["State"], serv["StartMode"]));
                    }
                    catch (KeyNotFoundException) { }
                }
            }
            else if (_conKey == ConsoleKey.D8)
            {
                Console.WriteLine($"⇒ Collecting processes...");
                var procs = GetWindowsProcesses();
                foreach (var proc in procs)
                {
                    try
                    {
                        Console.WriteLine(string.Format("{0,-60}{1,-30}{2,-10}", proc["Description"], WMICOffsetConversion(proc["CreationDate"]), proc["ProcessId"]));
                    }
                    catch (KeyNotFoundException) { }
                }
            }
            else if (_conKey == ConsoleKey.C)
            {
                if (config?.TestNumber == 3)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"\r\n⇒ Clearing {_chanman.GetItemCount()} from the Channel.");
                    _chanman.ClearItems();
                }
                else if (config?.TestNumber == 4)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"\r\n⇒ Clearing {_schedman.GetInactivatedCount()} items from the scheduler!");
                    _schedman.ClearSchedule();
                    _clearFlag = true;
                }
                else if (config?.TestNumber == 5)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"\r\n⇒ Clearing {_queueman.GetItemCount()} from the Queue.");
                    _queueman.ClearItems();
                }
            }
            else if (_conKey == ConsoleKey.B)
            {
                if (config?.TestNumber == 3)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"\r\nIsBusy? ⇒ {_chanman.IsBusy()}");
                }
                else if (config?.TestNumber == 4)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"\r\n⇒ Still waiting to be activated: {_schedman.GetInactivatedCount()}");
                }
            }
            else if (_conKey == ConsoleKey.T)
            {
                if (config?.TestNumber == 3)
                {
                    // This will not stop execution of the items if we're already inside the while loop.
                    _chanman.Toggle();
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"\r\nIs Channel Thread Suspended? ⇒ {_chanman.IsThreadSuspended()}");
                }
                else if (config?.TestNumber == 4) 
                {
                    // This will not stop execution of the items if we're already inside the while loop.
                    _schedman.Toggle();
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"\r\nIs Scheduler Thread Suspended? ⇒ {_schedman.IsThreadSuspended()}");
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
            Console.WriteLine($"\r\n⇒ {_chanman.GetItemCount()} items remain in the manager. ");
        }
        else if (config?.TestNumber == 4)
        {
            Console.WriteLine($"\r\n⇒ {_schedman.GetInactivatedCount()} items remain in the scheduler. ");
        }
        else if (config?.TestNumber == 5)
        {
            Console.WriteLine($"\r\n⇒ {_queueman.GetItemCount()} items remain in the queue. ");
        }
        
        // Inform the agents to close shop.
        _schedman.Dispose();
        _queueman.Dispose();
        _chanman.Dispose();
        
        // Signal any local thread loops.
        _shutdown = true;
        #endregion

        Console.WriteLine("\r\n⇒ Closing... ");
        Thread.Sleep(1800);
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
        if (Utils.IsWindowsCompatible())
            Console.WriteLine($"   6) Test WMIC                                    ");
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
        Console.WriteLine($"⇒ Writing {maxItems} items to the Channel...");
        _chanman.AddItems(list);
        Console.WriteLine($"\r\n⇒ Generation took {vsw.GetElapsedTime().GetReadableTime()}");
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
        Console.WriteLine($"\r\n⇒ AddItemValueTask process took {vsw.GetElapsedTime().GetReadableTime()}");
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
        Console.WriteLine($"\r\n⇒ WaitToWriteAsync process took {vsw.GetElapsedTime().GetReadableTime()}");
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
                Console.WriteLine($"⇒ Still working on existing items, please wait.");
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
        Console.WriteLine($"\r\n⇒ Attempting to write all to the Channel...");
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
            Console.WriteLine($"Produced: \"{ci.Title}\" ID#{ci.Id}.  Item Count: {itemCount}");
            Thread.Sleep(20);
        }
        // This step is only for parallel threads.
        items?.Writer.Complete();
        #endregion

        #region [Read]
        Console.WriteLine($"\r\n⇒ Attempting to read all from the Channel...");
        while (items?.Reader.Count > 0)
        {
            if (items.Reader.TryRead(out ChannelItem? item))
            {
                itemCount--;
                if (!item.Token.IsCancellationRequested)
                {
                    item.Activated = true;
                    Console.WriteLine($"Consumed: \"{item.Title}\" ID#{item.Id}.  Item Count: {itemCount}");
                    item.ToRun?.Invoke();
                }
                else
                {
                    Console.WriteLine($"Skipping \"{item.Title}\" ID#{item.Id}, since token has expired.  Item Count: {itemCount}");
                }
            }
            else
            {
                Console.WriteLine($"[WARNING]: Could not read from the Channel.");
                Thread.Sleep(100); // wait a little before retry
            }
        }
        #endregion

        Console.WriteLine($"⇒ Sequential Channel Test Complete.");
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
            Console.WriteLine($"\r\n⇒ Attempting to write all to the channel.");
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
                Console.WriteLine($"Produced: {ci.Title}, Item Count: {itemCount}");
                await Task.Delay(20);
            }
            // Signal any waiting threads.
            items.Writer.Complete();
        });
        #endregion

        #region [Read]
        var consumer = Task.Run(async () =>
        {
            Console.WriteLine($"\r\n⇒ Attempting to read all from the channel.");
            await foreach (var item in items.Reader.ReadAllAsync())
            {
                itemCount--;
                Console.WriteLine($"Consumed: \"{item.Title}\", Item Count: {itemCount}");
                item.ToRun?.Invoke();
            }
        });
        #endregion

        await Task.WhenAll(producer, consumer);

        Console.WriteLine($"⇒ Parallel Channel Test Complete.");
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
                    Console.WriteLine($"{title} #{trapped} scheduled for {runTime.ToLongTimeString()} started");
                    Thread.Sleep(Random.Shared.Next(100, 3001));
                    Console.WriteLine($"{title} #{trapped} ran for {vsw.GetElapsedTime().GetReadableTime()}");
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
                    ConsoleHelper.Beep(440, 200);
                    Thread.Sleep(1);
                    ConsoleHelper.Beep(784, 200);
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
            Console.WriteLine($"\r\n[Embedded resource keys]");
            // Iterate through the ResourceSet
            foreach (DictionaryEntry entry in rset)
            {
                // Get the resource key and value
                string resourceKey = (string)entry.Key;
                object? resourceValue = entry.Value;

                //Console.WriteLine($"Key: \"{resourceKey}\"   Value: {resourceValue}");
                Console.WriteLine($"Key: \"{resourceKey}\" ({resourceValue?.GetType()})");

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
                    Console.WriteLine("Task 1 completed!");
                },
                async token =>
                {
                    await Task.Delay(2000, token); // Simulate a synchronous operation
                    Console.WriteLine("Task 2 completed!");
                },
                async token =>
                {
                    await Task.Delay(2500, token); // Simulate a third asynchronous operation
                    Console.WriteLine("Task 3 completed!");
                    throw new Exception("I'm a fake error, ignore me.");
                },
                async token =>
                {
                    await Task.Delay(3000, token); // Simulate a fourth asynchronous operation
                    Console.WriteLine("Task 4 completed!");
                },
                async token =>
                {
                    await Task.Delay(4000, token); // Simulate a fith asynchronous operation
                    Console.WriteLine("Task 5 completed!");
                }
        };

        taskRunner.TaskCompleted += (sender, eventArgs) =>
        {
            Console.WriteLine($"Event fired: Task {eventArgs.CompletedTask.Id} completed!");
            Console.WriteLine($"Task Status: {eventArgs.CompletedTask.Status}\r\n");
        };

        taskRunner.TaskCanceled += (sender, eventArgs) =>
        {
            Console.WriteLine($"Event fired: Task {eventArgs.CanceledTask?.Id} canceled!");
            Console.WriteLine($"Task Status: {eventArgs.CanceledTask?.Status}\r\n");
        };

        taskRunner.TaskFailed += (sender, eventArgs) =>
        {
            Console.WriteLine($"Event fired: Task {eventArgs.FailedTask?.Id} faulted!");
            Console.WriteLine($"Task Error: {eventArgs.Exception.Message}\r\n");
            wasThereAnyFailure = true;
        };

        // Passing true indicates to not proceed if an error happens.
        await taskRunner.RunTasksSequentially(taskFactories, cts.Token, stopOnFault);

        if (stopOnFault && !wasThereAnyFailure)
            Console.WriteLine("All tasks completed!\r\n");
        else if (stopOnFault && wasThereAnyFailure)
            Console.WriteLine("Not all tasks were completed!\r\n");
        else
            Console.WriteLine("All tasks completed!\r\n");
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

        actionRunner.ActionCompleted += (sender, args) => {
            Console.WriteLine($"Action completed: {args.Action.Method.Name}");
        };

        actionRunner.ActionCanceled += (sender, args) => {
            Console.WriteLine($"Action canceled: {args.Action.Method.Name}");
        };

        actionRunner.ActionFailed += (sender, args) => {
            Console.WriteLine($"Action failed: {args.Action.Method.Name}, Exception: {args.Exception?.Message}");
        };

        List<Action> actions = new List<Action>
            {
                () => // 1st
                {
                    Thread.Sleep(1000);
                    Console.WriteLine("Action 1 complete");
                },
                () => // 2nd
                {
                    Thread.Sleep(1000);
                    Console.WriteLine("Action 2 complete");
                },
                () => // 3rd
                {
                    Thread.Sleep(2000);
                    throw new Exception("Error in Action 3");  // This will cause the sequence to stop
                },
                () => // 4th
                {
                    Thread.Sleep(3000);
                    Console.WriteLine("Action 4 complete");
                },
            };

        //Action act1 = new(() => { 
        //    Thread.Sleep(1000);
        //    Console.WriteLine("Action 1 complete");
        //});
        //Action act2 = new(() => {
        //    Thread.Sleep(1000);
        //    Console.WriteLine("Action 2 complete");
        //});
        //Action act3 = new(() => {
        //    Thread.Sleep(1000);
        //    throw new Exception("Error in Action 3");  // This will cause the sequence to stop
        //});
        //Action act4 = new(() => {
        //    Thread.Sleep(1000);
        //    Console.WriteLine("Action 4 complete");
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
                    Console.WriteLine("Action 1");
                    Thread.Sleep(1000);
                },
                () =>
                {
                    Console.WriteLine("Action 2");
                    Thread.Sleep(1000);
                },
                () =>
                {
                    Thread.Sleep(1000);
                    throw new Exception("Fake error in Action 3");
                },
                () =>
                {
                    Console.WriteLine("Action 4");
                    Thread.Sleep(1000);
                },
        };

        CancellationTokenSource cts = new CancellationTokenSource();
        cts.CancelAfter(5000); // Cancel after 5 seconds (adjust as needed)

        taskRunner.TaskCompleted += (sender, args) =>
        {
            Console.WriteLine($"Action completed. Task {args.CompletedTask.Id}\r\n");
        };

        taskRunner.TaskCanceled += (sender, eventArgs) =>
        {
            Console.WriteLine($"Event fired: Action canceled! Task {eventArgs.CanceledTask?.Id}\r\n");
        };

        taskRunner.TaskFailed += (sender, args) =>
        {
            Console.WriteLine($"Action failed: {args.Exception.Message}");
            wasThereAnyFailure = true;
        };

        // Passing true indicates to not proceed if an error happens.
        await taskRunner.RunActionsSequentially(actions, cts.Token, stopOnFault);

        if (stopOnFault && !wasThereAnyFailure)
            Console.WriteLine("All actions completed!\r\n");
        else if (stopOnFault && wasThereAnyFailure)
            Console.WriteLine("Not all actions were completed!\r\n");
        else
            Console.WriteLine("All actions completed!\r\n");
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
                    Console.WriteLine("Task 1 completed!");
                }),
                Task.Run(async () =>
                {
                    await Task.Delay(1500, cts.Token); // Simulate another asynchronous operation
                    Console.WriteLine("Task 2 completed!");
                    throw new Exception("I'm a fake error, ignore me.");
                }),
                Task.Run(async () =>
                {
                    await Task.Delay(2000, cts.Token); // Simulate a third asynchronous operation
                    Console.WriteLine("Task 3 completed!");
                }),
                Task.Run(async () =>
                {
                    await Task.Delay(3000, cts.Token); // Simulate a third asynchronous operation
                    Console.WriteLine("Task 4 completed!");
                })
        };

        taskRunner.TaskCompleted += (sender, eventArgs) =>
        {
            Console.WriteLine($"Event fired: Task {eventArgs.CompletedTask.Id} completed!");
            Console.WriteLine($"Task Status: {eventArgs.CompletedTask.Status}\r\n");
        };

        taskRunner.TaskFailed += (sender, eventArgs) =>
        {
            Console.WriteLine($"Event fired: Task {eventArgs.FailedTask?.Id} faulted!");
            Console.WriteLine($"Task Error: {eventArgs.Exception.Message}\r\n");
        };

        await taskRunner.RunTasksSequentially(tasks, true);

        Console.WriteLine("All tasks completed!");
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
                    Console.WriteLine($" • Func<Task> 1: t{Thread.CurrentThread.ManagedThreadId}");
                    await RunThreadTest();
                    Console.WriteLine($"Completed Func<Task> 1!");
                },
                async () =>
                {
                    Console.WriteLine($" • Func<Task> 2: t{Thread.CurrentThread.ManagedThreadId}");
                    await RunThreadTest();
                    Console.WriteLine($"Completed Func<Task> 2!");
                },
                async () =>
                {
                    Console.WriteLine($" • Func<Task> 3: t{Thread.CurrentThread.ManagedThreadId}");
                    await RunThreadTest();
                    Console.WriteLine($"Completed Func<Task> 3!");
                },
                async () =>
                {
                    Console.WriteLine($" • Func<Task> 4: t{Thread.CurrentThread.ManagedThreadId}");
                    await RunThreadTest();
                    Console.WriteLine($"Completed Func<Task> 4!");
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

        Console.WriteLine("All tasks completed!");
    }

    static async Task RunThreadTest()
    {
        Console.WriteLine($" • CallStackMethod.ConfigureAwait(true): t{Thread.CurrentThread.ManagedThreadId}");
        // This is the default which tells the compiler that you want
        // to return to the calling thread after the await is finished.
        await CallStackMethod().ConfigureAwait(true);

        Console.WriteLine($" • CallStackMethod.ConfigureAwait(false): t{Thread.CurrentThread.ManagedThreadId}");
        // This tells the compiler that we do not care if we
        // return to the calling thread after the await is finished.
        await CallStackMethod().ConfigureAwait(false);

        Console.WriteLine($" • RunThreadTest: t{Thread.CurrentThread.ManagedThreadId}");
    }

    static async Task CallStackMethod()
    {
        Console.WriteLine($" • SomeOtherMethod: t{Thread.CurrentThread.ManagedThreadId}");
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
            Console.WriteLine($"⇒ {os["Caption"]} • {os["OSArchitecture"]} • v{os["Version"]}");
            Console.WriteLine($"⇒ LastBoot {WMICOffsetConversion(os["LastBootUpTime"])} • InstallDate {WMICOffsetConversion(os["InstallDate"])}");
            Console.WriteLine($"⇒ CodePage {os["CodeSet"]} • {os["SystemDirectory"]} • {os["CSName"]} • Status {os["Status"]}");
            Console.WriteLine($"⇒ BootDevice \"{os["BootDevice"]}\" • SystemDevice \"{os["SystemDevice"]}\"");
            Console.WriteLine($"⇒ Organization \"{os["Organization"]}\" • SerialNumber \"{os["SerialNumber"]}\"");
            //var cpu = GetCPUSettings();
            //Console.WriteLine($"⇒ Processor \"{cpu["Name"]}\" • Cores \"{cpu["NumberOfCores"]}\"");
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
            //Console.WriteLine("Converted '{0}' to {1}.", dateString, result);
            return $"{result}";
        }
        else
        {
            //Console.WriteLine("Unable to convert '{0}' to a date.", dateString);
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
            Console.WriteLine($"Showing all values for \"{alias}\"...");
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
        proc.OutputDataReceived += (s, e) => {
            if (!string.IsNullOrEmpty(e.Data))
                output.Add(e.Data);
        };
        proc.ErrorDataReceived += (s, e) => {
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
                Console.WriteLine($">> Got tired of waiting, killing it now...");
                proc.Kill();
            }
        }

        if (error.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            foreach (var item in error)
                Console.WriteLine($"{item}");
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
}
