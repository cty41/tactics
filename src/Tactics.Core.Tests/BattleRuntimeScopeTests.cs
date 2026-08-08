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
}
