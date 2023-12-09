using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ProducerConsumer;

/// <summary>
/// This is a time based list version of my original ScheduleQueue.
/// It will only run one thread which monitors the list for something to do.
/// If an <see cref="ActionItem"/> is found that matches the criteria then
/// it will be invoked inside of a <see cref="Task"/>.
/// </summary>
public class ScheduleManager : IDisposable
{
    #region [Properties]
    int _resolution = 100;
    bool _debug = false;
    bool _shutdown = false;
    bool _suspended = false;
    bool _busy = false;
    bool _informExhausted = false;
    Thread _agent;
    // NOTE: Don't use List<ActionItem> and locking object, as a collection modified exception could be thrown.
    BlockingCollection<ActionItem> _itemList = new BlockingCollection<ActionItem>();
    // We could also experiment with ConcurrentQueue<T>.
    #endregion

    #region [Events]
    // These are simple events that a Console/WinForm/WPF/UWP/WinUI3 application can hook to update the interface.
    public event Action<object?, string> OnInvoke = (item, msg) => { };
    public event Action<object?, string> OnCancel = (item, msg) => { };
    public event Action<object?, string> OnError = (item, msg) => { };
    public event Action<object?, string> OnWarning = (item, msg) => { };
    public event Action<string> OnShutdown = (msg) => { };
    public event Action<string> OnExhausted = (msg) => { };
    #endregion

    /// <summary>
    /// Default Constructor
    /// </summary>
    public ScheduleManager()
    {
        _agent = new Thread(Loop)
        {
            IsBackground = true,
            Name = $"{nameof(ScheduleManager)}_{DateTime.Now.ToString("dddMMMdd")}",
            Priority = ThreadPriority.BelowNormal
        };
        _agent.Start();
    }

    /// <summary>
    /// Secondary Constructor
    /// </summary>
    public ScheduleManager(bool debugMode = false) : this()
    {
        // The debug flag could be used for other things,
        // but as of now it's used for debug console output.
        _debug = debugMode;
    }

    #region [Private Methods]
    /// <summary>
    /// The main loop for our agent thread.
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
                        Debug.WriteLine($"⇒ {_agent.Name} on thread {Thread.CurrentThread.ManagedThreadId} is paused.");

                    // Suspend thread execution.
                    Monitor.Wait(this, 500);

