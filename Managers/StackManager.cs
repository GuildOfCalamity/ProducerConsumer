using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ProducerConsumer;

public class StackManager : IDisposable
{
    bool _disposed = false;
    ConcurrentStack<StackItem> _dataStack = new ConcurrentStack<StackItem>();
    SemaphoreSlim _semaphore = new SemaphoreSlim(1);
    CancellationTokenSource cts = new CancellationTokenSource();

    public void Start(int producerCount, int consumerCount, int itemCount)
    {
        Task[] producerTasks = new Task[producerCount];
        for (int i = 0; i < producerCount; i++)
        {
            producerTasks[i] = Task.Run(() => ProduceItems(itemCount));
        }

        Task[] consumerTasks = new Task[consumerCount];
        for (int i = 0; i < consumerCount; i++)
        {
            consumerTasks[i] = Task.Run(() => ConsumeItems());
        }

        Task.WaitAll(producerTasks);
        cts.Cancel();
        Task.WaitAll(consumerTasks);
    }

    void ProduceItems(int itemCount)
    {
        for (int i = 0; i < itemCount; i++)
        {
            // Simulating item creation
            var siCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(Random.Shared.Next(100, 801)));
            StackItem newItem = new(i + 1, Utils.GetRandomName(), Random.Shared.Next(50, 501), siCts.Token);

            // Add it to the stack.
            _semaphore.Wait();
            _dataStack.Push(newItem);
            _semaphore.Release();

            Log.Instance.WriteConsole($"Produced item: {newItem.Id}", LogLevel.Info);
            Thread.Sleep(100); // Simulating some processing time
        }
    }

    void ConsumeItems()
    {
        while (!cts.Token.IsCancellationRequested)
        {
            _semaphore.Wait();

            if (_dataStack.TryPop(out StackItem? item))
            {
                if (!item.Token.IsCancellationRequested)
                {
                    Thread.Sleep(item.Delay); // Simulating some processing time
                    Log.Instance.WriteConsole($"Consumed item: {item.Id} with delay of {item.Delay} ms", LogLevel.Info);
                }
                else
                {
                    Log.Instance.WriteConsole($"Consumed item {item.Id} was canceled!", LogLevel.Warning);
                }
                // Inform any waiters.
                _semaphore.Release();
            }
            else
            {
                //Log.Instance.WriteToConsole($"Stack is empty.", LogLevel.Debug);
                _semaphore.Release();
                Thread.Sleep(100); // If the stack is empty, wait before checking again
            }
        }
        Log.Instance.WriteConsole($"Exiting ConsumeItems loop.", LogLevel.Success);
    }

    /// <summary>
    /// Synchronous <see cref="ConcurrentStack{T}"/> reader.
    /// </summary>
    /// <param name="token"><see cref="CancellationToken"/></param>
    /// <returns><see cref="StackItem"/></returns>
    public StackItem? Read(CancellationToken token = default)
    {
        if (_disposed)
            return null;

        _semaphore.Wait(token); // wait
        if (_dataStack.TryPop(out StackItem? item))
            return item;
        else
            return null;
    }

    /// <summary>
    /// Asynchronous <see cref="ConcurrentStack{T}"/> reader.
    /// </summary>
    /// <param name="token"><see cref="CancellationToken"/></param>
    /// <returns><see cref="StackItem"/></returns>
    public async ValueTask<StackItem?> ReadAsync(CancellationToken token = default)
    {
        if (_disposed)
            return null;

        await _semaphore.WaitAsync(token).ConfigureAwait(false); // wait
        if (_dataStack.TryPop(out StackItem? item))
            return item;
        else
            return null;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                cts.Cancel();
                cts.Dispose();
                _semaphore.Dispose();
            }
            _disposed = true;
        }
    }

    /// <summary>
    /// Finalizer for safety (if the Dispose method isn't explicitly called)
    /// </summary>
    ~StackManager()
    {
        Dispose(false);
    }
}

/// <summary>
/// Our support class for the <see cref="StackManager"/>.
/// </summary>
public class StackItem
{
    public int Id { get; set; }
    public int Delay { get; set; }
    public string? Title { get; set; }
    public CancellationToken Token { get; set; }

    public StackItem(int id, string? title, int delay, CancellationToken token = default)
    {
        Id = id;
        Delay = delay;
        Title = title;
        Token = token;
    }
}

