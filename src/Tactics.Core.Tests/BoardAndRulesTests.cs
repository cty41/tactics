using NUnit.Framework;
using Tactics.Core.Board;
using Tactics.Core.Combat;
using Tactics.Core.Content;
using Tactics.Core.Pathfinding;
using Tactics.Core.Presentation;
using Tactics.Core.Turns;

namespace Tactics.Core.Tests;

public sealed class BoardAndRulesTests
{
    [Test]
    public void BoardSpec_UsesFixedTenByTenLocalBounds()
    {
        Assert.That(BoardSpec.CellCount, Is.EqualTo(100));
        Assert.Multiple(() =>
        {
            Assert.That(BoardSpec.Contains(new GridPoint(0, 0)), Is.True);
            Assert.That(BoardSpec.Contains(new GridPoint(9, 9)), Is.True);
            Assert.That(BoardSpec.Contains(new GridPoint(-1, 0)), Is.False);
            Assert.That(BoardSpec.Contains(new GridPoint(10, 9)), Is.False);
        });
    }

    [Test]
    public void AStar_UsesFourNeighboursAndSkipsBlockedCells()
    {
        var cells = Enumerable.Range(0, BoardSpec.Width)
            .SelectMany(x => Enumerable.Range(0, BoardSpec.Height)
                .Select(y => new KeyValuePair<GridPoint, CellState>(new GridPoint(x, y), new CellState())))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        cells[new GridPoint(1, 0)] = new CellState(blocksMovement: true);

        var path = new AStarPathfinder().FindPath(
            new BoardSnapshot(cells),
            new GridPoint(0, 0),
            new GridPoint(2, 0));

        Assert.That(path, Is.EqualTo(new[]
        {
            new GridPoint(0, 1),
            new GridPoint(1, 1),
            new GridPoint(2, 1),
            new GridPoint(2, 0)
        }));
    }

    [Test]
    public void SupercoverLos_RejectsDiagonalCornerCutting()
    {
        var cells = Enumerable.Range(0, BoardSpec.Width)
            .SelectMany(x => Enumerable.Range(0, BoardSpec.Height)
                .Select(y => new KeyValuePair<GridPoint, CellState>(new GridPoint(x, y), new CellState())))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        cells[new GridPoint(1, 0)] = new CellState(blocksLineOfSight: true);

        bool visible = new SupercoverLineOfSight().HasLineOfSight(
            new BoardSnapshot(cells),
            new GridPoint(0, 0),
            new GridPoint(2, 2));

        Assert.That(visible, Is.False);
    }

    [Test]
    public void MovementActionState_AllowsOneMoveWithoutRemainingPointAccounting()
    {
        var movement = new MovementActionState(moveRange: 3);

        Assert.Multiple(() =>
        {
            Assert.That(movement.CanUseMove(3), Is.True);
            Assert.That(movement.TryUseMove(2), Is.True);
            Assert.That(movement.HasMovedThisTurn, Is.True);
            Assert.That(movement.CanUseMove(1), Is.False);
        });

        movement.PrepareForTurn();
        Assert.That(movement.TryUseMove(3), Is.True);
    }

    [Test]
    public void InitiativeOrder_UsesSpeedThenStableContentId()
    {
        var result = InitiativeOrder.Sort(new[]
        {
            new InitiativeEntry(new ContentId("unit.zombie"), 4),
            new InitiativeEntry(new ContentId("unit.amazon"), 4),
            new InitiativeEntry(new ContentId("unit.mage"), 5)
        });

        Assert.That(result.Select(entry => entry.UnitId.Value), Is.EqualTo(new[]
        {
            "unit.mage", "unit.amazon", "unit.zombie"
        }));
    }

    [Test]
    public void PoisonSpear_ResolvesDamageAndPoisonWithoutPresentationState()
    {
        var cells = Enumerable.Range(0, BoardSpec.Width)
            .SelectMany(x => Enumerable.Range(0, BoardSpec.Height)
                .Select(y => new KeyValuePair<GridPoint, CellState>(new GridPoint(x, y), new CellState())))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        var board = new BoardSnapshot(cells);
        var caster = new UnitState(new ContentId("unit.caster"), new GridPoint(1, 1), 3, 5);
        var target = new UnitState(new ContentId("unit.target"), new GridPoint(3, 2), 3, 4);

        var result = new PoisonSpearResolver().Resolve(
            board,
            caster,
            target,
            new PoisonSpearDefinition(new ContentId("skill.poison-spear.lv1"), 6, 8, 3));

        Assert.That(result, Is.EqualTo(new ActionResult(true, 8, 3)));
    }

    [Test]
    public void PresentationPlan_RejectsMissingChildrenAndCycles()
    {
        var missingChild = new PresentationExecutionPlan(
            1,
            "root",
            new[] { new PresentationNode("root", "sequence", PresentationNodeKind.Sequence, new[] { "missing" }) });
        Assert.Throws<InvalidOperationException>(() => missingChild.Validate());

        var cycle = new PresentationExecutionPlan(
            1,
            "root",
            new[]
            {
                new PresentationNode("root", "sequence", PresentationNodeKind.Sequence, new[] { "leaf" }),
                new PresentationNode("leaf", "sequence", PresentationNodeKind.Sequence, new[] { "root" })
            });
        Assert.Throws<InvalidOperationException>(() => cycle.Validate());
    }
}
