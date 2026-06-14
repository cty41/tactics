using System;
using System.Collections.Generic;
using System.Linq;

namespace Tactics.Common.Skills.Graph
{
    public class SkillGraphSpecFixResult
    {
        public SkillGraphSpec Spec;
        public List<string> FixesApplied = new();
        public List<SkillGraphDiagnostic> RemainingErrors = new();
        public bool AllFixed => RemainingErrors.Count == 0;
    }

    public static class SkillGraphSpecAutoFixer
    {
        public static SkillGraphSpecFixResult FixSpec(SkillGraphSpec spec)
        {
            var result = new SkillGraphSpecFixResult { Spec = spec };
            if (spec == null) return result;

            for (int round = 0; round < 5; round++)
            {
                var errors = SkillGraphValidation.ValidateSpec(spec);
                if (errors.Count == 0)
                {
                    result.RemainingErrors.Clear();
                    break;
                }

                var applied = new List<string>();

                foreach (var error in errors)
                {
                    string fix = TryFix(spec, error);
                    if (fix != null)
                        applied.Add(fix);
                }

                if (applied.Count == 0)
                {
                    result.RemainingErrors = errors;
                    break;
                }

                result.FixesApplied.AddRange(applied);
            }

            var finalErrors = SkillGraphValidation.ValidateSpec(spec);
            result.RemainingErrors = finalErrors;
            return result;
        }

        private static string TryFix(SkillGraphSpec spec, SkillGraphDiagnostic error)
        {
            switch (error.Code)
            {
                case SkillGraphValidation.MissingEntryNode:
                    return FixMissingEntryNode(spec);

                case SkillGraphValidation.NoTerminalNode:
                    return FixNoTerminalNode(spec);

                case SkillGraphValidation.SelfReferencingEdge:
                    return FixSelfReferencingEdge(spec, error);

                case SkillGraphValidation.MultipleEntryNodes:
                    return FixMultipleEntryNodes(spec);

                case SkillGraphValidation.InvalidEdgeSource:
                case SkillGraphValidation.InvalidEdgeTarget:
                    return FixDanglingEdge(spec, error);

                default:
                    return null;
            }
        }

        private static string FixMissingEntryNode(SkillGraphSpec spec)
        {
            var startNode = new SkillNodeSpec
            {
                Id = GenerateUniqueId(spec, "start"),
                Type = "Start"
            };
            spec.Nodes.Insert(0, startNode);

            if (spec.Nodes.Count > 1)
            {
                var firstNonStart = spec.Nodes.FirstOrDefault(n => n.Id != startNode.Id);
                if (firstNonStart != null)
                {
                    spec.Edges.Insert(0, new SkillEdgeSpec
                    {
                        Source = startNode.Id,
                        Target = firstNonStart.Id
                    });
                }
            }

            return $"Added Start node '{startNode.Id}'";
        }

        private static string FixNoTerminalNode(SkillGraphSpec spec)
        {
            var finishNode = new SkillNodeSpec
            {
                Id = GenerateUniqueId(spec, "finish"),
                Type = "Finish"
            };
            spec.Nodes.Add(finishNode);

            var lastNonTerminal = spec.Nodes.LastOrDefault(n =>
                !string.Equals(n.Type, "Finish", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(n.Type, "Fail", StringComparison.OrdinalIgnoreCase));

            if (lastNonTerminal != null)
            {
                spec.Edges.Add(new SkillEdgeSpec
                {
                    Source = lastNonTerminal.Id,
                    Target = finishNode.Id
                });
            }

            return $"Added Finish node '{finishNode.Id}'";
        }

        private static string FixSelfReferencingEdge(SkillGraphSpec spec, SkillGraphDiagnostic error)
        {
            string nodeId = error.NodeId;
            if (string.IsNullOrEmpty(nodeId)) return null;

            int removed = spec.Edges.RemoveAll(e => e.Source == nodeId && e.Target == nodeId);
            return removed > 0 ? $"Removed self-referencing edge on '{nodeId}'" : null;
        }

        private static string FixMultipleEntryNodes(SkillGraphSpec spec)
        {
            var startNodes = spec.Nodes.Where(n =>
                string.Equals(n.Type, "Start", StringComparison.OrdinalIgnoreCase)).ToList();

            if (startNodes.Count <= 1) return null;

            var keep = startNodes[0];
            for (int i = 1; i < startNodes.Count; i++)
            {
                var remove = startNodes[i];
                var outgoing = spec.Edges.Where(e => e.Source == remove.Id).ToList();
                foreach (var edge in outgoing)
                {
                    edge.Source = keep.Id;
                }
                spec.Nodes.Remove(remove);
            }

            return $"Merged {startNodes.Count} Start nodes into '{keep.Id}'";
        }

        private static string FixDanglingEdge(SkillGraphSpec spec, SkillGraphDiagnostic error)
        {
            string edgeSource = null;
            string edgeTarget = null;

            if (error.Code == SkillGraphValidation.InvalidEdgeSource)
            {
                var match = System.Text.RegularExpressions.Regex.Match(error.Message ?? "", @"'([^']+)'");
                if (match.Success) edgeSource = match.Groups[1].Value;
            }
            else
            {
                var match = System.Text.RegularExpressions.Regex.Match(error.Message ?? "", @"'([^']+)'");
                if (match.Success) edgeTarget = match.Groups[1].Value;
            }

            int removed = 0;
            if (edgeSource != null)
                removed = spec.Edges.RemoveAll(e => e.Source == edgeSource);
            else if (edgeTarget != null)
                removed = spec.Edges.RemoveAll(e => e.Target == edgeTarget);

            return removed > 0 ? $"Removed {removed} dangling edge(s)" : null;
        }

        private static string GenerateUniqueId(SkillGraphSpec spec, string baseName)
        {
            var existing = new HashSet<string>(spec.Nodes.Select(n => n.Id), StringComparer.OrdinalIgnoreCase);
            if (!existing.Contains(baseName)) return baseName;

            for (int i = 1; i < 100; i++)
            {
                string candidate = $"{baseName}_{i}";
                if (!existing.Contains(candidate)) return candidate;
            }

            return $"{baseName}_{Guid.NewGuid().ToString("N").Substring(0, 6)}";
        }
    }
}
