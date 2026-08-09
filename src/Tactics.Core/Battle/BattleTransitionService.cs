using Tactics.Core.Board;
using Tactics.Core.Combat;
using Tactics.Core.Content;
using Tactics.Core.Pathfinding;
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
    public const string ContractId = "battle-transition-v2";

    private readonly IPathfinder _pathfinder;
    private readonly PoisonSpearResolver _poisonSpearResolver;

    /// <summary>
    /// Creates the deterministic transition service.
    /// </summary>
    /// <param name="pathfinder">Optional deterministic path implementation.</param>
    /// <param name="lineOfSight">Optional deterministic line-of-sight implementation.</param>
    public BattleTransitionService(IPathfinder? pathfinder = null, ILineOfSightService? lineOfSight = null)
    {
        _pathfinder = pathfinder ?? new DeterministicDijkstraPathfinder();
        _poisonSpearResolver = new PoisonSpearResolver(lineOfSight);
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
        if (!actor.IsAlive)
            return Rejected(state, command.ActorId, "actor_defeated");
        if (state.ActiveUnitId != command.ActorId)
            return Rejected(state, command.ActorId, "not_active_unit");

        return command switch
        {
            MoveUnitCommand move => ApplyMove(state, actor, move),
            UsePoisonSpearCommand poisonSpear => ApplyPoisonSpear(state, actor, poisonSpear),
            EndTurnCommand endTurn => ApplyEndTurn(state, endTurn),
            _ => Rejected(state, command.ActorId, "unsupported_command")
        };
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
            int currentDuration = updatedTarget.Statuses.TryGetValue(
                command.Definition.PoisonStatusId,
                out BattleStatusState? activeStatus)
                ? activeStatus.RemainingTurns
                : 0;
            int resultingDuration = checked(currentDuration + action.PoisonTurns);
            updatedTarget = updatedTarget.WithStatus(new BattleStatusState(
                command.Definition.PoisonStatusId,
                command.ActorId,
                resultingDuration,
                command.Definition.PoisonDamagePerTurn));
            events.Add(new StatusAppliedEvent(
                command.ActorId,
                command.TargetId,
                command.Definition.PoisonStatusId,
                resultingDuration));
        }

        if (!updatedTarget.IsAlive)
            events.Add(new UnitDefeatedEvent(command.TargetId));

        events.Add(new SpearDroppedEvent(command.ActorId, dropCell.Value));

        BattleState nextState = state
            .WithUnit(updatedActor)
            .WithUnit(updatedTarget)
            .WithDroppedSpear(command.ActorId, dropCell.Value);
        return new BattleTransition(nextState, events);
    }

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

    private static BattleTransition ApplyEndTurn(BattleState state, EndTurnCommand command)
    {
        var events = new List<BattleEvent>();
        BattleUnitState outgoing = state.Units[command.ActorId];
        foreach (BattleStatusState status in outgoing.Statuses.Values
                     .OrderBy(item => item.ContentId.Value, StringComparer.Ordinal))
        {
            int remainingTurns = status.RemainingTurns - 1;
            if (remainingTurns == 0)
            {
                outgoing = outgoing.WithoutStatus(status.ContentId);
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
        foreach (BattleStatusState status in incoming.Statuses.Values
                     .OrderBy(item => item.ContentId.Value, StringComparer.Ordinal))
        {
            if (!incoming.IsAlive || status.DamagePerTurn <= 0)
                continue;
            int healthAfterDamage = Math.Max(0, incoming.CurrentHealth - status.DamagePerTurn);
            int appliedDamage = incoming.CurrentHealth - healthAfterDamage;
            incoming = incoming.WithHealth(healthAfterDamage);
            events.Add(new StatusTickedEvent(
                status.SourceId,
                incoming.Unit.InstanceId,
                status.ContentId,
                appliedDamage,
                healthAfterDamage));
            if (!incoming.IsAlive)
                events.Add(new UnitDefeatedEvent(incoming.Unit.InstanceId));
        }

        nextState = nextState.WithUnit(incoming);
        return new BattleTransition(nextState, events);
    }

    private static BattleTransition Rejected(BattleState state, UnitInstanceId actorId, string reason) =>
        new(state, new BattleEvent[] { new CommandRejectedEvent(actorId, reason) });
}
