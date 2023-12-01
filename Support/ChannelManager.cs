using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Channels;
using System.Threading;
using System.Diagnostics;

namespace ProducerConsumer;

public class ChannelManager
{
    bool _debug = false;
    bool _suspended = false;
    bool _shutdown = false;
    bool _busy = false;
    bool _adding = false;
    int _resolution = 100;
    Thread _agent;
    Channel<ChannelItem>? channel = Channel.CreateUnbounded<ChannelItem>();

    #region [Events]
    // These are simple events that a Console/WinForm/WPF/UWP/WinUI3 application can hook to update the interface.
    public event Action<object?, string> OnBeginInvoke = (item, msg) => { };
    public event Action<object?, string> OnEndInvoke = (item, msg) => { };
    public event Action<object?, string> OnCancel = (item, msg) => { };
    public event Action<object?, string> OnError = (item, msg) => { };
    public event Action<object?, string> OnWarning = (item, msg) => { };
    public event Action<string> OnShutdown = (msg) => { };
    #endregion

    /// <summary>
    /// Default Constructor
    /// </summary>
    public ChannelManager()
    {
        _agent = new Thread(Loop)
        {
            IsBackground = true,
            Name = $"{nameof(ChannelManager)}_{DateTime.Now.ToString("dddMMMdd")}",
            Priority = ThreadPriority.BelowNormal
        };
        _agent.Start();
    }

    /// <summary>
    /// Secondary Constructor
    /// </summary>
    public ChannelManager(bool debugMode = false) : this()
    {
        // The debug flag could be used for other things,
        // but as of now it's used for debug console output.
        _debug = debugMode;
    }

    #region [Public Methods]
    /// <summary>
    /// Adds an individual <see cref="ChannelItem"/>.
    /// </summary>
    public bool AddItem(ChannelItem item)
    {
        if (item == null || _adding)
            return false;

        _adding = true;
        var result = channel?.Writer.TryWrite(item);
        if (result.HasValue && result.Value)
        {
            if (_debug)
            {
                string leftSide = $"Added: \"{item.Title}\" ID#{item.Id}.";
                string rightSide = $"Item Count: {GetItemCount()}";
                Console.WriteLine(string.Format("{0,-46}{1,30}", leftSide, rightSide));
            }
        }
        else
        {
            OnError?.Invoke(item, $"Failed to add: \"{item.Title}\" ID#{item.Id}!");
            if (_debug)
                Console.WriteLine($"[WARNING] Failed to add: \"{item.Title}\" ID#{item.Id}!");
        }
        _adding = false;

        return true;
    }

    /// <summary>
    /// Adds multiple <see cref="ChannelItem"/>s.
    /// </summary>
    public bool AddItems(List<ChannelItem> items)
    {
        if (items == null || _adding)
            return false;

        _adding = true;
        foreach (var ci in items)
        {
            var result = channel?.Writer.TryWrite(ci);
            if (result.HasValue && result.Value)
            {
                if (_debug)
                {
                    string leftSide = $"Added: \"{ci.Title}\" ID#{ci.Id}.";
                    string rightSide = $"Item Count: {GetItemCount()}";
                    Console.WriteLine(string.Format("{0,-46}{1,30}", leftSide, rightSide));
                }
            }
            else
            {
                OnError?.Invoke(ci, $"Failed to add: \"{ci.Title}\" ID#{ci.Id}!");
                if (_debug)
                    Console.WriteLine($"[WARNING] Failed to add: \"{ci.Title}\" ID#{ci.Id}!");
            }
        }
        _adding = false;

        return true;
    }

    public int GetItemCount()
    {
        if (channel != null)
            return channel.Reader.Count;
        else
            return 0;
    }

    public void ClearItems()
    {
        if (channel != null)
        {
            while (channel.Reader.TryRead(out ChannelItem? item))
            {
                if (_debug)
                    Console.WriteLine($"Cleared \"{item.Title}\" ID#{item.Id}.");
            }
        }
    }

    /// <summary>
    /// Change the agent thread's suspended/running state.
    /// This does not suspend any currently executing <see cref="ChannelItem"/>.
    /// </summary>
    public void Toggle()
    {
        // Add basic checks for first-time users not familiar with the order of operations.
        if (_agent == null)
            return;

        _suspended = !_suspended;

        if (_debug)
            Console.WriteLine($"⇒ {_agent.Name} has been {(_suspended ? "paused" : "unpaused")}.");

        lock (this)
        {
            // If thread resumed, notify state change.
            if (!_suspended)
                Monitor.Pulse(this);
        }
    }

    /// <summary>
    /// Is the agent thread alive?
    /// </summary>
    /// <returns>true if agent thread is running, false otherwise</returns>
    public bool IsThreadAlive() => (_agent != null) ? _agent.IsAlive : false;

    /// <summary>
    /// Is our agent thread suspended?
    /// </summary>
    /// <returns>true if agent thread is running, false otherwise</returns>
    public bool IsAgentAlive() => !_suspended;

    /// <summary>
    /// Are any actions currently running?
    /// </summary>
    /// <returns>true if a task is running, false otherwise</returns>
    public bool IsBusy() => _busy;

