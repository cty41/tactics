using Tactics.Core.Board;
using Tactics.Core.Content;

namespace Tactics.Core.Runs;

/// <summary>Deterministic transitions for the save-backed tile adventure state.</summary>
public sealed class RunAdventureTransitionService
{
    public static readonly ContentId InitialBoardContentId = new("adventure-board.pure-run.initial");

    public RunAdventureState CreateInitial(IReadOnlyList<RunCharacterState> party)
    {
        ArgumentNullException.ThrowIfNull(party);
        if (party.Count != 3) throw new ArgumentException("Adventure requires exactly three party members.", nameof(party));
        GridPoint[] cells = [new(2, 5), new(1, 4), new(1, 6)];
        return new RunAdventureState(RunAdventureLifecycle.InitialExploration, InitialBoardContentId,
            party[0].CharacterId, party.Select((member, index) => new RunAdventureActorCell(member.CharacterId, cells[index])).ToArray(),
            null, null, RunAdventureEventContextKind.None, null, null, 0, 0, 0, 0, 0);
    }

    public PureRunState SelectLeader(PureRunState run, string actorId)
    {
        RunAdventureState state = Require(run);
        if (!state.ActorCells.Any(value => value.ActorId == actorId)) throw new InvalidOperationException("adventure.leader_not_found");
        if (state.LeaderId == actorId) return run;
        return Copy(run, state with { LeaderId = actorId, LeaderRevision = state.LeaderRevision + 1 });
    }

    public PureRunState MoveLeader(PureRunState run, AdventureBoardDefinition board, GridPoint destination)
    {
        RunAdventureState state = Require(run);
        RunAdventureActorCell leader = state.ActorCells.Single(value => value.ActorId == state.LeaderId);
        GridPoint[] occupied = state.ActorCells.Where(value => value.ActorId != state.LeaderId).Select(value => value.Cell).ToArray();
        if (AdventureBoardPathfinder.FindPath(board, leader.Cell, destination, occupied).Count == 0)
            throw new InvalidOperationException("adventure.destination_unreachable");
        return Copy(run, state with
        {
            ActorCells = state.ActorCells.Select(value => value.ActorId == state.LeaderId ? value with { Cell = destination } : value).ToArray(),
            InteractionRevision = state.InteractionRevision + 1
        });
    }

    public PureRunState BeginRouteSelection(PureRunState run, ContentId boardId) => Copy(run, Require(run) with
    {
        Lifecycle = RunAdventureLifecycle.RouteGroupOne,
        BoardContentId = boardId,
        InteractionRevision = Require(run).InteractionRevision + 1,
        SceneRevision = Require(run).SceneRevision + 1
    });

    public PureRunState SelectRoute(PureRunState run, int group, string nodeId)
    {
        RunAdventureState state = Require(run);
        if (group == 1 && state.Lifecycle == RunAdventureLifecycle.RouteGroupOne)
            return Copy(run, state with { RouteGroupOneSelection = Required(nodeId), Lifecycle = RunAdventureLifecycle.RouteGroupTwo, RouteRevision = state.RouteRevision + 1 });
        if (group == 2 && state.Lifecycle == RunAdventureLifecycle.RouteGroupTwo)
            return Copy(run, state with { RouteGroupTwoSelection = Required(nodeId), Lifecycle = RunAdventureLifecycle.RouteReady, RouteRevision = state.RouteRevision + 1 });
        throw new InvalidOperationException("adventure.route_selection_invalid");
    }

    public PureRunState CommitRoute(PureRunState run)
    {
        RunAdventureState state = Require(run);
        if (state.Lifecycle != RunAdventureLifecycle.RouteReady) throw new InvalidOperationException("adventure.route_not_ready");
        return Copy(run, state with { Lifecycle = RunAdventureLifecycle.RouteCommitted, RouteRevision = state.RouteRevision + 1 });
    }

    public PureRunState ActivateMap(PureRunState run, ContentId boardId)
    {
        RunAdventureState state = Require(run);
        if (state.Lifecycle != RunAdventureLifecycle.RouteCommitted) throw new InvalidOperationException("adventure.route_not_committed");
        return Copy(run, state with { Lifecycle = RunAdventureLifecycle.MapActive, BoardContentId = boardId, SceneRevision = state.SceneRevision + 1 });
    }

    public PureRunState EnterBoard(PureRunState run, ContentId boardId)
    {
        RunAdventureState state = Require(run);
        GridPoint[] cells = [new(2, 5), new(1, 4), new(1, 6)];
        return Copy(run, state with
        {
            BoardContentId = boardId,
            LeaderId = run.Party[0].CharacterId,
            ActorCells = run.Party.Select((member, index) => new RunAdventureActorCell(member.CharacterId, cells[index])).ToArray(),
            SceneRevision = state.SceneRevision + 1
        });
    }

    public PureRunState BeginEventBattle(PureRunState run, RunAdventureEventContextKind context, string nodeId, string objectId)
    {
        if (context == RunAdventureEventContextKind.None) throw new ArgumentException("Event context cannot be None.", nameof(context));
        RunAdventureState state = Require(run);
        if (state.PendingEventContext != RunAdventureEventContextKind.None) throw new InvalidOperationException("adventure.event_already_pending");
        return Copy(run, state with { PendingEventContext = context, PendingEventNodeId = Required(nodeId), PendingEventObjectId = Required(objectId), InteractionRevision = state.InteractionRevision + 1 });
    }

    public PureRunState ResolveEventBattle(PureRunState run)
    {
        RunAdventureState state = Require(run);
        if (state.PendingEventContext == RunAdventureEventContextKind.None) return run;
        return Copy(run, state with
        {
            BoardContentId = new ContentId(state.BoardContentId.Value.EndsWith(".resolved", StringComparison.Ordinal)
                ? state.BoardContentId.Value : state.BoardContentId.Value + ".resolved"),
            PendingEventContext = RunAdventureEventContextKind.None,
            PendingEventNodeId = null,
            PendingEventObjectId = null,
            SceneRevision = state.SceneRevision + 1
        });
    }

    public static bool IsAdjacent(GridPoint actor, GridPoint target) => Math.Abs(actor.X - target.X) + Math.Abs(actor.Y - target.Y) == 1;

    private static RunAdventureState Require(PureRunState run) => run.AdventureState ?? throw new InvalidOperationException("adventure.state_missing");
    private static string Required(string value) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value cannot be empty.") : value.Trim();

    private static PureRunState Copy(PureRunState run, RunAdventureState adventure) => new(
        run.RunId, run.Seed, run.Revision + 1, run.Phase, run.EncounterIndex, run.EncounterContentId, run.Party,
        run.BackpackConsumables, run.BackpackEquipment, run.PendingProgression, run.AppliedTransactionKeys, run.Gold,
        run.BattlesCompleted, run.EnemiesDefeated, run.AcquiredItems, run.Checkpoint, run.MapState, run.NodeTransaction,
        run.EscortState, adventure with { Revision = adventure.Revision + 1 });
}
