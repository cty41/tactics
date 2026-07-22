using System;
using System.Collections.Generic;
using UnityEngine;
using Tactics.Common.Skills.Graph;

namespace Tactics.Editor.SkillGraphEditor
{
    public class SkillGraphSpecCompileResult
    {
        public SkillGraphAsset Asset;
        public List<SkillGraphDiagnostic> Errors = new();
        public List<SkillGraphDiagnostic> Warnings = new();
        public bool Success => Errors.Count == 0;
    }

    public static class SkillGraphSpecCompiler
    {
        public static SkillGraphSpecCompileResult Compile(SkillGraphSpec spec)
        {
            var result = new SkillGraphSpecCompileResult();

            if (spec == null)
            {
                result.Errors.Add(new SkillGraphDiagnostic
                {
                    Code = "NullSpec",
                    Severity = SkillGraphDiagnosticSeverity.Error,
                    Message = "SkillGraphSpec is null."
                });
                return result;
            }

            var preErrors = SkillGraphValidation.ValidateSpec(spec);
            if (preErrors.Count > 0)
            {
                result.Errors.AddRange(preErrors);
                return result;
            }

            var asset = ScriptableObject.CreateInstance<SkillGraphAsset>();
            asset.DisplayName = spec.DisplayName ?? "Untitled";
            CopyTargeting(spec.Targeting, asset.Targeting);

            var nodeMap = new Dictionary<string, SkillGraphNodeRecord>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < spec.Nodes.Count; i++)
            {
                var nodeSpec = spec.Nodes[i];
                if (!TryParseNodeType(nodeSpec.Type, out var nodeType))
                {
                    result.Errors.Add(new SkillGraphDiagnostic
                    {
                        Code = SkillGraphValidation.UnsupportedNodeType,
                        Severity = SkillGraphDiagnosticSeverity.Error,
                        Category = SkillGraphDiagnosticCategory.Structure,
                        NodeId = nodeSpec.Id,
                        Message = $"Unknown node type '{nodeSpec.Type}' for node '{nodeSpec.Id}'."
                    });
                    continue;
                }

                var record = SkillGraphNodeRecord.Create(nodeType);
                if (record == null)
                {
                    result.Errors.Add(new SkillGraphDiagnostic
                    {
                        Code = SkillGraphValidation.UnsupportedNodeType,
                        Severity = SkillGraphDiagnosticSeverity.Error,
                        NodeId = nodeSpec.Id,
                        Message = $"Failed to create node record for type '{nodeSpec.Type}'."
                    });
                    continue;
                }

                record.NodeId = nodeSpec.Id;
                ApplyParameters(record, nodeSpec.Parameters);
                asset.Nodes.Add(record);
                nodeMap[nodeSpec.Id] = record;
            }

            if (result.Errors.Count > 0) return result;

            for (int i = 0; i < spec.Edges.Count; i++)
            {
                var edgeSpec = spec.Edges[i];
                var portType = SkillGraphPortType.Default;
                if (!string.IsNullOrEmpty(edgeSpec.Port) &&
                    !Enum.TryParse(edgeSpec.Port, true, out portType))
                {
                    result.Warnings.Add(new SkillGraphDiagnostic
                    {
                        Code = "UnknownPortType",
                        Severity = SkillGraphDiagnosticSeverity.Warning,
                        Message = $"Unknown port type '{edgeSpec.Port}' on edge {edgeSpec.Source}->{edgeSpec.Target}. Using Default."
                    });
                    portType = SkillGraphPortType.Default;
                }

                asset.AddEdge(edgeSpec.Source, edgeSpec.Target, portType);
            }

            result.Asset = asset;
            return result;
        }

        public static SkillGraphSpec ExportSpec(SkillGraphAsset asset)
        {
            if (asset == null) return null;

            var spec = new SkillGraphSpec
            {
                DisplayName = asset.DisplayName,
                Description = null,
                Targeting = CloneTargeting(asset.Targeting)
            };

            for (int i = 0; i < asset.Nodes.Count; i++)
            {
                var node = asset.Nodes[i];
                spec.Nodes.Add(new SkillNodeSpec
                {
                    Id = node.NodeId,
                    Type = node.NodeType.ToString(),
                    Parameters = SkillGraphMcpFacade.ExtractParametersPublic(node)
                });
            }

            for (int i = 0; i < asset.Edges.Count; i++)
            {
                var edge = asset.Edges[i];
                spec.Edges.Add(new SkillEdgeSpec
                {
                    Source = edge.SourceNodeId,
                    Target = edge.TargetNodeId,
                    Port = edge.PortType != SkillGraphPortType.Default ? edge.PortType.ToString() : null
                });
            }

            return spec;
        }

        private static bool TryParseNodeType(string typeStr, out SkillGraphNodeType nodeType)
        {
            nodeType = SkillGraphNodeType.Start;
            if (string.IsNullOrEmpty(typeStr)) return false;
            return Enum.TryParse(typeStr, true, out nodeType);
        }

        private static void ApplyParameters(SkillGraphNodeRecord record, Dictionary<string, object> parameters)
        {
            SkillGraphMcpFacade.ApplyParametersPublic(record, parameters);
        }

        private static SkillTargetingProtocol CloneTargeting(SkillTargetingProtocol source)
        {
            var clone = new SkillTargetingProtocol();
            CopyTargeting(source, clone);
            return clone;
        }

        private static void CopyTargeting(SkillTargetingProtocol source, SkillTargetingProtocol destination)
        {
            if (source == null || destination == null)
                return;

            destination.Mode = source.Mode;
            destination.MinimumSelections = source.MinimumSelections;
            destination.MaximumSelections = source.MaximumSelections;
            destination.ConeDepth = source.ConeDepth;
            destination.ConeWidth = source.ConeWidth;
            destination.AllowsEmptyCell = source.AllowsEmptyCell;
            destination.UsesPathfinding = source.UsesPathfinding;
        }
    }
}
