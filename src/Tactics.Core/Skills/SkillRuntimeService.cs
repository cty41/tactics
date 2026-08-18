using Tactics.Core.Battle;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Pathfinding;
using Tactics.Core.Randomness;
using Tactics.Core.Statuses;
using Tactics.Core.Units;

namespace Tactics.Core.Skills;

/// <summary>Executes the normalized starting-skill contract without engine or presentation dependencies.</summary>
public sealed class SkillRuntimeService
{
    public const string ContractId = "skill-runtime-v1";
    private static readonly ContentId SkeletonDefinitionId = new("unit.pure-run.skeleton-warrior");
    private static readonly ContentId FireDemonDefinitionId = new("unit.pure-run.fire-demon");
    private static readonly ContentId SkeletonMageDefinitionId = new("unit.pure-run.skeleton-mage");
    private static readonly ContentId DecoyDefinitionId = new("unit.pure-run.amazon-decoy");
    public static readonly ContentId RunPermanentDeathStatusId = new("status.run.permanent-death");
    private readonly ILineOfSightService _lineOfSight;
    private readonly StatusRuntimeService _statuses;

    public SkillRuntimeService(ILineOfSightService? lineOfSight = null, StatusRuntimeService? statuses = null)
    {
        _lineOfSight = lineOfSight ?? new ShadowConeLineOfSight();
        _statuses = statuses ?? new StatusRuntimeService();
    }

