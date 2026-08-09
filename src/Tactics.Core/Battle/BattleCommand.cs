using Tactics.Core.Board;
using Tactics.Core.Combat;
using Tactics.Core.Content;
using Tactics.Core.Units;

namespace Tactics.Core.Battle;

/// <summary>
/// Identifies an intent submitted to the deterministic battle transition service.
/// </summary>
/// <remarks>
/// Commands contain only engine-neutral values. They do not contain presentation objects, Nodes,
/// GameObjects, Resources, or mutable adapter state.
/// </remarks>
public abstract record BattleCommand(UnitInstanceId ActorId);

/// <summary>
/// Requests the active unit's one movement use to a local board cell.
/// </summary>
/// <param name="ActorId">Unit issuing the movement command.</param>
/// <param name="Destination">Requested destination cell.</param>
public sealed record MoveUnitCommand(UnitInstanceId ActorId, GridPoint Destination) : BattleCommand(ActorId);

/// <summary>
/// Requests the current Poison Spear vertical slice against one target.
/// </summary>
/// <param name="ActorId">Unit using the skill.</param>
/// <param name="TargetId">Target unit ID.</param>
/// <param name="Definition">Pure compiled skill definition.</param>
public sealed record UsePoisonSpearCommand(
    UnitInstanceId ActorId,
    UnitInstanceId TargetId,
    PoisonSpearDefinition Definition) : BattleCommand(ActorId);

/// <summary>
/// Ends the active unit's turn and advances deterministic turn order.
/// </summary>
/// <param name="ActorId">Unit ending its turn.</param>
public sealed record EndTurnCommand(UnitInstanceId ActorId) : BattleCommand(ActorId);
