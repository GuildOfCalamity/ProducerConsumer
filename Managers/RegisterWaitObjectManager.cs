using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ProducerConsumer;

/// <summary>
/// These are time-based <see cref="ManualResetEvent"/> callbacks.
/// </summary>
public class RegisterWaitObjectManager : IDisposable
{
    // For our demo we only need one RegisteredWaitHandle.
    // If you wanted to create a group where each one could
    // have its own TimeSpan maxWaitTime, then you'll need
    // to convert this into a List<RegisteredWaitHandle>.
    // It's simpler to pass CancellationTokens into the
    // object model and then test the token at run-time.
    System.Threading.RegisteredWaitHandle? _rwh = default;

    int _total = 20;
    DateTime _instantiated = DateTime.MinValue;
    //public System.Threading.WaitOrTimerCallback callback;
    List<TriggerObject> _triggers = new();
    List<AutoResetEvent> _starters = new();

    #region [Contructors]
    public RegisterWaitObjectManager() { _instantiated = DateTime.Now; }
    public RegisterWaitObjectManager(int total) : this() { _total = total; }
    #endregion

    public event Action<string> OnTimedOut = (msg) => { };

    /// <summary>
    /// Main work method.
    /// </summary>
    public void TestThreadPoolWaitRegister(TimeSpan maxWaitTime, List<Action> actions, bool executeOnlyOnce = true)
    {
        if (actions.Count == 0)
        {
            Log.Instance.WriteConsole($"Nothing to do.", LogLevel.Error);
            return;
        }

        if (_triggers.Count > 0 && AreAnyWaitObjectsNotTriggered())
        {
            Log.Instance.WriteConsole($"There are still untriggered objects in the list.", LogLevel.Warning);
            _triggers.Clear();
        }
        else if (_triggers.Count > 0)
        {
            Log.Instance.WriteConsole($"Clearing old objects.", LogLevel.Info);
            _triggers.Clear();
        }

        // ** Setup RegisterWaitForSingleObjects **
        for (int i = 0; i < actions.Count; i++)
        {
            int idx = i;
            var trigObj = new TriggerObject(i, $"RegisterWait #{idx}", new ManualResetEvent(false));
            _triggers.Add(trigObj);
            Log.Instance.WriteConsole($"Adding worker \"{trigObj.Title}\"...", LogLevel.Info);
            if (maxWaitTime == TimeSpan.Zero || maxWaitTime == TimeSpan.MinValue || maxWaitTime == TimeSpan.MaxValue)
            {   // The second parameter for the RegisterWaitForSingleObject is a callback that has the following definition: "delegate void WaitOrTimerCallback(object? state, bool timedOut);"
                _rwh = ThreadPool.RegisterWaitForSingleObject(trigObj.Trigger, SomeRunnerMethod, new StateObject(idx, actions[i]), -1, executeOnlyOnce);
            }
            else
            {   // The second parameter for the RegisterWaitForSingleObject is a callback that has the following definition: "delegate void WaitOrTimerCallback(object? state, bool timedOut);"
                _rwh = ThreadPool.RegisterWaitForSingleObject(trigObj.Trigger, SomeRunnerMethod, new StateObject(idx, actions[i]), maxWaitTime, executeOnlyOnce);
            }
        }
    }

    /// <summary>
    /// Testing method.
    /// </summary>
    public void TestThreadPoolWaitRegister(TimeSpan maxWaitTime)
    {
        for (int i = 0; i < _total; i++)
        {
            int idx = i;
            var so = new StateObject(i, () => 
            {
                Debug.WriteLine($">> I'm action #{idx}, watch me go! <<");
                Thread.Sleep(Random.Shared.Next(10, 5001));
            });
            _triggers.Add(new TriggerObject(i, $"RegisterWait #{idx}", new ManualResetEvent(false)));
        
            Log.Instance.WriteConsole($"Adding worker \"{_triggers[i].Title}\"...", LogLevel.Info);
        
            if (maxWaitTime == TimeSpan.Zero || maxWaitTime == TimeSpan.MinValue || maxWaitTime == TimeSpan.MaxValue)
            {   // The second parameter for the RegisterWaitForSingleObject is a callback that has the following definition: "delegate void WaitOrTimerCallback(object? state, bool timedOut);"
                _rwh = ThreadPool.RegisterWaitForSingleObject(_triggers[i].Trigger, SomeRunnerMethod, so, -1, true);
            }
            else
            {   // The second parameter for the RegisterWaitForSingleObject is a callback that has the following definition: "delegate void WaitOrTimerCallback(object? state, bool timedOut);"
                _rwh = ThreadPool.RegisterWaitForSingleObject(_triggers[i].Trigger, SomeRunnerMethod, so, maxWaitTime, true);
            }
        }
    }