    public BattleTransition Apply(BattleState state, BattleUnitState actor, UseSkillCommand command)
    {
        SkillDefinition skill = command.Definition;
        string? usageFailure = UsageFailure(actor, skill);
        if (usageFailure is not null) return Reject(state, actor, usageFailure);
        if (skill.IsPassive) return ApplyPassive(state, actor, skill);
        if (actor.CurrentMana < skill.ManaCost) return Reject(state, actor, "insufficient_mana");
        if (skill.ExecutionKind == SkillExecutionKind.PickupSpear) return ApplyPickup(state, actor, command);
        if (skill.ExecutionKind is SkillExecutionKind.SummonSkeleton or SkillExecutionKind.SummonSkeletonMage or SkillExecutionKind.SummonFireDemon) return ApplySummon(state, actor, command);
        if (skill.ExecutionKind == SkillExecutionKind.Teleport) return ApplyRelocation(state, actor, command, createDecoy: false);
        if (skill.ExecutionKind == SkillExecutionKind.Decoy) return ApplyRelocation(state, actor, command, createDecoy: true);
        if (skill.ExecutionKind == SkillExecutionKind.RecoverSpear) return ApplyRecoverSpear(state, actor, command);
        if (skill.ExecutionKind is SkillExecutionKind.IceArmor or SkillExecutionKind.BoneShield) return ApplySelfDefense(state, actor, command);
        if (skill.ExecutionKind == SkillExecutionKind.DemonicRegeneration) return ApplyDemonicRegeneration(state, actor, command);
        if (skill.ExecutionKind == SkillExecutionKind.MultiStab) return ApplyMultiStab(state, actor, command);
        if (skill.RequiresLineOfSight && state.Board.Contains(command.TargetCell) &&
            !_lineOfSight.Trace(state.Board, actor.Unit.Position, command.TargetCell,
                LivingBlockers(state, actor.Unit.InstanceId, command.TargetCell, skill.ExecutionKind)).IsClear)
            return Reject(state, actor, "line_of_sight_blocked");

        BattleUnitState[] targets = ResolveTargets(state, actor, command).ToArray();
        if (targets.Length == 0 && skill.ExecutionProfile.AllowsEmptyTarget)
        {
            BattleUnitState used = actor.WithMana(actor.CurrentMana - skill.ManaCost).WithSuccessfulSkillUse(skill.ContentId);
            var emptyEvents = new List<BattleEvent> { new SkillUsedEvent(actor.Unit.InstanceId, actor.Unit.InstanceId, skill.ContentId) };
            if (skill.ManaCost > 0) emptyEvents.Add(new ManaSpentEvent(actor.Unit.InstanceId, skill.ContentId, skill.ManaCost, used.CurrentMana));
            emptyEvents.Add(new SemanticCueEmittedEvent(actor.Unit.InstanceId, null, skill.ContentId, "resolution"));
            return new BattleTransition(state.WithUnit(used), emptyEvents);
        }
        if (targets.Length == 0) return Reject(state, actor, "no_valid_target");
        var events = new List<BattleEvent> { new SkillUsedEvent(actor.Unit.InstanceId, targets[0].Unit.InstanceId, skill.ContentId) };
        BattleState next = state;
        BattleUnitState updatedActor = actor.WithMana(actor.CurrentMana - skill.ManaCost).WithSuccessfulSkillUse(skill.ContentId);
        if (skill.ExecutionProfile.MovementDamagePerCell > 0) updatedActor = updatedActor.ResetMovementCells();
        if (skill.ManaCost > 0) events.Add(new ManaSpentEvent(actor.Unit.InstanceId, skill.ContentId, skill.ManaCost, updatedActor.CurrentMana));
        next = next.WithUnit(updatedActor);

        UnitInstanceId primaryTargetId = targets[0].Unit.InstanceId;
        foreach (BattleUnitState originalTarget in targets)
        {
            BattleUnitState target = next.Units[originalTarget.Unit.InstanceId];
            if (!IsHostile(state, actor, target)) return Reject(state, actor, "target_not_enemy");
            if (!target.IsAlive) return Reject(state, actor, "target_defeated");
            if (originalTarget.Unit.InstanceId == primaryTargetId &&
                skill.ExecutionProfile.DetonateStatusContentId is ContentId detonateId &&
                target.Statuses.TryGetValue(detonateId, out BattleStatusState? detonated))
            {
                int detonationDamage = Math.Min(target.CurrentHealth, Math.Max(0, detonated.StackCount));
                target = target.WithoutStatus(detonateId).WithHealth(target.CurrentHealth - detonationDamage);
                events.Add(new StatusExpiredEvent(target.Unit.InstanceId, detonateId));
                if (detonationDamage > 0)
                    events.Add(new DamageAppliedEvent(actor.Unit.InstanceId, target.Unit.InstanceId, skill.ContentId,
                        detonationDamage, target.CurrentHealth));
                next = next.WithUnit(target);
                next = BattleDefeatResolver.Apply(next, originalTarget, target, events);
                if (!target.IsAlive) continue;
            }
            bool dodged = false;
            bool critical = false;
            if (skill.Damage > 0 || skill.ExecutionKind is SkillExecutionKind.MagicAttack or SkillExecutionKind.MeleeAttack)
            {
                var random = new DeterministicRandom(next.RandomState);
                int roll = random.NextInt(100);
                dodged = target.HasCombatTechniquesLevelOne && roll < 30;
                int criticalThreshold = actor.CombatTechniquesLevel >= 3 ? 70 : 90;
                critical = skill.CanCrit && !dodged && (_statuses.EvaluateBeforeAttack(target).ForceCritical || roll >= criticalThreshold);
                events.Add(new CombatRollResolvedEvent(actor.Unit.InstanceId, target.Unit.InstanceId, skill.ContentId, roll, target.HasCombatTechniquesLevelOne ? 30 : 0, dodged ? "dodge" : critical ? "critical" : "hit", random.State));
                next = next.WithRandomState(random.State);
            }

            int rawDamage = skill.ExecutionKind switch
            {
                SkillExecutionKind.MagicAttack => actor.MagicalAttack,
                SkillExecutionKind.MeleeAttack => actor.PhysicalAttack,
                SkillExecutionKind.Fireball when originalTarget.Unit.InstanceId != primaryTargetId => Math.Max(1, skill.Damage / 2),
                SkillExecutionKind.Thrust => checked(skill.Damage + actor.MovementCellsThisTurn * skill.ExecutionProfile.MovementDamagePerCell),
                _ => skill.Damage
            };
            if (skill.ExecutionProfile.DamageScaling == SkillDamageScalingKind.PrimaryAttributeAboveNeutral)
                rawDamage = checked(rawDamage + actor.PrimaryAttributeDamageBonus);
            if (actor.Statuses.Values.Any(status => status.EffectKind == StatusEffectKind.DamageOutputReduction))
                rawDamage = (int)MathF.Round(rawDamage * 0.75f, MidpointRounding.AwayFromZero);
            if (critical) rawDamage = checked(rawDamage * 2);
            StatusDamagePolicy damagePolicy = _statuses.EvaluateDamageTaken(target, actor, skill.MaxRange > 1);
            int damage = dodged ? 0 : (int)MathF.Round(rawDamage * damagePolicy.DamageMultiplier, MidpointRounding.AwayFromZero);
            if (!dodged && target.DamageShield is BattleDamageShieldState shield &&
                (skill.DamageKind == SkillDamageKind.Physical || shield.AbsorbsAllDamage))
            {
                int absorbed = Math.Min(shield.RemainingPoints, damage);
                damage -= absorbed;
                int remaining = shield.RemainingPoints - absorbed;
                target = target.WithDamageShield(remaining > 0 ? shield with { RemainingPoints = remaining } : null);
                events.Add(new DamageShieldAbsorbedEvent(target.Unit.InstanceId, skill.ContentId, absorbed, remaining));
            }
            int beforeHealth = target.CurrentHealth;
            int health = Math.Max(0, beforeHealth - damage);
            target = target.WithHealth(health);
            events.Add(new DamageAppliedEvent(actor.Unit.InstanceId, target.Unit.InstanceId, skill.ContentId, beforeHealth - health, health));
            if (!target.IsAlive && beforeHealth > 0 && actor.DemonboundState?.IsPossessed == true &&
                target.Unit.PlayerNumber == actor.Unit.PlayerNumber)
            {
                var permanentRandom = new DeterministicRandom(next.RandomState);
                int permanentRoll = permanentRandom.NextInt(100);
                bool permanent = permanentRoll < 25;
                next = next.WithRandomState(permanentRandom.State);
                if (permanent)
                    target = target.WithStatus(new BattleStatusState(RunPermanentDeathStatusId,
                        actor.Unit.InstanceId, int.MaxValue, 0, polarity: StatusPolarity.Harmful));
                events.Add(new RunPermanentDeathRolledEvent(actor.Unit.InstanceId, target.Unit.InstanceId,
                    permanentRoll, permanent, permanentRandom.State));
            }

            if (target.IsAlive && !dodged && skill.StatusContentId is ContentId statusId)
            {
                bool applyStatus = true;
                if (skill.ExecutionProfile.StatusChancePercent < 100)
                {
                    var random = new DeterministicRandom(next.RandomState);
                    int roll = random.NextInt(100);
                    applyStatus = roll < skill.ExecutionProfile.StatusChancePercent;
                    events.Add(new StatusRollResolvedEvent(actor.Unit.InstanceId, target.Unit.InstanceId, skill.ContentId,
                        statusId, roll, skill.ExecutionProfile.StatusChancePercent, applyStatus, random.State));
                    next = next.WithRandomState(random.State);
                }
                if (applyStatus)
                {
                    StatusDefinition definition = StatusFor(skill, statusId);
                    int duration = skill.ExecutionKind == SkillExecutionKind.IceBolt && originalTarget.Unit.InstanceId != primaryTargetId
                        ? 1 : skill.StatusDuration;
                    StatusApplicationResult application = _statuses.Apply(target, definition, actor.Unit.InstanceId, duration);
                    target = application.Unit;
                    events.Add(new StatusAppliedEvent(actor.Unit.InstanceId, target.Unit.InstanceId, statusId, application.AppliedStatus.RemainingTurns));
                }
            }
            next = next.WithUnit(target);
            next = BattleDefeatResolver.Apply(next, originalTarget, target, events);

            if (originalTarget.Unit.InstanceId == primaryTargetId && target.IsAlive && !dodged &&
                actor.Unit.DefinitionId == new ContentId("unit.pure-run.amazon") && actor.CombatTechniquesLevel >= 2 &&
                skill.ExecutionKind == SkillExecutionKind.MeleeAttack)
            {
                var followUpRandom = new DeterministicRandom(next.RandomState);
                int followUpRoll = followUpRandom.NextInt(100);
                events.Add(new CombatRollResolvedEvent(actor.Unit.InstanceId, target.Unit.InstanceId, skill.ContentId,
                    followUpRoll, 30, followUpRoll < 30 ? "combat-techniques-follow-up" : "combat-techniques-no-follow-up",
                    followUpRandom.State));
                next = next.WithRandomState(followUpRandom.State);
                if (followUpRoll < 30)
                {
                    BattleUnitState current = next.Units[target.Unit.InstanceId];
                    int followUpDamage = Math.Min(current.CurrentHealth, actor.PhysicalAttack);
                    BattleUnitState followed = current.WithHealth(current.CurrentHealth - followUpDamage);
                    events.Add(new DamageAppliedEvent(actor.Unit.InstanceId, current.Unit.InstanceId, skill.ContentId,
                        followUpDamage, followed.CurrentHealth));
                    next = next.WithUnit(followed);
                    next = BattleDefeatResolver.Apply(next, current, followed, events);
                }
            }
        }
        if (skill.ExecutionKind == SkillExecutionKind.IceBolt && skill.ExecutionProfile.BounceCount > 0 &&
            next.TryGetUnit(primaryTargetId, out BattleUnitState? primary) && primary is not null)
        {
            foreach (BattleUnitState candidate in next.Units.Values
                         .Where(unit => unit.IsAlive && IsHostile(next, actor, unit) && unit.Unit.InstanceId != primaryTargetId)
                         .Where(unit => Manhattan(unit.Unit.Position, primary.Unit.Position) <= skill.ExecutionProfile.BounceRange)
                         .OrderBy(unit => Manhattan(unit.Unit.Position, primary.Unit.Position))
                         .ThenBy(unit => unit.Unit.InstanceId.Value, StringComparer.Ordinal)
                         .Take(skill.ExecutionProfile.BounceCount).ToArray())
            {
                BattleUnitState bounce = next.Units[candidate.Unit.InstanceId];
                var random = new DeterministicRandom(next.RandomState);
                int roll = random.NextInt(100);
                bool dodged = bounce.HasCombatTechniquesLevelOne && roll < 30;
                events.Add(new CombatRollResolvedEvent(actor.Unit.InstanceId, bounce.Unit.InstanceId, skill.ContentId,
                    roll, bounce.HasCombatTechniquesLevelOne ? 30 : 0, dodged ? "dodge" : "hit", random.State));
                next = next.WithRandomState(random.State);
                int damage = dodged ? 0 : Math.Min(bounce.CurrentHealth, Math.Max(1, skill.Damage / 2));
                BattleUnitState damaged = bounce.WithHealth(bounce.CurrentHealth - damage);
                events.Add(new DamageAppliedEvent(actor.Unit.InstanceId, bounce.Unit.InstanceId, skill.ContentId, damage, damaged.CurrentHealth));
                if (!dodged && damaged.IsAlive && skill.StatusContentId is ContentId slowId)
                {
                    StatusApplicationResult application = _statuses.Apply(damaged, StatusFor(skill, slowId), actor.Unit.InstanceId, 1);
                    damaged = application.Unit;
                    events.Add(new StatusAppliedEvent(actor.Unit.InstanceId, damaged.Unit.InstanceId, slowId, 1));
                }
                next = next.WithUnit(damaged);
                next = BattleDefeatResolver.Apply(next, bounce, damaged, events);
            }
        }
        events.Add(new SemanticCueEmittedEvent(actor.Unit.InstanceId, targets[0].Unit.InstanceId, skill.ContentId, "resolution"));
        return new BattleTransition(next, events);
    }

