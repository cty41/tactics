using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Units.Abilities;

namespace Tactics.Editor.SkillGraphEditor
{
    /// <summary>
    /// SkillGraph MCP 操作门面。
    /// 为 Agent/MCP 自动生成提供最小可用的资产创建、读取、校验入口。
    /// </summary>
    public static class SkillGraphMcpFacade
    {
        // ═══════════════════════════════════════════
        //  图资产操作
        // ═══════════════════════════════════════════

        /// <summary>
        /// 创建新的 SkillGraphAsset。
        /// </summary>
        public static SkillGraphAsset CreateGraph(string assetPath, string displayName = null)
        {
            if (string.IsNullOrEmpty(assetPath))
                return null;

            var graph = ScriptableObject.CreateInstance<SkillGraphAsset>();
            graph.DisplayName = displayName ?? System.IO.Path.GetFileNameWithoutExtension(assetPath);

            AssetDatabase.CreateAsset(graph, assetPath);
            AssetDatabase.SaveAssets();
            return graph;
        }

        /// <summary>
        /// 加载已有 SkillGraphAsset。
        /// </summary>
        public static SkillGraphAsset LoadGraph(string assetPath)
        {
            return AssetDatabase.LoadAssetAtPath<SkillGraphAsset>(assetPath);
        }

        /// <summary>
        /// 获取图摘要信息。
        /// </summary>
        public static SkillGraphSummary GetGraphSummary(SkillGraphAsset graph)
        {
            if (graph == null) return null;

            var bridge = SkillGraphAbilityConfigGenerator.FindAbilityConfigForGraph(graph);

            return new SkillGraphSummary
            {
                Path = AssetDatabase.GetAssetPath(graph),
                DisplayName = graph.DisplayName,
                Version = graph.Version,
                NodeCount = graph.Nodes.Count,
                EdgeCount = graph.Edges.Count,
                EntryNodeId = graph.FindEntryNode()?.NodeId,
                NodeIds = graph.Nodes.ConvertAll(n => n.NodeId).ToArray(),
                HasBridgeConfig = bridge != null,
                BridgeConfigPath = bridge != null ? AssetDatabase.GetAssetPath(bridge) : null
            };
        }

        /// <summary>
        /// 获取图完整详情。
        /// </summary>
        public static SkillGraphDetail GetGraphDetail(string graphPath)
        {
            var graph = LoadGraph(graphPath);
            if (graph == null)
                return null;

            var detail = new SkillGraphDetail
            {
                Summary = GetGraphSummary(graph),
                Nodes = new List<SkillNodeDetail>(),
                Edges = new List<SkillEdgeDetail>()
            };

            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                var node = graph.Nodes[i];
                detail.Nodes.Add(new SkillNodeDetail
                {
                    NodeId = node.NodeId,
                    NodeType = node.NodeType,
                    Position = node.Position,
                    Enabled = node.Enabled,
                    Parameters = ExtractParameters(node)
                });
            }

            for (int i = 0; i < graph.Edges.Count; i++)
            {
                var edge = graph.Edges[i];
                detail.Edges.Add(new SkillEdgeDetail
                {
                    EdgeId = edge.EdgeId,
                    SourceNodeId = edge.SourceNodeId,
                    TargetNodeId = edge.TargetNodeId,
                    PortType = edge.PortType
                });
            }

            detail.Validation = ValidateGraph(graph);
            return detail;
        }

        /// <summary>
        /// 列出目录下全部 SkillGraph 资产摘要。
        /// </summary>
        public static List<SkillGraphSummary> ListGraphs(string folderPath = null)
        {
            string root = string.IsNullOrEmpty(folderPath)
                ? SkillGraphAbilityConfigGenerator.SkillGraphDir
                : folderPath;

            var results = new List<SkillGraphSummary>();
            if (!AssetDatabase.IsValidFolder(root))
                return results;

            string[] guids = AssetDatabase.FindAssets("t:SkillGraphAsset", new[] { root });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var graph = LoadGraph(path);
                if (graph != null)
                    results.Add(GetGraphSummary(graph));
            }

