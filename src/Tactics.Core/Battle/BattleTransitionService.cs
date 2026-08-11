using Tactics.Core.Board;
using Tactics.Core.Combat;
using Tactics.Core.Content;
using Tactics.Core.Items;
using Tactics.Core.Pathfinding;
using Tactics.Core.Statuses;
using Tactics.Core.Skills;
using Tactics.Core.Units;

namespace Tactics.Core.Battle;

/// <summary>
/// Applies typed battle commands to immutable battle state.
/// </summary>
/// <remarks>
/// Validation, state mutation, and event ordering live here rather than in UI or presentation adapters.
/// Poison Spear semantics are bound to the frozen Unity AssetDatabase export: mana is committed only after
/// successful resolution, Poison uses AddDuration, ticks at turn start, and decrements at turn end.
/// </remarks>
public sealed class BattleTransitionService
{
    /// <summary>
    /// Identifies the versioned normalized command/state/event contract introduced by the migration.
    /// </summary>
    public const string ContractId = "battle-transition-v5";

    private readonly IPathfinder _pathfinder;
    private readonly PoisonSpearResolver _poisonSpearResolver;
    private readonly StatusRuntimeService _statusRuntime;
    private readonly SkillRuntimeService _skillRuntime;

    /// <summary>
    /// Creates the deterministic transition service.
    /// </summary>
    /// <param name="pathfinder">Optional deterministic path implementation.</param>
    /// <param name="lineOfSight">Optional deterministic line-of-sight implementation.</param>
    public BattleTransitionService(
        IPathfinder? pathfinder = null,
        ILineOfSightService? lineOfSight = null,
        StatusRuntimeService? statusRuntime = null)
    {
        _pathfinder = pathfinder ?? new DeterministicDijkstraPathfinder();
        _poisonSpearResolver = new PoisonSpearResolver(lineOfSight);
        _statusRuntime = statusRuntime ?? new StatusRuntimeService();
        _skillRuntime = new SkillRuntimeService(lineOfSight, _statusRuntime);
    }

    /// <summary>
    /// Applies one command and returns a complete state/event transition.
    /// </summary>
    /// <param name="state">Source battle snapshot.</param>
    /// <param name="command">Typed engine-neutral command.</param>
    /// <returns>The immutable transition result.</returns>
    public BattleTransition Apply(BattleState state, BattleCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);

        if (!state.TryGetUnit(command.ActorId, out BattleUnitState? actor) || actor is null)
            return Rejected(state, command.ActorId, "actor_not_found");
        if (!actor.IsAlive && command is not EndTurnCommand)
            return Rejected(state, command.ActorId, "actor_defeated");
        if (state.ActiveUnitId != command.ActorId)
            return Rejected(state, command.ActorId, "not_active_unit");
        if (command is not EndTurnCommand && !_statusRuntime.CanAct(actor))
            return Rejected(state, command.ActorId, "status_prevents_action");