    private BattleTransition ApplyPassive(BattleState state, BattleUnitState actor, SkillDefinition skill)
    {
        if (skill.ExecutionKind == SkillExecutionKind.Mindfulness)
        {
            DemonboundBattleState current = actor.DemonboundState ?? new DemonboundBattleState();
            BattleUnitState mindful = actor.WithDemonboundState(new DemonboundBattleState(current.Corruption,
                skill.Level, isPossessed: current.IsPossessed));
            return new BattleTransition(state.WithUnit(mindful), new BattleEvent[]
            {
                new SkillUsedEvent(actor.Unit.InstanceId, actor.Unit.InstanceId, skill.ContentId),
                new SemanticCueEmittedEvent(actor.Unit.InstanceId, actor.Unit.InstanceId, skill.ContentId, "passive-enabled")
            });
        }
        if (skill.ExecutionKind != SkillExecutionKind.CombatTechniques) return Reject(state, actor, "unsupported_passive");
        BattleUnitState updated = actor.WithCombatTechniquesLevel(skill.Level).WithSuccessfulSkillUse(skill.ContentId);
        return new BattleTransition(state.WithUnit(updated), new BattleEvent[]
        {
            new SkillUsedEvent(actor.Unit.InstanceId, actor.Unit.InstanceId, skill.ContentId),
            new SemanticCueEmittedEvent(actor.Unit.InstanceId, actor.Unit.InstanceId, skill.ContentId, "passive-enabled")
        });
    }