                    // If shutting down resume the thread.
                    if (_shutdown)
                        _suspended = false;
                }
            }

            // Go easy on the CPU. This is also our resolution, i.e.
            // the accuracy +/- when the ActionItems are fired off.
            // If two ActionItems are set to run within the default 100ms
            // of each other, then they might invoke together at runtime.
            Thread.Sleep(_resolution);

            // Is there anything to do?
            while (_itemList.Count > 0)
            {
                #region [Single Thread Servicing Method]
                // Make a copy to avoid any lambda trapping.
                ActionItem? item = null;
                if (!_itemList.TryTake(out item))
                {
                    OnWarning?.Invoke(item, $"During the service loop, \"{item?.Title}\" could not be removed from the collection!");
                    continue;
                }

                // Is the ActionItem ready for execution?
                if (item != null && item.RunTime <= DateTime.Now && !item.Activated)
                {
                    // Since we're no longer starting a thread for each ActionItem
                    // we need a way to determine if it is currently running so we
                    // don't kick off another task while it is already running.
                    item.Activated = true;

                    if (_debug)
                        Debug.WriteLine($"⇒ \"{item.Title}\" is ready, running now…");

                    // Configure the cancellation token registration for the ActionItem…
                    CancellationTokenRegistration ctr = item.Token.Register(() =>
                    {
                        if (_debug)
                            Debug.WriteLine($"⇒ \"{item.Title}\" was cancelled.");

                        OnCancel?.Invoke(item, $"\"{item?.Title}\" was canceled! [{DateTime.Now}]");
                    });

                    // We need to start the action on another thread in the event
                    // that said action takes a long time to run which would result
                    // in blocking the monitor thread too long to service the list.
                    Task.Run(() =>
                    {
                        try
                        {
                            if (!item.Token.IsCancellationRequested)
                            {
                                _busy = true;
                                OnInvoke?.Invoke(item, $"Invoking \"{item.Title}\" on thread {Thread.CurrentThread.ManagedThreadId} [{DateTime.Now}]");
                                item.ToRun?.Invoke();
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[{item?.Title}]: {ex.Message}");
                            OnError?.Invoke(item, $"\"{item?.Title}\" caused exception: {ex.Message}");
                        }
                        finally
                        {
                            if (_debug)
                                Debug.WriteLine($"⇒ \"{item.Title}\" is now complete on task {Task.CurrentId}.");
                        }
                    }, item.Token).ContinueWith((t) =>
                    {
                        if (_debug)
                            Debug.WriteLine($"⇒ \"{item.Title}\" status: {t.Status}");
                        // Be sure to dispose of the CancellationTokenRegistration so
                        // it doesn't hang around in memory after the task is gone.
                        ctr.Dispose();
                        _busy = false;

                    });

                    // Is there anything left to run?
                    if (GetNextToRun() == null)
                    {
                        // There may be an action still running. This only indicates that all ActionItems have been activated.
                        OnExhausted?.Invoke($"All {nameof(ActionItem)}'s have been exhausted on thread {Thread.CurrentThread.ManagedThreadId}. [{DateTime.Now}]");
                    }
                }
                else if (item != null && !item.Activated)
                {
                    // Add back into the collection if not ready.
                    // NOTE: This logic branch is necessary since the BlockingCollection
                    // does not offer a way to retrieve an item without removing it.
                    if (!_itemList.TryAdd(item))
                    {
                        OnWarning?.Invoke(item, $"During the service loop, \"{item?.Title}\" could not be added back into the collection!");
                    }
                }

                // If the list contained thousands of items then
                // it might be wise to check the flag mid-loop.
                if (_shutdown)
                {
                    if (_itemList.Count > 0)
                    {
                        Debug.WriteLine($"[WARNING]: Abandoning {_itemList.Count} scheduled {nameof(ActionItem)}s.");
                        OnWarning?.Invoke(item, $"Abandoning {_itemList.Count} scheduled {nameof(ActionItem)}s.");
                    }
                    break;
                }
                #endregion
            }
        }

        OnShutdown?.Invoke($"{_agent.Name} thread {Thread.CurrentThread.ManagedThreadId} finished. [{DateTime.Now}]");
    }
    #endregion

    #region [Public Methods]
    /// <summary>
    /// Adds an <see cref="ActionItem"/> to the <see cref="List{T}"/>.
    /// </summary>
    /// <param name="item"><see cref="ActionItem"/></param>
    public void ScheduleItem(ActionItem item)
    {
        // Check for disposed object.
        if (_shutdown)
            throw new Exception($"Thread has been shutdown, you must create a new {nameof(ScheduleManager)}.");

        if (item == null)
            return;

        if (!_itemList.TryAdd(item))
        {
            OnError?.Invoke(item, $"\"{item?.Title}\" could not be added to the collection!");
        }
        //else { _itemList.CompleteAdding(); }
    }

    /// <summary>
    /// Adds a list of <see cref="ActionItem"/>s to the <see cref="BlockingCollection{T}{T}"/>.
    /// </summary>
    /// <param name="items"><see cref="IList{QueueItem}"/></param>
    public void ScheduleItems(IList<ActionItem> items)
    {
        // Check for disposed object.
        if (_shutdown)
            throw new Exception($"Thread has been shutdown, you must create a new {nameof(ScheduleManager)}.");

        if (items == null || items.Count == 0)
            return;

        foreach (ActionItem item in items)
        {
            if (!_itemList.TryAdd(item))
            {
                OnError?.Invoke(item, $"\"{item?.Title}\" could not be added to the collection!");
            }
        }
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
                Debug.WriteLine($"⇒ Joining {_agent.Name} thread… ");

            _agent.Join();
        }

    }

    /// <summary>
    /// Change the agent thread's suspended/running state.
    /// This does not suspend any currently executing <see cref="ActionItem"/>.
    /// </summary>
    public void Toggle()
    {
        // Add basic checks for first-time users not familiar with the order of operations.
        if (_agent == null)
            return;

        _suspended = !_suspended;

        if (_debug)
            Debug.WriteLine($"⇒ {_agent.Name} has been {(_suspended ? "paused" : "unpaused")}.");

        lock (this)
        {
            // If thread resumed, notify state change.
            if (!_suspended)
                Monitor.Pulse(this);
        }
    }

    /// <summary>
    /// Returns the number of <see cref="ActionItem"/>s in the <see cref="List{ActionItem}"/>.
    /// </summary>
    public int GetCount()
    {
        return _itemList.Count;
    }

    /// <summary>
    /// Returns the <see cref="ActionItem"/>s with the nearest <see cref="ActionItem.RunTime"/>.
    /// </summary>
    public DateTime? GetNextToRun()
    {
        if (_itemList.Count > 0)
        {
            var rez = _itemList
                .Select(i => i)
                .Where(i => i != null && !i.Activated)
                .OrderBy(i => i.RunTime)
                .FirstOrDefault();

            if (rez != null)
                return rez.RunTime;
        }
        return null;
    }

    /// <summary>
    /// Returns the <see cref="ActionItem"/>s with the farthest <see cref="ActionItem.RunTime"/>.
    /// </summary>
    public DateTime? GetLastToRun()
    {
        if (_itemList.Count > 0)
        {
            var rez = _itemList
                .Select(i => i)
                .Where(i => i != null && !i.Activated)
                .OrderByDescending(i => i.RunTime)
                .FirstOrDefault();

            if (rez != null)
                return rez.RunTime;
        }
        return null;
    }

    /// <summary>
    /// Removes all <see cref="ActionItem"/>s from the <see cref="BlockingCollection{ActionItem}"/>.
    /// </summary>
    public void ClearSchedule()
    {
        while (_itemList.Count > 0)
        {
            if (!_itemList.TryTake(out ActionItem? item))
                OnWarning?.Invoke(item, $"Failed to remove the {(item != null ? item.Title : nameof(ActionItem))}.");
        }
    }

    /// <summary>
    /// Returns the amount of <see cref="ActionItem"/>s which are activated.
    /// </summary>
    /// <returns>-1 if empty or result is null, otherwise the activated amount</returns>
    public int GetActivatedCount()
    {
        if (_itemList.Count > 0)
        {
            var rez = _itemList
                .Select(i => i)
                .Where(i => i != null && i.Activated);

            if (rez != null)
                return rez.Count();
        }
        return 0;
    }

    /// <summary>
    /// Returns the amount of <see cref="ActionItem"/>s which are not activated.
    /// </summary>
    /// <returns>0 if empty or result is null, otherwise the inactivated amount</returns>
    public int GetInactivatedCount()
    {
        if (_itemList.Count > 0)
        {
            var rez = _itemList
                .Select(i => i)
                .Where(i => i != null && !i.Activated);

            if (rez != null)
                return rez.Count();
        }
        return 0;
    }

    /// <summary>
    /// Returns the <see cref="ActionItem"/>s which have yet to be activated.
    /// </summary>
    public IEnumerable<ActionItem> GetWaiting()
    {
        if (_itemList.Count > 0)
        {
            var rez = _itemList
                .Select(i => i)
                .Where(i => i != null && !i.Activated)
                .OrderBy(i => i.RunTime);

            if (rez != null)
                return rez;
        }
        return Enumerable.Empty<ActionItem>();
    }

    /// <summary>
    /// Have any <see cref="ActionItem"/>s been activated yet?
    /// This does not determine if an <see cref="ActionItem"/> is currently running.
    /// </summary>
    /// <returns>true if any <see cref="ActionItem"/>s have been activated, false otherwise</returns>
    public bool IsActived()
    {
        if (_itemList.Count > 0)
        {
            var rez = _itemList.Select(i => i).Where(i => i != null && i.Activated);

            if (rez != null)
                return rez.Any();
        }
        return false;
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
    public bool IsThreadSuspended() => !_suspended;

    /// <summary>
    /// Are any tasks currently running?
    /// </summary>
    /// <returns>true if a task is running, false otherwise</returns>
    public bool IsBusy() => _busy;

    /// <summary>
    /// Adjusts the update frequency of the monitor thread loop.
    /// If two <see cref="ActionItem"/>s are set to run within the default
    /// 100ms of each other, then they might invoke together at runtime.
    /// </summary>
    public void ChangeResolution(int milliseconds)
    {
        if (_resolution > 0)
            _resolution = milliseconds;
    }

    /// <summary>
    /// Triggers a shutdown for the thread.
    /// </summary>
    public void Dispose()
    {
        Shutdown(false);
    }

    /// <summary>
    /// Finalizer for safety (if the Dispose method isn't explicitly called)
    /// </summary>
    ~ScheduleManager()
    {
        Dispose();
    }

    /// <summary>
    /// Informs the <see cref="BlockingCollection{T}"/> that we are done with additions.
    /// WARNING: Do not use this method if you plan to re-use the Scheduler in your implementation!
    /// </summary>
    //public void SignalReady()
    //{
    //    if (!_itemList.IsAddingCompleted) 
    //        _itemList.CompleteAdding();
    //    else
    //        OnWarning?.Invoke(null, $"IsAddingCompleted flag already true.");
    //}
    #endregion
}

/// <summary>
/// Our support class for the <see cref="Scheduler"/>.
/// </summary>
public class ActionItem
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public bool Activated { get; set; } = false;
    public Action? ToRun { get; set; }
    public DateTime RunTime { get; set; }
    public CancellationToken Token { get; set; }

    public ActionItem(int id, string? title, Action? action, DateTime runTime, CancellationToken token = default(CancellationToken))
    {
        Id = id;
        Title = title;
        ToRun = action;
        RunTime = runTime;
        Token = token;
    }
}
