using System.Collections.Concurrent;

namespace Tactics.Core.Runtime;

/// <summary>
/// Engine-independent ownership scope for one battle's asynchronous work.
/// </summary>
public sealed class BattleRuntimeScope : IDisposable
{
    private readonly CancellationTokenSource _cancellation = new();
    private readonly ConcurrentDictionary<Task, byte> _trackedTasks = new();
    private readonly ConcurrentQueue<Exception> _faults = new();
    private readonly Timer? _timeoutTimer;
    private int _cancelling;
    private int _disposed;

    public BattleRuntimeScope(TimeSpan? timeout = null)
    {
        if (timeout is { } duration)
        {
            if (duration <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeout));

            _timeoutTimer = new Timer(_ => Cancel(), null, duration, Timeout.InfiniteTimeSpan);
        }
    }

    public CancellationToken Token => _cancellation.Token;

    public bool IsCancelling => Volatile.Read(ref _cancelling) != 0;

    public bool TryTrack(Task? task)
    {
        if (task is null || IsCancelling || Volatile.Read(ref _disposed) != 0)
            return false;

        if (!_trackedTasks.TryAdd(task, 0))
            return true;

        _ = ObserveTaskAsync(task);
        return true;
    }

    public void Track(Task task)
    {
        if (!TryTrack(task))
            throw new InvalidOperationException("The runtime scope is no longer accepting tasks.");
    }

    public void Cancel()
    {
        if (Interlocked.Exchange(ref _cancelling, 1) != 0)
            return;

        _cancellation.Cancel();
    }

    public async Task WhenIdleAsync()
    {
        Task[] snapshot = _trackedTasks.Keys.ToArray();
        if (snapshot.Length > 0)
        {
            try
            {
                await Task.WhenAll(snapshot).ConfigureAwait(false);
            }
            catch
            {
                // Faults are surfaced from the stable queue below.
            }
        }

        if (!_faults.IsEmpty)
            throw new AggregateException(_faults.ToArray());
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        Cancel();
        _timeoutTimer?.Dispose();
        _cancellation.Dispose();
    }

    private async Task ObserveTaskAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (task.IsCanceled)
        {
        }
        catch (Exception exception)
        {
            _faults.Enqueue(exception);
        }
        finally
        {
            _trackedTasks.TryRemove(task, out _);
        }
    }
}