    private static BattleTransition ApplyDemonicRegeneration(
        BattleState state, BattleUnitState actor, UseSkillCommand command)
    {
        SkillDefinition skill = command.Definition;
        if (actor.CurrentHealth >= actor.MaxHealth) return Reject(state, actor, "target_at_full_health");
        if (command.TargetId is UnitInstanceId targetId && targetId != actor.Unit.InstanceId)
            return Reject(state, actor, "regeneration_target_not_self");
        int amount = (int)Math.Ceiling(actor.MaxHealth * (skill.Level >= 2 ? 0.8d : 0.5d));
        BattleUnitState updated = actor.WithHealth(actor.CurrentHealth + amount)
            .WithMana(actor.CurrentMana - skill.ManaCost).WithSuccessfulSkillUse(skill.ContentId);
        var events = new List<BattleEvent>
        {
            new SkillUsedEvent(actor.Unit.InstanceId, actor.Unit.InstanceId, skill.ContentId),
            new HealthRestoredEvent(actor.Unit.InstanceId, actor.Unit.InstanceId, skill.ContentId,
                updated.CurrentHealth - actor.CurrentHealth, updated.CurrentHealth)
        };
        if (skill.ManaCost > 0)
            events.Insert(1, new ManaSpentEvent(actor.Unit.InstanceId, skill.ContentId, skill.ManaCost, updated.CurrentMana));
        return new BattleTransition(state.WithUnit(updated), events);
    }

    private static BattleTransition ApplyPickup(BattleState state, BattleUnitState actor, UseSkillCommand command)
    {
        if (!state.TryGetDroppedSpear(actor.Unit.InstanceId, out GridPoint spear)) return Reject(state, actor, "spear_not_dropped");
        if (command.TargetCell != spear) return Reject(state, actor, "spear_cell_mismatch");
        if (Math.Max(Math.Abs(actor.Unit.Position.X - spear.X), Math.Abs(actor.Unit.Position.Y - spear.Y)) != 1) return Reject(state, actor, "spear_not_adjacent");
        BattleState next = state.WithoutDroppedSpear(actor.Unit.InstanceId);
        return new BattleTransition(next, new BattleEvent[]
        {
            new SkillUsedEvent(actor.Unit.InstanceId, actor.Unit.InstanceId, command.Definition.ContentId),
            new SpearRecoveredEvent(actor.Unit.InstanceId, spear),
            new SemanticCueEmittedEvent(actor.Unit.InstanceId, actor.Unit.InstanceId, command.Definition.ContentId, "spear-recovered")
        });
    }

    private static BattleTransition ApplySummon(BattleState state, BattleUnitState actor, UseSkillCommand command)
    {
        GridPoint cell = command.TargetCell;
        bool requiresCorpse = command.Definition.ExecutionKind is SkillExecutionKind.SummonSkeleton or SkillExecutionKind.SummonSkeletonMage || command.Definition.ExecutionProfile.RequiresCorpse;
        if (requiresCorpse && !state.Corpses.Contains(cell)) return Reject(state, actor, "corpse_not_found");
        if (!requiresCorpse && (!state.Board.Contains(cell) || Manhattan(actor.Unit.Position, cell) > command.Definition.MaxRange)) return Reject(state, actor, "summon_cell_out_of_range");
        if (state.Units.Values.Any(unit => unit.IsAlive && unit.Unit.Position == cell)) return Reject(state, actor, "corpse_cell_occupied");
        if (actor.CurrentMana < command.Definition.ManaCost) return Reject(state, actor, "insufficient_mana");
        int ordinal = state.Units.Values.Where(unit => unit.SummonOwnerId == actor.Unit.InstanceId).Select(unit => unit.Unit.SpawnOrdinal).DefaultIfEmpty(-1).Max() + 1;
        ContentId definitionId = command.Definition.ExecutionProfile.SummonDefinitionId ?? command.Definition.ExecutionKind switch
        {
            SkillExecutionKind.SummonFireDemon => FireDemonDefinitionId,
            SkillExecutionKind.SummonSkeletonMage => SkeletonMageDefinitionId,
            _ => SkeletonDefinitionId
        };
        string category = string.IsNullOrEmpty(command.Definition.ExecutionProfile.SummonCategory) ? command.Definition.ExecutionKind.ToString() : command.Definition.ExecutionProfile.SummonCategory;
        var summonId = new UnitInstanceId($"{actor.Unit.InstanceId.Value}.{category.ToLowerInvariant()}.{ordinal}");
        int maxHealth = command.Definition.ExecutionKind switch { SkillExecutionKind.SummonSkeletonMage => command.Definition.Level >= 2 ? 8 : 6, SkillExecutionKind.SummonSkeleton => command.Definition.Level >= 2 ? 10 : 8, _ => 12 };
        int magicalAttack = command.Definition.ExecutionKind is SkillExecutionKind.SummonFireDemon or SkillExecutionKind.SummonSkeletonMage ? 4 : 0;
        var facts = new UnitState(summonId, definitionId, cell, 3, 10f, actor.Unit.PlayerNumber, ordinal);
        var summon = new BattleUnitState(facts, maxHealth, maxHealth, maxMana: 0, currentMana: 0, physicalAttack: 4, magicalAttack: magicalAttack, summonOwnerId: actor.Unit.InstanceId, canReceiveStandardHealing: false, canProduceCorpse: false, summonCategory: category);
        BattleUnitState updatedActor = actor.WithMana(actor.CurrentMana - command.Definition.ManaCost).WithSuccessfulSkillUse(command.Definition.ContentId);
        int maximum = command.Definition.ExecutionProfile.SummonLimit > 0 ? command.Definition.ExecutionProfile.SummonLimit : command.Definition.ExecutionKind switch { SkillExecutionKind.SummonSkeleton => Math.Min(3, command.Definition.Level), SkillExecutionKind.SummonSkeletonMage => Math.Min(2, command.Definition.Level), _ => Math.Max(1, command.Definition.Level) };
        BattleState next = state.WithUnit(updatedActor);
        if (requiresCorpse) next = next.WithoutCorpse(cell);
        next = next.WithSummon(summon, maximum, category);
        var events = new List<BattleEvent> { new SkillUsedEvent(actor.Unit.InstanceId, actor.Unit.InstanceId, command.Definition.ContentId) };
        if (command.Definition.ManaCost > 0) events.Add(new ManaSpentEvent(actor.Unit.InstanceId, command.Definition.ContentId, command.Definition.ManaCost, updatedActor.CurrentMana));
        if (requiresCorpse) events.Add(new CorpseConsumedEvent(cell, actor.Unit.InstanceId));
        events.Add(new UnitSummonedEvent(actor.Unit.InstanceId, summonId, definitionId, cell));
        events.Add(new SemanticCueEmittedEvent(actor.Unit.InstanceId, summonId, command.Definition.ContentId, "summoned"));
        return new BattleTransition(next, events);
    }