    /// <summary>
    /// Offers the ability to reuse the method callback.
    /// This must be used with a <see cref="AutoResetEvent"/>.
    /// </summary>
    public void TestThreadPoolWaitRegisterWithARE()
    {
        var starter = new AutoResetEvent(false);
        // The second parameter for the RegisterWaitForSingleObject is a callback that has the following definition: "delegate void WaitOrTimerCallback(object? state, bool timedOut);"
        var rwso = ThreadPool.RegisterWaitForSingleObject(starter, SomeMethod, "SomeMethod", -1, false);

        Thread.Sleep(2000);
        Log.Instance.WriteConsole($"Signaling worker (1st time)...", LogLevel.Info);
        starter.Set();

        Thread.Sleep(2000);
        Log.Instance.WriteConsole($"Signaling worker (2nd time)...", LogLevel.Info);
        starter.Set();

        Thread.Sleep(2000);
        Log.Instance.WriteConsole($"Signaling worker (3rd time)...", LogLevel.Info);
        starter.Set();

        #region [Registered Method]
        void SomeMethod(object? data, bool timedOut)
        {
            if (!timedOut)
            {
                Log.Instance.WriteConsole($"Started: {data}", LogLevel.Info);
                Thread.Sleep(Random.Shared.Next(10, 2000));
                Log.Instance.WriteConsole($"Ended: {data}", LogLevel.Success);
            }
            else
            {
                Log.Instance.WriteConsole($"TimedOut: {data}", LogLevel.Warning);
            }
        }
        #endregion
        
        // Wait a bit.
        Thread.Sleep(2000);

        // Clean up when we’re done.
        Log.Instance.WriteConsole($"Unregistering {starter.GetType()}.", LogLevel.Debug);
        rwso.Unregister(starter);
        // Civility for the GC.
        starter.Close();
    }

    #region [Registered Callback Method]
    /// <summary>
    /// These will show up in the debugger as category "Worker Thread", named ".NET TP Worker".
    /// </summary>
    void SomeRunnerMethod(object? data, bool timedOut)
    {
        var so = data as StateObject;
        if (!timedOut && so != null)
        {
            if (so.Triggered == null)
            {
                Log.Instance.WriteConsole($"Started action #{so.Id} on tid {Thread.CurrentThread.ManagedThreadId}", LogLevel.Info);
                try
                {
                    so.ToRun?.Invoke();
                }
                catch (Exception ex)
                {
                    Log.Instance.WriteConsole($"Error during action #{so.Id}: {ex.Message}", LogLevel.Error);
                }
                finally
                {
                    so.Triggered = DateTime.Now;
                    Log.Instance.WriteConsole($"Ended action #{so.Id}", LogLevel.Success);
                }
            }
            else
            {
                Log.Instance.WriteConsole($"Action #{so.Id} was already run: {so.Triggered}", LogLevel.Warning);
            }
        }
        else if(timedOut && so != null)
        {
            Log.Instance.WriteConsole($"TimedOut action #{so.Id}", LogLevel.Warning);
            so.Triggered = DateTime.Now;
            OnTimedOut?.Invoke($"Will not invoke #{so.Id} due to timeout.");
        }
        else
        {
            Log.Instance.WriteConsole($"Problem with \"{nameof(StateObject)}\"", LogLevel.Error);
        }
    }
    #endregion

