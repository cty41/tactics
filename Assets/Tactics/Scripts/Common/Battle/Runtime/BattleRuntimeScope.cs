using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tactics.Runtime.Utilities;

namespace Tactics.Common.Battle.Runtime
{
    /// <summary>
    /// IBattleRuntimeScope 的默认实现。
    /// 管理一场战斗或一次测试中的所有异步生命周期。
    /// </summary>
    public sealed class BattleRuntimeScope : IBattleRuntimeScope
    {
        private readonly CancellationTokenSource _cts;
        private readonly CancellationToken _token;
        private readonly List<Task> _trackedTasks = new();
        private readonly Dictionary<Task, AggregateException> _recordedFailures = new();
        private readonly object _gate = new();
        private Timer _timeoutTimer;
        private bool _isCancelling;
        private bool _disposed;
        private bool _cancellationDispatchInProgress;
        private bool _cancellationDispatchCompleted;
        private bool _disposeRequested;
        private bool _ctsDisposed;

        public BattleRuntimeScope()
        {
            _cts = new CancellationTokenSource();
            _token = _cts.Token;
        }

        public BattleRuntimeScope(TimeSpan timeout)
            : this()
        {
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
                {
                    return _isCancelling;
                }
            }
        }

        public void Track(Task task)
        {
            if (task == null)
                return;

            lock (_gate)
            {
                if (_disposed || _isCancelling)
                    return;

                if (task.IsCompleted)
                {
                    RecordFaultIfNeeded(task);
                    return;
                }

                _trackedTasks.Add(task);
                _ = RemoveWhenCompleteAsync(task);
            }
        }

        private async Task RemoveWhenCompleteAsync(Task task)
        {
            try
            {
                await task;
            }
            catch
            {
                // 任务结果由调用方或 WhenIdleAsync 观察
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
            // Task final status is stable once IsCompleted is true. Reading completion first
            // closes the IsFaulted=false -> IsCompleted=true race that could lose a fault.
            if (task.IsCompleted && task.IsFaulted)
                _recordedFailures[task] = task.Exception;
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
                    await Task.WhenAll(snapshot);
                }
                catch (OperationCanceledException)
                {
                    // 取消属于正常的生命周期结束
                }
                catch
                {
                    // Faults are collected below so timing cannot change observability.
                }
            }

            lock (_gate)
            {
                foreach (var pair in _recordedFailures)
                    failures[pair.Key] = pair.Value;
            }

            foreach (Task task in snapshot)
            {
                if (task.IsFaulted)
                    failures[task] = task.Exception;
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
            bool disposeCts = false;

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
                else if (_cancellationDispatchCompleted && !_ctsDisposed)
                {
                    _ctsDisposed = true;
                    disposeCts = true;
                }
            }

            if (dispatchCancellation)
                DispatchCancellation();
            else if (disposeCts)
                DisposeOwnedResources(null, true);
        }

        private void DispatchCancellation()
        {
            Timer timerToDispose = null;
            bool disposeCts = false;

            try
            {
                _cts.Cancel();
            }
            finally
            {
                lock (_gate)
                {
                    _cancellationDispatchInProgress = false;
                    _cancellationDispatchCompleted = true;
                    timerToDispose = _timeoutTimer;
                    _timeoutTimer = null;

                    if (_disposeRequested && !_ctsDisposed)
                    {
                        _ctsDisposed = true;
                        disposeCts = true;
                    }
                }

                DisposeOwnedResources(timerToDispose, disposeCts);
            }
        }

        private void CancelFromTimeout()
        {
            try
            {
                Cancel();
            }
            catch (Exception ex)
            {
                TLog.Error($"[BattleRuntimeScope] Runtime scope timeout cancellation callback failed: {ex}");
            }
        }

        private void DisposeOwnedResources(Timer timer, bool disposeCts)
        {
            try
            {
                timer?.Dispose();
            }
            finally
            {
                if (disposeCts)
                    _cts.Dispose();
            }
        }
    }
}
