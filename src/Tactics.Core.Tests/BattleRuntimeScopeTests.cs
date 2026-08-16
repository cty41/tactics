using NUnit.Framework;
using Tactics.Core.Runtime;

namespace Tactics.Core.Tests;

public sealed class BattleRuntimeScopeTests
{
    [Test]
    public void CancelAndDispose_CancelTrackedWorkAndDrain()
    {
        using var scope = new BattleRuntimeScope();
        Task work = Task.Run(async () => await Task.Delay(Timeout.InfiniteTimeSpan, scope.Token));
        scope.Track(work);

        scope.Cancel();
        Assert.That(scope.IsCancelling, Is.True);
        Assert.DoesNotThrowAsync(async () => await scope.WhenIdleAsync());
    }

    [Test]
    public void WhenIdle_ExposesNonCancellationFaults()
    {
        using var scope = new BattleRuntimeScope();
        Task fault = Task.FromException(new InvalidOperationException("fixture fault"));
        scope.Track(fault);

        var exception = Assert.ThrowsAsync<AggregateException>(async () => await scope.WhenIdleAsync());
        Assert.That(exception!.InnerExceptions.Single().Message, Is.EqualTo("fixture fault"));
    }

    [Test]
    public void Scope_StopsAcceptingTasksAfterDispose()
    {
        using var scope = new BattleRuntimeScope();
        scope.Dispose();

        Assert.That(scope.TryTrack(Task.CompletedTask), Is.False);
    }

    [Test]
    public void Track_AfterCancel_IsIgnoredToMatchFrozenOracle()
    {
        using var scope = new BattleRuntimeScope();
        scope.Cancel();

        Assert.Multiple(() =>
        {
            Assert.DoesNotThrow(() => scope.Track(Task.CompletedTask));
            Assert.That(scope.TryTrack(Task.CompletedTask), Is.False);
        });
    }

    [Test]
    public void AlreadyFaultedTask_IsObservedOnceEvenAfterRemoval()
    {
        using var scope = new BattleRuntimeScope();
        Task faulted = Task.FromException(new InvalidOperationException("stable fault"));
        scope.Track(faulted);

        AggregateException exception = Assert.ThrowsAsync<AggregateException>(async () => await scope.WhenIdleAsync())!;
        Assert.That(exception.InnerExceptions.Select(item => item.Message), Is.EqualTo(new[] { "stable fault" }));
    }

    [Test]
    public void DisposeFromCancellationCallback_IsIdempotentAndPreservesDrain()
    {
        var scope = new BattleRuntimeScope();
        var tracked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        scope.Track(tracked.Task);
        using CancellationTokenRegistration registration = scope.Token.Register(scope.Dispose);

        Assert.DoesNotThrow(scope.Cancel);
        tracked.SetResult();
        Assert.DoesNotThrowAsync(async () => await scope.WhenIdleAsync());
        Assert.DoesNotThrow(scope.Dispose);
    }

    [Test]
    public async Task TimeoutCancellation_ContainsCallbackExceptionAndRejectsNewWork()
    {
        var timeoutError = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var scope = new BattleRuntimeScope(TimeSpan.FromMilliseconds(10), exception => timeoutError.TrySetResult(exception));
        using CancellationTokenRegistration registration = scope.Token.Register(() => throw new InvalidOperationException("callback fault"));

        Exception observed = await timeoutError.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Multiple(() =>
        {
            Assert.That(observed, Is.TypeOf<AggregateException>());
            Assert.That(scope.IsCancelling, Is.True);
            Assert.That(scope.TryTrack(Task.CompletedTask), Is.False);
        });
    }
}