    #region [Initial proof of concept]
    /// <summary>
    /// First attempt at using the <see cref="ThreadPool.RegisterWaitForSingleObject(WaitHandle, WaitOrTimerCallback, object?, TimeSpan, bool)"/>.
    /// </summary>
    void TestThreadPoolWaitRegisterWithMRE()
    {
        var starter = new ManualResetEvent(false);
        // The second parameter for the RegisterWaitForSingleObject is a callback that has the following definition: "delegate void WaitOrTimerCallback(object? state, bool timedOut);"
        var rwso1 = ThreadPool.RegisterWaitForSingleObject(starter, SomeMethod1, "SomeMethod1", -1, true);
        var rwso2 = ThreadPool.RegisterWaitForSingleObject(starter, SomeMethod2, "SomeMethod2", TimeSpan.FromSeconds(0.5), true);
        // Using a delegate/anonymous method.
        var rwso3 = ThreadPool.RegisterWaitForSingleObject(starter, (data, timedOut) =>
        {
            if (!timedOut)
            {
                Log.Instance.WriteConsole("Started: " + data, LogLevel.Info);
                Thread.Sleep(Random.Shared.Next(10, 5001));
                Log.Instance.WriteConsole("Ended: " + data, LogLevel.Success);
            }
            else
            {
                Log.Instance.WriteConsole("TimedOut - " + data, LogLevel.Warning);
            }
        },
        "SomeAnonymous3",
        TimeSpan.FromMinutes(5),
        true);

        Log.Instance.WriteConsole($"Signaling worker...", LogLevel.Info);
        Thread.Sleep(1000);
        starter.Set();

        #region [Registered Methods]
        void SomeMethod1(object? data, bool timedOut)
        {
            if (!timedOut)
            {
                Log.Instance.WriteConsole("Started: " + data, LogLevel.Info);
                Thread.Sleep(Random.Shared.Next(10, 5001));
                Log.Instance.WriteConsole("Ended: " + data, LogLevel.Success);
            }
            else
            {
                Log.Instance.WriteConsole("TimedOut - " + data, LogLevel.Warning);
            }
        }

        void SomeMethod2(object? data, bool timedOut)
        {
            if (!timedOut)
            {
                Log.Instance.WriteConsole("Started: " + data, LogLevel.Info);
                Thread.Sleep(Random.Shared.Next(10, 5001));
                Log.Instance.WriteConsole("Ended: " + data, LogLevel.Success);
            }
            else
            {
                Log.Instance.WriteConsole("TimedOut - " + data, LogLevel.Warning);
            }
        }
        #endregion

        // Clean up when we’re done.
        Log.Instance.WriteConsole($"Unregistering {starter.GetType()}s.", LogLevel.Debug);
        rwso1.Unregister(starter);
        rwso2.Unregister(starter);
        rwso3.Unregister(starter);
        // Civility for the GC.
        starter.Close();
    }
    #endregion

    #region [Helpers]
    public void TriggerWaitObject(int id)
    {
        if (id <= _triggers.Count)
        {
            // Check if the ManualResetEvent is in a signaled state without waiting.
            bool isSet = _triggers[id].Trigger.WaitOne(0);
            if (isSet)
            {
                Log.Instance.WriteConsole($"Worker #{id} is already set.", LogLevel.Warning);
            }
            else
            {
                Log.Instance.WriteConsole($"Worker #{id} is not set yet, signaling.", LogLevel.Event);
                _triggers[id].Trigger.Set();
            }
        }
    }

    /// <summary>
    /// Check if any <see cref="ManualResetEvent"/> is not signaled using LINQ
    /// </summary>
    public bool AreAnyWaitObjectsNotTriggered()
    {
        if (_triggers.Count == 0)
            return false;

        bool anyNotSignaled = _triggers.Any(e => !e.Trigger.WaitOne(0));
        return anyNotSignaled;

        #region [Non-LINQ Method]
        //bool result = false;
        //foreach (var tobj in _triggers)
        //{
        //    bool isSet = tobj.Trigger.WaitOne(0);
        //    if (!isSet)
        //    {
        //        Debug.WriteLine($"Worker #{tobj.Id} is not set.");
        //        result = true;
        //        break;
        //    }
        //}
        //return result;
        #endregion
    }