    /// <summary>
    /// Adjusts the update frequency of the monitor thread loop.
    /// If two <see cref="ChannelItem"/>s are set to run within the default
    /// 100ms of each other, then they might invoke together at runtime.
    /// </summary>
    public void ChangeResolution(int milliseconds)
    {
        if (_resolution > 0)
            _resolution = milliseconds;
    }

    /// <summary>
    /// Signal the agent thread to close shop.
    /// </summary>
    /// <param name="waitForRemaining">To block or not to block, that is the question.</param>
    public void Shutdown(bool waitForRemaining)
    {
        // Add basic checks for first-time users not familiar with the order of operations.
        if (_agent == null)
            return;

        // Signal our thread loop.
        _shutdown = true;

        // Wait for remaining tasks to finish.
        if (waitForRemaining)
        {
            if (_debug)
                Console.WriteLine($"⇒ Joining {_agent.Name} thread… ");

            _agent.Join();
        }
    }
    #endregion

    /// <summary>
    /// <see cref="ChannelItem"/>s are processed sequentially as they are added.
    /// The next item will not be executed until the previous one is finished.
    /// </summary>
    private void Loop()
    {
        while (!_shutdown)
        {
            lock (this)
            {
                // Loop until not suspended.
                while (_suspended)
                {
                    if (_debug)
                        Console.WriteLine($"⇒ {_agent.Name} on thread {Thread.CurrentThread.ManagedThreadId} is paused.");

                    // Suspend thread execution.
                    Monitor.Wait(this, 500);

                    // If shutting down resume the thread.
                    if (_shutdown)
                        _suspended = false;
                }
            }

            //if (_debug) { Console.Write($"•"); }

            Thread.Sleep(_resolution); // go easy on the CPU

            while (channel?.Reader.Count > 0 && !_adding)
            {
                if (channel.Reader.TryRead(out ChannelItem? item))
                {
                    // We could also employ a CancellationTokenRegistration ctr = item.Token.Register(() => { });
                    if (!item.Token.IsCancellationRequested)
                    {
                        item.Activated = true;
                        if (_debug)
                        {
                            string leftSide = $"Consuming: \"{item.Title}\" ID#{item.Id}.";
                            string rightSide = $"Item Count: {GetItemCount()}";
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine(string.Format("{0,-46}{1,30}", leftSide, rightSide));
                            Console.ForegroundColor = ConsoleColor.Gray;
                        }
                        try
                        {
                            _busy = true;
                            OnBeginInvoke?.Invoke(item, $"Invoking \"{item?.Title}\" ID#{item?.Id} on thread {Thread.CurrentThread.ManagedThreadId} [{DateTime.Now}]");
                            var vsw = ValueStopwatch.StartNew();
                            item?.ToRun?.Invoke();
                            OnEndInvoke?.Invoke(item, $"Invoked \"{item?.Title}\" ID#{item?.Id} ran for {vsw.GetElapsedTime().GetReadableTime()}.");
                        }
                        catch (Exception ex)
                        {
                            OnError?.Invoke(item, $"\"{item?.Title}\" ID#{item?.Id} caused exception: {ex.Message}");

                            if (_debug)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"{ex.Message}");
                                Console.ForegroundColor = ConsoleColor.Gray;
                            }
                        }
                        finally 
                        { 
                            _busy = false; 
                        }
                    }
                    else
                    {
                        // We could also employ a CancellationTokenRegistration ctr = item.Token.Register(() => { });
                        OnCancel?.Invoke(item, $"\"{item?.Title}\" ID#{item?.Id} was canceled! [{DateTime.Now}]");

                        if (_debug)
                        {
                            string leftSide = $"Abandoning \"{item.Title}\" ID#{item.Id}, since token has expired.";
                            string rightSide = $"Item Count: {GetItemCount()}";
                            Console.ForegroundColor = ConsoleColor.Magenta;
                            Console.WriteLine(string.Format("{0,-46}{1,30}", leftSide, rightSide)); // negative left-justifies, while positive right-justifies
                            Console.ForegroundColor = ConsoleColor.Gray;
                        }
                    }
                }
                else
                {
                    // Item will most likely be null, but we'll pass it anyways.
                    OnWarning?.Invoke(item, $"Could not read from the Channel during the service loop.");

                    if (_debug)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"[WARNING]: Could not read from the Channel.");
                        Console.ForegroundColor = ConsoleColor.Gray;
                    }
                    Thread.Sleep(100); // wait a little before retry
                }

                if (_shutdown)
                    break;
            }
        }

        OnShutdown?.Invoke($"{_agent.Name} thread {Thread.CurrentThread.ManagedThreadId} finished. [{DateTime.Now}]");
        if (_debug)
            Console.WriteLine($"⇒ Exiting {_agent.Name} thread.");
    }
}

/// <summary>
/// Our support class for the <see cref="System.Threading.Channels.Channel{T}"/>.
/// </summary>
public class ChannelItem
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public Action? ToRun { get; set; }
    public bool Activated { get; set; } = false;
    public CancellationToken Token { get; set; }

    public ChannelItem(int id, string? title, Action? action, CancellationToken token = default(CancellationToken))
    {
        Id = id;
        Title = title;
        ToRun = action;
        Token = token;
    }
}
