using Tactics.Core.Content;
using Tactics.Core.Runs;

namespace Tactics.Application.Runs;

public enum PureRunFlowPage
{
    Home,
    Map,
    Battle,
    Progression,
    Rest,
    Store,
    Mystery,
    NewRunSetup,
    Inventory,
    Summary
}

public enum PureRunFlowAction
{
    OpenMap,
    OpenInventory,
    CompleteProgression,
    BeginAvailableNode,
    ResumeBattle,
    ResolveNode,
    ChooseStartingSkill,
    ReturnHome
}

public enum PureRunMapNodeState
{
    Locked,
    Available,
    Current,
    Selected,
    Pending,
    Completed
}

public sealed record PureRunFlowPartyMemberSnapshot(
    string CharacterId,
    int Level,
    int CurrentHealth,
    int MaxHealth,
    int CurrentMana,
    int MaxMana,
    bool IsDead);

public sealed record PureRunMapNodeSnapshot(
    string NodeId,
    int Layer,
    PureRunNodeKind Kind,
    string Title,
    ContentId? ContentId,
    float Lane,
    PureRunMapNodeState State,
    string? UnavailableReason = null);

public sealed record PureRunMapConnectionSnapshot(
    string FromNodeId,
    string ToNodeId,
    bool Revealed,
    bool Traversed);

public sealed record PureRunMapSnapshot(
    IReadOnlyList<PureRunMapNodeSnapshot> Nodes,
    IReadOnlyList<PureRunMapConnectionSnapshot> Connections,
    string FocusNodeId);

public sealed record PureRunFlowSnapshot(
    PureRunFlowPage Page,
    long Revision,
    int Gold,
    int InventoryCount,
    IReadOnlyList<PureRunFlowPartyMemberSnapshot> Party,
    IReadOnlyList<PureRunFlowAction> Actions,
    PureRunMapSnapshot? Map,
    string? BlockingReason = null);

/// <summary>Projects committed Pure Run state into engine-neutral flow and seven-layer map UI facts.</summary>
public sealed class PureRunFlowProjector
{
    private static readonly (string From, string To)[] Edges = BuildEdges();

    public PureRunFlowSnapshot Project(PureRunState run, PureRunDefinition definition,
        PureRunMapDefinition branchMap)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(branchMap);

        PureRunFlowPage page = Page(run);
        var actions = new List<PureRunFlowAction> { PureRunFlowAction.OpenMap, PureRunFlowAction.OpenInventory };
        if (run.PendingProgression.Count > 0) actions.Add(PureRunFlowAction.CompleteProgression);
        if (page == PureRunFlowPage.Battle) actions.Add(PureRunFlowAction.ResumeBattle);
        if (page == PureRunFlowPage.Map && run.PendingProgression.Count == 0) actions.Add(PureRunFlowAction.BeginAvailableNode);
        if (page is PureRunFlowPage.Rest or PureRunFlowPage.Store or PureRunFlowPage.Mystery)
            actions.Add(PureRunFlowAction.ResolveNode);

