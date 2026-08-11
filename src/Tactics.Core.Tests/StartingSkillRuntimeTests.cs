using NUnit.Framework;
using Tactics.Core.Battle;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Skills;
using Tactics.Core.Units;

namespace Tactics.Core.Tests;

public sealed class StartingSkillRuntimeTests
{
    [Test]
    public void FireballHitsFirstEnemyAndAppliesBurning()
    {
        BattleState state = State(new GridPoint(1, 1), new[] { ("enemy.near.0", new GridPoint(3, 1)), ("enemy.far.0", new GridPoint(4, 1)) });
        SkillDefinition skill = Skill("skill.mage.fireball.lv1", SkillExecutionKind.Fireball, 7, 4, 2, "buff.ignite", 2);
        BattleTransition result = new BattleTransitionService().Apply(state, new UseSkillCommand(state.ActiveUnitId, new UnitInstanceId("enemy.far.0"), new GridPoint(4, 1), skill));
        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.State.Units[new UnitInstanceId("enemy.near.0")].CurrentHealth, Is.EqualTo(18));
            Assert.That(result.State.Units[new UnitInstanceId("enemy.far.0")].CurrentHealth, Is.EqualTo(20));
            Assert.That(result.State.Units[new UnitInstanceId("enemy.near.0")].Statuses.ContainsKey(new ContentId("buff.ignite")), Is.True);
            Assert.That(result.State.Units[state.ActiveUnitId].CurrentMana, Is.EqualTo(13));
        });
    }

    [Test]
    public void AmplifyDamageRaisesFollowingDamageByThirtyPercent()
    {
        BattleState state = State(new GridPoint(1, 1), new[] { ("enemy.target.0", new GridPoint(2, 1)) });
        var target = new UnitInstanceId("enemy.target.0");
        SkillDefinition amplify = Skill("skill.necromancer.amplify-damage.lv1", SkillExecutionKind.AmplifyDamage, 3, 4, 0, "buff.curse-damage-amplifier", 5);
        BattleTransition cursed = new BattleTransitionService().Apply(state, new UseSkillCommand(state.ActiveUnitId, target, new GridPoint(2, 1), amplify));
        SkillDefinition melee = Skill("skill.basic.melee", SkillExecutionKind.MeleeAttack, 0, 1, 2);
        BattleTransition hit = new BattleTransitionService().Apply(cursed.State, new UseSkillCommand(state.ActiveUnitId, target, new GridPoint(2, 1), melee));
        Assert.That(hit.Events.OfType<DamageAppliedEvent>().Single().Amount, Is.EqualTo(3));
    }

    [Test]
    public void CombatTechniquesUsesExplicitRepeatableDodgeRoll()
    {
        BattleState state = State(new GridPoint(1, 1), new[] { ("enemy.target.0", new GridPoint(2, 1)) }, randomState: 2);
        var targetId = new UnitInstanceId("enemy.target.0");
        BattleUnitState target = state.Units[targetId].WithCombatTechniquesLevelOne(true);
        state = state.WithUnit(target);
        SkillDefinition melee = Skill("skill.basic.melee", SkillExecutionKind.MeleeAttack, 0, 1, 2);
        BattleTransition result = new BattleTransitionService().Apply(state, new UseSkillCommand(state.ActiveUnitId, targetId, new GridPoint(2, 1), melee));
        Assert.Multiple(() =>
        {
            Assert.That(result.Events.OfType<CombatRollResolvedEvent>().Single().Outcome, Is.EqualTo("dodge"));
            Assert.That(result.State.Units[targetId].CurrentHealth, Is.EqualTo(20));
            Assert.That(result.State.RandomState, Is.Not.EqualTo(2UL));
        });
    }

    [Test]
    public void PickupRequiresAdjacentOwnedSpearAndHasNoManaCost()
    {
        BattleState state = State(new GridPoint(1, 1), Array.Empty<(string, GridPoint)>()).WithDroppedSpear(new UnitInstanceId("party.caster.0"), new GridPoint(2, 2));
        SkillDefinition pickup = Skill("skill.amazon.pickup-spear.lv1", SkillExecutionKind.PickupSpear, 0, 1, 0);
        BattleTransition result = new BattleTransitionService().Apply(state, new UseSkillCommand(state.ActiveUnitId, null, new GridPoint(2, 2), pickup));
        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.State.DroppedSpears, Is.Empty);
            Assert.That(result.State.Units[state.ActiveUnitId].CurrentMana, Is.EqualTo(20));
            Assert.That(result.Events.OfType<SpearRecoveredEvent>(), Has.Exactly(1).Items);
        });
    }

    [Test]
    public void SummonConsumesCorpseCreatesOwnedNonHealingUnitAndDoesNotAct()
    {
        GridPoint corpse = new(3, 1);
        BattleState state = State(new GridPoint(1, 1), Array.Empty<(string, GridPoint)>()).WithCorpse(corpse);
        SkillDefinition summon = Skill("skill.necromancer.summon-skeleton.lv1", SkillExecutionKind.SummonSkeleton, 3, 999, 0);
        BattleTransition result = new BattleTransitionService().Apply(state, new UseSkillCommand(state.ActiveUnitId, null, corpse, summon));
        BattleUnitState created = result.State.Units.Values.Single(unit => unit.SummonOwnerId == state.ActiveUnitId);
        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.State.Corpses, Does.Not.Contain(corpse));
            Assert.That(created.CanReceiveStandardHealing, Is.False);
            Assert.That(created.CanProduceCorpse, Is.False);
            Assert.That(result.Events.OfType<UnitSummonedEvent>(), Has.Exactly(1).Items);
            Assert.That(result.Events.OfType<DamageAppliedEvent>(), Is.Empty);
        });
    }

    [Test]
    public void DefeatCreatesOneConsumableCorpseAtOriginalCell()
    {
        GridPoint cell=new(2,1);BattleState state=State(new GridPoint(1,1),new[]{("enemy.target.0",cell)});
        SkillDefinition lethal=Skill("skill.test.lethal",SkillExecutionKind.Thrust,0,1,30);
        BattleTransition result=new BattleTransitionService().Apply(state,new UseSkillCommand(state.ActiveUnitId,new UnitInstanceId("enemy.target.0"),cell,lethal));
        Assert.Multiple(()=>
        {
            Assert.That(result.State.Corpses,Does.Contain(cell));
            Assert.That(result.Events.OfType<UnitDefeatedEvent>(),Has.Exactly(1).Items);
            Assert.That(result.Events.OfType<CorpseCreatedEvent>().Single().Cell,Is.EqualTo(cell));
        });
    }

    [Test]
    public void BasicAbilityConsumesOneSuccessfulUseUntilNextOwnTurn()
    {
        BattleState state = State(new GridPoint(1, 1), new[] { ("enemy.target.0", new GridPoint(2, 1)) });
        SkillDefinition basic = new(new ContentId("skill.basic.magic"), "basic.magic", SkillRole.Any,
            SkillKind.Basic, 1, 0, 1, 3, SkillExecutionKind.MagicAttack, 0, SkillDamageKind.Magical,
            isBasicAbility: true);
        var target = new UnitInstanceId("enemy.target.0");
        BattleTransition first = new BattleTransitionService().Apply(state,
            new UseSkillCommand(state.ActiveUnitId, target, new GridPoint(2, 1), basic));
        BattleTransition repeated = new BattleTransitionService().Apply(first.State,
            new UseSkillCommand(state.ActiveUnitId, target, new GridPoint(2, 1), basic));
        BattleTransition enemyTurn = new BattleTransitionService().Apply(first.State, new EndTurnCommand(state.ActiveUnitId));
        BattleTransition ownTurn = new BattleTransitionService().Apply(enemyTurn.State, new EndTurnCommand(target));
        BattleTransition afterReset = new BattleTransitionService().Apply(ownTurn.State,
            new UseSkillCommand(state.ActiveUnitId, target, new GridPoint(2, 1), basic));
        Assert.Multiple(() =>
        {
            Assert.That(first.Succeeded, Is.True);
            Assert.That(repeated.Events.OfType<CommandRejectedEvent>().Single().Reason, Is.EqualTo("basic_ability_already_used"));
            Assert.That(afterReset.Succeeded, Is.True);
        });
    }

    [Test]
    public void EndTurnRestoresManaFromImmutableRecoveryAmount()
    {
        BattleState state = State(new GridPoint(1, 1), new[] { ("enemy.target.0", new GridPoint(2, 1)) });
        BattleUnitState caster = new(new UnitState(state.ActiveUnitId, new ContentId("unit.caster"), new GridPoint(1, 1), 3, 10, 0, 0),
            20, 20, maxMana: 20, currentMana: 13, manaRecoveryPerTurn: 5);
        BattleTransition result = new BattleTransitionService().Apply(state.WithUnit(caster), new EndTurnCommand(state.ActiveUnitId));
        Assert.Multiple(() =>
        {
            Assert.That(result.State.Units[state.ActiveUnitId].CurrentMana, Is.EqualTo(18));
            Assert.That(result.Events.OfType<ManaRestoredEvent>().Single().Amount, Is.EqualTo(5));
        });
    }

    private static SkillDefinition Skill(string id, SkillExecutionKind execution, int mana, int range, int damage, string? status = null, int duration = 0) =>
        new(new ContentId(id), id, SkillRole.Any, execution == SkillExecutionKind.PickupSpear ? SkillKind.Utility : SkillKind.Active, 1, mana, execution == SkillExecutionKind.SummonSkeleton ? 0 : execution == SkillExecutionKind.PickupSpear ? 0 : 1, range, execution, damage, damage == 0 ? SkillDamageKind.None : SkillDamageKind.Physical, status is null ? null : new ContentId(status), duration);

    private static BattleState State(GridPoint casterCell, IEnumerable<(string Id, GridPoint Cell)> targets, ulong randomState = 42)
    {
        var cells = Enumerable.Range(0, BoardSpec.Width).SelectMany(x => Enumerable.Range(0, BoardSpec.Height).Select(y => new KeyValuePair<GridPoint, CellState>(new GridPoint(x, y), new CellState()))).ToDictionary();
        var casterId = new UnitInstanceId("party.caster.0");
        var units = new List<BattleUnitState> { new(new UnitState(casterId, new ContentId("unit.caster"), casterCell, 3, 10, 0, 0), 20, 20, maxMana: 20, currentMana: 20, physicalAttack: 2, magicalAttack: 4) };
        units.AddRange(targets.Select((item, index) => new BattleUnitState(new UnitState(new UnitInstanceId(item.Id), new ContentId("unit.target"), item.Cell, 3, 8, 1, index), 20, 20)));
        return new BattleState(new BoardSnapshot(cells), units, units.Select(unit => unit.Unit.InstanceId).ToArray(), randomState: randomState);
    }
}
