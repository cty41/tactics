using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tactics.Common.Battle.Runtime;
using Tactics.Common.Cells;
using Tactics.Common.Units;
using Tactics.Common.Units.Tween;
using Tactics.Common.Utilities;
using Tactics.Runtime.Utilities;
using UnityEngine;

namespace Tactics.Common.Skills.Graph
{
    /// <summary>
    /// Immutable inputs shared by every presentation backend during one cue playback.
    /// </summary>
    public sealed class PresentationExecutionContext
    {
        public PresentationExecutionContext(
            IUnit caster,
            IUnit primaryTarget,
            ICell targetPoint,
            int skillLevel,
            CancellationToken cancellationToken,
            IBattleRuntimeScope runtimeScope = null,
            UnitPoseFamily poseFamily = null,
            Action prepareRelease = null)
        {
            Caster = caster;
            PrimaryTarget = primaryTarget;
            TargetPoint = targetPoint;
            SkillLevel = Mathf.Max(1, skillLevel);
            CancellationToken = cancellationToken;
            RuntimeScope = runtimeScope;
            PoseFamily = poseFamily;
            PrepareRelease = prepareRelease;
            VfxCueContext = null;
            SourceWorldPosition = SkillVfxPositionUtility.ResolveUnitCenter(caster);
            TargetWorldPosition = primaryTarget != null
                ? SkillVfxPositionUtility.ResolveUnitCenter(primaryTarget)
                : targetPoint != null
                    ? targetPoint.WorldPosition.ToVector3() + Vector3.up * 0.45f
                    : SourceWorldPosition;
            TargetGroundWorldPosition = primaryTarget != null
                ? SkillVfxPositionUtility.ResolveUnitGround(primaryTarget)
                : targetPoint != null
                    ? targetPoint.WorldPosition.ToVector3()
                    : TargetWorldPosition;
        }

        internal PresentationExecutionContext(
            IUnit caster,
            IUnit primaryTarget,
            ICell targetPoint,
            int skillLevel,
            CancellationToken cancellationToken,
            IBattleRuntimeScope runtimeScope,
            Vector3 sourceWorldPosition,
            Vector3 targetWorldPosition,
            SkillVfxCueContext vfxCueContext = null)
        {
            Caster = caster;
            PrimaryTarget = primaryTarget;
            TargetPoint = targetPoint;
            SkillLevel = Mathf.Max(1, skillLevel);
            CancellationToken = cancellationToken;
            RuntimeScope = runtimeScope;
            PoseFamily = null;
            PrepareRelease = null;
            VfxCueContext = vfxCueContext;
            SourceWorldPosition = sourceWorldPosition;
            TargetWorldPosition = targetWorldPosition;
            TargetGroundWorldPosition = primaryTarget != null
                ? SkillVfxPositionUtility.ResolveUnitGround(primaryTarget)
                : targetPoint != null
                    ? targetPoint.WorldPosition.ToVector3()
                    : targetWorldPosition;
        }

        public IUnit Caster { get; }
        public IUnit PrimaryTarget { get; }
        public ICell TargetPoint { get; }
        public int SkillLevel { get; }
        public CancellationToken CancellationToken { get; }
        public IBattleRuntimeScope RuntimeScope { get; }
        public UnitPoseFamily PoseFamily { get; }
        public Action PrepareRelease { get; }
        internal SkillVfxCueContext VfxCueContext { get; }
        public Vector3 SourceWorldPosition { get; }
        public Vector3 TargetWorldPosition { get; }
        public Vector3 TargetGroundWorldPosition { get; }
        public Vector3 Direction
        {
            get
            {
                Vector3 direction = TargetWorldPosition - SourceWorldPosition;
                return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.right;
            }
        }
    }

    /// <summary>
    /// Executes visual-only presentation subgraphs while keeping gameplay state authoritative elsewhere.
    /// </summary>
    internal static class BattlePresentationCoordinator
    {
        public static bool HasEntry(BattlePresentationGraph graph, PresentationCueKind cue)
        {
            return graph != null && graph.FindEntry(cue) != null;
        }

        public static async Task<bool> TryPlayCueAsync(
            BattlePresentationGraph graph,
            PresentationCueKind cue,
            PresentationExecutionContext context,
            Action<PresentationMarkerKind> markerCallback = null)
        {
            if (!TryCreateRunner(graph, cue, context, markerCallback, out PresentationGraphRunner runner))
                return false;
            await runner.PlayAsync(cue);
            return true;
        }