    private BattleTransition ApplySelfDefense(BattleState state, BattleUnitState actor, UseSkillCommand command)
    {
        SkillDefinition skill = command.Definition;
        BattleUnitState updated = actor.WithMana(actor.CurrentMana - skill.ManaCost).WithSuccessfulSkillUse(skill.ContentId);
        if (skill.ExecutionKind == SkillExecutionKind.BoneShield)
        {
            int points = Math.Max(1, (actor.MaxMana / 3) * Math.Max(1, skill.ExecutionProfile.ShieldMultiplier));
            updated = updated.WithDamageShield(new BattleDamageShieldState(points, skill.ExecutionProfile.ShieldAbsorbsAllDamage || skill.Level >= 2));
            var shieldEvents = new List<BattleEvent> { new SkillUsedEvent(actor.Unit.InstanceId, actor.Unit.InstanceId, skill.ContentId) };
            if (skill.ManaCost > 0) shieldEvents.Add(new ManaSpentEvent(actor.Unit.InstanceId, skill.ContentId, skill.ManaCost, updated.CurrentMana));
            shieldEvents.Add(new DamageShieldAppliedEvent(actor.Unit.InstanceId, skill.ContentId, points, updated.DamageShield!.AbsorbsAllDamage));
            return new BattleTransition(state.WithUnit(updated), shieldEvents);
        }
        ContentId statusId = skill.StatusContentId ?? new ContentId(skill.ExecutionKind == SkillExecutionKind.IceArmor ? "buff.ice-armor" : "buff.bone-shield");
        var definition = new StatusDefinition(statusId, skill.SourceId, Math.Max(1, skill.StatusDuration), true, StatusPolarity.Beneficial,
            StatusEffectKind.DamageReduction, StatusTriggerTiming.DamageTaken, StatusRefreshStrategy.RefreshDuration,
            damageReductionPercent: skill.ExecutionKind == SkillExecutionKind.IceArmor ? 0.25f : 0f,
            meleeRetaliationStatusId: skill.ExecutionKind == SkillExecutionKind.IceArmor && skill.Level >= 2 ? new ContentId("buff.slow") : null,
            meleeRetaliationDuration: skill.ExecutionKind == SkillExecutionKind.IceArmor && skill.Level >= 2 ? 2 : 0);
        StatusApplicationResult application = _statuses.Apply(updated, definition, actor.Unit.InstanceId, Math.Max(1, skill.StatusDuration));
        updated = application.Unit;
        var events = new List<BattleEvent> { new SkillUsedEvent(actor.Unit.InstanceId, actor.Unit.InstanceId, skill.ContentId) };
        if (skill.ManaCost > 0) events.Add(new ManaSpentEvent(actor.Unit.InstanceId, skill.ContentId, skill.ManaCost, updated.CurrentMana));
        events.Add(new StatusAppliedEvent(actor.Unit.InstanceId, actor.Unit.InstanceId, statusId, application.AppliedStatus.RemainingTurns));
        return new BattleTransition(state.WithUnit(updated), events);
    }

    private BattleTransition ApplyRelocation(BattleState state, BattleUnitState actor, UseSkillCommand command, bool createDecoy)
    {
        SkillDefinition skill = command.Definition;
        GridPoint destination = command.TargetCell;
        int distance = Manhattan(actor.Unit.Position, destination);
        if (!state.Board.Contains(destination) || distance < skill.MinRange || distance > skill.MaxRange) return Reject(state, actor, "destination_out_of_range");
        if (state.Units.Values.Any(unit => unit.IsAlive && unit.Unit.Position == destination)) return Reject(state, actor, "destination_occupied");
        if (skill.RequiresLineOfSight && !_lineOfSight.Trace(state.Board, actor.Unit.Position, destination,
                LivingBlockers(state, actor.Unit.InstanceId, destination, skill.ExecutionKind)).IsClear) return Reject(state, actor, "line_of_sight_blocked");
        GridPoint origin = actor.Unit.Position;
        BattleUnitState moved = actor.WithPosition(destination, actor.HasMovedThisTurn).WithMana(actor.CurrentMana - skill.ManaCost).WithSuccessfulSkillUse(skill.ContentId);
        BattleState next = state.WithUnit(moved);
        var events = new List<BattleEvent> { new SkillUsedEvent(actor.Unit.InstanceId, actor.Unit.InstanceId, skill.ContentId) };
        if (skill.ManaCost > 0) events.Add(new ManaSpentEvent(actor.Unit.InstanceId, skill.ContentId, skill.ManaCost, moved.CurrentMana));
        events.Add(new UnitMovedEvent(actor.Unit.InstanceId, origin, destination, new[] { destination }));
        if (createDecoy)
        {
            int ordinal = state.Units.Values.Select(unit => unit.Unit.SpawnOrdinal).DefaultIfEmpty(-1).Max() + 1;
            var id = new UnitInstanceId($"{actor.Unit.InstanceId.Value}.decoy.{ordinal}");
            var facts = new UnitState(id, DecoyDefinitionId, origin, 0, actor.Unit.Initiative, actor.Unit.PlayerNumber, ordinal);
            var decoy = new BattleUnitState(facts, Math.Max(1, actor.MaxHealth / 2), Math.Max(1, actor.MaxHealth / 2), summonOwnerId: actor.Unit.InstanceId, canReceiveStandardHealing: false, canProduceCorpse: false, summonCategory: "Decoy");
            if (skill.ExecutionProfile.CleanseHarmful || skill.Level >= 2) moved = _statuses.RemoveHarmful(moved, out _);
            next = next.WithUnit(moved).WithSummon(decoy, 1, "Decoy");
            events.Add(new UnitSummonedEvent(actor.Unit.InstanceId, id, DecoyDefinitionId, origin));
        }
        return new BattleTransition(next, events);
    }

