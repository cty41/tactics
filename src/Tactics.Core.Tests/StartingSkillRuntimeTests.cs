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
    public void DirectAttackLifeStealUsesFinalHealthDamageAndPrecedesDefeatResolution()
    {
        BattleState state = State(new GridPoint(1, 1), new[] { ("enemy.target", new GridPoint(2, 1)) });
        state = state.WithUnit(state.Units[state.ActiveUnitId].WithHealth(10));
        UnitInstanceId targetId = new("enemy.target");
        state = state.WithUnit(state.Units[targetId].WithHealth(3));
        var skill = new SkillDefinition(new ContentId("skill.enemy.maw-bat-bite.lv1"), "maw_bat_bite",
            SkillRole.Any, SkillKind.Active, 1, 0, 1, 1, SkillExecutionKind.DirectAttack, 4,
            SkillDamageKind.Physical, maxUsesPerTurn: 1,
            executionProfile: new SkillExecutionProfile(LifeStealPercent: 50), canCrit: false);

        BattleTransition result = new BattleTransitionService().Apply(state,
            new UseSkillCommand(state.ActiveUnitId, targetId, new GridPoint(2, 1), skill));
        BattleEvent[] ordered = result.Events
            .Where(value => value is DamageAppliedEvent or HealthRestoredEvent or UnitDefeatedEvent).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(result.State.Units[state.ActiveUnitId].CurrentHealth, Is.EqualTo(11));
            Assert.That(result.State.Units[targetId].CurrentHealth, Is.Zero);
            Assert.That(ordered[0], Is.TypeOf<DamageAppliedEvent>());
            Assert.That(ordered[1], Is.TypeOf<HealthRestoredEvent>());
            Assert.That(((HealthRestoredEvent)ordered[1]).Amount, Is.EqualTo(1));
            Assert.That(ordered[2], Is.TypeOf<UnitDefeatedEvent>());
        });
    }

    [Test]
    public void DirectAttackLifeStealEmitsZeroRestoreAtFullHealth()
    {
        BattleState state = State(new GridPoint(1, 1), new[] { ("enemy.target", new GridPoint(2, 1)) });
        var skill = new SkillDefinition(new ContentId("skill.enemy.maw-bat-bite.lv1"), "maw_bat_bite",
            SkillRole.Any, SkillKind.Active, 1, 0, 1, 1, SkillExecutionKind.DirectAttack, 4,
            SkillDamageKind.Physical, maxUsesPerTurn: 1,
            executionProfile: new SkillExecutionProfile(LifeStealPercent: 50), canCrit: false);

        BattleTransition result = new BattleTransitionService().Apply(state,
            new UseSkillCommand(state.ActiveUnitId, new UnitInstanceId("enemy.target"), new GridPoint(2, 1), skill));

        Assert.That(result.Events.OfType<HealthRestoredEvent>().Single().Amount, Is.Zero);
    }

    [Test]
    public void FireballHitsSelectedUnblockedEnemyAndAppliesBurning()
    {
        BattleState state = State(new GridPoint(1, 1), new[] { ("enemy.near.0", new GridPoint(3, 1)), ("enemy.far.0", new GridPoint(4, 1)) });
        SkillDefinition skill = Skill("skill.mage.fireball.lv1", SkillExecutionKind.Fireball, 7, 4, 2, "buff.ignite", 2);
        BattleTransition result = new BattleTransitionService().Apply(state, new UseSkillCommand(state.ActiveUnitId, new UnitInstanceId("enemy.near.0"), new GridPoint(3, 1), skill));
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
    public void FireballAcceptsDiagonalRayAndHitsSelectedUnblockedEnemyOnly()
    {
        BattleState state = State(new GridPoint(1, 1), new[]
        {
            ("enemy.near.0", new GridPoint(2, 2)),
            ("enemy.far.0", new GridPoint(3, 3)),
            ("enemy.adjacent.0", new GridPoint(3, 2))
        });
        SkillDefinition skill = Skill("skill.mage.fireball.lv1", SkillExecutionKind.Fireball, 7, 4, 2, "buff.ignite", 2);

        BattleTransition result = new BattleTransitionService().Apply(state,
            new UseSkillCommand(state.ActiveUnitId, new UnitInstanceId("enemy.near.0"), new GridPoint(2, 2), skill));

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.State.Units[new UnitInstanceId("enemy.near.0")].CurrentHealth, Is.EqualTo(18));
            Assert.That(result.State.Units[new UnitInstanceId("enemy.far.0")].CurrentHealth, Is.EqualTo(20));
            Assert.That(result.State.Units[new UnitInstanceId("enemy.adjacent.0")].CurrentHealth, Is.EqualTo(20), "Fireball Lv1 must not splash.");
            Assert.That(result.State.Units[new UnitInstanceId("enemy.near.0")].Statuses.ContainsKey(new ContentId("buff.ignite")), Is.True);
        });
    }

    [Test]
    public void FireballRejectsAnEnemyThatIsNotOnTheSelectedRay()
    {
        BattleState state = State(new GridPoint(1, 1), new[] { ("enemy.target.0", new GridPoint(3, 2)) });
        SkillDefinition skill = Skill("skill.mage.fireball.lv1", SkillExecutionKind.Fireball, 7, 4, 2, "buff.ignite", 2);

        BattleTransition result = new BattleTransitionService().Apply(state,
            new UseSkillCommand(state.ActiveUnitId, new UnitInstanceId("enemy.target.0"), new GridPoint(3, 3), skill));

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.State.Units[new UnitInstanceId("enemy.target.0")].CurrentHealth, Is.EqualTo(20));
    }

    [Test]
    public void BoneSpearPreservesUnityFirstEnemyInterceptionAlongItsLine()
    {
        BattleState state = State(new GridPoint(1, 1), new[]
        {
            ("enemy.near.0", new GridPoint(2, 1)),
            ("enemy.far.0", new GridPoint(4, 1))
        });
        SkillDefinition skill = Skill("skill.necromancer.bone-spear.lv1", SkillExecutionKind.BoneSpear, 5, 4, 7);

        BattleTransition result = new BattleTransitionService().Apply(state,
            new UseSkillCommand(state.ActiveUnitId, new UnitInstanceId("enemy.far.0"), new GridPoint(4, 1), skill));

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.State.Units[new UnitInstanceId("enemy.near.0")].CurrentHealth, Is.EqualTo(13));
            Assert.That(result.State.Units[new UnitInstanceId("enemy.far.0")].CurrentHealth, Is.EqualTo(20));
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
    public void CombatTechniquesAddsThirtyPointsToDerivedDodge()
    {
        BattleState state = State(new GridPoint(1, 1), new[] { ("enemy.target.0", new GridPoint(2, 1)) }, randomState: 2);
        var targetId = new UnitInstanceId("enemy.target.0");
        BattleUnitState target = state.Units[targetId].WithCombatTechniquesLevelOne(true);
        state = state.WithUnit(target);
        SkillDefinition melee = Skill("skill.basic.melee", SkillExecutionKind.MeleeAttack, 0, 1, 2);
        BattleTransition result = new BattleTransitionService().Apply(state, new UseSkillCommand(state.ActiveUnitId, targetId, new GridPoint(2, 1), melee));
        Assert.Multiple(() =>
        {
            Assert.That(result.Events.OfType<CombatRollResolvedEvent>().Single().Threshold, Is.EqualTo(35));
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
            Assert.That(created.PhysicalAttack, Is.EqualTo(4));
            Assert.That(result.Events.OfType<UnitSummonedEvent>(), Has.Exactly(1).Items);
            Assert.That(result.Events.OfType<DamageAppliedEvent>(), Is.Empty);
        });
    }

    [Test]
    public void SummonCannotStopOnFlyoverObstacle()
    {
        GridPoint corpse = new(3, 1);
        BattleState original = State(new GridPoint(1, 1), Array.Empty<(string, GridPoint)>()).WithCorpse(corpse);
        var cells = original.Board.Cells.ToDictionary(value => value.Key, value => value.Value);
        cells[corpse] = new CellState(obstacle: MovementObstacleKind.Flyover);
        BattleState state = new(new BoardSnapshot(cells), original.Units.Values, original.TurnOrder,
            original.Round, original.ActiveIndex, original.RandomState, original.DroppedSpears, original.Corpses);
        SkillDefinition summon = Skill("skill.necromancer.summon-skeleton.lv1", SkillExecutionKind.SummonSkeleton, 3, 999, 0);

        BattleTransition result = new BattleTransitionService().Apply(state,
            new UseSkillCommand(state.ActiveUnitId, null, corpse, summon));

        Assert.That(result.Events.OfType<CommandRejectedEvent>().Single().Reason, Is.EqualTo("corpse_cell_occupied"));
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
    public void MovementRejectsCorpseDestinationAndTreatsCorpsesAsPathObstacles()
    {
        GridPoint corpse = new(2, 1);
        BattleState state = State(new GridPoint(1, 1), Array.Empty<(string, GridPoint)>()).WithCorpse(corpse);

        BattleTransition result = new BattleTransitionService().Apply(
            state,
            new MoveUnitCommand(state.ActiveUnitId, corpse));

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Events.OfType<CommandRejectedEvent>().Single().Reason, Is.EqualTo("destination_occupied_by_corpse"));
            Assert.That(state.CreateMovementBoard(state.ActiveUnitId).Cells[corpse].IsOccupied, Is.True);
        });
    }

    [Test]
    public void DroppedSpearCannotOccupyCorpseCell()
    {
        BattleState state = State(new GridPoint(1, 1), Array.Empty<(string, GridPoint)>())
            .WithCorpse(new GridPoint(2, 2));

        Assert.Throws<InvalidOperationException>(() => state.WithDroppedSpear(
            new UnitInstanceId("party.caster.0"), new GridPoint(2, 2)));
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

    [Test]
    public void LevelTwoSummonsUseIndependentCategoriesAndConsumeCorpseWhenRequired()
    {
        GridPoint skeletonCell = new(2, 1);
        GridPoint mageCell = new(3, 1);
        BattleState state = State(new GridPoint(1, 1), Array.Empty<(string, GridPoint)>()).WithCorpse(skeletonCell).WithCorpse(mageCell);
        SkillDefinition skeleton = new(new ContentId("skill.necromancer.summon-skeleton.lv2"), "skeleton", SkillRole.Necromancer,
            SkillKind.Active, 2, 3, 0, 9, SkillExecutionKind.SummonSkeleton, 0, SkillDamageKind.None,
            executionProfile: new SkillExecutionProfile(SummonLimit: 2, SummonCategory: "Skeleton", RequiresCorpse: true));
        SkillDefinition mage = new(new ContentId("skill.necromancer.skeleton-mage.lv2"), "mage", SkillRole.Necromancer,
            SkillKind.Active, 2, 7, 0, 9, SkillExecutionKind.SummonSkeletonMage, 0, SkillDamageKind.None,
            executionProfile: new SkillExecutionProfile(SummonLimit: 2, SummonCategory: "SkeletonMage", RequiresCorpse: true));

        BattleTransition first = new BattleTransitionService().Apply(state, new UseSkillCommand(state.ActiveUnitId, null, skeletonCell, skeleton));
        BattleTransition second = new BattleTransitionService().Apply(first.State, new UseSkillCommand(state.ActiveUnitId, null, mageCell, mage));

        Assert.Multiple(() =>
        {
            Assert.That(second.Succeeded, Is.True);
            Assert.That(second.State.Corpses, Is.Empty);
            Assert.That(second.State.Units.Values.Count(unit => unit.SummonCategory == "Skeleton"), Is.EqualTo(1));
            Assert.That(second.State.Units.Values.Count(unit => unit.SummonCategory == "SkeletonMage"), Is.EqualTo(1));
        });
    }

    [Test]
    public void MultiStabRequiresAndResolvesOrderedTargetsAtomically()
    {
        BattleState state = State(new GridPoint(1, 1), new[]
        {
            ("enemy.one", new GridPoint(2, 1)), ("enemy.two", new GridPoint(2, 2)), ("enemy.three", new GridPoint(1, 2))
        });
        SkillDefinition skill = new(new ContentId("skill.amazon.multi-stab.lv1"), "multi", SkillRole.Amazon,
            SkillKind.Active, 1, 8, 1, 4, SkillExecutionKind.MultiStab, 4, SkillDamageKind.Physical,
            executionProfile: new SkillExecutionProfile(OrderedTargetCount: 3));
        UnitInstanceId[] targets = { new("enemy.one"), new("enemy.two"), new("enemy.three") };
        BattleTransition result = new BattleTransitionService().Apply(state,
            new UseSkillCommand(state.ActiveUnitId, targets[0], new GridPoint(2, 1), skill) { OrderedTargetIds = targets });

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Events.OfType<DamageAppliedEvent>().Count(), Is.EqualTo(3));
            Assert.That(result.Events.OfType<CombatRollResolvedEvent>().Count(), Is.EqualTo(3));
            Assert.That(result.Events.OfType<DamageAppliedEvent>().Count(), Is.EqualTo(3));
            Assert.That(result.State.Units[state.ActiveUnitId].CurrentMana, Is.EqualTo(12));
        });
    }

    [Test]
    public void TeleportAndDecoyRelocateThroughTheSharedBattleState()
    {
        BattleState state = State(new GridPoint(1, 1), Array.Empty<(string, GridPoint)>());
        SkillDefinition teleport = new(new ContentId("skill.mage.teleport.lv1"), "teleport", SkillRole.Mage,
            SkillKind.Active, 1, 8, 1, 4, SkillExecutionKind.Teleport, 0, SkillDamageKind.None);
        BattleTransition moved = new BattleTransitionService().Apply(state,
            new UseSkillCommand(state.ActiveUnitId, null, new GridPoint(4, 1), teleport));
        Assert.Multiple(() =>
        {
            Assert.That(moved.Succeeded, Is.True);
            Assert.That(moved.State.Units[state.ActiveUnitId].Unit.Position, Is.EqualTo(new GridPoint(4, 1)));
            Assert.That(moved.Events.OfType<UnitMovedEvent>(), Has.Exactly(1).Items);
        });
    }

    [Test]
    public void BoneShieldLevelTwoAbsorbsMagicBeforeHealth()
    {
        BattleState state = State(new GridPoint(1, 1), new[] { ("enemy.target", new GridPoint(2, 1)) });
        SkillDefinition shield = new(new ContentId("skill.necromancer.bone-shield.lv2"), "shield", SkillRole.Necromancer,
            SkillKind.Active, 2, 8, 0, 0, SkillExecutionKind.BoneShield, 0, SkillDamageKind.None,
            executionProfile: new SkillExecutionProfile(ShieldMultiplier: 2, ShieldAbsorbsAllDamage: true));
        BattleTransition applied = new BattleTransitionService().Apply(state,
            new UseSkillCommand(state.ActiveUnitId, state.ActiveUnitId, new GridPoint(1, 1), shield));
        Assert.Multiple(() =>
        {
            Assert.That(applied.Succeeded, Is.True);
            Assert.That(applied.State.Units[state.ActiveUnitId].DamageShield?.RemainingPoints, Is.EqualTo(17));
            Assert.That(applied.Events.OfType<DamageShieldAppliedEvent>().Single().AbsorbsAllDamage, Is.True);
        });
    }

    [Test]
    public void RecoverSpearLevelTwoDamagesAdjacentEnemiesAfterRecovery()
    {
        BattleState state = State(new GridPoint(1, 1), new[] { ("enemy.target", new GridPoint(2, 1)) })
            .WithDroppedSpear(new UnitInstanceId("party.caster.0"), new GridPoint(4, 1));
        SkillDefinition recover = new(new ContentId("skill.amazon.recover-spear.lv2"), "recover", SkillRole.Amazon,
            SkillKind.Active, 2, 4, 0, 5, SkillExecutionKind.RecoverSpear, 0, SkillDamageKind.None,
            executionProfile: new SkillExecutionProfile(SecondaryDamage: 6));
        BattleTransition result = new BattleTransitionService().Apply(state,
            new UseSkillCommand(state.ActiveUnitId, null, new GridPoint(4, 1), recover));
        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.State.DroppedSpears, Is.Empty);
            Assert.That(result.State.Units[new UnitInstanceId("enemy.target")].CurrentHealth, Is.EqualTo(14));
        });
    }

    [Test]
    public void FireballLevelThreeDetonatesExistingIgniteBeforeResolvingLevelTwoArea()
    {
        BattleState state = State(new GridPoint(1, 1), new[]
        {
            ("enemy.primary", new GridPoint(3, 1)), ("enemy.splash", new GridPoint(3, 2))
        });
        UnitInstanceId primaryId = new("enemy.primary");
        BattleUnitState primary = state.Units[primaryId].WithStatus(new BattleStatusState(
            new ContentId("buff.ignite"), state.ActiveUnitId, 2, 1, stackCount: 3));
        state = state.WithUnit(primary);
        SkillDefinition skill = Lv3("skill.mage.fireball.lv3", SkillExecutionKind.Fireball, 4,
            status: "buff.ignite", duration: 3,
            profile: new SkillExecutionProfile(AreaRadius: 1, DetonateStatusContentId: new ContentId("buff.ignite")));

        BattleTransition result = new BattleTransitionService().Apply(state,
            new UseSkillCommand(state.ActiveUnitId, primaryId, new GridPoint(3, 1), skill));

        Assert.Multiple(() =>
        {
            Assert.That(result.State.Units[primaryId].CurrentHealth, Is.EqualTo(13));
            Assert.That(result.State.Units[new UnitInstanceId("enemy.splash")].CurrentHealth, Is.EqualTo(18));
            Assert.That(result.Events.OfType<StatusExpiredEvent>().Single().StatusId, Is.EqualTo(new ContentId("buff.ignite")));
        });
    }

    [Test]
    public void IceBoltLevelThreeBouncesToStableNearestEnemy()
    {
        BattleState state = State(new GridPoint(1, 1), new[]
        {
            ("enemy.primary", new GridPoint(3, 1)), ("enemy.b", new GridPoint(4, 2)), ("enemy.a", new GridPoint(2, 2))
        });
        SkillDefinition skill = Lv3("skill.mage.ice-bolt.lv3", SkillExecutionKind.IceBolt, 8,
            status: "buff.slow", duration: 2, profile: new SkillExecutionProfile(BounceRange: 3, BounceCount: 1));
        BattleTransition result = new BattleTransitionService().Apply(state,
            new UseSkillCommand(state.ActiveUnitId, new UnitInstanceId("enemy.primary"), new GridPoint(3, 1), skill));
        Assert.Multiple(() =>
        {
            Assert.That(result.State.Units[new UnitInstanceId("enemy.primary")].CurrentHealth, Is.EqualTo(12));
            Assert.That(result.State.Units[new UnitInstanceId("enemy.a")].CurrentHealth, Is.EqualTo(16));
            Assert.That(result.State.Units[new UnitInstanceId("enemy.b")].CurrentHealth, Is.EqualTo(20));
        });
    }

    [Test]
    public void BoneSpearLevelThreePiercesAllEnemiesAndAcceptsEmptyEndpoint()
    {
        BattleState state = State(new GridPoint(1, 1), new[]
        {
            ("enemy.near", new GridPoint(2, 1)), ("enemy.far", new GridPoint(4, 1))
        });
        SkillDefinition skill = Lv3("skill.necromancer.bone-spear.lv3", SkillExecutionKind.BoneSpear, 7,
            profile: new SkillExecutionProfile(PierceAll: true, AllowsEmptyTarget: true));
        BattleTransition hit = new BattleTransitionService().Apply(state,
            new UseSkillCommand(state.ActiveUnitId, new UnitInstanceId("enemy.far"), new GridPoint(4, 1), skill));
        BattleState emptyState = State(new GridPoint(1, 1), Array.Empty<(string, GridPoint)>());
        BattleTransition empty = new BattleTransitionService().Apply(emptyState,
            new UseSkillCommand(emptyState.ActiveUnitId, null, new GridPoint(4, 1), skill));
        Assert.Multiple(() =>
        {
            Assert.That(hit.State.Units[new UnitInstanceId("enemy.near")].CurrentHealth, Is.EqualTo(13));
            Assert.That(hit.State.Units[new UnitInstanceId("enemy.far")].CurrentHealth, Is.EqualTo(13));
            Assert.That(empty.Succeeded, Is.True);
        });
    }

    [Test]
    public void ThrustLevelThreeConsumesAccumulatedMovementForDamage()
    {
        BattleState state = State(new GridPoint(1, 1), new[] { ("enemy.target", new GridPoint(4, 1)) });
        BattleTransition moved = new BattleTransitionService().Apply(state, new MoveUnitCommand(state.ActiveUnitId, new GridPoint(3, 1)));
        SkillDefinition skill = Lv3("skill.amazon.thrust.lv3", SkillExecutionKind.Thrust, 6,
            profile: new SkillExecutionProfile(MovementDamagePerCell: 1));
        BattleTransition result = new BattleTransitionService().Apply(moved.State,
            new UseSkillCommand(state.ActiveUnitId, new UnitInstanceId("enemy.target"), new GridPoint(4, 1), skill));
        Assert.Multiple(() =>
        {
            Assert.That(result.Events.OfType<DamageAppliedEvent>().Single().Amount, Is.EqualTo(8));
            Assert.That(result.State.Units[state.ActiveUnitId].MovementCellsThisTurn, Is.Zero);
        });
    }

    [Test]
    public void PoisonSpearLevelThreeUsesSquareArea()
    {
        BattleState state = State(new GridPoint(1, 1), new[]
        {
            ("enemy.primary", new GridPoint(3, 3)), ("enemy.diagonal", new GridPoint(4, 4))
        });
        SkillDefinition skill = Lv3("skill.amazon.poison-spear.lv3", SkillExecutionKind.PoisonSpear, 10,
            status: "buff.poison", duration: 3, profile: new SkillExecutionProfile(AreaRadius: 1, AreaShape: "square"));
        BattleTransition result = new BattleTransitionService().Apply(state,
            new UseSkillCommand(state.ActiveUnitId, new UnitInstanceId("enemy.primary"), new GridPoint(3, 3), skill));
        Assert.That(result.State.Units[new UnitInstanceId("enemy.diagonal")].Statuses.ContainsKey(new ContentId("buff.poison")), Is.True);
    }

    [Test]
    public void PoisonSpearMissDropsSpearButDoesNotDamagePoisonOrPropagate()
    {
        BattleState state = State(new GridPoint(1, 1), new[]
        {
            ("enemy.primary", new GridPoint(3, 3)), ("enemy.diagonal", new GridPoint(4, 4))
        });
        var profile = new SkillExecutionProfile(AreaRadius: 1, AreaShape: "square",
            EffectScaling: SkillEffectScalingKind.RangedPhysical, AccuracyFactor: 0.01m);
        var skill = new SkillDefinition(new ContentId("skill.amazon.poison-spear.lv3"), "test.poison",
            SkillRole.Amazon, SkillKind.Active, 3, 0, 1, 6, SkillExecutionKind.PoisonSpear, 4,
            SkillDamageKind.Physical, new ContentId("buff.poison"), 3, executionProfile: profile,
            canCrit: false);

        BattleTransition result = new BattleTransitionService().Apply(state,
            new UseSkillCommand(state.ActiveUnitId, new UnitInstanceId("enemy.primary"), new GridPoint(3, 3), skill));

        Assert.Multiple(() =>
        {
            Assert.That(result.Events.OfType<CombatRollResolvedEvent>().Single().Outcome, Is.EqualTo("dodge"));
            Assert.That(result.State.Units[new UnitInstanceId("enemy.primary")].CurrentHealth, Is.EqualTo(20));
            Assert.That(result.State.Units[new UnitInstanceId("enemy.primary")].Statuses, Is.Empty);
            Assert.That(result.State.Units[new UnitInstanceId("enemy.diagonal")].Statuses, Is.Empty);
            Assert.That(result.State.DroppedSpears.ContainsKey(state.ActiveUnitId), Is.True);
        });
    }

    private static SkillDefinition Lv3(string id, SkillExecutionKind execution, int damage, string? status = null,
        int duration = 0, SkillExecutionProfile? profile = null) => new(new ContentId(id), id, SkillRole.Any,
        SkillKind.Active, 3, 0, 1, 6, execution, damage, damage == 0 ? SkillDamageKind.None : SkillDamageKind.Magical,
        status is null ? null : new ContentId(status), duration, executionProfile: profile, canCrit: false);

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
