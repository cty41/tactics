using System;
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
        //  Spec 操作
        // ═══════════════════════════════════════════

        /// <summary>
        /// 将 SkillGraphSpec 编译并应用到指定图资产。
        /// 流程：预校验 → 编译 → 替换节点/边 → 校验 → 保存。
        /// </summary>
        public static SkillGraphSpecApplyResult ApplySpec(string graphPath, SkillGraphSpec spec)
        {
            var result = new SkillGraphSpecApplyResult { GraphPath = graphPath };

            if (spec == null)
            {
                result.CompileErrors.Add(new SkillGraphDiagnostic
                {
                    Code = "NullSpec",
                    Severity = SkillGraphDiagnosticSeverity.Error,
                    Message = "SkillGraphSpec is null."
                });
                return result;
            }

            var compileResult = SkillGraphSpecCompiler.Compile(spec);
            result.CompileErrors.AddRange(compileResult.Errors);
            result.CompileWarnings.AddRange(compileResult.Warnings);

            if (!compileResult.Success)
                return result;

            var compiledAsset = compileResult.Asset;
            var existingAsset = LoadGraph(graphPath);

            if (existingAsset != null)
            {
                existingAsset.Clear();
                existingAsset.DisplayName = compiledAsset.DisplayName;

                for (int i = 0; i < compiledAsset.Nodes.Count; i++)
                {
                    var node = compiledAsset.Nodes[i];
                    existingAsset.Nodes.Add(node);
                }
                for (int i = 0; i < compiledAsset.Edges.Count; i++)
                {
                    var edge = compiledAsset.Edges[i];
                    existingAsset.Edges.Add(edge);
                }

                EditorUtility.SetDirty(existingAsset);
                AssetDatabase.SaveAssets();

                var validationResult = ValidateGraph(existingAsset);
                result.ValidationErrors.AddRange(validationResult.Errors);
                result.ValidationWarnings.AddRange(validationResult.Warnings);
                result.IsValid = validationResult.IsValid;
                result.Asset = existingAsset;
            }
            else
            {
                var dir = System.IO.Path.GetDirectoryName(graphPath);
                if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
                {
                    EnsureFolder(dir);
                }

                AssetDatabase.CreateAsset(compiledAsset, graphPath);
                AssetDatabase.SaveAssets();

                var validationResult = ValidateGraph(compiledAsset);
                result.ValidationErrors.AddRange(validationResult.Errors);
                result.ValidationWarnings.AddRange(validationResult.Warnings);
                result.IsValid = validationResult.IsValid;
                result.Asset = compiledAsset;
            }

            result.Success = result.CompileErrors.Count == 0 && result.ValidationErrors.Count == 0;
            return result;
        }

        /// <summary>
        /// 从已有 SkillGraphAsset 导出 SkillGraphSpec。
        /// </summary>
        public static SkillGraphSpec ExportSpec(string graphPath)
        {
            var asset = LoadGraph(graphPath);
            if (asset == null) return null;
            return SkillGraphSpecCompiler.ExportSpec(asset);
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            var parts = folderPath.Replace('\\', '/').Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
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
                    if (parameters.TryGetValue("minRange", out var minr)) r.MinRange = ToInt(minr);
                    if (parameters.TryGetValue("maxRange", out var mr)) r.MaxRange = ToInt(mr);
                    break;
                case SelectTargetPointNodeRecord r:
                    if (parameters.TryGetValue("maxRange", out var mr2)) r.MaxRange = ToInt(mr2);
                    break;
                case CollectTargetsInAreaNodeRecord r:
                    if (parameters.TryGetValue("radius", out var rad)) r.Radius = ToInt(rad);
                    if (parameters.TryGetValue("shape", out var shape)) r.Shape = (SkillGraphAreaShape)ToInt(shape);
                    if (parameters.TryGetValue("targetFaction", out var faction)) r.TargetFaction = (SkillGraphTargetFaction)ToInt(faction);
                    break;
                case DashToTargetNodeRecord r:
                    if (parameters.TryGetValue("maxRange", out var mr3)) r.MaxRange = ToInt(mr3);
                    if (parameters.TryGetValue("collisionDamage", out var cd)) r.CollisionDamage = ToFloat(cd);
                    break;
                case ApplyDamageNodeRecord r:
                    if (parameters.TryGetValue("baseDamage", out var bd)) r.BaseDamage = ToFloat(bd);
                    if (parameters.TryGetValue("damageType", out var dt)) r.DamageType = (SkillGraphDamageType)ToInt(dt);
                    if (parameters.TryGetValue("isRanged", out var ir)) r.IsRanged = ToBool(ir);
                    if (parameters.TryGetValue("canCrit", out var cc)) r.CanCrit = ToBool(cc);
                    if (parameters.TryGetValue("accuracyPenalty", out var ap)) r.AccuracyPenalty = ToFloat(ap);
                    break;
                case ApplyKnockbackNodeRecord r:
                    if (parameters.TryGetValue("distance", out var dist)) r.Distance = ToInt(dist);
                    if (parameters.TryGetValue("height", out var h)) r.Height = ToFloat(h);
                    if (parameters.TryGetValue("duration", out var dur)) r.Duration = ToFloat(dur);
                    break;
                case ProjectileLaunchNodeRecord r:
                    if (parameters.TryGetValue("travelTime", out var tt)) r.TravelTime = ToFloat(tt);
                    if (parameters.TryGetValue("speed", out var sp)) r.Speed = ToFloat(sp);
                    if (parameters.TryGetValue("dropOnHit", out var doh)) r.DropOnHit = ToBool(doh);
                    if (parameters.TryGetValue("dropSearchRadius", out var dsr)) r.DropSearchRadius = ToInt(dsr);
                    break;
                case ApplyBuffNodeRecord r:
                    if (parameters.TryGetValue("duration", out var buffDur)) r.Duration = ToInt(buffDur);
                    if (parameters.TryGetValue("buffAssetPath", out var bp) && bp is string buffPath && !string.IsNullOrEmpty(buffPath))
                    {
                        var buffConfig = AssetDatabase.LoadAssetAtPath<Tactics.Common.Units.Buffs.BuffConfig>(buffPath);
                        if (buffConfig != null) r.BuffConfig = buffConfig;
                    }
                    break;
                case SelectAllyNodeRecord r:
                    if (parameters.TryGetValue("maxRange", out var allyRange)) r.MaxRange = ToInt(allyRange);
                    break;
                case ApplyHealNodeRecord r:
                    if (parameters.TryGetValue("healAmount", out var healAmt)) r.HealAmount = ToFloat(healAmt);
                    break;
                case DashToAllyNodeRecord r:
                    if (parameters.TryGetValue("maxRange", out var dashAllyRange)) r.MaxRange = ToInt(dashAllyRange);
                    break;
                case LaunchUnitNodeRecord r:
                    if (parameters.TryGetValue("launchDistance", out var ld)) r.LaunchDistance = ToInt(ld);
                    if (parameters.TryGetValue("landingDamage", out var ldm)) r.LandingDamage = ToFloat(ldm);
                    if (parameters.TryGetValue("flightHeight", out var fh)) r.FlightHeight = ToFloat(fh);
                    if (parameters.TryGetValue("flightDuration", out var fd)) r.FlightDuration = ToFloat(fd);
                    if (parameters.TryGetValue("bounceHeight", out var bounch)) r.BounceHeight = ToFloat(bounch);
                    if (parameters.TryGetValue("bounceDuration", out var bouncd)) r.BounceDuration = ToFloat(bouncd);
                    break;
                case SummonUnitNodeRecord r:
                    if (parameters.TryGetValue("unitPrefabPath", out var upp) && upp is string prefabPath) r.UnitPrefabPath = prefabPath;
                    if (parameters.TryGetValue("requiresCorpse", out var rc)) r.RequiresCorpse = ToBool(rc);
                    if (parameters.TryGetValue("summonName", out var sn) && sn is string summonName) r.SummonName = summonName;
                    break;
                case SelectMoveDestinationNodeRecord r:
                    if (parameters.TryGetValue("respectMovementRules", out var rmr)) r.RespectMovementRules = ToBool(rmr);
                    break;
                case ExecuteMoveNodeRecord r:
                    if (parameters.TryGetValue("consumeMovementPoints", out var cmp)) r.ConsumeMovementPoints = ToBool(cmp);
                    if (parameters.TryGetValue("markAsBasicAbilityUsed", out var mabu)) r.MarkAsBasicAbilityUsed = ToBool(mabu);
                    break;
                case TeleportNodeRecord r:
                    if (parameters.TryGetValue("maxRange", out var teleportRange)) r.MaxRange = ToInt(teleportRange);
                    break;
                case MultiStabNodeRecord r:
                    if (parameters.TryGetValue("segmentCount", out var segmentCount)) r.SegmentCount = ToInt(segmentCount);
                    if (parameters.TryGetValue("damagePerSegment", out var segmentDamage)) r.DamagePerSegment = ToFloat(segmentDamage);
                    break;
                case ApplyShieldNodeRecord r:
                    if (parameters.TryGetValue("attributeMultiplier", out var shieldMultiplier)) r.AttributeMultiplier = ToFloat(shieldMultiplier);
                    break;
            }
        }

        private static int ToInt(object v) => v is long l ? (int)l : Convert.ToInt32(v);
        private static float ToFloat(object v) => v is double d ? (float)d : Convert.ToSingle(v);
        private static bool ToBool(object v) => v is bool b ? b : Convert.ToBoolean(v);

        private static Dictionary<string, object> ExtractParameters(SkillGraphNodeRecord record)
        {
            var dict = new Dictionary<string, object>();
            switch (record)
            {
                case SelectPrimaryTargetNodeRecord r:
                    dict["minRange"] = r.MinRange;
                    dict["maxRange"] = r.MaxRange;
                    break;
                case SelectTargetPointNodeRecord r:
                    dict["maxRange"] = r.MaxRange;
                    break;
                case CollectTargetsInAreaNodeRecord r:
                    dict["radius"] = r.Radius;
                    dict["shape"] = r.Shape;
                    dict["targetFaction"] = r.TargetFaction;
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
                    dict["accuracyPenalty"] = r.AccuracyPenalty;
                    break;
                case ApplyKnockbackNodeRecord r:
                    dict["distance"] = r.Distance;
                    dict["height"] = r.Height;
                    dict["duration"] = r.Duration;
                    break;
                case ProjectileLaunchNodeRecord r:
                    dict["travelTime"] = r.TravelTime;
                    dict["speed"] = r.Speed;
                    dict["dropOnHit"] = r.DropOnHit;
                    dict["dropSearchRadius"] = r.DropSearchRadius;
                    break;
                case ApplyBuffNodeRecord r:
                    dict["duration"] = r.Duration;
                    dict["buffAssetPath"] = r.BuffConfig != null ? AssetDatabase.GetAssetPath(r.BuffConfig) : null;
                    break;
                case SelectAllyNodeRecord r:
                    dict["maxRange"] = r.MaxRange;
                    break;
                case ApplyHealNodeRecord r:
                    dict["healAmount"] = r.HealAmount;
                    break;
                case DashToAllyNodeRecord r:
                    dict["maxRange"] = r.MaxRange;
                    break;
                case LaunchUnitNodeRecord r:
                    dict["launchDistance"] = r.LaunchDistance;
                    dict["landingDamage"] = r.LandingDamage;
                    dict["flightHeight"] = r.FlightHeight;
                    dict["flightDuration"] = r.FlightDuration;
                    dict["bounceHeight"] = r.BounceHeight;
                    dict["bounceDuration"] = r.BounceDuration;
                    break;
                case SummonUnitNodeRecord r:
                    dict["unitPrefabPath"] = r.UnitPrefabPath;
                    dict["requiresCorpse"] = r.RequiresCorpse;
                    dict["summonName"] = r.SummonName;
                    break;
                case SelectMoveDestinationNodeRecord r:
                    dict["respectMovementRules"] = r.RespectMovementRules;
                    break;
                case ExecuteMoveNodeRecord r:
                    dict["consumeMovementPoints"] = r.ConsumeMovementPoints;
                    dict["markAsBasicAbilityUsed"] = r.MarkAsBasicAbilityUsed;
                    break;
                case TeleportNodeRecord r:
                    dict["maxRange"] = r.MaxRange;
                    break;
                case MultiStabNodeRecord r:
                    dict["segmentCount"] = r.SegmentCount;
                    dict["damagePerSegment"] = r.DamagePerSegment;
                    break;
                case ApplyShieldNodeRecord r:
                    dict["attributeMultiplier"] = r.AttributeMultiplier;
                    break;
            }
            return dict;
        }

        public static void ApplyParametersPublic(SkillGraphNodeRecord record, Dictionary<string, object> parameters)
            => ApplyParameters(record, parameters);

        public static Dictionary<string, object> ExtractParametersPublic(SkillGraphNodeRecord record)
            => ExtractParameters(record);

        // ═══════════════════════════════════════════
        //  RoleConfig Mount Operations
        // ═══════════════════════════════════════════

        public static RoleConfigAbilitiesResult ListRoleConfigAbilities(string roleConfigPath)
        {
            var result = new RoleConfigAbilitiesResult { RoleConfigPath = roleConfigPath };
            var roleConfig = AssetDatabase.LoadAssetAtPath<Tactics.Common.Units.Classes.RoleConfig>(roleConfigPath);
            if (roleConfig == null)
            {
                result.Errors.Add($"RoleConfig not found: {roleConfigPath}");
                return result;
            }

            result.RoleConfigName = roleConfig.DisplayName;
            var abilities = roleConfig.Abilities;
            for (int i = 0; i < abilities.Count; i++)
            {
                var ability = abilities[i];
                if (ability == null)
                {
                    result.Entries.Add(new RoleAbilityEntry { Index = i, AssetName = "null", DisplayName = "null", IsSkillGraphBridge = false });
                    continue;
                }

                string assetPath = AssetDatabase.GetAssetPath(ability);
                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                bool isGraph = ability is Tactics.Common.Units.Abilities.SkillGraphAbilityConfig;

                result.Entries.Add(new RoleAbilityEntry
                {
                    Index = i,
                    AssetPath = assetPath,
                    AssetGuid = guid,
                    AssetName = ability.name,
                    AssetType = ability.GetType().Name,
                    DisplayName = ability.DisplayName,
                    IsSkillGraphBridge = isGraph
                });
            }

            return result;
        }

        public static RoleConfigLookupResult GetRoleConfigForPrefab(string prefabPath)
        {
            var result = new RoleConfigLookupResult { PrefabPath = prefabPath };
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                result.Errors.Add($"Prefab not found: {prefabPath}");
                return result;
            }

            result.PrefabName = prefab.name;
            var unit = prefab.GetComponent<Tactics.Common.Units.Unit>();
            if (unit == null)
            {
                result.HasRoleConfig = false;
                result.Warnings.Add("Prefab has no Unit component.");
                return result;
            }

            var so = new SerializedObject(unit);
            var roleConfigProp = so.FindProperty("_roleConfig");
            var roleConfig = roleConfigProp?.objectReferenceValue as Tactics.Common.Units.Classes.RoleConfig;
            if (roleConfig == null)
            {
                result.HasRoleConfig = false;
                result.Warnings.Add("Unit has no RoleConfig assigned.");
                return result;
            }

            result.HasRoleConfig = true;
            result.RoleConfigPath = AssetDatabase.GetAssetPath(roleConfig);
            result.RoleConfigName = roleConfig.DisplayName;

            var abilitiesResult = ListRoleConfigAbilities(result.RoleConfigPath);
            result.Entries = abilitiesResult.Entries;
            result.Warnings.AddRange(abilitiesResult.Warnings);
            result.Errors.AddRange(abilitiesResult.Errors);

            return result;
        }

        public static RoleConfigAttachResult AttachAbilityConfigToRoleConfig(
            string configPath,
            string roleConfigPath,
            bool append = true,
            int? replaceIndex = null,
            string replaceByName = null,
            bool dryRun = true)
        {
            var result = new RoleConfigAttachResult
            {
                ConfigPath = configPath,
                RoleConfigPath = roleConfigPath,
                DryRun = dryRun
            };

            var abilityConfig = AssetDatabase.LoadAssetAtPath<Tactics.Common.Units.Abilities.AbilityConfig>(configPath);
            if (abilityConfig == null)
            {
                result.Errors.Add($"AbilityConfig not found: {configPath}");
                result.Message = "AbilityConfig not found.";
                return result;
            }

            var roleConfig = AssetDatabase.LoadAssetAtPath<Tactics.Common.Units.Classes.RoleConfig>(roleConfigPath);
            if (roleConfig == null)
            {
                result.Errors.Add($"RoleConfig not found: {roleConfigPath}");
                result.Message = "RoleConfig not found.";
                return result;
            }

            var beforeResult = ListRoleConfigAbilities(roleConfigPath);
            result.Before = beforeResult.Entries;

            var so = new SerializedObject(roleConfig);
            var abilitiesProp = so.FindProperty("_abilities");
            if (abilitiesProp == null)
            {
                result.Errors.Add("RoleConfig has no _abilities serialized field.");
                result.Message = "Missing _abilities field.";
                return result;
            }

            bool changed = false;
            int actionIndex = -1;
            string actionType = "none";

            if (replaceIndex.HasValue)
            {
                // Replace by index
                int idx = replaceIndex.Value;
                if (idx < 0 || idx >= abilitiesProp.arraySize)
                {
                    result.Errors.Add($"ReplaceIndex {idx} is out of range (0-{abilitiesProp.arraySize - 1}).");
                    result.Message = "ReplaceIndex out of range.";
                    return result;
                }

                abilitiesProp.GetArrayElementAtIndex(idx).objectReferenceValue = abilityConfig;
                changed = true;
                actionIndex = idx;
                actionType = "replace_index";
            }
            else if (!string.IsNullOrEmpty(replaceByName))
            {
                // Replace by name
                bool found = false;
                for (int i = 0; i < abilitiesProp.arraySize; i++)
                {
                    var elem = abilitiesProp.GetArrayElementAtIndex(i);
                    var existingAbility = elem.objectReferenceValue as Tactics.Common.Units.Abilities.AbilityConfig;
                    if (existingAbility != null &&
                        (existingAbility.DisplayName == replaceByName || existingAbility.name == replaceByName))
                    {
                        elem.objectReferenceValue = abilityConfig;
                        changed = true;
                        actionIndex = i;
                        actionType = "replace_by_name";
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    result.Warnings.Add($"No ability found with name '{replaceByName}'. No changes made.");
                }
            }
            else if (append)
            {
                // Append mode
                bool alreadyExists = false;
                for (int i = 0; i < abilitiesProp.arraySize; i++)
                {
                    var elem = abilitiesProp.GetArrayElementAtIndex(i);
                    if (elem.objectReferenceValue == abilityConfig)
                    {
                        alreadyExists = true;
                        break;
                    }
                }

                if (alreadyExists)
                {
                    result.Warnings.Add($"AbilityConfig '{abilityConfig.name}' is already attached to this RoleConfig.");
                }
                else
                {
                    int newIndex = abilitiesProp.arraySize;
                    abilitiesProp.arraySize++;
                    abilitiesProp.GetArrayElementAtIndex(newIndex).objectReferenceValue = abilityConfig;
                    changed = true;
                    actionIndex = newIndex;
                    actionType = "append";
                }
            }

            if (changed && !dryRun)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(roleConfig);
                AssetDatabase.SaveAssets();
            }

            var afterResult = ListRoleConfigAbilities(roleConfigPath);
            result.After = afterResult.Entries;
            result.Changed = changed;
            result.Message = $"Action={actionType}, Index={actionIndex}, Changed={changed}, DryRun={dryRun}";

            return result;
        }

        public static RoleConfigDetachResult DetachAbilityConfigFromRoleConfig(
            string configPath,
            string roleConfigPath,
            bool dryRun = true)
        {
            var result = new RoleConfigDetachResult
            {
                ConfigPath = configPath,
                RoleConfigPath = roleConfigPath,
                DryRun = dryRun
            };

            var abilityConfig = AssetDatabase.LoadAssetAtPath<Tactics.Common.Units.Abilities.AbilityConfig>(configPath);
            if (abilityConfig == null)
            {
                result.Errors.Add($"AbilityConfig not found: {configPath}");
                result.Message = "AbilityConfig not found.";
                return result;
            }

            var roleConfig = AssetDatabase.LoadAssetAtPath<Tactics.Common.Units.Classes.RoleConfig>(roleConfigPath);
            if (roleConfig == null)
            {
                result.Errors.Add($"RoleConfig not found: {roleConfigPath}");
                result.Message = "RoleConfig not found.";
                return result;
            }

            var beforeResult = ListRoleConfigAbilities(roleConfigPath);
            result.Before = beforeResult.Entries;

            var so = new SerializedObject(roleConfig);
            var abilitiesProp = so.FindProperty("_abilities");
            if (abilitiesProp == null)
            {
                result.Errors.Add("RoleConfig has no _abilities serialized field.");
                result.Message = "Missing _abilities field.";
                return result;
            }

            // Find the ability by reference
            int foundIndex = -1;
            for (int i = 0; i < abilitiesProp.arraySize; i++)
            {
                var elem = abilitiesProp.GetArrayElementAtIndex(i);
                if (elem.objectReferenceValue == abilityConfig)
                {
                    foundIndex = i;
                    break;
                }
            }

            if (foundIndex < 0)
            {
                result.Warnings.Add($"AbilityConfig '{abilityConfig.name}' is not attached to this RoleConfig.");
                result.Changed = false;
                result.Message = "AbilityConfig not found in RoleConfig. No changes made.";
                return result;
            }

            // Apply removal if not dry-run
            if (!dryRun)
            {
                // Unity ObjectReference array: first null the reference, then delete element
                abilitiesProp.GetArrayElementAtIndex(foundIndex).objectReferenceValue = null;
                abilitiesProp.DeleteArrayElementAtIndex(foundIndex);

                // Double-check for null leftover (Unity serialization quirk)
                if (foundIndex < abilitiesProp.arraySize &&
                    abilitiesProp.GetArrayElementAtIndex(foundIndex).objectReferenceValue == null)
                {
                    abilitiesProp.DeleteArrayElementAtIndex(foundIndex);
                }

                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(roleConfig);
                AssetDatabase.SaveAssets();
            }

            // Build projected after (works for both dry-run and real apply)
            var afterEntries = new List<RoleAbilityEntry>();
            int newIndex = 0;
            for (int i = 0; i < result.Before.Count; i++)
            {
                var entry = result.Before[i];
                if (entry.AssetPath != configPath)
                {
                    var projected = new RoleAbilityEntry
                    {
                        Index = newIndex++,
                        AssetPath = entry.AssetPath,
                        AssetGuid = entry.AssetGuid,
                        AssetName = entry.AssetName,
                        AssetType = entry.AssetType,
                        DisplayName = entry.DisplayName,
                        IsSkillGraphBridge = entry.IsSkillGraphBridge
                    };
                    afterEntries.Add(projected);
                }
            }

            result.After = afterEntries;
            result.Changed = true;
            result.Message = $"Removed '{abilityConfig.name}' at index {foundIndex}. Changed=true, DryRun={dryRun}";

            return result;
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

    public class SkillGraphSpecApplyResult
    {
        public string GraphPath;
        public bool Success;
        public bool IsValid;
        public SkillGraphAsset Asset;
        public List<SkillGraphDiagnostic> CompileErrors = new();
        public List<SkillGraphDiagnostic> CompileWarnings = new();
        public List<SkillGraphDiagnostic> ValidationErrors = new();
        public List<SkillGraphDiagnostic> ValidationWarnings = new();
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

    // ═══════════════════════════════════════════
    //  RoleConfig Mount Data Structures
    // ═══════════════════════════════════════════

    public class RoleAbilityEntry
    {
        public int Index;
        public string AssetPath;
        public string AssetGuid;
        public string AssetName;
        public string AssetType;
        public string DisplayName;
        public bool IsSkillGraphBridge;
    }

    public class RoleConfigAbilitiesResult
    {
        public string RoleConfigPath;
        public string RoleConfigName;
        public List<RoleAbilityEntry> Entries = new();
        public List<string> Warnings = new();
        public List<string> Errors = new();
    }

    public class RoleConfigLookupResult
    {
        public string PrefabPath;
        public string PrefabName;
        public string RoleConfigPath;
        public string RoleConfigName;
        public bool HasRoleConfig;
        public List<RoleAbilityEntry> Entries = new();
        public List<string> Warnings = new();
        public List<string> Errors = new();
    }

    public class RoleConfigAttachResult
    {
        public string ConfigPath;
        public string RoleConfigPath;
        public bool Changed;
        public bool DryRun;
        public List<RoleAbilityEntry> Before = new();
        public List<RoleAbilityEntry> After = new();
        public List<string> Warnings = new();
        public List<string> Errors = new();
        public string Message;
    }

    public class RoleConfigDetachResult
    {
        public string ConfigPath;
        public string RoleConfigPath;
        public bool Changed;
        public bool DryRun;
        public List<RoleAbilityEntry> Before = new();
        public List<RoleAbilityEntry> After = new();
        public List<string> Warnings = new();
        public List<string> Errors = new();
        public string Message;
    }
}
