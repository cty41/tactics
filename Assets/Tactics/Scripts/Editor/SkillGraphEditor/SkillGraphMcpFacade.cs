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

            return new SkillGraphSummary
            {
                DisplayName = graph.DisplayName,
                Version = graph.Version,
                NodeCount = graph.Nodes.Count,
                EdgeCount = graph.Edges.Count,
                EntryNodeId = graph.FindEntryNode()?.NodeId,
                NodeIds = graph.Nodes.ConvertAll(n => n.NodeId).ToArray()
            };
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
        public string DisplayName;
        public int Version;
        public int NodeCount;
        public int EdgeCount;
        public string EntryNodeId;
        public string[] NodeIds;
    }

    public class SkillNodeDetail
    {
        public string NodeId;
        public SkillGraphNodeType NodeType;
        public Vector2 Position;
        public bool Enabled;
        public Dictionary<string, object> Parameters;
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
}
