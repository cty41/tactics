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
    private readonly ILineOfSightService _lineOfSight;
    private readonly StatusRuntimeService _statuses;

    public SkillRuntimeService(ILineOfSightService? lineOfSight = null, StatusRuntimeService? statuses = null)
    {
        _lineOfSight = lineOfSight ?? new SupercoverLineOfSight();
        _statuses = statuses ?? new StatusRuntimeService();
    }

    public BattleTransition Apply(BattleState state, BattleUnitState actor, UseSkillCommand command)
    {
        SkillDefinition skill = command.Definition;
        if (skill.IsPassive) return ApplyPassive(state, actor, skill);
        if (actor.CurrentMana < skill.ManaCost) return Reject(state, actor, "insufficient_mana");
        if (skill.ExecutionKind == SkillExecutionKind.PickupSpear) return ApplyPickup(state, actor, command);
        if (skill.ExecutionKind == SkillExecutionKind.SummonSkeleton) return ApplySummon(state, actor, command);

        BattleUnitState[] targets = ResolveTargets(state, actor, command).ToArray();
        if (targets.Length == 0) return Reject(state, actor, "no_valid_target");
        var events = new List<BattleEvent> { new SkillUsedEvent(actor.Unit.InstanceId, targets[0].Unit.InstanceId, skill.ContentId) };
        BattleState next = state;
        BattleUnitState updatedActor = actor.WithMana(actor.CurrentMana - skill.ManaCost);
        if (skill.ManaCost > 0) events.Add(new ManaSpentEvent(actor.Unit.InstanceId, skill.ContentId, skill.ManaCost, updatedActor.CurrentMana));
        next = next.WithUnit(updatedActor);

        foreach (BattleUnitState originalTarget in targets)
        {
            BattleUnitState target = next.Units[originalTarget.Unit.InstanceId];
            if (target.Unit.PlayerNumber == actor.Unit.PlayerNumber) return Reject(state, actor, "target_not_enemy");
            if (!target.IsAlive) return Reject(state, actor, "target_defeated");
            bool dodged = false;
            bool critical = false;
            if (skill.Damage > 0 || skill.ExecutionKind is SkillExecutionKind.MagicAttack or SkillExecutionKind.MeleeAttack)
            {
                var random = new DeterministicRandom(next.RandomState);
                int roll = random.NextInt(100);
                dodged = target.HasCombatTechniquesLevelOne && roll < 30;
                critical = !dodged && (_statuses.EvaluateBeforeAttack(target).ForceCritical || roll >= 90);
                events.Add(new CombatRollResolvedEvent(actor.Unit.InstanceId, target.Unit.InstanceId, skill.ContentId, roll, target.HasCombatTechniquesLevelOne ? 30 : 0, dodged ? "dodge" : critical ? "critical" : "hit", random.State));
                next = next.WithRandomState(random.State);
            }

            int rawDamage = skill.ExecutionKind switch
            {
                SkillExecutionKind.MagicAttack => actor.MagicalAttack,
                SkillExecutionKind.MeleeAttack => actor.PhysicalAttack,
                _ => skill.Damage
            };
            if (critical) rawDamage = checked(rawDamage * 2);
            StatusDamagePolicy damagePolicy = _statuses.EvaluateDamageTaken(target, actor, skill.MaxRange > 1);
            int damage = dodged ? 0 : (int)MathF.Round(rawDamage * damagePolicy.DamageMultiplier, MidpointRounding.AwayFromZero);
            int health = Math.Max(0, target.CurrentHealth - damage);
            target = target.WithHealth(health);
            events.Add(new DamageAppliedEvent(actor.Unit.InstanceId, target.Unit.InstanceId, skill.ContentId, originalTarget.CurrentHealth - health, health));

            if (target.IsAlive && skill.StatusContentId is ContentId statusId)
            {
                StatusDefinition definition = StatusFor(skill, statusId);
                StatusApplicationResult application = _statuses.Apply(target, definition, actor.Unit.InstanceId, skill.StatusDuration);
                target = application.Unit;
                events.Add(new StatusAppliedEvent(actor.Unit.InstanceId, target.Unit.InstanceId, statusId, application.AppliedStatus.RemainingTurns));
            }
            if (!target.IsAlive) events.Add(new UnitDefeatedEvent(target.Unit.InstanceId));
            next = next.WithUnit(target);
        }
        events.Add(new SemanticCueEmittedEvent(actor.Unit.InstanceId, targets[0].Unit.InstanceId, skill.ContentId, "resolution"));
        return new BattleTransition(next, events);
    }

    private BattleTransition ApplyPassive(BattleState state, BattleUnitState actor, SkillDefinition skill)
    {
        if (skill.ExecutionKind != SkillExecutionKind.CombatTechniques) return Reject(state, actor, "unsupported_passive");
        BattleUnitState updated = actor.WithCombatTechniquesLevelOne(true);
        return new BattleTransition(state.WithUnit(updated), new BattleEvent[]
        {
            new SkillUsedEvent(actor.Unit.InstanceId, actor.Unit.InstanceId, skill.ContentId),
            new SemanticCueEmittedEvent(actor.Unit.InstanceId, actor.Unit.InstanceId, skill.ContentId, "passive-enabled")
        });
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
        if (!state.Corpses.Contains(cell)) return Reject(state, actor, "corpse_not_found");
        if (state.Units.Values.Any(unit => unit.IsAlive && unit.Unit.Position == cell)) return Reject(state, actor, "corpse_cell_occupied");
        if (actor.CurrentMana < command.Definition.ManaCost) return Reject(state, actor, "insufficient_mana");
        int ordinal = state.Units.Values.Where(unit => unit.SummonOwnerId == actor.Unit.InstanceId).Select(unit => unit.Unit.SpawnOrdinal).DefaultIfEmpty(-1).Max() + 1;
        var summonId = new UnitInstanceId($"{actor.Unit.InstanceId.Value}.skeleton.{ordinal}");
        var facts = new UnitState(summonId, SkeletonDefinitionId, cell, 3, 10f, actor.Unit.PlayerNumber, ordinal);
        var summon = new BattleUnitState(facts, 12, 12, maxMana: 0, currentMana: 0, physicalAttack: 2, magicalAttack: 0, summonOwnerId: actor.Unit.InstanceId, canReceiveStandardHealing: false);
        BattleUnitState updatedActor = actor.WithMana(actor.CurrentMana - command.Definition.ManaCost);
        BattleState next = state.WithUnit(updatedActor).WithoutCorpse(cell).WithSummon(summon);
        var events = new List<BattleEvent> { new SkillUsedEvent(actor.Unit.InstanceId, actor.Unit.InstanceId, command.Definition.ContentId) };
        if (command.Definition.ManaCost > 0) events.Add(new ManaSpentEvent(actor.Unit.InstanceId, command.Definition.ContentId, command.Definition.ManaCost, updatedActor.CurrentMana));
        events.Add(new CorpseConsumedEvent(cell, actor.Unit.InstanceId));
        events.Add(new UnitSummonedEvent(actor.Unit.InstanceId, summonId, SkeletonDefinitionId, cell));
        events.Add(new SemanticCueEmittedEvent(actor.Unit.InstanceId, summonId, command.Definition.ContentId, "summoned"));
        return new BattleTransition(next, events);
    }

    private IEnumerable<BattleUnitState> ResolveTargets(BattleState state, BattleUnitState actor, UseSkillCommand command)
    {
        SkillDefinition skill = command.Definition;
        int distance = Math.Abs(actor.Unit.Position.X - command.TargetCell.X) + Math.Abs(actor.Unit.Position.Y - command.TargetCell.Y);
        if (!state.Board.Contains(command.TargetCell) || distance < skill.MinRange || distance > skill.MaxRange) yield break;
        if (skill.RequiresLineOfSight && !_lineOfSight.HasLineOfSight(state.Board, actor.Unit.Position, command.TargetCell)) yield break;
        if (skill.ExecutionKind == SkillExecutionKind.AreaBlast)
        {
            BattleUnitState[] area = state.Units.Values.Where(unit => unit.IsAlive && unit.Unit.PlayerNumber != actor.Unit.PlayerNumber)
                .Where(unit => Math.Abs(unit.Unit.Position.X-command.TargetCell.X)+Math.Abs(unit.Unit.Position.Y-command.TargetCell.Y) <= 2)
                .OrderBy(unit => unit.Unit.InstanceId.Value, StringComparer.Ordinal).ToArray();
            foreach (BattleUnitState unit in area) yield return unit;
            yield break;
        }
        if (skill.UsesLineTargeting)
        {
            int dx = command.TargetCell.X - actor.Unit.Position.X;
            int dy = command.TargetCell.Y - actor.Unit.Position.Y;
            if (dx != 0 && dy != 0) yield break;
            int sx = Math.Sign(dx); int sy = Math.Sign(dy);
            IEnumerable<BattleUnitState> ray = state.Units.Values.Where(unit => unit.IsAlive && unit.Unit.PlayerNumber != actor.Unit.PlayerNumber)
                .Where(unit => (sx == 0 ? unit.Unit.Position.X == actor.Unit.Position.X : unit.Unit.Position.Y == actor.Unit.Position.Y))
                .Where(unit => Math.Sign(unit.Unit.Position.X - actor.Unit.Position.X) == sx && Math.Sign(unit.Unit.Position.Y - actor.Unit.Position.Y) == sy)
                .Where(unit => Math.Abs(unit.Unit.Position.X - actor.Unit.Position.X) + Math.Abs(unit.Unit.Position.Y - actor.Unit.Position.Y) <= skill.MaxRange)
                .OrderBy(unit => Math.Abs(unit.Unit.Position.X - actor.Unit.Position.X) + Math.Abs(unit.Unit.Position.Y - actor.Unit.Position.Y));
            foreach (BattleUnitState unit in skill.ExecutionKind == SkillExecutionKind.Thrust ? ray : ray.Take(1)) yield return unit;
            yield break;
        }
        if (command.TargetId is UnitInstanceId id && state.TryGetUnit(id, out BattleUnitState? target) && target is not null && target.Unit.Position == command.TargetCell) yield return target;
    }

    private static StatusDefinition StatusFor(SkillDefinition skill, ContentId statusId) => skill.ExecutionKind switch
    {
        SkillExecutionKind.Fireball => new StatusDefinition(statusId, "Ignite", 2, true, StatusPolarity.Harmful, StatusEffectKind.Burning, StatusTriggerTiming.TurnStart, StatusRefreshStrategy.AddStacks, damagePerTurn: 1, elementKind: StatusElementKind.Fire),
        SkillExecutionKind.IceBolt => new StatusDefinition(statusId, "Slow", 1, true, StatusPolarity.Harmful, StatusEffectKind.Slow, StatusTriggerTiming.None, StatusRefreshStrategy.RefreshDuration, speedModifier: -2f, elementKind: StatusElementKind.Ice),
        SkillExecutionKind.AmplifyDamage => new StatusDefinition(statusId, "CurseDamageAmplifier", 5, true, StatusPolarity.Harmful, StatusEffectKind.CurseDamageAmplifier, StatusTriggerTiming.None, StatusRefreshStrategy.RefreshDuration, curseCategory: "damage-taken"),
        _ => throw new InvalidOperationException($"Unsupported status contract for {skill.ExecutionKind}.")
    };

    private static BattleTransition Reject(BattleState state, BattleUnitState actor, string reason) => new(state, new BattleEvent[] { new CommandRejectedEvent(actor.Unit.InstanceId, reason) });
}
