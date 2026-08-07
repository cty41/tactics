using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tactics.Common.Battle.Runtime
{
    /// <summary>
    /// 统一管理一场战斗或一次测试中的所有异步生命周期。
    /// </summary>
    public interface IBattleRuntimeScope : IDisposable
    {
        /// <summary>
        /// 取消令牌，所有异步操作都应使用此令牌。
        /// </summary>
        CancellationToken Token { get; }

        /// <summary>
        /// 是否正在关闭（cancel 已被调用）。
        /// </summary>
        bool IsCancelling { get; }

        /// <summary>
        /// 注册一个异步任务到 scope 中，scope 会追踪其完成状态。
        /// </summary>
        void Track(Task task);

        /// <summary>
        /// Attempts to register an asynchronous task and reports whether this scope accepted
        /// ownership of its lifecycle.
        /// </summary>
        bool TryTrack(Task task);

        /// <summary>
        /// 等待所有已注册的异步任务完成或取消。
        /// </summary>
        Task WhenIdleAsync();

        /// <summary>
        /// 取消所有异步操作。
        /// </summary>
        void Cancel();
    }
}
