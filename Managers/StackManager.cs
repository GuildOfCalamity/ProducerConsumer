using System;
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
            var timeout = Random.Shared.Next(1, 201);
            var siCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeout));
            StackItem newItem = new(i + 1, Utils.GetRandomName(), siCts.Token);

            // Add it to the stack.
            _semaphore.Wait();
            _dataStack.Push(newItem);
            _semaphore.Release();

            Console.WriteLine($"Produced item: {newItem.Id}");
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
                Console.WriteLine($"Consumed item: {item.Id}");

                _semaphore.Release();

                if (!item.Token.IsCancellationRequested)
                    Thread.Sleep(200); // Simulating some processing time
                else
                    Console.WriteLine($"Consumed item {item.Id} was canceled!");
            }
            else
            {
                _semaphore.Release();
                Thread.Sleep(100); // If the stack is empty, wait before checking again
            }
        }
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
    public string? Title { get; set; }
    public CancellationToken Token { get; set; }

    public StackItem(int id, string? title, CancellationToken token = default(CancellationToken))
    {
        Id = id;
        Title = title;
        Token = token;
    }
}