    private BattleTransition ApplyRecoverSpear(BattleState state, BattleUnitState actor, UseSkillCommand command)
    {
        if (!state.TryGetDroppedSpear(actor.Unit.InstanceId, out GridPoint spear)) return Reject(state, actor, "spear_not_dropped");
        if (command.TargetCell != spear || Manhattan(actor.Unit.Position, spear) > command.Definition.MaxRange) return Reject(state, actor, "spear_out_of_range");
        BattleUnitState updated = actor.WithMana(actor.CurrentMana - command.Definition.ManaCost).WithSuccessfulSkillUse(command.Definition.ContentId);
        var events = new List<BattleEvent> { new SkillUsedEvent(actor.Unit.InstanceId, actor.Unit.InstanceId, command.Definition.ContentId) };
        if (command.Definition.ManaCost > 0) events.Add(new ManaSpentEvent(actor.Unit.InstanceId, command.Definition.ContentId, command.Definition.ManaCost, updated.CurrentMana));
        events.Add(new SpearRecoveredEvent(actor.Unit.InstanceId, spear));
        BattleState next = state.WithUnit(updated).WithoutDroppedSpear(actor.Unit.InstanceId);
        int secondaryDamage = command.Definition.ExecutionProfile.SecondaryDamage;
        if (secondaryDamage > 0)
        {
            foreach (BattleUnitState original in next.Units.Values
                         .Where(unit => unit.IsAlive && IsHostile(state, actor, unit) && Manhattan(unit.Unit.Position, actor.Unit.Position) == 1)
                         .OrderBy(unit => unit.Unit.InstanceId.Value, StringComparer.Ordinal).ToArray())
            {
                BattleUnitState target = next.Units[original.Unit.InstanceId];
                BattleUnitState damaged = target.WithHealth(target.CurrentHealth - secondaryDamage);
                next = next.WithUnit(damaged);
                events.Add(new DamageAppliedEvent(actor.Unit.InstanceId, target.Unit.InstanceId, command.Definition.ContentId, target.CurrentHealth - damaged.CurrentHealth, damaged.CurrentHealth));
                next = BattleDefeatResolver.Apply(next, target, damaged, events);
            }
        }
        return new BattleTransition(next, events);
    }

    private BattleTransition ApplyMultiStab(BattleState state, BattleUnitState actor, UseSkillCommand command)
    {
        int expected = command.Definition.ExecutionProfile.OrderedTargetCount > 0 ? command.Definition.ExecutionProfile.OrderedTargetCount : command.Definition.Level >= 2 ? 4 : 3;
        if (command.OrderedTargetIds.Count != expected) return Reject(state, actor, "ordered_targets_required");
        BattleState next = state.WithUnit(actor.WithMana(actor.CurrentMana - command.Definition.ManaCost).WithSuccessfulSkillUse(command.Definition.ContentId));
        var events = new List<BattleEvent> { new SkillUsedEvent(actor.Unit.InstanceId, command.OrderedTargetIds[0], command.Definition.ContentId) };
        if (command.Definition.ManaCost > 0) events.Add(new ManaSpentEvent(actor.Unit.InstanceId, command.Definition.ContentId, command.Definition.ManaCost, next.Units[actor.Unit.InstanceId].CurrentMana));
        foreach (UnitInstanceId targetId in command.OrderedTargetIds)
        {
            if (!next.TryGetUnit(targetId, out BattleUnitState? target) || target is null || !target.IsAlive || !IsHostile(next, actor, target)) return Reject(state, actor, "invalid_ordered_target");
            int before = target.CurrentHealth;
            BattleUnitState damaged = target.WithHealth(before - command.Definition.Damage);
            next = next.WithUnit(damaged);
            events.Add(new DamageAppliedEvent(actor.Unit.InstanceId, targetId, command.Definition.ContentId, before - damaged.CurrentHealth, damaged.CurrentHealth));
            next = BattleDefeatResolver.Apply(next, target, damaged, events);
        }
        return new BattleTransition(next, events);
    }

    private static int Manhattan(GridPoint left, GridPoint right) => Math.Abs(left.X - right.X) + Math.Abs(left.Y - right.Y);

    private static bool IsHostile(BattleState state, BattleUnitState actor, BattleUnitState target)
    {
        if (actor.DemonboundState?.IsPossessed != true)
            return target.Unit.PlayerNumber != actor.Unit.PlayerNumber;
        bool livingAlly = state.Units.Values.Any(unit => unit.IsAlive &&
            unit.Unit.InstanceId != actor.Unit.InstanceId &&
            unit.Unit.PlayerNumber == actor.Unit.PlayerNumber);
        return target.Unit.InstanceId != actor.Unit.InstanceId &&
            (livingAlly
                ? target.Unit.PlayerNumber == actor.Unit.PlayerNumber
                : target.Unit.PlayerNumber != actor.Unit.PlayerNumber);
    }

