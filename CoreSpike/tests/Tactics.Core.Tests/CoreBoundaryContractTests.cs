using NUnit.Framework;
using Tactics.Core.Battle;
using Tactics.Core.Content;

namespace Tactics.Core.Tests;

[TestFixture]
public sealed class CoreBoundaryContractTests
{
    [Test]
    public void TenByTenBoundsContainOnlyLocalCoordinates()
    {
        var bounds = BattleBoardBounds.TenByTen;

        Assert.That(bounds.CellCount, Is.EqualTo(100));
        Assert.That(bounds.Contains(new GridPosition(0, 0)), Is.True);
        Assert.That(bounds.Contains(new GridPosition(9, 9)), Is.True);
        Assert.That(bounds.Contains(new GridPosition(-1, 0)), Is.False);
        Assert.That(bounds.Contains(new GridPosition(10, 0)), Is.False);
        Assert.That(bounds.Contains(new GridPosition(0, 10)), Is.False);
    }

    [Test]
    public void ContentIdTrimsOnlyTransportWhitespaceAndRejectsEmptyValues()
    {
        Assert.That(ContentId.TryCreate("  skill.poison_spear  ", out var contentId), Is.True);
        Assert.That(contentId.Value, Is.EqualTo("skill.poison_spear"));
        Assert.That(ContentId.TryCreate("   ", out _), Is.False);
        Assert.That(ContentId.TryCreate(null, out _), Is.False);
    }

    [Test]
    public void DamageResolutionFactoriesPreserveSemanticOutcome()
    {
        Assert.That(DamageResolution.Hit(12.5f, true), Is.EqualTo(
            new DamageResolution(true, false, false, true, 12.5f)));
        Assert.That(DamageResolution.Dodged().WasHit, Is.False);
        Assert.That(DamageResolution.Blocked().WasBlocked, Is.True);
        Assert.That(DamageResolution.Invalid().DamageApplied, Is.Zero);
    }
}