        /// <summary>
        /// Waits only until the requested gameplay marker, then lets visual tail nodes finish under
        /// the battle runtime scope. This keeps impact timing authoritative without cutting off residue.
        /// </summary>
        public static async Task<bool> TryPlayCueUntilMarkerAsync(
            BattlePresentationGraph graph,
            PresentationCueKind cue,
            PresentationExecutionContext context,
            PresentationMarkerKind marker)
        {
            var markerReached = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            if (!TryCreateRunner(
                    graph,
                    cue,
                    context,
                    emittedMarker =>
                    {
                        if (emittedMarker == marker)
                            markerReached.TrySetResult(true);
                    },
                    out PresentationGraphRunner runner))
            {
                return false;
            }

            Task playback = runner.PlayAsync(cue);
            Task completed = await Task.WhenAny(markerReached.Task, playback);
            if (completed == playback)
            {
                await playback;
                return markerReached.Task.IsCompletedSuccessfully;
            }

            if (context.RuntimeScope != null)
                context.RuntimeScope.Track(playback);
            else
                _ = ObserveVisualTailAsync(playback, graph.name, cue);
            return true;
        }

        private static bool TryCreateRunner(
            BattlePresentationGraph graph,
            PresentationCueKind cue,
            PresentationExecutionContext context,
            Action<PresentationMarkerKind> markerCallback,
            out PresentationGraphRunner runner)
        {
            runner = null;
            if (graph == null || context == null || graph.FindEntry(cue) == null)
                return false;

            if (!BattlePresentationGraphValidation.Validate(graph, out List<PresentationGraphDiagnostic> errors))
            {
                string details = string.Join("; ", errors.Take(4).Select(error =>
                    $"{error.Code}:{error.NodeId ?? "graph"}"));
                TLog.Error($"[BattlePresentation] Invalid graph '{graph.name}': {details}");
                return false;
            }

            runner = new PresentationGraphRunner(graph, context, markerCallback);
            return true;
        }

        private static async Task ObserveVisualTailAsync(
            Task playback,
            string graphName,
            PresentationCueKind cue)
        {
            try
            {
                await playback;
            }
            catch (OperationCanceledException)
            {
                // Cancellation is an expected battle teardown path.
            }
            catch (Exception exception)
            {
                TLog.Error(
                    $"[BattlePresentation] Visual tail failed for graph '{graphName}', cue '{cue}': {exception}");
            }
        }
    }

    internal sealed class PresentationGraphRunner
    {
        private readonly BattlePresentationGraph _graph;
        private readonly PresentationExecutionContext _context;
        private readonly Action<PresentationMarkerKind> _markerCallback;
        private readonly Action<PresentationNodeRecord> _nodeVisitedCallback;
        private readonly HashSet<PresentationMarkerKind> _emittedMarkers = new();

        public PresentationGraphRunner(
            BattlePresentationGraph graph,
            PresentationExecutionContext context,
            Action<PresentationMarkerKind> markerCallback,
            Action<PresentationNodeRecord> nodeVisitedCallback = null)
        {
            _graph = graph;
            _context = context;
            _markerCallback = markerCallback;
            _nodeVisitedCallback = nodeVisitedCallback;
        }

        public Task PlayAsync(PresentationCueKind cue)
        {
            PresentationEntryNodeRecord entry = _graph.FindEntry(cue);
            return entry == null ? Task.CompletedTask : ExecuteNextAsync(entry.NodeId, null);
        }

        private async Task ExecuteNextAsync(string sourceNodeId, string stopBeforeNodeId)
        {
            List<PresentationEdgeRecord> edges = _graph.GetEdgesFrom(sourceNodeId);
            if (edges.Count == 0)
                return;
            await ExecuteNodeAsync(_graph.FindNode(edges[0].TargetNodeId), stopBeforeNodeId);
        }