    private IEnumerable<BattleUnitState> ResolveTargets(BattleState state, BattleUnitState actor, UseSkillCommand command)
    {
        SkillDefinition skill = command.Definition;
        if (skill.ExecutionKind == SkillExecutionKind.Bane)
        {
            int dx = Math.Sign(command.TargetCell.X - actor.Unit.Position.X);
            int dy = Math.Sign(command.TargetCell.Y - actor.Unit.Position.Y);
            int selectedDistance = Manhattan(command.TargetCell, actor.Unit.Position);
            if (Math.Abs(dx) + Math.Abs(dy) != 1 || selectedDistance != 1) yield break;
            GridPoint first = new(actor.Unit.Position.X + dx, actor.Unit.Position.Y + dy);
            GridPoint second = new(actor.Unit.Position.X + dx * 2, actor.Unit.Position.Y + dy * 2);
            foreach (BattleUnitState unit in state.Units.Values
                         .Where(unit => unit.IsAlive && IsHostile(state, actor, unit) &&
                             (unit.Unit.Position == first || unit.Unit.Position == second))
                         .OrderBy(unit => Manhattan(unit.Unit.Position, actor.Unit.Position))
                         .ThenBy(unit => unit.Unit.InstanceId.Value, StringComparer.Ordinal))
                yield return unit;
            yield break;
        }
        if (skill.ExecutionKind is SkillExecutionKind.Cleave or SkillExecutionKind.InfernalBlast)
        {
            int dx = Math.Sign(command.TargetCell.X - actor.Unit.Position.X);
            int dy = Math.Sign(command.TargetCell.Y - actor.Unit.Position.Y);
            if (Math.Abs(dx) + Math.Abs(dy) != 1) yield break;
            int depth = skill.ExecutionKind == SkillExecutionKind.Cleave ? Math.Min(2, skill.Level) : 4;
            int halfWidth = skill.ExecutionKind == SkillExecutionKind.Cleave || skill.Level >= 3 ? 1 : 0;
            HashSet<GridPoint> cells = Enumerable.Range(1, depth)
                .SelectMany(step => Enumerable.Range(-halfWidth, halfWidth * 2 + 1)
                    .Select(offset => new GridPoint(actor.Unit.Position.X + dx * step - dy * offset,
                        actor.Unit.Position.Y + dy * step + dx * offset)))
                .Where(state.Board.Contains).ToHashSet();
            foreach (BattleUnitState unit in state.Units.Values
                         .Where(unit => unit.IsAlive && IsHostile(state, actor, unit) &&
                             cells.Contains(unit.Unit.Position))
                         .OrderBy(unit => unit.Unit.InstanceId.Value, StringComparer.Ordinal))
                yield return unit;
            yield break;
        }
        if (skill.ExecutionKind == SkillExecutionKind.Hellfire)
        {
            foreach (BattleUnitState unit in state.Units.Values
                         .Where(unit => unit.IsAlive && IsHostile(state, actor, unit) &&
                             Manhattan(unit.Unit.Position, actor.Unit.Position) is >= 1 and <= 2)
                         .OrderBy(unit => unit.Unit.InstanceId.Value, StringComparer.Ordinal))
                yield return unit;
            yield break;
        }
        int distance = Math.Abs(actor.Unit.Position.X - command.TargetCell.X) + Math.Abs(actor.Unit.Position.Y - command.TargetCell.Y);
        if (!state.Board.Contains(command.TargetCell) || distance < skill.MinRange || distance > skill.MaxRange) yield break;
        if (skill.RequiresLineOfSight && !_lineOfSight.Trace(state.Board, actor.Unit.Position, command.TargetCell,
                LivingBlockers(state, actor.Unit.InstanceId, command.TargetCell, skill.ExecutionKind)).IsClear) yield break;
        if (skill.AreaRadius > 0 && skill.ExecutionKind is SkillExecutionKind.AreaBlast or SkillExecutionKind.Fireball or SkillExecutionKind.AmplifyDamage or SkillExecutionKind.FearCurse or SkillExecutionKind.PoisonSpear)
        {
            BattleUnitState[] area = state.Units.Values.Where(unit => unit.IsAlive && IsHostile(state, actor, unit))
                .Where(unit => skill.ExecutionProfile.AreaShape == "square"
                    ? Math.Max(Math.Abs(unit.Unit.Position.X-command.TargetCell.X), Math.Abs(unit.Unit.Position.Y-command.TargetCell.Y)) <= skill.AreaRadius
                    : Math.Abs(unit.Unit.Position.X-command.TargetCell.X)+Math.Abs(unit.Unit.Position.Y-command.TargetCell.Y) <= skill.AreaRadius)
                .OrderBy(unit => unit.Unit.InstanceId.Value, StringComparer.Ordinal).ToArray();
            if (skill.ExecutionKind == SkillExecutionKind.Fireball && command.TargetId is UnitInstanceId fireballTarget)
            {
                BattleUnitState? primary = area.FirstOrDefault(unit => unit.Unit.InstanceId == fireballTarget);
                if (primary is null) yield break;
                yield return primary;
                foreach (BattleUnitState unit in area.Where(unit => unit.Unit.InstanceId != fireballTarget)) yield return unit;
                yield break;
            }
            foreach (BattleUnitState unit in area) yield return unit;
            yield break;
        }
        if (skill.UsesLineTargeting)
        {
            int dx = command.TargetCell.X - actor.Unit.Position.X;
            int dy = command.TargetCell.Y - actor.Unit.Position.Y;
            if (skill.ExecutionKind == SkillExecutionKind.Thrust && dx != 0 && dy != 0) yield break;
            if (skill.ExecutionKind != SkillExecutionKind.Thrust &&
                (command.TargetId is not UnitInstanceId selectedId ||
                 !state.TryGetUnit(selectedId, out BattleUnitState? selectedUnit) ||
                 selectedUnit is null || !selectedUnit.IsAlive ||
                 !IsHostile(state, actor, selectedUnit) ||
                 selectedUnit.Unit.Position != command.TargetCell))
                yield break;
            int selectedDistance = Math.Abs(dx) + Math.Abs(dy);
            IEnumerable<BattleUnitState> ray = state.Units.Values.Where(unit => unit.IsAlive && IsHostile(state, actor, unit))
                .Where(unit => IsOnSelectedRay(actor.Unit.Position, command.TargetCell, unit.Unit.Position))
                .Where(unit => Math.Abs(unit.Unit.Position.X - actor.Unit.Position.X) + Math.Abs(unit.Unit.Position.Y - actor.Unit.Position.Y) <= selectedDistance)
                .OrderBy(unit => Math.Abs(unit.Unit.Position.X - actor.Unit.Position.X) + Math.Abs(unit.Unit.Position.Y - actor.Unit.Position.Y));
            IEnumerable<BattleUnitState> resolvedRay = skill.ExecutionKind == SkillExecutionKind.Thrust ||
                skill.ExecutionKind == SkillExecutionKind.BoneSpear && skill.ExecutionProfile.PierceAll
                    ? ray
                    : ray.Take(1);
            foreach (BattleUnitState unit in resolvedRay) yield return unit;
            yield break;
        }
        if (command.TargetId is UnitInstanceId id && state.TryGetUnit(id, out BattleUnitState? target) && target is not null && target.Unit.Position == command.TargetCell) yield return target;
    }