        return new PureRunFlowSnapshot(page, run.Revision, run.Gold,
            run.BackpackConsumables.Count + run.BackpackEquipment.Count,
            run.Party.Select(value => new PureRunFlowPartyMemberSnapshot(value.CharacterId, value.Level,
                value.CurrentHealth, value.MaxHealth, value.CurrentMana, value.MaxMana, value.IsDead)).ToArray(),
            actions.Distinct().ToArray(), ProjectMap(run, definition, branchMap),
            run.PendingProgression.Count > 0 ? "progression.required" : null);
    }

    public PureRunFlowSnapshot ProjectTerminal(PureRunSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        return new PureRunFlowSnapshot(PureRunFlowPage.Summary, 0, summary.TotalGoldEarned,
            summary.AcquiredItems.Count, Array.Empty<PureRunFlowPartyMemberSnapshot>(),
            new[] { PureRunFlowAction.ReturnHome }, null);
    }

    public PureRunMapSnapshot ProjectMap(PureRunState run, PureRunDefinition definition,
        PureRunMapDefinition branchMap)
    {
        var definitions = StaticNodes(definition, branchMap);
        PureRunMapNodeSnapshot[] nodes = definitions.Select(value => value with
        {
            State = State(run, value.NodeId),
            UnavailableReason = UnavailableReason(run, value.NodeId)
        }).ToArray();
        var states = nodes.ToDictionary(value => value.NodeId, value => value.State, StringComparer.Ordinal);
        PureRunMapConnectionSnapshot[] connections = Edges.Select(edge =>
        {
            PureRunMapNodeState from = states[edge.From];
            PureRunMapNodeState to = states[edge.To];
            bool traversed = from == PureRunMapNodeState.Completed &&
                to is PureRunMapNodeState.Completed or PureRunMapNodeState.Current or PureRunMapNodeState.Selected or PureRunMapNodeState.Pending;
            bool revealed = traversed || from != PureRunMapNodeState.Locked || to != PureRunMapNodeState.Locked;
        return new PureRunMapConnectionSnapshot(edge.From, edge.To, true, traversed);
        }).ToArray();
        string focus = nodes.FirstOrDefault(value => value.State is PureRunMapNodeState.Pending or PureRunMapNodeState.Selected)?.NodeId
            ?? nodes.FirstOrDefault(value => value.State == PureRunMapNodeState.Current)?.NodeId
            ?? nodes.FirstOrDefault(value => value.State == PureRunMapNodeState.Available)?.NodeId
            ?? nodes.Last(value => value.State == PureRunMapNodeState.Completed).NodeId;
        return new PureRunMapSnapshot(nodes, connections, focus);
    }

    private static PureRunFlowPage Page(PureRunState run)
    {
        if (run.Phase == PureRunPhase.PendingBattle) return PureRunFlowPage.Battle;
        if (run.PendingProgression.Count > 0) return PureRunFlowPage.Progression;
        if (run.Phase is PureRunPhase.AwaitingLayerFourChoice or PureRunPhase.ReadyForLayerFive or
            PureRunPhase.ReadyForLayerSix or PureRunPhase.AwaitingLayerSixChoice or PureRunPhase.ReadyForBoss or PureRunPhase.Ready)
            return PureRunFlowPage.Map;
        if (run.Phase is PureRunPhase.ResolvingLayerFourNode or PureRunPhase.ResolvingLayerSixNode)
            return run.NodeTransaction?.Kind switch
            {
                PureRunNodeKind.Rest => PureRunFlowPage.Rest,
                PureRunNodeKind.Store => PureRunFlowPage.Store,
                PureRunNodeKind.Mystery => PureRunFlowPage.Mystery,
                _ => PureRunFlowPage.Battle
            };
        return PureRunFlowPage.Map;
    }

    private static PureRunMapNodeState State(PureRunState run, string nodeId)
    {
        if (nodeId == "start") return PureRunMapNodeState.Completed;
        if (nodeId.StartsWith("layer_0", StringComparison.Ordinal) && nodeId.EndsWith("_battle", StringComparison.Ordinal))
        {
            int layer = int.Parse(nodeId.AsSpan(6, 2), System.Globalization.CultureInfo.InvariantCulture);
            if (layer <= 3)
            {
                if (run.BattlesCompleted >= layer) return PureRunMapNodeState.Completed;
                if (run.EncounterIndex == layer - 1 && run.Phase == PureRunPhase.PendingBattle) return PureRunMapNodeState.Pending;
                if (run.EncounterIndex == layer - 1 && run.Phase == PureRunPhase.Ready && run.PendingProgression.Count == 0)
                    return PureRunMapNodeState.Current;
                return PureRunMapNodeState.Locked;
            }
        }
        if (nodeId.StartsWith("layer_04_", StringComparison.Ordinal) || nodeId.StartsWith("layer_06_", StringComparison.Ordinal))
        {
            PureRunMapState? map = run.MapState;
            if (map?.VisitedNodeIds.Contains(nodeId, StringComparer.Ordinal) == true) return PureRunMapNodeState.Completed;
            if (map?.SelectedNodeId == nodeId)
                return map.NodeLifecycle is RunNodeLifecycle.Pending or RunNodeLifecycle.Resolved
                    ? PureRunMapNodeState.Pending : PureRunMapNodeState.Selected;
            if (map?.ReachableNodeIds.Contains(nodeId, StringComparer.Ordinal) == true && run.PendingProgression.Count == 0)
                return PureRunMapNodeState.Available;
            if (nodeId.StartsWith("layer_06_", StringComparison.Ordinal) && run.Phase == PureRunPhase.ReadyForLayerSix &&
                run.PendingProgression.Count == 0)
                return PureRunMapNodeState.Available;
            return PureRunMapNodeState.Locked;
        }
        if (nodeId == "layer_05_battle")
        {
            if (run.Phase is PureRunPhase.ReadyForLayerSix or PureRunPhase.AwaitingLayerSixChoice or
                PureRunPhase.ResolvingLayerSixNode or PureRunPhase.ReadyForBoss) return PureRunMapNodeState.Completed;
            if (run.EncounterIndex == 4 && run.Phase == PureRunPhase.PendingBattle) return PureRunMapNodeState.Pending;
            if (run.Phase == PureRunPhase.ReadyForLayerFive && run.PendingProgression.Count == 0)
                return PureRunMapNodeState.Current;
            return PureRunMapNodeState.Locked;
        }
        if (nodeId == "layer_07_battle")
        {
            if (run.EncounterIndex == 6 && run.Phase == PureRunPhase.PendingBattle) return PureRunMapNodeState.Pending;
            return run.Phase == PureRunPhase.ReadyForBoss && run.PendingProgression.Count == 0
                ? PureRunMapNodeState.Current : PureRunMapNodeState.Locked;
        }
        return PureRunMapNodeState.Locked;
    }

    private static string? UnavailableReason(PureRunState run, string nodeId)
    {
        PureRunMapNodeState state = State(run, nodeId);
        if (state is PureRunMapNodeState.Available or PureRunMapNodeState.Current) return null;
        if (run.PendingProgression.Count > 0) return "progression.required";
        if (state == PureRunMapNodeState.Completed) return "map.node_completed";
        if ((nodeId.StartsWith("layer_04_", StringComparison.Ordinal) || nodeId.StartsWith("layer_06_", StringComparison.Ordinal)) &&
            run.MapState?.SelectedNodeId is not null) return "map.route_locked";
        return "map.node_locked";
    }

    private static IReadOnlyList<PureRunMapNodeSnapshot> StaticNodes(PureRunDefinition definition,
        PureRunMapDefinition map)
    {
        var nodes = new List<PureRunMapNodeSnapshot>
        {
            Node("start", 0, PureRunNodeKind.Battle, "Start", null, 0),
            Node("layer_01_battle", 1, PureRunNodeKind.Battle, "N1", definition.Encounters[0], 0),
            Node("layer_02_battle", 2, PureRunNodeKind.Battle, "N2", definition.Encounters[1], 0),
            Node("layer_03_battle", 3, PureRunNodeKind.Battle, "N3", definition.Encounters[2], 0)
        };
        float Lane(PureRunNodeKind kind) => kind switch
        {
            PureRunNodeKind.Battle => -1.5f,
            PureRunNodeKind.Rest => -.5f,
            PureRunNodeKind.Store => .5f,
            _ => 1.5f
        };
        nodes.AddRange(map.Nodes.OrderBy(value => value.Layer).ThenBy(value => value.Kind)
            .Select(value => Node(value.NodeId, value.Layer, value.Kind,
                value.Kind == PureRunNodeKind.Mystery ? "Mystery" : value.Kind.ToString(), value.ContentId, Lane(value.Kind))));
        nodes.Add(Node("layer_05_battle", 5, PureRunNodeKind.Battle, "Elite", null, 0));
        nodes.Add(Node("layer_07_battle", 7, PureRunNodeKind.Battle, "Special Boss", new ContentId("encounter.pure-run.special"), 0));
        return nodes.OrderBy(value => value.Layer).ThenBy(value => value.Lane).ToArray();
    }

    private static PureRunMapNodeSnapshot Node(string id, int layer, PureRunNodeKind kind, string title,
        ContentId? contentId, float lane) => new(id, layer, kind, title, contentId, lane,
        PureRunMapNodeState.Locked);

    private static (string From, string To)[] BuildEdges()
    {
        var edges = new List<(string, string)>
        {
            ("start", "layer_01_battle"), ("layer_01_battle", "layer_02_battle"),
            ("layer_02_battle", "layer_03_battle")
        };
        string[] kinds = ["battle", "rest", "store", "event"];
        edges.AddRange(kinds.Select(kind => ("layer_03_battle", $"layer_04_{kind}")));
        edges.AddRange(kinds.Select(kind => ($"layer_04_{kind}", "layer_05_battle")));
        edges.AddRange(kinds.Select(kind => ("layer_05_battle", $"layer_06_{kind}")));
        edges.AddRange(kinds.Select(kind => ($"layer_06_{kind}", "layer_07_battle")));
        return edges.ToArray();
    }
}