    /// <summary>
    /// Selects the next <see cref="TriggerObject"/> that has not been signaled yet.
    /// </summary>
    public TriggerObject? GetNextUntriggeredWaitObject()
    {
        if (_triggers.Count == 0)
            return null;

        return _triggers.Select(e => e).Where(e => !e.Trigger.WaitOne(0)).FirstOrDefault();
    }

    /// <summary>
    /// Sets all <see cref="ManualResetEvent"/> that have not been signaled.
    /// </summary>
    public bool TriggeredAllWaitObjects()
    {
        if (_triggers.Count == 0)
            return false;

        var list = _triggers.Select(e => e).Where(e => !e.Trigger.WaitOne(0));
        foreach (var tObj in list)
        {
            tObj.Trigger.Set();
        }

        return true;
    }

    /// <summary>
    /// Resets all <see cref="ManualResetEvent"/>s that have not been signaled.
    /// </summary>
    public bool ResetAllWaitObjects()
    {
        if (_triggers.Count == 0)
            return false;

        var list = _triggers.Select(e => e).Where(e => e.Trigger.WaitOne(0));
        foreach (var tObj in list)
        {
            tObj.Trigger.Reset();
        }

        return true;
    }

    public int GetWaitObjectCount()
    {
        return _triggers.Count;
    }
    #endregion

    /// <summary>
    /// Triggers a shutdown for the object manager.
    /// NOTE: This should only be called once all the WaitOrTimerCallbacks have been fired.
    /// </summary>
    public void Dispose()
    {
        if (AreAnyWaitObjectsNotTriggered())
            Log.Instance.WriteConsole($"There are untriggered objects! The cause may be related to timeouts.", LogLevel.Warning);

        for (int i = 0; i < _triggers.Count; i++)
        {
            Log.Instance.WriteConsole($"Unregistering {_triggers[i].Trigger.GetType()} #{i}.", LogLevel.Debug);
            if (_triggers[i].Trigger != null && _rwh != null)
            {
                _rwh.Unregister(_triggers[i].Trigger);
                // Civility for the GC.
                _triggers[i].Trigger.Close();
            }
        }
    }

    /// <summary>
    /// Finalizer for safety (if the Dispose method isn't explicitly called).
    /// </summary>
    ~RegisterWaitObjectManager() => Dispose();
}

/// <summary>
/// The basic object model for tracking our treads using the <see cref="ThreadPool.RegisterWaitForSingleObject"/>.
/// </summary>
public class TriggerObject
{
    public int Id { get; set; }
    public string Title { get; set; }
    public ManualResetEvent Trigger { get; set; }

    public TriggerObject(int id, string title, ManualResetEvent trigger)
    {
        Id = id;
        Title = title;
        Trigger = trigger;
    }
}

/// <summary>
/// For state passing during the <see cref="ThreadPool.RegisterWaitForSingleObject"/>.
/// </summary>
public class StateObject
{
    public int Id { get; set; }
    public Action? ToRun { get; set; }
    public DateTime? Triggered { get; set; } = null;
    public StateObject(int id, Action? toRun)
    {
        Id = id;
        ToRun = toRun;
    }
}

// <summary>
/// For state passing during the <see cref="ThreadPool.RegisterWaitForSingleObject"/>.
/// </summary>
public class ActionObject
{
    public int Id { get; set; }
    public Action? ToRun { get; set; }
    public DateTime? Triggered { get; set; } = null;
    public AutoResetEvent? Starter { get; set; } = null;
    public CancellationToken Token { get; set; } = default(CancellationToken);
    public ActionObject(int id, AutoResetEvent? starter, Action? toRun, CancellationToken token)
    {
        Id = id;
        ToRun = toRun;
        Starter = starter;
        Token = token;
    }
}