        return command switch
        {
            MoveUnitCommand move => ApplyMove(state, actor, move),
            UsePoisonSpearCommand poisonSpear => ApplyPoisonSpear(state, actor, poisonSpear),
            UseSkillCommand skill => ApplySkill(state, actor, skill),
            UseConsumableCommand consumable => ApplyConsumable(state, actor, consumable),
            EndTurnCommand endTurn => ApplyEndTurn(state, endTurn),
            _ => Rejected(state, command.ActorId, "unsupported_command")
        };
    }

    private BattleTransition ApplySkill(BattleState state, BattleUnitState actor, UseSkillCommand command)
    {
        if (command.Definition.ExecutionKind != SkillExecutionKind.PoisonSpear)
            return _skillRuntime.Apply(state, actor, command);
        string? usageFailure = SkillRuntimeService.UsageFailure(actor, command.Definition);
        if (usageFailure is not null)
            return Rejected(state, command.ActorId, usageFailure);
        if (command.TargetId is not UnitInstanceId targetId)
            return Rejected(state, command.ActorId, "target_not_found");
        BattleTransition transition = ApplyPoisonSpear(state, actor, new UsePoisonSpearCommand(
            command.ActorId,
            targetId,
            new PoisonSpearDefinition(
                command.Definition.ContentId,
                command.Definition.MaxRange,
                command.Definition.Damage,
                command.Definition.StatusDuration,
                command.Definition.StatusContentId,
                poisonDamagePerTurn: 2,
                manaCost: command.Definition.ManaCost,
                dropSearchRadius: 3)));
        if (transition.Events.OfType<CommandRejectedEvent>().Any()) return transition;
        BattleUnitState usedActor = transition.State.Units[actor.Unit.InstanceId]
            .WithSuccessfulSkillUse(command.Definition.ContentId);
        return new BattleTransition(transition.State.WithUnit(usedActor), transition.Events);
    }

    private BattleTransition ApplyMove(BattleState state, BattleUnitState actor, MoveUnitCommand command)
    {
        if (actor.HasMovedThisTurn)
            return Rejected(state, command.ActorId, "move_already_used");
        if (!state.Board.Contains(command.Destination))
            return Rejected(state, command.ActorId, "destination_out_of_bounds");
        if (state.Units.Values.Any(unit => unit.IsAlive && unit.Unit.Position == command.Destination))
            return Rejected(state, command.ActorId, "destination_occupied");

        IReadOnlyList<GridPoint> path = _pathfinder.FindPath(
            state.CreateMovementBoard(command.ActorId),
            actor.Unit.Position,
            command.Destination);
        if (path.Count == 0 && actor.Unit.Position != command.Destination)
            return Rejected(state, command.ActorId, "path_not_found");
        if (path.Count > actor.Unit.MoveRange)
            return Rejected(state, command.ActorId, "path_exceeds_move_range");

        GridPoint origin = actor.Unit.Position;
        BattleUnitState moved = actor.WithPosition(command.Destination, hasMovedThisTurn: true);
        BattleState nextState = state.WithUnit(moved);
        return new BattleTransition(nextState, new BattleEvent[]
        {
            new UnitMovedEvent(command.ActorId, origin, command.Destination, Array.AsReadOnly(path.ToArray()))
        });
    }

    private BattleTransition ApplyPoisonSpear(
        BattleState state,
        BattleUnitState actor,
        UsePoisonSpearCommand command)
    {
        if (!state.TryGetUnit(command.TargetId, out BattleUnitState? target) || target is null)
            return Rejected(state, command.ActorId, "target_not_found");
        if (state.TryGetDroppedSpear(command.ActorId, out _))
            return Rejected(state, command.ActorId, "spear_not_held");
        if (actor.CurrentMana < command.Definition.ManaCost)
            return Rejected(state, command.ActorId, "insufficient_mana");

        GridPoint? dropCell = FindSpearDropCell(
            state,
            actor.Unit.Position,
            target.Unit.Position,
            command.Definition.DropSearchRadius);
        if (dropCell is null)
            return Rejected(state, command.ActorId, "no_legal_spear_drop");

        ActionResult action = _poisonSpearResolver.Resolve(
            state.Board,
            actor.Unit,
            target.Unit,
            command.Definition);
        if (!action.Succeeded)
            return Rejected(state, command.ActorId, action.FailureReason);

        int healthAfterDamage = Math.Max(0, target.CurrentHealth - action.Damage);
        int appliedDamage = target.CurrentHealth - healthAfterDamage;
        BattleUnitState updatedTarget = target.WithHealth(healthAfterDamage);
        BattleUnitState updatedActor = actor.WithMana(actor.CurrentMana - command.Definition.ManaCost);
        var events = new List<BattleEvent>
        {
            new SkillUsedEvent(command.ActorId, command.TargetId, command.Definition.SkillId),
        };
        if (command.Definition.ManaCost > 0)
        {
            events.Add(new ManaSpentEvent(
                command.ActorId,
                command.Definition.SkillId,
                command.Definition.ManaCost,
                updatedActor.CurrentMana));
        }
        events.Add(new DamageAppliedEvent(
                command.ActorId,
                command.TargetId,
                command.Definition.SkillId,
                appliedDamage,
                healthAfterDamage));

        if (updatedTarget.IsAlive && action.PoisonTurns > 0)
        {
            var poison = new StatusDefinition(
                command.Definition.PoisonStatusId,
                "Poison",
                action.PoisonTurns,
                canAct: true,
                StatusPolarity.Harmful,
                StatusEffectKind.Poison,
                StatusTriggerTiming.TurnStart,
                StatusRefreshStrategy.AddDuration,
                damagePerTurn: command.Definition.PoisonDamagePerTurn);
            StatusApplicationResult application = _statusRuntime.Apply(
                updatedTarget,
                poison,
                command.ActorId,
                action.PoisonTurns);
            updatedTarget = application.Unit;
            events.Add(new StatusAppliedEvent(
                command.ActorId,
                command.TargetId,
                command.Definition.PoisonStatusId,
                application.AppliedStatus.RemainingTurns));
        }

        events.Add(new SpearDroppedEvent(command.ActorId, dropCell.Value));

        BattleState nextState = state
            .WithUnit(updatedActor)
            .WithUnit(updatedTarget)
            .WithDroppedSpear(command.ActorId, dropCell.Value);
        nextState = BattleDefeatResolver.Apply(nextState, target, updatedTarget, events);
        return new BattleTransition(nextState, events);
    }

    private BattleTransition ApplyConsumable(
        BattleState state,
        BattleUnitState actor,
        UseConsumableCommand command)
    {
        if (!state.TryGetUnit(command.TargetId, out BattleUnitState? target) || target is null)
            return Rejected(state, command.ActorId, "target_not_found");
        if (!target.IsAlive)
            return Rejected(state, command.ActorId, "target_defeated");
        if (!actor.Consumables.TryGetValue(command.ItemInstanceId, out BattleConsumableState? item))
            return Rejected(state, command.ActorId, "consumable_not_carried");
        if (item.DefinitionId != command.Definition.ContentId)
            return Rejected(state, command.ActorId, "consumable_definition_mismatch");
        if (item.MaxCharges != command.Definition.MaxCharges)
            return Rejected(state, command.ActorId, "consumable_charge_contract_mismatch");
        if (item.RemainingCharges <= 0)
            return Rejected(state, command.ActorId, "consumable_depleted");
        if (actor.LastSuccessfulConsumableUseRound == state.Round)
            return Rejected(state, command.ActorId, "consumable_already_used_this_round");
        if (actor.Unit.PlayerNumber != target.Unit.PlayerNumber)
            return Rejected(state, command.ActorId, "consumable_target_not_ally");
        if (command.Definition.TargetMode == ConsumableTargetMode.Self && command.TargetId != command.ActorId)
            return Rejected(state, command.ActorId, "consumable_target_not_self");
        if (Manhattan(actor.Unit.Position, target.Unit.Position) > command.Definition.MaxRange)
            return Rejected(state, command.ActorId, "consumable_target_out_of_range");
        if (command.Definition.EffectKind == ConsumableEffectKind.RestoreHealth && !target.CanReceiveStandardHealing)
            return Rejected(state, command.ActorId, "target_rejects_standard_healing");

        var events = new List<BattleEvent>
        {
            new ConsumableUsedEvent(
                command.ActorId,
                command.TargetId,
                command.ItemInstanceId,
                command.Definition.ContentId)
        };
        BattleUnitState updatedTarget = target;
        switch (command.Definition.EffectKind)
        {
            case ConsumableEffectKind.RestoreHealth:
            {
                int before = updatedTarget.CurrentHealth;
                updatedTarget = updatedTarget.WithHealth(checked(before + command.Definition.Magnitude));
                events.Add(new HealthRestoredEvent(
                    command.ActorId,
                    command.TargetId,
                    command.Definition.ContentId,
                    updatedTarget.CurrentHealth - before,
                    updatedTarget.CurrentHealth));
                break;
            }
            case ConsumableEffectKind.RestoreMana:
            {
                int before = updatedTarget.CurrentMana;
                updatedTarget = updatedTarget.WithMana(checked(before + command.Definition.Magnitude));
                events.Add(new ManaRestoredEvent(
                    command.ActorId,
                    command.TargetId,
                    command.Definition.ContentId,
                    updatedTarget.CurrentMana - before,
                    updatedTarget.CurrentMana));
                break;
            }
            case ConsumableEffectKind.RemoveHarmfulBuffs:
                updatedTarget = _statusRuntime.RemoveHarmful(updatedTarget, out IReadOnlyList<ContentId> removed);
                events.Add(new StatusesCleansedEvent(
                    command.ActorId,
                    command.TargetId,
                    command.Definition.ContentId,
                    removed));
                break;
            default:
                return Rejected(state, command.ActorId, "unsupported_consumable_effect");
        }

        BattleConsumableState updatedItem = item.WithRemainingCharges(item.RemainingCharges - 1);
        BattleUnitState updatedActor = command.TargetId == command.ActorId ? updatedTarget : actor;
        updatedActor = updatedActor
            .WithConsumable(updatedItem)
            .WithSuccessfulConsumableUse(state.Round);
        events.Add(new ConsumableChargesChangedEvent(
            command.ActorId,
            command.ItemInstanceId,
            updatedItem.RemainingCharges));

        BattleState nextState = WithUnitAndInitiative(state, actor, updatedActor);
        if (command.TargetId != command.ActorId)
            nextState = WithUnitAndInitiative(nextState, target, updatedTarget);
        return new BattleTransition(nextState, events);
    }

    private static BattleState WithUnitAndInitiative(
        BattleState state,
        BattleUnitState previous,
        BattleUnitState updated) =>
        previous.Unit.Initiative == updated.Unit.Initiative
            ? state.WithUnit(updated)
            : state.WithInitiativeChanged(updated);

    private static GridPoint? FindSpearDropCell(
        BattleState state,
        GridPoint ownerCell,
        GridPoint targetCell,
        int radius)
    {
        HashSet<GridPoint> terrainReachable = CollectTerrainReachable(state.Board, ownerCell);
        int awayX = Math.Sign(targetCell.X - ownerCell.X);
        int awayY = Math.Sign(targetCell.Y - ownerCell.Y);
        HashSet<GridPoint> occupied = state.Units.Values
            .Where(unit => unit.IsAlive)
            .Select(unit => unit.Unit.Position)
            .Concat(state.DroppedSpears.Values)
            .ToHashSet();

        return state.Board.Cells.Keys
            .Where(cell => cell != targetCell && !occupied.Contains(cell) && !state.Board.GetCell(cell).BlocksMovement)
            .Where(cell => Manhattan(cell, targetCell) <= Math.Max(1, radius))
            .Where(cell => state.Board.GetNeighbours(cell).Any(neighbour =>
                terrainReachable.Contains(neighbour) && !state.Board.GetCell(neighbour).BlocksMovement))
            .OrderBy(cell => Manhattan(cell, targetCell))
            .ThenByDescending(cell =>
                (cell.X - targetCell.X) * awayX + (cell.Y - targetCell.Y) * awayY)
            .ThenBy(cell => cell.X)
            .ThenBy(cell => cell.Y)
            .Cast<GridPoint?>()
            .FirstOrDefault();
    }

    private static HashSet<GridPoint> CollectTerrainReachable(BoardSnapshot board, GridPoint start)
    {
        var result = new HashSet<GridPoint> { start };
        var queue = new Queue<GridPoint>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            GridPoint current = queue.Dequeue();
            foreach (GridPoint neighbour in board.GetNeighbours(current))
            {
                if (result.Contains(neighbour) || board.GetCell(neighbour).BlocksMovement)
                    continue;
                result.Add(neighbour);
                queue.Enqueue(neighbour);
            }
        }
        return result;
    }

    private static int Manhattan(GridPoint left, GridPoint right) =>
        Math.Abs(left.X - right.X) + Math.Abs(left.Y - right.Y);

    private BattleTransition ApplyEndTurn(BattleState state, EndTurnCommand command)
    {
        var events = new List<BattleEvent>();
        BattleUnitState outgoing = state.Units[command.ActorId];
        int restoredMana = Math.Min(outgoing.ManaRecoveryPerTurn, outgoing.MaxMana - outgoing.CurrentMana);
        if (restoredMana > 0)
        {
            outgoing = outgoing.WithMana(outgoing.CurrentMana + restoredMana);
            events.Add(new ManaRestoredEvent(command.ActorId, command.ActorId,
                new ContentId("system.turn-end-mana"), restoredMana, outgoing.CurrentMana));
        }
        foreach (BattleStatusState status in outgoing.Statuses.Values
                     .OrderBy(item => item.ContentId.Value, StringComparer.Ordinal))
        {
            if (status.EffectKind == StatusEffectKind.Burning)
                continue;
            int remainingTurns = status.RemainingTurns - 1;
            if (remainingTurns == 0)
            {
                outgoing = _statusRuntime.Remove(outgoing, status.ContentId);
                events.Add(new StatusExpiredEvent(command.ActorId, status.ContentId));
            }
            else
            {
                outgoing = outgoing.WithStatus(status.WithRemainingTurns(remainingTurns));
                events.Add(new StatusDurationChangedEvent(
                    command.ActorId,
                    status.ContentId,
                    remainingTurns));
            }
        }

        BattleState nextState = state.WithUnit(outgoing).AdvanceTurn();
        events.Add(new TurnAdvancedEvent(command.ActorId, nextState.ActiveUnitId, nextState.Round));

        BattleUnitState incoming = nextState.Units[nextState.ActiveUnitId];
        BattleUnitState incomingBeforeTicks = incoming;
        foreach (BattleStatusState status in incoming.Statuses.Values
                     .OrderBy(item => item.ContentId.Value, StringComparer.Ordinal))
        {
            if (!incoming.IsAlive)
                continue;
            int tickDamage = status.EffectKind == StatusEffectKind.Burning
                ? status.StackCount
                : status.DamagePerTurn;
            if (tickDamage <= 0)
                continue;
            int healthAfterDamage = Math.Max(0, incoming.CurrentHealth - tickDamage);
            int appliedDamage = incoming.CurrentHealth - healthAfterDamage;
            incoming = incoming.WithHealth(healthAfterDamage);
            events.Add(new StatusTickedEvent(
                status.SourceId,
                incoming.Unit.InstanceId,
                status.ContentId,
                appliedDamage,
                healthAfterDamage));
            if (status.EffectKind == StatusEffectKind.Burning)
            {
                int remainingStacks = status.StackCount - 1;
                if (remainingStacks == 0)
                {
                    incoming = _statusRuntime.Remove(incoming, status.ContentId);
                    events.Add(new StatusExpiredEvent(incoming.Unit.InstanceId, status.ContentId));
                }
                else
                {
                    incoming = incoming.WithStatus(status.WithStackCount(remainingStacks));
                    events.Add(new StatusStackChangedEvent(
                        incoming.Unit.InstanceId,
                        status.ContentId,
                        remainingStacks));
                }
            }
        }

        nextState = nextState.WithUnit(incoming);
        nextState = BattleDefeatResolver.Apply(nextState, incomingBeforeTicks, incoming, events);
        return new BattleTransition(nextState, events);
    }

    private static BattleTransition Rejected(BattleState state, UnitInstanceId actorId, string reason) =>
        new(state, new BattleEvent[] { new CommandRejectedEvent(actorId, reason) });
}
