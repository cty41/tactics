using NUnit.Framework;
using Tactics.Core.Battle;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Items;
using Tactics.Core.Skills;
using Tactics.Core.Units;

namespace Tactics.Core.Tests;

public sealed class DemonboundMeditationTests
{
    [TestCase(1, true, "meditation_blocked_by_move")]
    [TestCase(2, false, null)]
    public void MovementRequiresMindfulnessLevelTwo(int level, bool rejected, string? reason)
    {
        BattleState state = CreateState(new DemonboundBattleState(7, level));
        BattleTransition moved = new BattleTransitionService().Apply(state,
            new MoveUnitCommand(state.ActiveUnitId, new GridPoint(1, 2)));
        BattleTransition result = new BattleTransitionService().Apply(moved.State,
            new MeditateCommand(state.ActiveUnitId));

        Assert.That(result.Succeeded, Is.EqualTo(!rejected));
        if (reason is not null)
            Assert.That(result.Events.Single(), Is.EqualTo(new CommandRejectedEvent(state.ActiveUnitId, reason)));
        else
        {
            Assert.That(result.Events.OfType<MeditationUsedEvent>().Single().RemainingCorruption, Is.EqualTo(2));
            Assert.That(result.State.ActiveUnitId, Is.Not.EqualTo(state.ActiveUnitId));
        }
    }

    [Test]
    public void LevelThreeAllowsBasicAttackAndItemButNormalSkillAlwaysBlocks()
    {
        BattleState initial = CreateState(new DemonboundBattleState(8, 3));
        BattleUnitState enemy = initial.Units[new UnitInstanceId("enemy.0")];
        var basic = new SkillDefinition(new ContentId("skill.basic"), "test.basic", SkillRole.Any,
            SkillKind.Basic, 1, 0, 1, 1, SkillExecutionKind.MeleeAttack, 1, SkillDamageKind.Physical);
        BattleTransition attacked = new BattleTransitionService().Apply(initial,
            new UseSkillCommand(initial.ActiveUnitId, enemy.Unit.InstanceId, enemy.Unit.Position, basic));
        BattleTransition allowed = new BattleTransitionService().Apply(attacked.State,
            new MeditateCommand(initial.ActiveUnitId));
        Assert.That(allowed.Succeeded, Is.True);

        var active = initial.Units[initial.ActiveUnitId];
        var itemDefinition = new ConsumableDefinition(new ContentId("item.mana"), "test.item", "Mana", "",
            ItemRarity.Common, 1, 1, ConsumableEffectKind.RestoreMana, 1, 0, ConsumableTargetMode.Self);
        var item = new BattleConsumableState(new ItemInstanceId("item.0"), itemDefinition.ContentId, 1, 1);
        BattleState withItem = initial.WithUnit(active.WithConsumable(item));
        BattleTransition usedItem = new BattleTransitionService().Apply(withItem,
            new UseConsumableCommand(withItem.ActiveUnitId, withItem.ActiveUnitId, item.InstanceId, itemDefinition));
        Assert.That(new BattleTransitionService().Apply(usedItem.State,
            new MeditateCommand(withItem.ActiveUnitId)).Succeeded, Is.True);

        var activeSkill = new SkillDefinition(new ContentId("skill.active"), "test.active", SkillRole.Any,
            SkillKind.Active, 1, 0, 1, 1, SkillExecutionKind.MeleeAttack, 1, SkillDamageKind.Physical);
        BattleTransition cast = new BattleTransitionService().Apply(initial,
            new UseSkillCommand(initial.ActiveUnitId, enemy.Unit.InstanceId, enemy.Unit.Position, activeSkill));
        BattleTransition blocked = new BattleTransitionService().Apply(cast.State,
            new MeditateCommand(initial.ActiveUnitId));
        Assert.That(blocked.Events.Single(), Is.EqualTo(
            new CommandRejectedEvent(initial.ActiveUnitId, "meditation_blocked_by_skill")));
    }

