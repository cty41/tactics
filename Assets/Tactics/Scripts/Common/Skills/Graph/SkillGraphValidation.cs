using System.Collections.Generic;

namespace Tactics.Common.Skills.Graph
{
    public enum SkillGraphDiagnosticSeverity
    {
        Error,
        Warning
    }

    public enum SkillGraphDiagnosticCategory
    {
        Structure,
        Runtime,
        Unsupported,
        Bridge,
        Migration
    }

    public enum SkillGraphSuggestedFixType
    {
        None,
        AddNode,
        RemoveEdge,
        ReplaceNode,
        SetParameter,
        ReconnectEdge,
        CreateBridge,
        SyncBridge,
        ReviewLegacyImplementation,
        DesignProjectileSemantic
    }

    public class SkillGraphDiagnostic
    {
        public string Code { get; set; }
        public SkillGraphDiagnosticSeverity Severity { get; set; }
        public SkillGraphDiagnosticCategory Category { get; set; } = SkillGraphDiagnosticCategory.Structure;
        private bool? _blocking;
        public bool Blocking { get => _blocking ?? Severity == SkillGraphDiagnosticSeverity.Error; set => _blocking = value; }
        public string NodeId { get; set; }
        public string EdgeId { get; set; }
        public List<string> RelatedNodeIds { get; set; } = new List<string>();
        public List<string> RelatedEdgeIds { get; set; } = new List<string>();
        public string Message { get; set; }
        public string SuggestedFix { get; set; }
        public SkillGraphSuggestedFixType SuggestedFixType { get; set; } = SkillGraphSuggestedFixType.None;

        public override string ToString()
            => $"[{Severity}/{Category}/blocking={Blocking}] {Code}: {Message}" +
               (string.IsNullOrEmpty(NodeId) ? "" : $" (node={NodeId})") +
               (string.IsNullOrEmpty(EdgeId) ? "" : $" (edge={EdgeId})");
    }

    public static class SkillGraphValidation
    {
        // ── 错误码常量 ──

        public const string MissingEntryNode = "MissingEntryNode";
        public const string MultipleEntryNodes = "MultipleEntryNodes";
        public const string NoTerminalNode = "NoTerminalNode";
        public const string OrphanNode = "OrphanNode";
        public const string InvalidEdgeSource = "InvalidEdgeSource";
        public const string InvalidEdgeTarget = "InvalidEdgeTarget";
        public const string SelfReferencingEdge = "SelfReferencingEdge";
        public const string EntryNodeHasIncoming = "EntryNodeHasIncoming";
        public const string TerminalNodeHasOutgoing = "TerminalNodeHasOutgoing";
        public const string MissingRequiredParameter = "MissingRequiredParameter";
        public const string UnsupportedNodeType = "UnsupportedNodeType";
        public const string MissingTargetSource = "MissingTargetSource";
        public const string MissingPointSource = "MissingPointSource";
        public const string UnreachableNode = "UnreachableNode";
        public const string ProjectileSemanticMissing = "ProjectileSemanticMissing";
        public const string LegacyAbilityNotMigrated = "LegacyAbilityNotMigrated";
        public const string BridgeMissing = "BridgeMissing";
        public const string WrongGraphReference = "WrongGraphReference";
        public const string TargetRangeDrift = "TargetRangeDrift";
        public const string DisplayNameDrift = "DisplayNameDrift";

        // ── 首版支持节点集合 ──

        private static readonly HashSet<SkillGraphNodeType> SupportedPhase1Types = new()
        {
            SkillGraphNodeType.Start,
            SkillGraphNodeType.SelectPrimaryTarget,
            SkillGraphNodeType.SelectTargetPoint,
            SkillGraphNodeType.CollectTargetsInArea,
            SkillGraphNodeType.ForEachTarget,
            SkillGraphNodeType.DashToTarget,
            SkillGraphNodeType.ApplyDamage,
            SkillGraphNodeType.ApplyKnockback,
            SkillGraphNodeType.Branch,
            SkillGraphNodeType.Finish,
            SkillGraphNodeType.Fail,
            SkillGraphNodeType.ProjectileLaunch,
            SkillGraphNodeType.OnHit,
            SkillGraphNodeType.ApplyBuff
        };

