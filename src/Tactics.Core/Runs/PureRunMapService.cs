using Tactics.Core.Content;

namespace Tactics.Core.Runs;

public sealed class PureRunMapService
{
    private static readonly string[] EventPool = ["cursed_chest_001", "fallen_altar_001", "lost_villager_001"];
    private readonly PureRunMapDefinition _definition;

    public PureRunMapService(PureRunMapDefinition definition) => _definition = definition ?? throw new ArgumentNullException(nameof(definition));

    public PureRunMapState UnlockLayerFour(int runSeed)
    {
        string[] reachable = _definition.Nodes.Where(node => node.Layer == 4)
            .Select(node => node.NodeId).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        string eventId = EventPool
            .Select((value, index) => (value, order: DeriveSeed(runSeed, "pure-run-event-pool", index)))
            .OrderBy(value => value.order).ThenBy(value => value.value, StringComparer.Ordinal).First().value;
        return new PureRunMapState(PureRunMapPhase.ChoosingLayerFour, "layer_03_battle", reachable,
            ["start", "layer_01_battle", "layer_02_battle", "layer_03_battle"],
            new Dictionary<string, string>(StringComparer.Ordinal) { ["layer_04_event"] = eventId });
    }

    public PureRunMapState AdvanceToLayerFive(PureRunMapState state) => state with
    {
        Phase = PureRunMapPhase.ReadyForLayerFive,
        CurrentNodeId = state.CurrentNodeId,
        ReachableNodeIds = new[] { "layer_05_battle" },
        SelectedNodeId = null,
        NodeLifecycle = RunNodeLifecycle.Available,
        StoreOffers = null,
        MysteryResolution = null
    };

    public PureRunMapState UnlockLayerSix(PureRunMapState state, int runSeed)
    {
        string[] nodes = ["layer_06_battle", "layer_06_event", "layer_06_rest", "layer_06_store"];
        string prior = state.MysteryEventAssignments.GetValueOrDefault("layer_04_event") ?? string.Empty;
        string assigned = EventPool.Select((value, index) => (value, order: DeriveSeed(runSeed, "pure-run-event-pool", index)))
            .OrderBy(value => value.order).ThenBy(value => value.value, StringComparer.Ordinal)
            .Select(value => value.value).First(value => value != prior);
        var assignments = state.MysteryEventAssignments.ToDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal);
        assignments["layer_06_event"] = assigned;
        return state with { Phase = PureRunMapPhase.ChoosingLayerSix, CurrentNodeId = "layer_05_battle",
            ReachableNodeIds = nodes, MysteryEventAssignments = assignments, PendingNodeId = null,
            PendingTransactionKey = null, SelectedNodeId = null, NodeLifecycle = RunNodeLifecycle.Available,
            StoreOffers = null, MysteryResolution = null };
    }

    public ContentId SelectLateEncounter(int seed, string nodeId, bool boss = false)
    {
        if (boss) return new ContentId("encounter.pure-run.special");
        string[] pool = ["e1", "e2"];
        uint index = unchecked((uint)DeriveSeed(seed, $"pure-run-encounter:{nodeId}"));
        return new ContentId($"encounter.pure-run.{pool[index % pool.Length]}");
    }

    public PureRunMapResult BeginNode(PureRunMapState state, string nodeId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Phase is not (PureRunMapPhase.ChoosingLayerFour or PureRunMapPhase.ChoosingLayerSix)) return Fail("map.not_choosing", state);
        if (!state.ReachableNodeIds.Contains(nodeId, StringComparer.Ordinal)) return Fail("map.node_not_reachable", state);
        PureRunMapNodeDefinition? node = _definition.Nodes.FirstOrDefault(candidate => candidate.NodeId == nodeId);
        int layer = state.Phase == PureRunMapPhase.ChoosingLayerSix ? 6 : 4;
        if (node is null || node.Layer != layer) return Fail("map.node_unknown", state);
        string key = $"node:{node.NodeId}:resolve";
        var transaction = new RunNodeTransaction(key, node.NodeId, node.Kind);
        return new PureRunMapResult(true, null, state with
        {
            Phase = PureRunMapPhase.ResolvingNode,
            PendingNodeId = node.NodeId,
            PendingTransactionKey = key,
            SelectedNodeId = node.NodeId,
            ReachableNodeIds = new[] { node.NodeId },
            NodeLifecycle = RunNodeLifecycle.Selected
        }, transaction);
    }

    public PureRunMapResult CommitNode(PureRunMapState state, RunNodeTransaction transaction, IReadOnlyCollection<string> appliedKeys)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(transaction);
        if (appliedKeys.Contains(transaction.TransactionKey, StringComparer.Ordinal))
            return new PureRunMapResult(true, null, state, transaction with { Committed = true }, true);
        if (state.Phase != PureRunMapPhase.ResolvingNode || state.PendingNodeId != transaction.NodeId ||
            state.PendingTransactionKey != transaction.TransactionKey)
            return Fail("map.transaction_mismatch", state);
        int layer = _definition.Nodes.Single(node => node.NodeId == transaction.NodeId).Layer;
        PureRunMapPhase completedPhase = layer == 6 ? PureRunMapPhase.ReadyForBoss : PureRunMapPhase.ReadyForLayerFive;
        return new PureRunMapResult(true, null, state with
        {
            Phase = completedPhase,
            CurrentNodeId = transaction.NodeId,
            ReachableNodeIds = Array.Empty<string>(),
            VisitedNodeIds = state.VisitedNodeIds.Append(transaction.NodeId).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            PendingNodeId = null,
            PendingTransactionKey = null,
            NodeLifecycle = RunNodeLifecycle.Committed
        }, transaction with { Committed = true });
    }

    public static int DeriveSeed(int seed, string scope, int ordinal = 0)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (char value in $"{seed}:{scope}:{ordinal}") { hash ^= value; hash *= 16777619; }
            return (int)hash;
        }
    }

    private static PureRunMapResult Fail(string code, PureRunMapState state) => new(false, code, state);
}
