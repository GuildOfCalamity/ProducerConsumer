using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ProducerConsumer;

/// <summary>
/// A basic home-brew channel implementation.
/// </summary>
public class ConcurrentManager
{
    bool _debug = false;
    bool _suspended = false;
    bool _shutdown = false;
    bool _busy = false;
    int _resolution = 100;
    Thread? _agent;
    ConcurrentQueue<QueueItem> _queue = new ConcurrentQueue<QueueItem>();
    SemaphoreSlim _semaphore = new SemaphoreSlim(0);

    #region [Events]
    // These are simple events that a Console/WinForm/WPF/UWP/WinUI3 application can hook to update the interface.
    public event Action<object?, string> OnBeginInvoke = (item, msg) => { };
    public event Action<object?, string> OnEndInvoke = (item, msg) => { };
    public event Action<object?, string> OnCancel = (item, msg) => { };
    public event Action<object?, string> OnError = (item, msg) => { };
    public event Action<object?, string> OnWarning = (item, msg) => { };
    public event Action<string> OnShutdown = (msg) => { };
    public event Action<string> OnExhausted = (msg) => { };
    #endregion

    /// <summary>
    /// Default Constructor
    /// </summary>
    public ConcurrentManager()
    {
        _agent = new Thread(Loop)
        {
            IsBackground = true,
            Name = $"{nameof(ConcurrentManager)}_{DateTime.Now.ToString("dddMMMdd")}",
            Priority = ThreadPriority.BelowNormal
        };
        _agent.Start();
    }

    /// <summary>
    /// Secondary Constructor
    /// </summary>
    public ConcurrentManager(bool debugMode = false) : this()
    {
        // The debug flag could be used for other things,
        // but as of now it's used for debug console output.
        _debug = debugMode;
    }