    [Test]
    public void EmptyCorruptionAndSecondUseAreRejectedWithoutSideEffects()
    {
        BattleState empty = CreateState(new DemonboundBattleState());
        BattleTransition emptyResult = new BattleTransitionService().Apply(empty, new MeditateCommand(empty.ActiveUnitId));
        Assert.That(emptyResult.State, Is.SameAs(empty));
        Assert.That(emptyResult.Events.Single(), Is.EqualTo(
            new CommandRejectedEvent(empty.ActiveUnitId, "meditation_corruption_empty")));

        BattleState alreadyUsed = CreateState(new DemonboundBattleState(5, 3, meditationUsedThisTurn: true));
        BattleTransition usedResult = new BattleTransitionService().Apply(alreadyUsed,
            new MeditateCommand(alreadyUsed.ActiveUnitId));
        Assert.That(usedResult.State, Is.SameAs(alreadyUsed));
        Assert.That(usedResult.Events.Single(), Is.EqualTo(
            new CommandRejectedEvent(alreadyUsed.ActiveUnitId, "meditation_already_used")));
    }

    [TestCase(0, 3, 3)]
    [TestCase(1, 3, 2)]
    [TestCase(3, 1, 0)]
    public void MindfulnessLevelOneReducesEverySkillCorruptionByOne(
        int mindfulnessLevel, int configuredCost, int expectedCost)
    {
        BattleState state = CreateState(new DemonboundBattleState(4, mindfulnessLevel));
        BattleUnitState enemy = state.Units[new UnitInstanceId("enemy.0")];
        var skill = new SkillDefinition(new ContentId("skill.demonbound.test"), "test.corruption",
            SkillRole.Any, SkillKind.Active, 1, 0, 1, 1, SkillExecutionKind.MeleeAttack, 1,
            SkillDamageKind.Physical, executionProfile: new SkillExecutionProfile(CorruptionCost: configuredCost));

        BattleTransition result = new BattleTransitionService().Apply(state,
            new UseSkillCommand(state.ActiveUnitId, enemy.Unit.InstanceId, enemy.Unit.Position, skill));

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.State.Units[state.ActiveUnitId].DemonboundState!.Corruption,
            Is.EqualTo(4 + expectedCost));
        Assert.That(result.Events.OfType<CorruptionChangedEvent>().Select(value => value.Amount),
            expectedCost == 0 ? Is.Empty : Is.EqualTo(new[] { expectedCost }));
    }

    [Test]
    public void BaneLevelTwoCutsTwoCellsWithPrimaryAttributeScalingAndAppliesDamageDebuff()
    {
        BattleState state = CreateState(new DemonboundBattleState(0, 1), includeFarEnemy: true);
        var farId = new UnitInstanceId("enemy.1");
        var bane = new SkillDefinition(new ContentId("skill.demonbound.bane.lv2"), "godot.bane",
            SkillRole.Demonbound, SkillKind.Active, 2, 3, 1, 1, SkillExecutionKind.Bane, 6,
            SkillDamageKind.Magical, new ContentId("buff.demonbound.bane-debuff"), 1,
            executionProfile: new SkillExecutionProfile(IgnoreLineOfSight: true, CorruptionCost: 3,
                DamageScaling: SkillDamageScalingKind.PrimaryAttributeAboveNeutral), canCrit: false);
        BattleTransition hit = new BattleTransitionService().Apply(state,
            new UseSkillCommand(state.ActiveUnitId, new UnitInstanceId("enemy.0"), new GridPoint(2, 1), bane));

        Assert.Multiple(() =>
        {
            Assert.That(hit.State.Units[state.ActiveUnitId].CurrentMana, Is.EqualTo(3));
            Assert.That(hit.State.Units[state.ActiveUnitId].DemonboundState!.Corruption, Is.EqualTo(2));
            Assert.That(hit.Events.OfType<DamageAppliedEvent>().Select(value => value.TargetId),
                Is.EqualTo(new[] { new UnitInstanceId("enemy.0"), farId }));
            Assert.That(hit.Events.OfType<DamageAppliedEvent>().Select(value => value.Amount),
                Is.EqualTo(new[] { 8, 8 }));
            Assert.That(hit.State.Units.Values.Where(unit => unit.Unit.PlayerNumber == 1)
                .All(unit => unit.Statuses.Values.Any(status =>
                    status.EffectKind == Tactics.Core.Statuses.StatusEffectKind.DamageOutputReduction &&
                    status.RemainingTurns == 1)), Is.True);
        });
    }

    [Test]
    public void BaneRejectsAnEmptyDirectionWithoutResourceOrRandomSideEffects()
    {
        BattleState state = CreateState(new DemonboundBattleState(4, 0));
        SkillDefinition bane = new(new ContentId("skill.demonbound.bane.lv1"), "godot.bane",
            SkillRole.Demonbound, SkillKind.Active, 1, 3, 1, 1, SkillExecutionKind.Bane, 5,
            SkillDamageKind.Magical, executionProfile: new SkillExecutionProfile(CorruptionCost: 3,
                DamageScaling: SkillDamageScalingKind.PrimaryAttributeAboveNeutral));

        BattleTransition result = new BattleTransitionService().Apply(state,
            new UseSkillCommand(state.ActiveUnitId, null, new GridPoint(1, 2), bane));

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.State, Is.SameAs(state));
        Assert.That(result.State.RandomState, Is.EqualTo(state.RandomState));
        Assert.That(result.State.Units[state.ActiveUnitId].CurrentMana, Is.EqualTo(6));
        Assert.That(result.State.Units[state.ActiveUnitId].DemonboundState!.Corruption, Is.EqualTo(4));
    }

    [TestCase(1, 5, 0)]
    [TestCase(2, 6, 1)]
    [TestCase(3, 7, 2)]
    public void BaneLevelsKeepTheirDamageAndDebuffProgression(int level, int baseDamage, int duration)
    {
        BattleState state = CreateState(new DemonboundBattleState(), blockFirstCell: true);
        ContentId? statusId = duration == 0 ? null : new ContentId("buff.demonbound.bane-debuff");
        SkillDefinition bane = new(new ContentId($"skill.demonbound.bane.lv{level}"), "godot.bane",
            SkillRole.Demonbound, SkillKind.Active, level, 3, 1, 1, SkillExecutionKind.Bane, baseDamage,
            SkillDamageKind.Magical, statusId, duration,
            executionProfile: new SkillExecutionProfile(IgnoreLineOfSight: true, CorruptionCost: 3,
                DamageScaling: SkillDamageScalingKind.PrimaryAttributeAboveNeutral), canCrit: false);

        BattleTransition result = new BattleTransitionService().Apply(state,
            new UseSkillCommand(state.ActiveUnitId, new UnitInstanceId("enemy.0"), new GridPoint(2, 1), bane));

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Events.OfType<DamageAppliedEvent>().Single().Amount, Is.EqualTo(baseDamage + 2));
        BattleUnitState target = result.State.Units[new UnitInstanceId("enemy.0")];
        if (duration == 0)
            Assert.That(target.Statuses, Is.Empty);
        else
            Assert.That(target.Statuses[statusId!.Value].RemainingTurns, Is.EqualTo(duration));
    }

    [Test]
    public void BaneRollsEachTargetIndependentlyInNearToFarOrder()
    {
        BattleState state = CreateState(new DemonboundBattleState(), includeFarEnemy: true);
        SkillDefinition bane = new(new ContentId("skill.demonbound.bane.lv1"), "godot.bane",
            SkillRole.Demonbound, SkillKind.Active, 1, 3, 1, 1, SkillExecutionKind.Bane, 5,
            SkillDamageKind.Magical, executionProfile: new SkillExecutionProfile(CorruptionCost: 3,
                DamageScaling: SkillDamageScalingKind.PrimaryAttributeAboveNeutral));

        BattleTransition result = new BattleTransitionService().Apply(state,
            new UseSkillCommand(state.ActiveUnitId, new UnitInstanceId("enemy.0"), new GridPoint(2, 1), bane));

        CombatRollResolvedEvent[] rolls = result.Events.OfType<CombatRollResolvedEvent>().ToArray();
        Assert.That(rolls.Select(value => value.TargetId), Is.EqualTo(new[]
            { new UnitInstanceId("enemy.0"), new UnitInstanceId("enemy.1") }));
        Assert.That(rolls[0].RandomState, Is.Not.EqualTo(rolls[1].RandomState));
    }

    [TestCase(SkillExecutionKind.Cleave, 2, 6)]
    [TestCase(SkillExecutionKind.InfernalBlast, 3, 4)]
    [TestCase(SkillExecutionKind.Hellfire, 2, 5)]
    public void DemonboundAreaSkillsResolveEnemyCellsWithoutLineOfSight(
        SkillExecutionKind kind, int level, int damage)
    {
        BattleState state = CreateState(new DemonboundBattleState(0, 1));
        BattleUnitState enemy = state.Units[new UnitInstanceId("enemy.0")];
        var skill = new SkillDefinition(new ContentId($"skill.demonbound.{kind.ToString().ToLowerInvariant()}"),
            "godot.area", SkillRole.Demonbound, SkillKind.Active, level, 0, 0, 1, kind, damage,
            kind == SkillExecutionKind.Cleave ? SkillDamageKind.Physical : SkillDamageKind.Magical,
            kind == SkillExecutionKind.Hellfire ? new ContentId("buff.stun") : null,
            kind == SkillExecutionKind.Hellfire ? 1 : 0,
            executionProfile: new SkillExecutionProfile(StatusChancePercent: 100, CorruptionCost: 2));

        GridPoint direction = kind == SkillExecutionKind.Hellfire
            ? state.Units[state.ActiveUnitId].Unit.Position
            : enemy.Unit.Position;
        BattleTransition result = new BattleTransitionService().Apply(state,
            new UseSkillCommand(state.ActiveUnitId, enemy.Unit.InstanceId, direction, skill));

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Events.OfType<DamageAppliedEvent>().Single().Amount, Is.EqualTo(damage + 2));
    }

    [Test]
    public void DemonicRegenerationHealsBeforeCorruptionIsCommitted()
    {
        BattleState state = CreateState(new DemonboundBattleState(7, 1));
        BattleUnitState actor = state.Units[state.ActiveUnitId].WithHealth(3);
        state = state.WithUnit(actor);
        var skill = new SkillDefinition(new ContentId("skill.demonbound.regeneration.lv2"), "godot.regen",
            SkillRole.Demonbound, SkillKind.Active, 2, 5, 0, 0, SkillExecutionKind.DemonicRegeneration, 0,
            SkillDamageKind.None, executionProfile: new SkillExecutionProfile(CorruptionCost: 6));

        BattleTransition result = new BattleTransitionService().Apply(state,
            new UseSkillCommand(state.ActiveUnitId, state.ActiveUnitId, actor.Unit.Position, skill));

        Assert.Multiple(() =>
        {
            // 治疗 80% (16) 在腐化提交前完成:3 + 16 = 19。
            // 随后腐化越过 10 进入附身,强化投影把 MaxHP 20→40,当前值按比例保持:floor(19*40/20)=38。
            Assert.That(result.State.Units[state.ActiveUnitId].MaxHealth, Is.EqualTo(40));
            Assert.That(result.State.Units[state.ActiveUnitId].CurrentHealth, Is.EqualTo(38));
            Assert.That(result.State.Units[state.ActiveUnitId].DemonboundState!.Corruption, Is.EqualTo(10));
            Assert.That(result.State.Units[state.ActiveUnitId].DemonboundState!.IsPossessed, Is.True);
            Assert.That(result.State.Units[state.ActiveUnitId].DemonboundState!.PossessedBoostApplied, Is.True);
            Assert.That(result.Events.OfType<DemonboundPossessedEvent>().Count(), Is.EqualTo(1));
            Assert.That(result.Events.OfType<HealthRestoredEvent>().Single().Amount, Is.EqualTo(16));
        });
    }

    [Test]
    public void PossessedFriendlyLethalHitRollsOnceAndMarksOnlyTheTwentyFivePercentOutcome()
    {
        BattleState baseline = CreateState(new DemonboundBattleState(10, 3, isPossessed: true));
        BattleUnitState ally = baseline.Units[new UnitInstanceId("enemy.0")]
            .WithUnitFacts(baseline.Units[new UnitInstanceId("enemy.0")].Unit with { PlayerNumber = 0 })
            .WithHealth(1);
        baseline = baseline.WithUnit(ally);
        var melee = new SkillDefinition(new ContentId("skill.basic.melee"), "basic.melee", SkillRole.Any,
            SkillKind.Basic, 1, 0, 1, 1, SkillExecutionKind.MeleeAttack, 0, SkillDamageKind.Physical);
        BattleTransition? permanentResult = null;
        for (ulong seed = 0; seed < 100 && permanentResult is null; seed++)
        {
            BattleState state = baseline.WithRandomState(seed);
            BattleTransition result = new BattleTransitionService().Apply(state,
                new UseSkillCommand(state.ActiveUnitId, ally.Unit.InstanceId, ally.Unit.Position, melee));
            if (result.Events.OfType<RunPermanentDeathRolledEvent>().Single().PermanentDeath)
                permanentResult = result;
        }

        Assert.That(permanentResult, Is.Not.Null);
        Assert.That(permanentResult!.Events.OfType<RunPermanentDeathRolledEvent>().Count(), Is.EqualTo(1));
        Assert.That(permanentResult.State.Units[ally.Unit.InstanceId].Statuses,
            Does.ContainKey(SkillRuntimeService.RunPermanentDeathStatusId));
    }

    [Test]
    public void PossessedSkillUseIgnoresFurtherCorruptionCost()
    {
        BattleState state = CreateState(new DemonboundBattleState(10, 3, isPossessed: true));
        BattleUnitState target = state.Units[new UnitInstanceId("enemy.0")]
            .WithUnitFacts(state.Units[new UnitInstanceId("enemy.0")].Unit with { PlayerNumber = 0 });
        state = state.WithUnit(target);
        var skill = new SkillDefinition(new ContentId("skill.demonbound.infernal-blast.lv1"), "blast",
            SkillRole.Demonbound, SkillKind.Active, 1, 0, 1, 1, SkillExecutionKind.InfernalBlast, 4,
            SkillDamageKind.Magical, executionProfile: new SkillExecutionProfile(CorruptionCost: 3));

        BattleTransition result = new BattleTransitionService().Apply(state,
            new UseSkillCommand(state.ActiveUnitId, target.Unit.InstanceId, target.Unit.Position, skill));

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.State.Units[state.ActiveUnitId].DemonboundState!.Corruption, Is.EqualTo(10));
            Assert.That(result.Events.OfType<CorruptionChangedEvent>(), Is.Empty);
        });
    }

    private static BattleState CreateState(DemonboundBattleState demonbound, bool includeFarEnemy = false,
        bool blockFirstCell = false)
    {
        Dictionary<GridPoint, CellState> cells = Enumerable.Range(0, BoardSpec.Width)
            .SelectMany(x => Enumerable.Range(0, BoardSpec.Height)
                .Select(y => new KeyValuePair<GridPoint, CellState>(new GridPoint(x, y), new CellState())))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        if (blockFirstCell) cells[new GridPoint(2, 1)] = new CellState(blocksMovement: true, blocksLineOfSight: true);
        var heroId = new UnitInstanceId("demonbound.0");
        var enemyId = new UnitInstanceId("enemy.0");
        BattleUnitState[] units =
        {
            new BattleUnitState(new UnitState(heroId, new ContentId("unit.demonbound"), new GridPoint(1, 1),
                4, 10, 0, 0), 20, 20, maxMana: 18, currentMana: 6, demonboundState: demonbound,
                primaryAttributeDamageBonus: 1),
            new BattleUnitState(new UnitState(enemyId, new ContentId("unit.enemy"), new GridPoint(2, 1),
                3, 8, 1, 1), 20, 20)
        };
        if (!includeFarEnemy)
            return new BattleState(new BoardSnapshot(cells), units, new[] { heroId, enemyId }, randomState: 42);

        var farId = new UnitInstanceId("enemy.1");
        return new BattleState(new BoardSnapshot(cells), units.Append(
                new BattleUnitState(new UnitState(farId, new ContentId("unit.enemy"),
                    new GridPoint(3, 1), 3, 7, 1, 2), 20, 20)),
            new[] { heroId, enemyId, farId }, randomState: 42);
    }
}