        /// <summary>
        /// 执行三层校验：结构校验、运行时可执行校验、首版支持域校验。
        /// </summary>
        public static bool Validate(SkillGraphAsset asset, out List<SkillGraphDiagnostic> errors, out List<SkillGraphDiagnostic> warnings)
        {
            errors = new List<SkillGraphDiagnostic>();
            warnings = new List<SkillGraphDiagnostic>();

            if (asset == null)
            {
                errors.Add(new SkillGraphDiagnostic
                {
                    Code = "NullAsset",
                    Severity = SkillGraphDiagnosticSeverity.Error,
                    Category = SkillGraphDiagnosticCategory.Structure,
                    Message = "SkillGraphAsset is null.",
                    SuggestedFixType = SkillGraphSuggestedFixType.AddNode
                });
                return false;
            }

            ValidateStructure(asset, errors, warnings);
            ValidateRuntimeExecutable(asset, errors, warnings);
            ValidatePhase1SupportDomain(asset, errors, warnings);

            return errors.Count == 0;
        }

        // ───────────────────────────────────────────
        //  1. 结构校验
        // ───────────────────────────────────────────

        private static void ValidateStructure(SkillGraphAsset asset, List<SkillGraphDiagnostic> errors, List<SkillGraphDiagnostic> warnings)
        {
            var nodes = asset.Nodes;
            var edges = asset.Edges;

            // 1.1 入口节点
            int startCount = 0;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] is StartNodeRecord) startCount++;
            }
            if (startCount == 0)
            {
                errors.Add(new SkillGraphDiagnostic
                {
                    Code = MissingEntryNode,
                    Severity = SkillGraphDiagnosticSeverity.Error,
                    Message = "Graph has no Start node."
                });
            }
            else if (startCount > 1)
            {
                errors.Add(new SkillGraphDiagnostic
                {
                    Code = MultipleEntryNodes,
                    Severity = SkillGraphDiagnosticSeverity.Error,
                    Message = $"Graph has {startCount} Start nodes; only one is allowed."
                });
            }

            // 1.2 终止节点
            bool hasTerminal = false;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] is FinishNodeRecord || nodes[i] is FailNodeRecord)
                {
                    hasTerminal = true;
                    break;
                }
            }
            if (!hasTerminal)
            {
                errors.Add(new SkillGraphDiagnostic
                {
                    Code = NoTerminalNode,
                    Severity = SkillGraphDiagnosticSeverity.Error,
                    Message = "Graph has no Finish or Fail node."
                });
            }

            // 1.3 边合法性
            var nodeIds = new HashSet<string>();
            for (int i = 0; i < nodes.Count; i++)
                nodeIds.Add(nodes[i].NodeId);

            for (int i = 0; i < edges.Count; i++)
            {
                var edge = edges[i];

                if (string.IsNullOrEmpty(edge.SourceNodeId) || !nodeIds.Contains(edge.SourceNodeId))
                {
                    errors.Add(new SkillGraphDiagnostic
                    {
                        Code = InvalidEdgeSource,
                        Severity = SkillGraphDiagnosticSeverity.Error,
                        Category = SkillGraphDiagnosticCategory.Structure,
                        EdgeId = edge.EdgeId,
                        Message = $"Edge '{edge.EdgeId}' references non-existent source node '{edge.SourceNodeId}'.",
                        SuggestedFix = "Delete the edge or reconnect to a valid source node.",
                        SuggestedFixType = SkillGraphSuggestedFixType.ReconnectEdge,
                        RelatedEdgeIds = new List<string> { edge.EdgeId }
                    });
                }

                if (string.IsNullOrEmpty(edge.TargetNodeId) || !nodeIds.Contains(edge.TargetNodeId))
                {
                    errors.Add(new SkillGraphDiagnostic
                    {
                        Code = InvalidEdgeTarget,
                        Severity = SkillGraphDiagnosticSeverity.Error,
                        Category = SkillGraphDiagnosticCategory.Structure,
                        EdgeId = edge.EdgeId,
                        Message = $"Edge '{edge.EdgeId}' references non-existent target node '{edge.TargetNodeId}'.",
                        SuggestedFix = "Delete the edge or reconnect to a valid target node.",
                        SuggestedFixType = SkillGraphSuggestedFixType.ReconnectEdge,
                        RelatedEdgeIds = new List<string> { edge.EdgeId }
                    });
                }

                if (!string.IsNullOrEmpty(edge.SourceNodeId) && edge.SourceNodeId == edge.TargetNodeId)
                {
                    errors.Add(new SkillGraphDiagnostic
                    {
                        Code = SelfReferencingEdge,
                        Severity = SkillGraphDiagnosticSeverity.Error,
                        Category = SkillGraphDiagnosticCategory.Structure,
                        EdgeId = edge.EdgeId,
                        NodeId = edge.SourceNodeId,
                        Message = $"Edge '{edge.EdgeId}' self-references node '{edge.SourceNodeId}'.",
                        SuggestedFix = "Remove the self-referencing edge.",
                        SuggestedFixType = SkillGraphSuggestedFixType.RemoveEdge,
                        RelatedNodeIds = new List<string> { edge.SourceNodeId },
                        RelatedEdgeIds = new List<string> { edge.EdgeId }
                    });
                }
            }

            // 1.4 入口节点不应有入边
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] is StartNodeRecord && asset.HasIncomingEdge(nodes[i].NodeId))
                {
                    errors.Add(new SkillGraphDiagnostic
                    {
                        Code = EntryNodeHasIncoming,
                        Severity = SkillGraphDiagnosticSeverity.Error,
                        Category = SkillGraphDiagnosticCategory.Structure,
                        NodeId = nodes[i].NodeId,
                        Message = "Start node should not have incoming edges.",
                        SuggestedFix = "Remove incoming edges from the Start node.",
                        SuggestedFixType = SkillGraphSuggestedFixType.RemoveEdge,
                        RelatedNodeIds = new List<string> { nodes[i].NodeId }
                    });
                }
            }

            // 1.5 终止节点不应有出边
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] is FinishNodeRecord || nodes[i] is FailNodeRecord)
                {
                    var outgoing = asset.GetEdgesFrom(nodes[i].NodeId);
                    if (outgoing.Count > 0)
                    {
                        warnings.Add(new SkillGraphDiagnostic
                        {
                            Code = TerminalNodeHasOutgoing,
                            Severity = SkillGraphDiagnosticSeverity.Warning,
                            Category = SkillGraphDiagnosticCategory.Structure,
                            Blocking = false,
                            NodeId = nodes[i].NodeId,
                            Message = $"Terminal node '{nodes[i].NodeId}' has {outgoing.Count} outgoing edge(s). They will be ignored at runtime.",
                            SuggestedFix = "Remove outgoing edges from terminal nodes.",
                            SuggestedFixType = SkillGraphSuggestedFixType.RemoveEdge,
                            RelatedNodeIds = new List<string> { nodes[i].NodeId }
                        });
                    }
                }
            }

            // 1.6 孤立节点（Start 除外，因为它是入口）
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node is StartNodeRecord) continue;

                bool hasIncoming = asset.HasIncomingEdge(node.NodeId);
                bool hasOutgoing = asset.GetEdgesFrom(node.NodeId).Count > 0;

                if (!hasIncoming && !hasOutgoing)
                {
                    warnings.Add(new SkillGraphDiagnostic
                    {
                        Code = OrphanNode,
                        Severity = SkillGraphDiagnosticSeverity.Warning,
                        Category = SkillGraphDiagnosticCategory.Structure,
                        Blocking = false,
                        NodeId = node.NodeId,
                        Message = $"Node '{node.NodeId}' ({node.NodeType}) is orphaned — no incoming or outgoing edges.",
                        SuggestedFix = "Connect the node or remove it.",
                        SuggestedFixType = SkillGraphSuggestedFixType.ReconnectEdge,
                        RelatedNodeIds = new List<string> { node.NodeId }
                    });
                }
            }
        }

        // ───────────────────────────────────────────
        //  2. 运行时可执行校验
        // ───────────────────────────────────────────

        private static void ValidateRuntimeExecutable(SkillGraphAsset asset, List<SkillGraphDiagnostic> errors, List<SkillGraphDiagnostic> warnings)
        {
            // 从入口可达的节点集合
            var reachable = CollectReachableNodes(asset);
            var nodes = asset.Nodes;

            // 2.1 不可达节点
            for (int i = 0; i < nodes.Count; i++)
            {
                if (!reachable.Contains(nodes[i].NodeId))
                {
                    warnings.Add(new SkillGraphDiagnostic
                    {
                        Code = UnreachableNode,
                        Severity = SkillGraphDiagnosticSeverity.Warning,
                        Category = SkillGraphDiagnosticCategory.Runtime,
                        Blocking = false,
                        NodeId = nodes[i].NodeId,
                        Message = $"Node '{nodes[i].NodeId}' ({nodes[i].NodeType}) is not reachable from the Start node.",
                        SuggestedFix = "Connect the node to the execution flow or remove it.",
                        SuggestedFixType = SkillGraphSuggestedFixType.ReconnectEdge,
                        RelatedNodeIds = new List<string> { nodes[i].NodeId }
                    });
                }
            }

            // 2.2 数据依赖校验（简化：检查前序是否可能产出所需上下文）
            ValidateDataDependencies(asset, reachable, errors);
        }

        /// <summary>
        /// 简化的数据依赖校验：检查从入口到每个节点的路径上是否存在可能产出所需上下文的节点。
        /// 这不是完整的静态分析，但能捕获明显的遗漏。
        /// </summary>
        private static void ValidateDataDependencies(SkillGraphAsset asset, HashSet<string> reachable, List<SkillGraphDiagnostic> errors)
        {
            // 从入口做 BFS，沿途跟踪已产出的上下文能力
            var entry = asset.FindEntryNode();
            if (entry == null) return;

            var visited = new HashSet<string>();
            var queue = new Queue<string>();
            queue.Enqueue(entry.NodeId);
            visited.Add(entry.NodeId);

            // 每个节点到达时，已具备的能力集合
            var nodeCapabilities = new Dictionary<string, HashSet<string>>();
            nodeCapabilities[entry.NodeId] = new HashSet<string>();

            while (queue.Count > 0)
            {
                var currentId = queue.Dequeue();
                var currentCaps = nodeCapabilities[currentId];
                var currentNode = asset.FindNode(currentId);

                // 当前节点产出的能力
                var nextCaps = new HashSet<string>(currentCaps);
                switch (currentNode)
                {
                    case SelectPrimaryTargetNodeRecord:
                        nextCaps.Add("PrimaryTarget");
                        break;
                    case SelectTargetPointNodeRecord:
                        nextCaps.Add("TargetPoint");
                        break;
                    case CollectTargetsInAreaNodeRecord:
                        nextCaps.Add("TargetSet");
                        break;
                }

                // 当前节点的依赖检查
                switch (currentNode)
                {
                    case DashToTargetNodeRecord:
                        if (!currentCaps.Contains("PrimaryTarget"))
                        {
                            errors.Add(new SkillGraphDiagnostic
                            {
                                Code = MissingTargetSource,
                                Severity = SkillGraphDiagnosticSeverity.Error,
                                Category = SkillGraphDiagnosticCategory.Runtime,
                                NodeId = currentId,
                                Message = $"DashToTarget node '{currentId}' requires a PrimaryTarget, but no SelectPrimaryTarget node precedes it in the execution path.",
                                SuggestedFix = "Add a SelectPrimaryTarget node before this node.",
                                SuggestedFixType = SkillGraphSuggestedFixType.AddNode,
                                RelatedNodeIds = new List<string> { currentId }
                            });
                        }
                        break;
                    case ApplyDamageNodeRecord:
                        // ApplyDamage 作用于 PrimaryTarget 或 ForEachTarget 的当前目标
                        if (!currentCaps.Contains("PrimaryTarget") && !currentCaps.Contains("TargetSet"))
                        {
                            errors.Add(new SkillGraphDiagnostic
                            {
                                Code = MissingTargetSource,
                                Severity = SkillGraphDiagnosticSeverity.Error,
                                Category = SkillGraphDiagnosticCategory.Runtime,
                                NodeId = currentId,
                                Message = $"ApplyDamage node '{currentId}' requires a target source, but no SelectPrimaryTarget or CollectTargetsInArea node precedes it.",
                                SuggestedFix = "Add a SelectPrimaryTarget or CollectTargetsInArea node before this node.",
                                SuggestedFixType = SkillGraphSuggestedFixType.AddNode,
                                RelatedNodeIds = new List<string> { currentId }
                            });
                        }
                        break;
                    case ApplyKnockbackNodeRecord:
                        if (!currentCaps.Contains("PrimaryTarget"))
                        {
                            errors.Add(new SkillGraphDiagnostic
                            {
                                Code = MissingTargetSource,
                                Severity = SkillGraphDiagnosticSeverity.Error,
                                Category = SkillGraphDiagnosticCategory.Runtime,
                                NodeId = currentId,
                                Message = $"ApplyKnockback node '{currentId}' requires a PrimaryTarget, but no SelectPrimaryTarget node precedes it.",
                                SuggestedFix = "Add a SelectPrimaryTarget node before this node.",
                                SuggestedFixType = SkillGraphSuggestedFixType.AddNode,
                                RelatedNodeIds = new List<string> { currentId }
                            });
                        }
                        break;
                    case CollectTargetsInAreaNodeRecord:
                        if (!currentCaps.Contains("TargetPoint"))
                        {
                            errors.Add(new SkillGraphDiagnostic
                            {
                                Code = MissingPointSource,
                                Severity = SkillGraphDiagnosticSeverity.Error,
                                Category = SkillGraphDiagnosticCategory.Runtime,
                                NodeId = currentId,
                                Message = $"CollectTargetsInArea node '{currentId}' requires a TargetPoint, but no SelectTargetPoint node precedes it.",
                                SuggestedFix = "Add a SelectTargetPoint node before this node.",
                                SuggestedFixType = SkillGraphSuggestedFixType.AddNode,
                                RelatedNodeIds = new List<string> { currentId }
                            });
                        }
                        break;
                    case ForEachTargetNodeRecord:
                        if (!currentCaps.Contains("TargetSet"))
                        {
                            errors.Add(new SkillGraphDiagnostic
                            {
                                Code = MissingTargetSource,
                                Severity = SkillGraphDiagnosticSeverity.Error,
                                Category = SkillGraphDiagnosticCategory.Runtime,
                                NodeId = currentId,
                                Message = $"ForEachTarget node '{currentId}' requires a TargetSet, but no CollectTargetsInArea node precedes it.",
                                SuggestedFix = "Add a CollectTargetsInArea node before this node.",
                                SuggestedFixType = SkillGraphSuggestedFixType.AddNode,
                                RelatedNodeIds = new List<string> { currentId }
                            });
                        }
                        break;
                }

                // 遍历子节点
                var children = asset.GetEdgesFrom(currentId);
                for (int i = 0; i < children.Count; i++)
                {
                    var childId = children[i].TargetNodeId;
                    if (visited.Contains(childId)) continue;
                    visited.Add(childId);
                    nodeCapabilities[childId] = new HashSet<string>(nextCaps);
                    queue.Enqueue(childId);
                }
            }
        }

        // ───────────────────────────────────────────
        //  3. 首版支持域校验
        // ───────────────────────────────────────────

        private static void ValidatePhase1SupportDomain(SkillGraphAsset asset, List<SkillGraphDiagnostic> errors, List<SkillGraphDiagnostic> warnings)
        {
            var nodes = asset.Nodes;

            for (int i = 0; i < nodes.Count; i++)
            {
                if (!SupportedPhase1Types.Contains(nodes[i].NodeType))
                {
                    errors.Add(new SkillGraphDiagnostic
                    {
                        Code = UnsupportedNodeType,
                        Severity = SkillGraphDiagnosticSeverity.Error,
                        Category = SkillGraphDiagnosticCategory.Unsupported,
                        NodeId = nodes[i].NodeId,
                        Message = $"Node '{nodes[i].NodeId}' has type '{nodes[i].NodeType}' which is not supported in Phase 1.",
                        SuggestedFix = "Remove the node or replace it with a Phase 1 supported type.",
                        SuggestedFixType = SkillGraphSuggestedFixType.ReplaceNode,
                        RelatedNodeIds = new List<string> { nodes[i].NodeId }
                    });
                }
            }
        }

        // ───────────────────────────────────────────
        //  Helpers
        // ───────────────────────────────────────────

        private static HashSet<string> CollectReachableNodes(SkillGraphAsset asset)
        {
            var reachable = new HashSet<string>();
            var entry = asset.FindEntryNode();
            if (entry == null) return reachable;

            var queue = new Queue<string>();
            queue.Enqueue(entry.NodeId);
            reachable.Add(entry.NodeId);

            while (queue.Count > 0)
            {
                var currentId = queue.Dequeue();
                var edges = asset.GetEdgesFrom(currentId);
                for (int i = 0; i < edges.Count; i++)
                {
                    if (reachable.Add(edges[i].TargetNodeId))
                        queue.Enqueue(edges[i].TargetNodeId);
                }
            }

            return reachable;
        }
    }
}
