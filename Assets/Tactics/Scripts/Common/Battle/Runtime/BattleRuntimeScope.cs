using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Tactics.Common.Battle.Runtime
{
    /// <summary>
    /// IBattleRuntimeScope 的默认实现。
    /// 管理一场战斗或一次测试中的所有异步生命周期。
    /// </summary>
    public sealed class BattleRuntimeScope : IBattleRuntimeScope
    {
        private readonly CancellationTokenSource _cts;
        private readonly List<Task> _trackedTasks = new();
        private readonly object _lock = new();
        private bool _isCancelling;
        private bool _disposed;

        public BattleRuntimeScope()
        {
            _cts = new CancellationTokenSource();
        }

        public BattleRuntimeScope(TimeSpan timeout)
        {
            _cts = new CancellationTokenSource(timeout);
        }

        public CancellationToken Token => _cts.Token;

        public bool IsCancelling => _isCancelling;

        public void Track(Task task)
        {
            if (_disposed)
                return;

            if (task == null || task.IsCompleted)
                return;

            lock (_lock)
            {
                _trackedTasks.Add(task);
            }

            // 任务完成后自动移除
            _ = task.ContinueWith(t =>
            {
                lock (_lock)
                {
                    _trackedTasks.Remove(t);
                }
            }, TaskContinuationOptions.ExecuteSynchronously);
        }

        public async Task WhenIdleAsync()
        {
            Task[] snapshot;
            lock (_lock)
            {
                snapshot = _trackedTasks.ToArray();
            }

            if (snapshot.Length == 0)
                return;

            try
            {
                await Task.WhenAll(snapshot);
            }
            catch
            {
                // 忽略异常，我们只关心任务完成
            }
        }

        public void Cancel()
        {
            if (_disposed)
                return;

            _isCancelling = true;
            _cts.Cancel();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _isCancelling = true;
            _cts.Cancel();
            _cts.Dispose();
            _trackedTasks.Clear();
        }
    }
}