            return results;
        }

        /// <summary>
        /// 获取单个节点的连接详情。
        /// </summary>
        public static SkillNodeConnections GetGraphNodeConnections(string graphPath, string nodeId)
        {
            var graph = LoadGraph(graphPath);
            if (graph == null)
                return null;

            var node = graph.FindNode(nodeId);
            if (node == null)
                return null;

            var connections = new SkillNodeConnections
            {
                GraphPath = graphPath,
                Node = new SkillNodeDetail
                {
                    NodeId = node.NodeId,
                    NodeType = node.NodeType,
                    Position = node.Position,
                    Enabled = node.Enabled,
                    Parameters = ExtractParameters(node)
                },
                Incoming = new List<SkillEdgeDetail>(),
                Outgoing = new List<SkillEdgeDetail>()
            };

            var incoming = graph.GetEdgesTo(nodeId);
            for (int i = 0; i < incoming.Count; i++)
            {
                var edge = incoming[i];
                connections.Incoming.Add(new SkillEdgeDetail
                {
                    EdgeId = edge.EdgeId,
                    SourceNodeId = edge.SourceNodeId,
                    TargetNodeId = edge.TargetNodeId,
                    PortType = edge.PortType
                });
            }

            var outgoing = graph.GetEdgesFrom(nodeId);
            for (int i = 0; i < outgoing.Count; i++)
            {
                var edge = outgoing[i];
                connections.Outgoing.Add(new SkillEdgeDetail
                {
                    EdgeId = edge.EdgeId,
                    SourceNodeId = edge.SourceNodeId,
                    TargetNodeId = edge.TargetNodeId,
                    PortType = edge.PortType
                });
            }

            return connections;
        }

        // ═══════════════════════════════════════════
        //  节点操作
        // ═══════════════════════════════════════════

        /// <summary>
        /// 添加或更新节点。
        /// 如果 nodeId 已存在则更新参数，否则新增节点。
        /// </summary>
        public static SkillGraphNodeRecord UpsertNode(SkillGraphAsset graph, string nodeId, SkillGraphNodeType nodeType, Vector2 position, Dictionary<string, object> parameters = null)
        {
            if (graph == null) return null;

            var existing = graph.FindNode(nodeId);
            if (existing != null)
            {
                existing.Position = position;
                ApplyParameters(existing, parameters);
                EditorUtility.SetDirty(graph);
                return existing;
            }

            var record = SkillGraphNodeRecord.Create(nodeType);
            if (record == null) return null;

            record.NodeId = nodeId;
            record.Position = position;
            ApplyParameters(record, parameters);

            graph.Nodes.Add(record);
            EditorUtility.SetDirty(graph);
            return record;
        }

        /// <summary>
        /// 删除节点。
        /// </summary>
        public static bool RemoveNode(SkillGraphAsset graph, string nodeId)
        {
            if (graph == null) return false;
            bool removed = graph.RemoveNode(nodeId);
            if (removed) EditorUtility.SetDirty(graph);
            return removed;
        }

        /// <summary>
        /// 获取节点详情。
        /// </summary>
        public static SkillNodeDetail GetNodeDetail(SkillGraphAsset graph, string nodeId)
        {
            var record = graph?.FindNode(nodeId);
            if (record == null) return null;

            return new SkillNodeDetail
            {
                NodeId = record.NodeId,
                NodeType = record.NodeType,
                Position = record.Position,
                Enabled = record.Enabled,
                Parameters = ExtractParameters(record)
            };
        }

        // ═══════════════════════════════════════════
        //  边操作
        // ═══════════════════════════════════════════

        /// <summary>
        /// 添加边。
        /// </summary>
        public static SkillGraphEdgeRecord UpsertEdge(SkillGraphAsset graph, string sourceNodeId, string targetNodeId, SkillGraphPortType portType = SkillGraphPortType.Default)
        {
            if (graph == null) return null;
            var edge = graph.AddEdge(sourceNodeId, targetNodeId, portType);
            if (edge != null) EditorUtility.SetDirty(graph);
            return edge;
        }

        /// <summary>
        /// 删除边。
        /// </summary>
        public static bool RemoveEdge(SkillGraphAsset graph, string edgeId)
        {
            if (graph == null) return false;
            bool removed = graph.RemoveEdge(edgeId);
            if (removed) EditorUtility.SetDirty(graph);
            return removed;
        }

        // ═══════════════════════════════════════════
        //  校验操作
        // ═══════════════════════════════════════════

        /// <summary>
        /// 执行图校验，返回结构化诊断结果。
        /// </summary>
        public static SkillGraphValidationResult ValidateGraph(SkillGraphAsset graph)
        {
            if (graph == null)
            {
                return new SkillGraphValidationResult
                {
                    IsValid = false,
                    Errors = new List<SkillGraphDiagnostic>
                    {
                        new SkillGraphDiagnostic
                        {
                            Code = "NullGraph",
                            Severity = SkillGraphDiagnosticSeverity.Error,
                            Message = "Graph asset is null."
                        }
                    },
                    Warnings = new List<SkillGraphDiagnostic>()
                };
            }

            bool isValid = SkillGraphValidation.Validate(graph, out var errors, out var warnings);
            return new SkillGraphValidationResult
            {
                IsValid = isValid,
                Errors = errors,
                Warnings = warnings
            };
        }

        // ═══════════════════════════════════════════
        //  AbilityConfig bridge operations
        // ═══════════════════════════════════════════

        public static SkillGraphAbilityConfigResult CreateAbilityConfigForGraph(
            string graphPath,
            string configPath = null,
            int manaCost = 0,
            int? targetRangeOverride = null,
            string iconAssetPath = null,
            bool overwriteExisting = true)
        {
            var graph = LoadGraph(graphPath);
            if (graph == null)
            {
                return new SkillGraphAbilityConfigResult
                {
                    GraphPath = graphPath,
                    Created = false,
                    Updated = false,
                    Message = "Graph not found."
                };
            }

            var before = SkillGraphAbilityConfigGenerator.FindAbilityConfigForGraph(graph);
            var config = SkillGraphAbilityConfigGenerator.CreateOrSync(
                graph,
                configPath,
                manaCost,
                targetRangeOverride,
                iconAssetPath,
                overwriteExisting);

            return new SkillGraphAbilityConfigResult
            {
                GraphPath = graphPath,
                ConfigPath = config != null ? AssetDatabase.GetAssetPath(config) : null,
                Created = before == null && config != null,
                Updated = before != null && config != null,
                Message = config != null ? "Ability config created or synced." : "Ability config creation failed."
            };
        }

        public static SkillGraphAbilityConfigResult GetAbilityConfigForGraph(string graphPath)
        {
            var graph = LoadGraph(graphPath);
            if (graph == null)
            {
                return new SkillGraphAbilityConfigResult
                {
                    GraphPath = graphPath,
                    Message = "Graph not found."
                };
            }

            var config = SkillGraphAbilityConfigGenerator.FindAbilityConfigForGraph(graph);
            return new SkillGraphAbilityConfigResult
            {
                GraphPath = graphPath,
                ConfigPath = config != null ? AssetDatabase.GetAssetPath(config) : null,
                Created = false,
                Updated = false,
                Message = config != null ? "Ability config found." : "No ability config found for graph."
            };
        }

        public static SkillGraphBridgeSyncStatus GetBridgeSyncStatus(string graphPath)
        {
            var graph = LoadGraph(graphPath);
            if (graph == null)
            {
                return new SkillGraphBridgeSyncStatus
                {
                    GraphPath = graphPath,
                    GraphExists = false,
                    ExpectedConfigPath = null,
                    ActualConfigPath = null,
                    BridgeExists = false,
                    IsGraphReferenceMatch = false,
                    IsDisplayNameMatch = false,
                    IsTargetRangeMatch = false,
                    ExpectedDisplayName = null,
                    ActualDisplayName = null,
                    ExpectedTargetRange = 0,
                    ActualTargetRange = 0
                };
            }

            var config = SkillGraphAbilityConfigGenerator.FindAbilityConfigForGraph(graph);
            int expectedTargetRange = SkillGraphAbilityConfigGenerator.InferTargetRange(graph);

            return new SkillGraphBridgeSyncStatus
            {
                GraphPath = graphPath,
                GraphExists = true,
                ExpectedConfigPath = SkillGraphAbilityConfigGenerator.BuildAbilityConfigPath(graph),
                ActualConfigPath = config != null ? AssetDatabase.GetAssetPath(config) : null,
                BridgeExists = config != null,
                IsGraphReferenceMatch = config != null && config.SkillGraph == graph,
                IsDisplayNameMatch = config != null && config.DisplayName == graph.DisplayName,
                IsTargetRangeMatch = config != null && config.TargetRange == expectedTargetRange,
                ExpectedDisplayName = graph.DisplayName,
                ActualDisplayName = config != null ? config.DisplayName : null,
                ExpectedTargetRange = expectedTargetRange,
                ActualTargetRange = config != null ? config.TargetRange : 0
            };
        }

        public static SkillGraphBridgeValidationResult ValidateBridge(string graphPath)
        {
            var status = GetBridgeSyncStatus(graphPath);
            var diagnostics = new List<SkillGraphDiagnostic>();

            if (!status.GraphExists)
            {
                diagnostics.Add(new SkillGraphDiagnostic
                {
                    Code = "NullGraph",
                    Severity = SkillGraphDiagnosticSeverity.Error,
                    Category = SkillGraphDiagnosticCategory.Bridge,
                    Message = $"Graph not found: {graphPath}",
                    SuggestedFix = "Provide a valid graph asset path.",
                    SuggestedFixType = SkillGraphSuggestedFixType.None
                });
            }
            else if (!status.BridgeExists)
            {
                diagnostics.Add(new SkillGraphDiagnostic
                {
                    Code = SkillGraphValidation.BridgeMissing,
                    Severity = SkillGraphDiagnosticSeverity.Error,
                    Category = SkillGraphDiagnosticCategory.Bridge,
                    Message = $"No SkillGraphAbilityConfig found for graph '{graphPath}'.",
                    SuggestedFix = "Create a bridge config from this graph.",
                    SuggestedFixType = SkillGraphSuggestedFixType.CreateBridge,
                    Blocking = true
                });
            }
            else
            {
                if (!status.IsGraphReferenceMatch)
                {
                    diagnostics.Add(new SkillGraphDiagnostic
                    {
                        Code = SkillGraphValidation.WrongGraphReference,
                        Severity = SkillGraphDiagnosticSeverity.Error,
                        Category = SkillGraphDiagnosticCategory.Bridge,
                        Message = $"Bridge config '{status.ActualConfigPath}' references the wrong SkillGraph.",
                        SuggestedFix = "Sync the bridge config graph reference.",
                        SuggestedFixType = SkillGraphSuggestedFixType.SyncBridge,
                        Blocking = true
                    });
                }

                if (!status.IsDisplayNameMatch)
                {
                    diagnostics.Add(new SkillGraphDiagnostic
                    {
                        Code = SkillGraphValidation.DisplayNameDrift,
                        Severity = SkillGraphDiagnosticSeverity.Warning,
                        Category = SkillGraphDiagnosticCategory.Bridge,
                        Message = $"Bridge config display name '{status.ActualDisplayName}' differs from graph display name '{status.ExpectedDisplayName}'.",
                        SuggestedFix = "Sync the bridge config display name from the graph.",
                        SuggestedFixType = SkillGraphSuggestedFixType.SyncBridge,
                        Blocking = false
                    });
                }

                if (!status.IsTargetRangeMatch)
                {
                    diagnostics.Add(new SkillGraphDiagnostic
                    {
                        Code = SkillGraphValidation.TargetRangeDrift,
                        Severity = SkillGraphDiagnosticSeverity.Warning,
                        Category = SkillGraphDiagnosticCategory.Bridge,
                        Message = $"Bridge target range '{status.ActualTargetRange}' differs from graph-inferred range '{status.ExpectedTargetRange}'.",
                        SuggestedFix = "Sync the bridge config target range from the graph.",
                        SuggestedFixType = SkillGraphSuggestedFixType.SyncBridge,
                        Blocking = false
                    });
                }
            }

            return new SkillGraphBridgeValidationResult
            {
                Status = status,
                IsValid = diagnostics.FindAll(d => d.Blocking).Count == 0,
                Diagnostics = diagnostics
            };
        }

        // ═══════════════════════════════════════════
        //  Legacy readiness audit
        // ═══════════════════════════════════════════

        public static List<LegacyAbilityAuditResult> ListLegacyAbilityConfigs()
        {
            return SkillGraphLegacyAbilityAudit.RunAudit();
        }

        public static LegacyAbilityAuditSummary RunLegacyAbilityReadinessAudit()
        {
            var items = SkillGraphLegacyAbilityAudit.RunAudit();
            var summary = new LegacyAbilityAuditSummary
            {
                Items = items,
                Total = items.Count
            };

            for (int i = 0; i < items.Count; i++)
            {
                switch (items[i].Status)
                {
                    case LegacyAbilityReadinessStatus.ReadyForMigration:
                        summary.ReadyForMigration++;
                        break;
                    case LegacyAbilityReadinessStatus.NeedsProjectileSemantic:
                        summary.NeedsProjectileSemantic++;
                        break;
                    case LegacyAbilityReadinessStatus.BlockedByLegacyIncompleteImplementation:
                        summary.BlockedByLegacyIncompleteImplementation++;
                        break;
                    case LegacyAbilityReadinessStatus.NeedsManualDesign:
                        summary.NeedsManualDesign++;
                        break;
                    case LegacyAbilityReadinessStatus.SpecialCase:
                        summary.SpecialCase++;
                        break;
                }
            }

            return summary;
        }

        public static SkillGraphAbilityConfigResult SyncAbilityConfigFromGraph(
            string graphPath,
            string configPath = null,
            int manaCost = 0,
            int? targetRangeOverride = null,
            string iconAssetPath = null)
        {
            return CreateAbilityConfigForGraph(graphPath, configPath, manaCost, targetRangeOverride, iconAssetPath, overwriteExisting: true);
        }

        // ═══════════════════════════════════════════
        //  Helpers
        // ═══════════════════════════════════════════

        private static void ApplyParameters(SkillGraphNodeRecord record, Dictionary<string, object> parameters)
        {
            if (parameters == null) return;

            switch (record)
            {
                case SelectPrimaryTargetNodeRecord r:
                    if (parameters.TryGetValue("maxRange", out var mr)) r.MaxRange = (int)mr;
                    break;
                case SelectTargetPointNodeRecord r:
                    if (parameters.TryGetValue("maxRange", out var mr2)) r.MaxRange = (int)mr2;
                    break;
                case CollectTargetsInAreaNodeRecord r:
                    if (parameters.TryGetValue("radius", out var rad)) r.Radius = (int)rad;
                    if (parameters.TryGetValue("shape", out var shape)) r.Shape = (SkillGraphAreaShape)shape;
                    break;
                case DashToTargetNodeRecord r:
                    if (parameters.TryGetValue("maxRange", out var mr3)) r.MaxRange = (int)mr3;
                    if (parameters.TryGetValue("collisionDamage", out var cd)) r.CollisionDamage = (float)cd;
                    break;
                case ApplyDamageNodeRecord r:
                    if (parameters.TryGetValue("baseDamage", out var bd)) r.BaseDamage = (float)bd;
                    if (parameters.TryGetValue("damageType", out var dt)) r.DamageType = (SkillGraphDamageType)dt;
                    if (parameters.TryGetValue("isRanged", out var ir)) r.IsRanged = (bool)ir;
                    if (parameters.TryGetValue("canCrit", out var cc)) r.CanCrit = (bool)cc;
                    break;
                case ApplyKnockbackNodeRecord r:
                    if (parameters.TryGetValue("distance", out var dist)) r.Distance = (int)dist;
                    if (parameters.TryGetValue("height", out var h)) r.Height = (float)h;
                    if (parameters.TryGetValue("duration", out var dur)) r.Duration = (float)dur;
                    break;
            }
        }

        private static Dictionary<string, object> ExtractParameters(SkillGraphNodeRecord record)
        {
            var dict = new Dictionary<string, object>();
            switch (record)
            {
                case SelectPrimaryTargetNodeRecord r:
                    dict["maxRange"] = r.MaxRange;
                    break;
                case SelectTargetPointNodeRecord r:
                    dict["maxRange"] = r.MaxRange;
                    break;
                case CollectTargetsInAreaNodeRecord r:
                    dict["radius"] = r.Radius;
                    dict["shape"] = r.Shape;
                    break;
                case DashToTargetNodeRecord r:
                    dict["maxRange"] = r.MaxRange;
                    dict["collisionDamage"] = r.CollisionDamage;
                    break;
                case ApplyDamageNodeRecord r:
                    dict["baseDamage"] = r.BaseDamage;
                    dict["damageType"] = r.DamageType;
                    dict["isRanged"] = r.IsRanged;
                    dict["canCrit"] = r.CanCrit;
                    break;
                case ApplyKnockbackNodeRecord r:
                    dict["distance"] = r.Distance;
                    dict["height"] = r.Height;
                    dict["duration"] = r.Duration;
                    break;
            }
            return dict;
        }
    }

    // ═══════════════════════════════════════════
    //  Data Structures
    // ═══════════════════════════════════════════

    public class SkillGraphSummary
    {
        public string Path;
        public string DisplayName;
        public int Version;
        public int NodeCount;
        public int EdgeCount;
        public string EntryNodeId;
        public string[] NodeIds;
        public bool HasBridgeConfig;
        public string BridgeConfigPath;
    }

    public class SkillGraphDetail
    {
        public SkillGraphSummary Summary;
        public List<SkillNodeDetail> Nodes;
        public List<SkillEdgeDetail> Edges;
        public SkillGraphValidationResult Validation;
    }

    public class SkillNodeDetail
    {
        public string NodeId;
        public SkillGraphNodeType NodeType;
        public Vector2 Position;
        public bool Enabled;
        public Dictionary<string, object> Parameters;
    }

    public class SkillEdgeDetail
    {
        public string EdgeId;
        public string SourceNodeId;
        public string TargetNodeId;
        public SkillGraphPortType PortType;
    }

    public class SkillNodeConnections
    {
        public string GraphPath;
        public SkillNodeDetail Node;
        public List<SkillEdgeDetail> Incoming;
        public List<SkillEdgeDetail> Outgoing;
    }

    public class SkillGraphValidationResult
    {
        public bool IsValid;
        public List<SkillGraphDiagnostic> Errors;
        public List<SkillGraphDiagnostic> Warnings;
    }

    public class SkillGraphAbilityConfigResult
    {
        public string GraphPath;
        public string ConfigPath;
        public bool Created;
        public bool Updated;
        public string Message;
    }

    public class SkillGraphBridgeSyncStatus
    {
        public string GraphPath;
        public bool GraphExists;
        public string ExpectedConfigPath;
        public string ActualConfigPath;
        public bool BridgeExists;
        public bool IsGraphReferenceMatch;
        public bool IsDisplayNameMatch;
        public bool IsTargetRangeMatch;
        public string ExpectedDisplayName;
        public string ActualDisplayName;
        public int ExpectedTargetRange;
        public int ActualTargetRange;
    }

    public class SkillGraphBridgeValidationResult
    {
        public SkillGraphBridgeSyncStatus Status;
        public bool IsValid;
        public List<SkillGraphDiagnostic> Diagnostics;
    }

    public class LegacyAbilityAuditSummary
    {
        public int Total;
        public int ReadyForMigration;
        public int NeedsProjectileSemantic;
        public int BlockedByLegacyIncompleteImplementation;
        public int NeedsManualDesign;
        public int SpecialCase;
        public List<LegacyAbilityAuditResult> Items;
    }
}
