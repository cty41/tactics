using NUnit.Framework;
using Tactics.Core.Runs;

namespace Tactics.Core.Tests;

 [TestFixture]
public sealed class AdventureRoutePlanTests
{
    [Test]
    public void TwoOrderedSelectionsAreRequiredBeforeCommit()
    {
        AdventureRoutePlan plan = AdventureRoutePlan.Create(Candidates());
        Assert.That(plan.CurrentCandidateNodeIds, Is.EqualTo(new[] { "route-a-rest", "route-a-store", "route-a-treasure" }));

        plan = plan.Select("route-a-store");
        Assert.That(plan.CurrentCandidateNodeIds, Is.EqualTo(new[] { "route-b-battle", "route-b-event", "route-b-escort" }));
        Assert.That(plan.Lifecycle, Is.EqualTo(AdventureRouteLifecycle.Draft));

        plan = plan.Select("route-b-event");
        Assert.That(plan.Lifecycle, Is.EqualTo(AdventureRouteLifecycle.ReadyToCommit));
        Assert.That(plan.Commit().Lifecycle, Is.EqualTo(AdventureRouteLifecycle.Committed));
    }

    [Test]
    public void RejectsSkippingAGroupAndPrematureCommit()
    {
        AdventureRoutePlan plan = AdventureRoutePlan.Create(Candidates());
        Assert.Throws<InvalidOperationException>(() => plan.Select("route-b-battle"));
        Assert.Throws<InvalidOperationException>(() => plan.Commit());
    }

    private static AdventureRouteCandidate[] Candidates() =>
    [
        new("route-a-rest", 1, AdventureObjectKind.Rest), new("route-a-store", 1, AdventureObjectKind.Store),
        new("route-a-treasure", 1, AdventureObjectKind.Treasure), new("route-b-battle", 2, AdventureObjectKind.Battle),
        new("route-b-event", 2, AdventureObjectKind.Event), new("route-b-escort", 2, AdventureObjectKind.Escort)
    ];
}
