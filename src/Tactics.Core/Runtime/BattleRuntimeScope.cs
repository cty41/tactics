namespace Tactics.Core.Runtime;

/// <summary>
/// Engine-independent ownership scope for one battle's asynchronous work.
/// Its cancellation, fault observation and re-entrant disposal semantics mirror
/// the frozen Unity Oracle without depending on Unity logging.
/// </summary>
public sealed class BattleRuntimeScope : IDisposable
{
    private readonly CancellationTokenSource _cancellation;
    private readonly CancellationToken _token;
    private readonly List<Task> _trackedTasks = new();
    private readonly Dictionary<Task, AggregateException> _recordedFailures = new();
    private readonly object _gate = new();
    private readonly Action<Exception>? _timeoutErrorSink;
    private Timer? _timeoutTimer;
    private bool _isCancelling;
    private bool _disposed;
    private bool _cancellationDispatchInProgress;
    private bool _cancellationDispatchCompleted;
    private bool _disposeRequested;
    private bool _cancellationDisposed;

    public BattleRuntimeScope()
    {
        _cancellation = new CancellationTokenSource();
        _token = _cancellation.Token;
    }

    public BattleRuntimeScope(TimeSpan timeout, Action<Exception>? timeoutErrorSink = null)
        : this()
    {
        _timeoutErrorSink = timeoutErrorSink;
        _timeoutTimer = new Timer(
            _ => CancelFromTimeout(),
            null,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);
        try
        {
            _timeoutTimer.Change(timeout, Timeout.InfiniteTimeSpan);
        }
        catch
        {
            _timeoutTimer.Dispose();
            _timeoutTimer = null;
            throw;
        }
    }

    public CancellationToken Token => _token;

    public bool IsCancelling
    {
        get
        {
            lock (_gate)
                return _isCancelling;
        }
    }

    public void Track(Task? task) => _ = TryTrack(task);

    public bool TryTrack(Task? task)
    {
        if (task is null)
            return false;

        lock (_gate)
        {
            if (_disposed || _isCancelling)
                return false;

            if (task.IsCompleted)
            {
                RecordFaultIfNeeded(task);
                return true;
            }

            _trackedTasks.Add(task);
            _ = RemoveWhenCompleteAsync(task);
            return true;
        }
    }

    public async Task WhenIdleAsync()
    {
        Task[] snapshot;
        Dictionary<Task, AggregateException> failures;
        lock (_gate)
        {
            snapshot = _trackedTasks.ToArray();
            failures = new Dictionary<Task, AggregateException>(_recordedFailures);
        }

        if (snapshot.Length > 0)
        {
            try
            {
                await Task.WhenAll(snapshot).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Cancellation is the normal lifecycle end.
            }
            catch
            {
                // Faults are collected below so timing cannot change visibility.
            }
        }

        lock (_gate)
        {
            foreach ((Task task, AggregateException failure) in _recordedFailures)
                failures[task] = failure;
        }

        foreach (Task task in snapshot)
        {
            if (task.IsFaulted)
                failures[task] = task.Exception!;
        }

        if (failures.Count > 0)
        {
            throw new AggregateException(
                failures.Values.SelectMany(exception => exception.InnerExceptions));
        }
    }

    public void Cancel()
    {
        lock (_gate)
        {
            if (_disposed || _isCancelling)
                return;

            _isCancelling = true;
            _cancellationDispatchInProgress = true;
        }

        DispatchCancellation();
    }

    public void Dispose()
    {
        bool dispatchCancellation = false;
        bool disposeCancellation = false;

        lock (_gate)
        {
            if (_disposed)
                return;

            _disposed = true;
            _disposeRequested = true;

            if (!_cancellationDispatchInProgress && !_cancellationDispatchCompleted)
            {
                _isCancelling = true;
                _cancellationDispatchInProgress = true;
                dispatchCancellation = true;
            }
            else if (_cancellationDispatchCompleted && !_cancellationDisposed)
            {
                _cancellationDisposed = true;
                disposeCancellation = true;
            }
        }

        if (dispatchCancellation)
            DispatchCancellation();
        else if (disposeCancellation)
            DisposeOwnedResources(null, true);
    }

    private async Task RemoveWhenCompleteAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch
        {
            // The caller or WhenIdleAsync owns result observation.
        }
        finally
        {
            lock (_gate)
            {
                _trackedTasks.Remove(task);
                RecordFaultIfNeeded(task);
            }
        }
    }

    private void RecordFaultIfNeeded(Task task)
    {
        if (task.IsCompleted && task.IsFaulted)
            _recordedFailures[task] = task.Exception!;
    }

    private void DispatchCancellation()
    {
        Timer? timerToDispose;
        bool disposeCancellation = false;

        try
        {
            _cancellation.Cancel();
        }
        finally
        {
            lock (_gate)
            {
                _cancellationDispatchInProgress = false;
                _cancellationDispatchCompleted = true;
                timerToDispose = _timeoutTimer;
                _timeoutTimer = null;

                if (_disposeRequested && !_cancellationDisposed)
                {
                    _cancellationDisposed = true;
                    disposeCancellation = true;
                }
            }

            DisposeOwnedResources(timerToDispose, disposeCancellation);
        }
    }

    private void CancelFromTimeout()
    {
        try
        {
            Cancel();
        }
        catch (Exception exception)
        {
            _timeoutErrorSink?.Invoke(exception);
        }
    }

    private void DisposeOwnedResources(Timer? timer, bool disposeCancellation)
    {
        try
        {
            timer?.Dispose();
        }
        finally
        {
            if (disposeCancellation)
                _cancellation.Dispose();
        }
    }
}
