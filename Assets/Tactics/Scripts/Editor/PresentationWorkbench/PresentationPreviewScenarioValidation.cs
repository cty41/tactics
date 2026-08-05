#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Tactics.Common.Skills.Graph;

namespace Tactics.Editor.PresentationGraph
{
    internal static class PresentationPreviewScenarioValidation
    {
        internal static bool Validate(BattlePresentationGraph graph, out List<string> errors)
        {
            errors = new List<string>();
            if (graph == null)
            {
                errors.Add("PreviewScenarioMissingGraph: Presentation graph is missing.");
                return false;
            }
            if (!graph.HasPreviewScenario)
            {
                errors.Add("PreviewScenarioMissing: Full skill preview scenario is not configured.");
                return false;
            }

            for (int phaseIndex = 0; phaseIndex < graph.PreviewPhases.Count; phaseIndex++)
            {
                PresentationPreviewPhaseRecord phase = graph.PreviewPhases[phaseIndex];
                if (phase == null || phase.Cues == null || phase.Cues.Count == 0)
                {
                    errors.Add($"PreviewPhaseEmpty [{phaseIndex}]: Phase requires at least one cue.");
                    continue;
                }

                var uniqueCues = new HashSet<PresentationCueKind>();
                foreach (PresentationCueKind cue in phase.Cues)
                {
                    if (!uniqueCues.Add(cue))
                        errors.Add($"PreviewPhaseDuplicateCue [{phaseIndex}]: {cue} appears more than once.");
                    PresentationEntryNodeRecord entry = graph.FindEntry(cue);
                    if (entry == null || !entry.Enabled)
                        errors.Add($"PreviewPhaseMissingEntry [{phaseIndex}]: Enabled {cue} entry is required.");
                }

                if (!phase.Cues.Contains(phase.ContinuationCue))
                {
                    errors.Add(
                        $"PreviewPhaseMissingDriver [{phaseIndex}]: Continuation cue must belong to the phase.");
                    continue;
                }
                if (phase.AdvanceKind != PresentationPreviewAdvanceKind.Complete &&
                    !HasAdvancePoint(graph, phase.ContinuationCue, phase.AdvanceKind))
                {
                    errors.Add(
                        $"PreviewPhaseMissingMarker [{phaseIndex}]: {phase.ContinuationCue} has no " +
                        $"{phase.AdvanceKind} continuation point.");
                }
            }
            return errors.Count == 0;
        }

        private static bool HasAdvancePoint(
            BattlePresentationGraph graph,
            PresentationCueKind cue,
            PresentationPreviewAdvanceKind advanceKind)
        {
            PresentationEntryNodeRecord entry = graph.FindEntry(cue);
            if (entry == null)
                return false;
            var pending = new Stack<string>();
            var visited = new HashSet<string>();
            pending.Push(entry.NodeId);
            while (pending.Count > 0)
            {
                string nodeId = pending.Pop();
                if (!visited.Add(nodeId))
                    continue;
                PresentationNodeRecord node = graph.FindNode(nodeId);
                if (node is PresentationUnitTweenNodeRecord tween &&
                    advanceKind == PresentationPreviewAdvanceKind.Release &&
                    tween.EmitReleaseMarker)
                    return true;
                if (node is PresentationProjectileNodeRecord projectile &&
                    advanceKind == PresentationPreviewAdvanceKind.Impact &&
                    projectile.EmitImpactMarker)
                    return true;
                if (node is PresentationMarkerNodeRecord marker &&
                    ((advanceKind == PresentationPreviewAdvanceKind.Release &&
                      marker.Marker == PresentationMarkerKind.Release) ||
                     (advanceKind == PresentationPreviewAdvanceKind.Impact &&
                      marker.Marker == PresentationMarkerKind.Impact)))
                    return true;
                if (node is PresentationProceduralVfxNodeRecord procedural &&
                    advanceKind == PresentationPreviewAdvanceKind.Blocking &&
                    procedural.Recipe != null &&
                    procedural.Recipe.GetLayers(procedural.Cue).Any(layer =>
                        layer != null && layer.BlockingMarker > 0f))
                    return true;

                foreach (PresentationEdgeRecord edge in graph.GetEdgesFrom(nodeId))
                    pending.Push(edge.TargetNodeId);
            }
            return false;
        }
    }
}
#endif
