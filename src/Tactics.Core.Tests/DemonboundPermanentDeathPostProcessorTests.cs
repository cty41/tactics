using NUnit.Framework;
using Tactics.Core.Battle;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Skills;
using Tactics.Core.Units;

namespace Tactics.Core.Tests;

[TestFixture]
public class DemonboundPermanentDeathPostProcessorTests
{
    private static readonly ContentId DemonboundUnit = new("unit.pure-run.demonbound");

    [Test]
    public void ChancePercent_IsBaselineAtOrBelowLuckThreshold()
    {
        Assert.That(Chance(5), Is.EqualTo(25));
        Assert.That(Chance(3), Is.EqualTo(25));
    }

    [Test]
    public void ChancePercent_ReducesByTwoPointsPerLuckAboveThreshold()
    {
        Assert.That(Chance(7), Is.EqualTo(21));
        Assert.That(Chance(10), Is.EqualTo(15));
    }

    [Test]
    public void ChancePercent_NeverDropsBelowZero()
    {
        Assert.That(Chance(18), Is.EqualTo(0));
        Assert.That(Chance(30), Is.EqualTo(0));
    }

    [Test]
    public void Apply_DoesNotRollForNonPossessedActor()
    {
        BattleTransition transition = Attack(
            possessed: false,
            targetLuck: 5,
            seededRandom: 0,
            out BattleUnitState defeatTarget);

        Assert.That(transition.Events.OfType<RunPermanentDeathRolledEvent>(), Is.Empty);
    }

    [Test]
    public void Apply_DoesNotRollForEnemyFactionDefeats()
    {
        var cells = new Dictionary<GridPoint, CellState>();
        for (int x = 0; x < 5; x++) for (int y = 0; y < 3; y++) cells[new GridPoint(x, y)] = new CellState();
        var actorId = new UnitInstanceId("demonbound");
        var enemyId = new UnitInstanceId("enemy-goat");
        BattleUnitState actor = new(new UnitState(actorId, DemonboundUnit, new GridPoint(1, 1), 0, 5, 0, 0),
            20, 20, maxMana: 10, currentMana: 10,
            demonboundState: new DemonboundBattleState(10, 3, isPossessed: true));
        BattleUnitState enemy = new(new UnitState(enemyId, new ContentId("unit.pure-run.goat-charger"),
            new GridPoint(2, 1), 1, 2, 1, 0), 20, 1);
        BattleState state = new(new BoardSnapshot(cells), [actor, enemy], [actorId, enemyId], randomState: 0);
        SkillDefinition melee = new(new ContentId("skill.basic.melee"), "basic.melee", SkillRole.Any,
            SkillKind.Basic, 1, 0, 1, 1, SkillExecutionKind.MeleeAttack, 0, SkillDamageKind.Physical);

        BattleTransition transition = new BattleTransitionService().Apply(state,
            new UseSkillCommand(actorId, enemyId, enemy.Unit.Position, melee));

        Assert.That(transition.Events.OfType<RunPermanentDeathRolledEvent>(), Is.Empty);
    }

    [Test]
    public void Apply_PossessedFriendlyLethalHitCanRollPermanentDeathExactlyOnce()
    {
        // Walk fixed seeds until the deterministic splitmix64 draw lands inside the
        // 25% permanent-death band (chance is seeded and stable, so the walk is fixed).
        for (ulong seed = 0; seed < 128; seed++)
        {
            BattleTransition transition = Attack(possessed: true, targetLuck: 5, seededRandom: seed,
                out BattleUnitState defeatedTarget);
            RunPermanentDeathRolledEvent[] rolled = transition.Events
                .OfType<RunPermanentDeathRolledEvent>().ToArray();
            if (rolled.Length != 1 || !rolled[0].PermanentDeath) continue;

            Assert.Multiple(() =>
            {
                Assert.That(transition.State.Units[defeatedTarget.Unit.InstanceId].Statuses,
                    Does.ContainKey(SkillRuntimeService.RunPermanentDeathStatusId));
                Assert.That(rolled[0].Roll,
                    Is.LessThan(DemonboundPermanentDeathPostProcessor.ChancePercent(defeatedTarget)));
            });
            return;
        }
        Assert.Fail("No fixed seed landed inside the 25% permanent-death band within 128 attempts.");
    }

    private static int Chance(int luck)
    {
        BattleUnitState victim = new(new UnitState(new UnitInstanceId("victim"),
            DemonboundUnit, new GridPoint(1, 1), 0, 5, 0, 0), 20, 20,
            physicalAttack: 1, magicalAttack: 1);
        return DemonboundPermanentDeathPostProcessor.ChancePercent(
            victim.WithUnitFacts(victim.Unit with { EffectiveAttributes = new UnitAttributes(5, 5, 5, 5, 5, luck) }));
    }

    private static BattleTransition Attack(bool possessed, int targetLuck, ulong seededRandom,
        out BattleUnitState defeatTarget)
    {
        var cells = new Dictionary<GridPoint, CellState>();
        for (int x = 0; x < 5; x++) for (int y = 0; y < 3; y++) cells[new GridPoint(x, y)] = new CellState();
        var actorId = new UnitInstanceId("demonbound");
        var allyId = new UnitInstanceId("ally");
        BattleUnitState actor = new(new UnitState(actorId, DemonboundUnit, new GridPoint(1, 1), 0, 5, 0, 0),
            20, 20, maxMana: 10, currentMana: 10,
            demonboundState: possessed ? new DemonboundBattleState(10, 3, isPossessed: true) : new DemonboundBattleState(7, 1));
        BattleUnitState ally = new(new UnitState(allyId, new ContentId("unit.pure-run.mage"),
            new GridPoint(2, 1), 0, 4, 0, 1,
            effectiveAttributes: new UnitAttributes(5, 5, 5, 5, 5, targetLuck)), 20, 1);
        BattleState state = new(new BoardSnapshot(cells), [actor, ally], [actorId, allyId], randomState: seededRandom);
        SkillDefinition melee = new(new ContentId("skill.basic.melee"), "basic.melee", SkillRole.Any,
            SkillKind.Basic, 1, 0, 1, 1, SkillExecutionKind.MeleeAttack, 0, SkillDamageKind.Physical);

        BattleTransition transition = new BattleTransitionService().Apply(state,
            new UseSkillCommand(actorId, allyId, ally.Unit.Position, melee));
        defeatTarget = transition.State.Units[allyId];
        return transition;
    }
}