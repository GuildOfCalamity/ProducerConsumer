using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ProducerConsumer;

public class TaskRunner
{
    #region [Actions]
    //public event Action<ActionEventArgs>? OnActionInvoked = (aea) => { };
    public event EventHandler<ActionEventArgs>? ActionCompleted;
    public event EventHandler<ActionEventArgs>? ActionCanceled;
    public event EventHandler<ActionEventArgs>? ActionFailed;
    protected virtual void OnActionCompleted(Action action) => ActionCompleted?.Invoke(this, new ActionEventArgs(action));
    protected virtual void OnActionCanceled(Action action) => ActionCanceled?.Invoke(this, new ActionEventArgs(action));
    protected virtual void OnActionFailed(Action action, Exception exception) => ActionFailed?.Invoke(this, new ActionEventArgs(action, exception));

    public void RunActionsSequentially(List<Action> actions, CancellationToken token, bool stopOnFault = false)
    {
        foreach (var action in actions)
        {
            try
            {
                token.ThrowIfCancellationRequested(); // Check for cancellation
                action();
                OnActionCompleted(action);
            }
            catch (OperationCanceledException)
            {
                // Handle cancellation and stop execution
                Console.WriteLine("Action execution was canceled!");
                OnActionCanceled(action);
                if (stopOnFault)
                    break; // Halt execution on cancellation
            }
            catch (Exception ex)
            {
                OnActionFailed(action, ex);
                if (stopOnFault)
                    break; // Halt execution on exception
            }
        }
    }

    /// <summary>
    /// This version runs the actions inside of a task object.
    /// If you're working with Action delegates instead of Func<CancellationToken, Task>, you won't 
    /// have the built-in asynchronous behavior or the ability to directly await tasks within the loop. 
    /// However, you can simulate the behavior using Task.Run to run actions asynchronously within tasks.
    /// Here's an example of how you might modify the method to handle an array of Action delegates:
    /// </summary>
    public async Task RunActionsSequentially(Action[] actions, CancellationToken token, bool stopOnFault = false)
    {
        for (int i = 0; i < actions.Length; i++)
        {
            // We'll run each action inside a Task.
            Task? task = null;

            try
            {
                token.ThrowIfCancellationRequested(); // Check for cancellation
                task = Task.Run(() => actions[i]());
                await task;
                OnTaskCompleted(task);
            }
            catch (OperationCanceledException)
            {
                // Handle task cancellation
                Console.WriteLine($"Action {i + 1} was canceled.");
                OnTaskCanceled(task);
                if (stopOnFault)
                    break; // Halt execution on cancellation
            }
            catch (Exception ex)
            {
                OnTaskFailed(task, ex);
                if (stopOnFault)
                    break; // Halt execution on exception
            }
        }
    }
    #endregion

    #region [Tasks]
    public event EventHandler<TaskCompletedEventArgs>? TaskCompleted;
    public event EventHandler<TaskCanceledEventArgs>? TaskCanceled;
    public event EventHandler<TaskFailedEventArgs>? TaskFailed;
    protected virtual void OnTaskCompleted(Task completedTask) => TaskCompleted?.Invoke(this, new TaskCompletedEventArgs(completedTask));
    protected virtual void OnTaskCanceled(Task? canceledTask) => TaskCanceled?.Invoke(this, new TaskCanceledEventArgs(canceledTask));
    protected virtual void OnTaskFailed(Task? failedTask, Exception exception) => TaskFailed?.Invoke(this, new TaskFailedEventArgs(failedTask, exception));

    /// <summary>
    /// When working with Func<CancellationToken, Task>, the provided CancellationToken allows you to 
    /// facilitate cancellation within the tasks. You can utilize this token to check for cancellation 
    /// requests and gracefully exit the tasks if cancellation is requested.
    /// </summary>
    public async Task RunTasksSequentially(Func<CancellationToken, Task>[] taskFactories, CancellationToken token, bool stopOnFault = false)
    {
        for (int i = 0; i < taskFactories.Length; i++)
        {
            Task? task = null;
            try
            {
                token.ThrowIfCancellationRequested(); // Check for cancellation
                task = taskFactories[i](token);
                await task;
                OnTaskCompleted(task);
            }
            catch (OperationCanceledException)
            {
                // Handle task cancellation
                Console.WriteLine($"Task {i + 1} was canceled.");
                OnTaskCanceled(task);
                if (stopOnFault)
                    break; // Halt execution on cancellation
            }
            catch (Exception ex)
            {
                OnTaskFailed(task, ex);
                if (stopOnFault)
                    break; // Halt execution on exception
            }
        }
    }

    #region [Not as useful as methods above]
    public async Task RunTasksSequentially(Task[] tasks, bool stopOnFault = false)
    {
        foreach (var task in tasks)
        {
            try
            {
                await task;
                OnTaskCompleted(task);
            }
            catch (OperationCanceledException)
            {
                OnTaskCanceled(task);
                if (stopOnFault)
                    break; // Halt execution on cancellation
            }
            catch (Exception ex)
            {
                OnTaskFailed(task, ex);
                if (stopOnFault)
                    break; // Halt execution on exception
            }
        }
    }
    public async Task RunTasksSequentially(List<Task> tasks, bool stopOnFault = false)
    {
        foreach (var task in tasks)
        {
            try
            {
                await task;
                OnTaskCompleted(task);
            }
            catch (OperationCanceledException)
            {
                OnTaskCanceled(task);
                if (stopOnFault)
                    break; // Halt execution on cancellation
            }
            catch (Exception ex)
            {
                OnTaskFailed(task, ex);
                if (stopOnFault)
                    break;
            }
        }
    }
    #endregion
    
    #endregion
}

#region [Supporting EventArgs]
public class TaskCompletedEventArgs : EventArgs
{
    public Task CompletedTask { get; }

    public TaskCompletedEventArgs(Task completedTask)
    {
        CompletedTask = completedTask;
    }
}

public class TaskCanceledEventArgs : EventArgs
{
    public Task? CanceledTask { get; }

    public TaskCanceledEventArgs(Task? canceledTask)
    {
        CanceledTask = canceledTask;
    }
}

public class TaskFailedEventArgs : EventArgs
{
    public Task? FailedTask { get; }
    public Exception Exception { get; }

    public TaskFailedEventArgs(Task? failedTask, Exception exception)
    {
        FailedTask = failedTask;
        Exception = exception;
    }
}

public class ActionEventArgs : EventArgs
{
    public Action Action { get; }
    public Exception? Exception { get; }

    public ActionEventArgs(Action action)
    {
        Action = action;
    }

    public ActionEventArgs(Action action, Exception exception)
    {
        Action = action;
        Exception = exception;
    }
}
#endregion