        private async Task ExecuteNodeAsync(PresentationNodeRecord node, string stopBeforeNodeId)
        {
            _context.CancellationToken.ThrowIfCancellationRequested();
            if (node == null || node.NodeId == stopBeforeNodeId || node is PresentationFinishNodeRecord)
                return;
            if (!node.Enabled)
            {
                await ExecuteNextAsync(node.NodeId, stopBeforeNodeId);
                return;
            }

            _nodeVisitedCallback?.Invoke(node);

            switch (node)
            {
                case PresentationEntryNodeRecord:
                case PresentationJoinNodeRecord:
                    break;
                case PresentationUnitTweenNodeRecord tween:
                    await PlayUnitTweenAsync(tween);
                    break;
                case PresentationProjectileNodeRecord projectile:
                    await ProjectileVisualCoordinator.PlayFromSnapshotAsync(
                        _context.Caster,
                        _context.SourceWorldPosition,
                        _context.TargetWorldPosition,
                        projectile.Profile,
                        projectile.Speed,
                        projectile.FallbackTravelTime,
                        _context.CancellationToken,
                        _context.RuntimeScope);
                    if (projectile.EmitImpactMarker)
                        Emit(PresentationMarkerKind.Impact);
                    break;
                case PresentationPrefabFxNodeRecord prefabFx:
                    await PlayPrefabFxAsync(prefabFx.Profile);
                    break;
                case PresentationProceduralVfxNodeRecord procedural:
                    await PlayProceduralVfxAsync(procedural);
                    break;
                case PresentationDelayNodeRecord delay:
                    await global::Tactics.GameTimeService.DelayScaledAsync(
                        delay.Duration,
                        _context.CancellationToken);
                    break;
                case PresentationMarkerNodeRecord marker:
                    Emit(marker.Marker);
                    break;
                case PresentationForkNodeRecord fork:
                    await PlayForkAsync(fork, stopBeforeNodeId);
                    return;
            }

            await ExecuteNextAsync(node.NodeId, stopBeforeNodeId);
        }

        private async Task PlayUnitTweenAsync(PresentationUnitTweenNodeRecord tween)
        {
            bool handledRelease = false;
            await UnitAnimationCoordinator.PlayActionAsync(
                _context.Caster,
                tween.Action,
                _context.PoseFamily,
                _context.TargetPoint,
                _context.PrepareRelease,
                () =>
                {
                    if (tween.EmitReleaseMarker)
                        Emit(PresentationMarkerKind.Release);
                    handledRelease = true;
                    return Task.FromResult(true);
                },
                _context.CancellationToken);

            if (!handledRelease && tween.EmitReleaseMarker && !_context.CancellationToken.IsCancellationRequested)
                Emit(PresentationMarkerKind.Release);
        }

        private async Task PlayPrefabFxAsync(VisualCueProfile profile)
        {
            Task playback = VisualCueCoordinator.PlayFromSnapshotAsync(
                _context.Caster,
                _context.PrimaryTarget,
                profile,
                _context.SourceWorldPosition,
                _context.TargetWorldPosition,
                _context.TargetGroundWorldPosition,
                _context.CancellationToken);
            if (profile != null &&
                profile.CompletionPolicy == VisualCueCompletionPolicy.FireAndForget &&
                _context.RuntimeScope != null)
            {
                _context.RuntimeScope.Track(playback);
                if (_context.RuntimeScope.IsCancelling)
                    await playback;
                return;
            }
            await playback;
        }

        private Task PlayProceduralVfxAsync(PresentationProceduralVfxNodeRecord node)
        {
            var coordinator = new SkillVfxCoordinator(node.Recipe, _context.Caster);
            SkillVfxCueContext cueContext = _context.VfxCueContext ?? new SkillVfxCueContext(
                _context.SkillLevel,
                _context.SourceWorldPosition,
                _context.TargetWorldPosition,
                _context.Direction,
                primaryHitWorldPosition: _context.TargetWorldPosition);
            return coordinator.PlayAsync(node.Cue, cueContext, _context.CancellationToken);
        }

        private async Task PlayForkAsync(
            PresentationForkNodeRecord fork,
            string stopBeforeNodeId)
        {
            List<PresentationEdgeRecord> branches = _graph.GetEdgesFrom(fork.NodeId);
            Task[] tasks = branches.Select(edge =>
                ExecuteNodeAsync(_graph.FindNode(edge.TargetNodeId), fork.JoinNodeId)).ToArray();
            await Task.WhenAll(tasks);
            PresentationNodeRecord join = _graph.FindNode(fork.JoinNodeId);
            if (join != null)
                await ExecuteNextAsync(join.NodeId, stopBeforeNodeId);
        }

        private void Emit(PresentationMarkerKind marker)
        {
            if (_emittedMarkers.Add(marker))
                _markerCallback?.Invoke(marker);
        }
    }
}