    private static bool IsOnSelectedRay(GridPoint origin, GridPoint selected, GridPoint candidate)
    {
        int selectedX = selected.X - origin.X;
        int selectedY = selected.Y - origin.Y;
        int candidateX = candidate.X - origin.X;
        int candidateY = candidate.Y - origin.Y;
        int cross = candidateX * selectedY - candidateY * selectedX;
        int dot = candidateX * selectedX + candidateY * selectedY;
        return cross == 0 && dot > 0;
    }

    public static IReadOnlyDictionary<GridPoint, LineOfSightBlocker> LivingBlockers(BattleState state, UnitInstanceId actorId, GridPoint targetCell, SkillExecutionKind executionKind) =>
        executionKind == SkillExecutionKind.BoneSpear
            ? new Dictionary<GridPoint, LineOfSightBlocker>()
            : state.Units.Values
            .Where(unit => unit.IsAlive && unit.Unit.InstanceId != actorId && unit.Unit.Position != targetCell)
            .ToDictionary(unit => unit.Unit.Position,
                unit => new LineOfSightBlocker(LineOfSightBlockingKind.LivingUnit, unit.Unit.InstanceId));

    private static StatusDefinition StatusFor(SkillDefinition skill, ContentId statusId) => skill.ExecutionKind switch
    {
        SkillExecutionKind.Fireball or SkillExecutionKind.FireDemonAttack => new StatusDefinition(statusId, "Ignite", 2, true, StatusPolarity.Harmful, StatusEffectKind.Burning, StatusTriggerTiming.TurnStart, StatusRefreshStrategy.AddStacks, damagePerTurn: 1, elementKind: StatusElementKind.Fire),
        SkillExecutionKind.IceBolt => new StatusDefinition(statusId, "Slow", 1, true, StatusPolarity.Harmful, StatusEffectKind.Slow, StatusTriggerTiming.None, StatusRefreshStrategy.RefreshDuration, speedModifier: -2f, elementKind: StatusElementKind.Ice),
        SkillExecutionKind.Lightning or SkillExecutionKind.Hellfire => new StatusDefinition(statusId, "Stun", 1, true, StatusPolarity.Harmful, StatusEffectKind.Stun, StatusTriggerTiming.None, StatusRefreshStrategy.RefreshDuration, elementKind: StatusElementKind.Lightning),
        SkillExecutionKind.AmplifyDamage => new StatusDefinition(statusId, "CurseDamageAmplifier", 5, true, StatusPolarity.Harmful, StatusEffectKind.CurseDamageAmplifier, StatusTriggerTiming.None, StatusRefreshStrategy.RefreshDuration, curseCategory: "damage-taken"),
        SkillExecutionKind.FearCurse => new StatusDefinition(statusId, "Fear", Math.Max(1, skill.StatusDuration), true, StatusPolarity.Harmful, StatusEffectKind.Fear, StatusTriggerTiming.None, StatusRefreshStrategy.RefreshDuration, curseCategory: "fear"),
        SkillExecutionKind.Bane => new StatusDefinition(statusId, "Bane", Math.Max(1, skill.StatusDuration), true,
            StatusPolarity.Harmful, StatusEffectKind.DamageOutputReduction, StatusTriggerTiming.None,
            StatusRefreshStrategy.RefreshDuration),
        _ => throw new InvalidOperationException($"Unsupported status contract for {skill.ExecutionKind}.")
    };

    private static BattleTransition Reject(BattleState state, BattleUnitState actor, string reason) => new(state, new BattleEvent[] { new CommandRejectedEvent(actor.Unit.InstanceId, reason) });

    public static string? UsageFailure(BattleUnitState actor, SkillDefinition skill)
    {
        int uses = actor.SuccessfulUsesOf(skill.ContentId);
        if (skill.IsBasicAbility && uses >= 1) return "basic_ability_already_used";
        if (!skill.IsBasicAbility && skill.MaxUsesPerTurn > 0 && uses >= skill.MaxUsesPerTurn)
            return "ability_use_limit_reached";
        return null;
    }

    /// <summary>Returns the stable reason why a skill cannot currently be selected before targeting.</summary>
    public static string? AvailabilityFailure(BattleUnitState actor, SkillDefinition skill)
    {
        string? usageFailure = UsageFailure(actor, skill);
        if (usageFailure is not null) return usageFailure;
        return actor.CurrentMana < skill.ManaCost ? "insufficient_mana" : null;
    }
}
