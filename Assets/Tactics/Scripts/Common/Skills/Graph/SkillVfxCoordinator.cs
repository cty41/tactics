using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tactics.Common.Units;
using Tactics.Common.Utilities;
using Tactics.Runtime.Utilities;
using UnityEngine;

namespace Tactics.Common.Skills.Graph
{
    /// <summary>
    /// Resolves recipes, starts primitive layers and waits only for authored contact markers.
    /// </summary>
    public sealed class SkillVfxCoordinator : ISkillVfxSink
    {
        private readonly SkillVfxRecipe _recipe;
        private readonly Transform _parent;
        private readonly int _sortingLayerId;
        private readonly int _baseSortingOrder;

        public SkillVfxCoordinator(SkillVfxRecipe recipe, IUnit source)
        {
            _recipe = recipe;
            var renderer = SkillVfxPositionUtility.ResolveRenderer(source);
            if (renderer != null)
            {
                _parent = renderer.transform.parent;
                _sortingLayerId = renderer.sortingLayerID;
                _baseSortingOrder = renderer.sortingOrder;
            }
        }

        public async Task PlayAsync(
            SkillVfxCueKind cue,
            SkillVfxCueContext context,
            CancellationToken cancellationToken)
        {
            if (_recipe == null || context == null)
                return;

            IReadOnlyList<SkillVfxPrimitiveLayer> layers = _recipe.GetLayers(cue);
            if (layers.Count == 0)
                return;

            var blockingTasks = new List<Task>(layers.Count);
            foreach (SkillVfxPrimitiveLayer layer in layers.Where(layer => layer != null))
            {
                try
                {
                    SkillVfxPrimitivePlayback playback = SkillVfxPrimitiveBuilder.Play(
                        _recipe,
                        layer,
                        context,
                        _sortingLayerId,
                        _baseSortingOrder,
                        _parent,
                        cancellationToken);
                    if (playback.BlockingTask != null)
                        blockingTasks.Add(playback.BlockingTask);
                    _ = ObserveCompletionAsync(playback.CompletionTask);
                }
                catch (System.Exception exception)
                {
                    TLog.Error($"[SkillVfxCoordinator] Failed to start {layer.PrimitiveKind}: {exception}");
                }
            }

            if (blockingTasks.Count > 0)
                await Task.WhenAll(blockingTasks);
        }

        private static async Task ObserveCompletionAsync(Task completionTask)
        {
            if (completionTask == null)
                return;
            try
            {
                await completionTask;
            }
            catch (System.OperationCanceledException)
            {
                // Primitive cleanup owns cancellation; no gameplay state depends on completion.
            }
            catch (System.Exception exception)
            {
                TLog.Error($"[SkillVfxCoordinator] Non-blocking primitive failed: {exception}");
            }
        }
    }

    /// <summary>
    /// Centralizes stable visual anchors so cue producers do not retain Unity object references.
    /// </summary>
    public static class SkillVfxPositionUtility
    {
        public static Vector3 ResolveUnitCenter(IUnit unit)
        {
            if (!IsUnitAvailable(unit))
                return Vector3.zero;

            SpriteRenderer renderer = ResolveRenderer(unit);
            if (renderer != null)
                return renderer.bounds.center;
            return unit?.WorldPosition.ToVector3() ?? Vector3.zero;
        }

        public static Vector3 ResolveUnitGround(IUnit unit)
        {
            if (!IsUnitAvailable(unit))
                return Vector3.zero;

            // The unit root is the stable logical tile landing point. Renderer bounds
            // include transparent sprite padding and vary between character sheets.
            return unit?.WorldPosition.ToVector3() ?? Vector3.zero;
        }

        public static SpriteRenderer ResolveRenderer(IUnit unit)
        {
            if (!IsUnitAvailable(unit) || unit is not Component component)
                return null;

            var directional = component.GetComponent<FourDirectionSpriteVisual>();
            if (directional?.TargetRenderer != null)
                return directional.TargetRenderer;

            foreach (SpriteRenderer renderer in component.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.gameObject.name == "Sprite")
                    return renderer;
            }
            return null;
        }

        private static bool IsUnitAvailable(IUnit unit)
        {
            // Interface null checks bypass Unity fake-null semantics after a unit is destroyed.
            return unit != null &&
                   (unit is not UnityEngine.Object unityObject || unityObject != null);
        }
    }
}