    #region [Private Methods]
    /// <summary>
    /// <see cref="QueueItem"/>s are processed sequentially as they are added.
    /// The next item will not be executed until the previous one is finished.
    /// </summary>
    private void Loop()
    {
        while (!_shutdown)
        {
            Thread.Sleep(_resolution); // go easy on the CPU

            // If we have anything new then extract and invoke it.
            while (_queue.Count > 0)
            {
                //var item = await ReadAsync();
                var item = Read();
                if (item != null)
                {
                    // We could also employ a CancellationTokenRegistration ctr = item.Token.Register(() => { });
                    if (!item.Token.IsCancellationRequested)
                    {
                        item.Activated = true;
                        if (_debug)
                        {
                            string leftSide = $"Consuming: \"{item?.Title}\" ID #{item?.Id}.";
                            string rightSide = $"Item Count: {GetItemCount()}";
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine(string.Format("{0,-46}{1,30}", leftSide, rightSide));
                            Console.ForegroundColor = ConsoleColor.Gray;
                        }
                        try
                        {
                            _busy = true;
                            OnBeginInvoke?.Invoke(item, $"Invoking \"{item?.Title}\" ID #{item?.Id} on thread {Thread.CurrentThread.ManagedThreadId} [{DateTime.Now}]");
                            var vsw = ValueStopwatch.StartNew();
                            item?.ToRun?.Invoke();
                            OnEndInvoke?.Invoke(item, $"Invoked \"{item?.Title}\" ID #{item?.Id} ran for {vsw.GetElapsedTime().GetReadableTime()}.");
                        }
                        catch (Exception ex)
                        {
                            OnError?.Invoke(item, $"\"{item?.Title}\" ID #{item?.Id} caused exception: {ex.Message}");
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
                        OnCancel?.Invoke(item, $"\"{item?.Title}\" ID #{item?.Id} was canceled! [{DateTime.Now}]");
                        if (_debug)
                        {
                            string leftSide = $"Abandoning \"{item?.Title}\" ID #{item?.Id}, token has expired.";
                            string rightSide = $"Item Count: {GetItemCount()}";
                            Console.ForegroundColor = ConsoleColor.Magenta;
                            Console.WriteLine(string.Format("{0,-46}{1,30}", leftSide, rightSide)); // negative left-justifies, while positive right-justifies
                            Console.ForegroundColor = ConsoleColor.Gray;
                        }
                    }
                }

                if (_shutdown)
                    break;

                if (GetItemCount() == 0)
                {
                    OnExhausted?.Invoke($"All items consumed from {_agent?.Name}");
                }
            }
        }

        OnShutdown?.Invoke($"{_agent?.Name} thread {Thread.CurrentThread.ManagedThreadId} finished. [{DateTime.Now}]");
        if (_debug)
            Console.WriteLine($"⇒ Exiting {_agent?.Name} thread.");
    }
    #endregion

    #region [Public Methods]
    /// <summary>
    /// Returns the ConcurrentQueue's count.
    /// </summary>
    public int GetItemCount()
    {
        return _queue.Count;
    }

    /// <summary>
    /// Empties the ConcurrentQueue.
    /// </summary>
    public void ClearItems()
    {
        if (_queue != null && _queue.Count > 0)
        {
            _queue.Clear();
            if (_debug)
                Console.WriteLine($"ConcurrentQueue was cleared.");
        }
    }

    /// <summary>
    /// Adds a <see cref="QueueItem"/> to the <see cref="ConcurrentQueue{T}"/>.
    /// </summary>
    /// <param name="item"><see cref="QueueItem"/></param>
    public void AddItem(QueueItem item)
    {
        if (item == null)
            return;

        _queue.Enqueue(item);
        // Notify consumers that data is available.
        _semaphore.Release();
    }

    /// <summary>
    /// Adds a list of <see cref="QueueItem"/>s to the <see cref="ConcurrentQueue{T}"/>.
    /// </summary>
    /// <param name="items"><see cref="List{QueueItem}"/></param>
    public void AddItems(IList<QueueItem> items)
    {
        if (items == null || items.Count == 0)
            return;

        foreach (QueueItem item in items)
        {
            _queue.Enqueue(item);
        }
        // Notify consumers that data is available.
        _semaphore.Release();
    }

    /// <summary>
    /// Synchronous <see cref="ConcurrentQueue{T}"/> reader.
    /// </summary>
    /// <param name="token"><see cref="CancellationToken"/></param>
    /// <returns><see cref="QueueItem"/></returns>
    public QueueItem? Read(CancellationToken token = default)
    {
        _semaphore.Wait(token); // wait
        if (_queue.TryDequeue(out QueueItem? item))
            return item;
        else
            return null;
    }

    /// <summary>
    /// Asynchronous <see cref="ConcurrentQueue{T}"/> reader.
    /// </summary>
    /// <param name="token"><see cref="CancellationToken"/></param>
    /// <returns><see cref="QueueItem"/></returns>
    public async ValueTask<QueueItem?> ReadAsync(CancellationToken token = default)
    {
        await _semaphore.WaitAsync(token).ConfigureAwait(false); // wait
        if (_queue.TryDequeue(out QueueItem? item))
            return item;
        else
            return null;
    }

    /// <summary>
    /// Is the agent thread alive?
    /// </summary>
    /// <returns>true if agent thread is running, false otherwise</returns>
    public bool IsThreadAlive() => (_agent != null) ? _agent.IsAlive : false;

    /// <summary>
    /// Are any actions currently running?
    /// </summary>
    /// <returns>true if an action is running, false otherwise</returns>
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
}

/// <summary>
/// Our support class for the <see cref="ConcurrentManager"/>.
/// </summary>
public class QueueItem
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public Action? ToRun { get; set; }
    public bool Activated { get; set; } = false;
    public CancellationToken Token { get; set; }

    public QueueItem(int id, string? title, Action? action, CancellationToken token = default(CancellationToken))
    {
        Id = id;
        Title = title;
        ToRun = action;
        Token = token;
    }
}
