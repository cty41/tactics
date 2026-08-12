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

    public PureRunMapResult BeginNode(PureRunMapState state, string nodeId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Phase != PureRunMapPhase.ChoosingLayerFour) return Fail("map.not_choosing", state);
        if (!state.ReachableNodeIds.Contains(nodeId, StringComparer.Ordinal)) return Fail("map.node_not_reachable", state);
        PureRunMapNodeDefinition? node = _definition.Nodes.FirstOrDefault(candidate => candidate.NodeId == nodeId);
        if (node is null || node.Layer != 4) return Fail("map.node_unknown", state);
        string key = $"node:{node.NodeId}:resolve";
        var transaction = new RunNodeTransaction(key, node.NodeId, node.Kind);
        return new PureRunMapResult(true, null, state with
        {
            Phase = PureRunMapPhase.ResolvingNode,
            PendingNodeId = node.NodeId,
            PendingTransactionKey = key
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
        return new PureRunMapResult(true, null, state with
        {
            Phase = PureRunMapPhase.ReadyForLayerFive,
            CurrentNodeId = transaction.NodeId,
            ReachableNodeIds = Array.Empty<string>(),
            VisitedNodeIds = state.VisitedNodeIds.Append(transaction.NodeId).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            PendingNodeId = null,
            PendingTransactionKey = null
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
