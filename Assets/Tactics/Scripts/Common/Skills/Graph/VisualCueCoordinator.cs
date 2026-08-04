using System.Threading;
using System.Threading.Tasks;
using Tactics.Common.Cells;
using Tactics.Common.Units;
using Tactics.Common.Utilities;
using UnityEngine;

namespace Tactics.Common.Skills.Graph
{
    /// <summary>
    /// Resolves skill anchors and plays transient VFX without affecting battle state.
    /// </summary>
    internal static class VisualCueCoordinator
    {
        public static Task PlayAsync(
            IUnit caster,
            IUnit primaryTarget,
            ICell targetPoint,
            VisualCueProfile profile,
            CancellationToken cancellationToken)
        {
            if (profile == null || profile.Prefab == null)
                return Task.CompletedTask;

            if (!TryResolvePosition(
                    caster,
                    primaryTarget,
                    targetPoint,
                    profile.Anchor,
                    out Vector3 position))
            {
                return Task.CompletedTask;
            }

            var referenceRenderer = ResolveRenderer(primaryTarget) ?? ResolveRenderer(caster);
            int sortingLayerId = referenceRenderer != null ? referenceRenderer.sortingLayerID : 0;
            int sortingOrder = (referenceRenderer != null ? referenceRenderer.sortingOrder : 0) +
                profile.SortingOrderOffset;
            Vector3 sourceWorldPosition = ResolveUnitCenter(caster, position);
            Vector3 targetWorldPosition = ResolveDirectionTarget(
                primaryTarget,
                targetPoint,
                position);
            Quaternion rotation = VisualCueTransformUtility.ResolveRotation(
                profile,
                sourceWorldPosition,
                targetWorldPosition);
            Vector3 scale = VisualCueTransformUtility.ResolveScale(
                profile,
                sourceWorldPosition,
                targetWorldPosition);

            if (profile.CompletionPolicy == VisualCueCompletionPolicy.FireAndForget)
            {
                return TransientVfxPool.PlayOneShot(
                    profile.Prefab,
                    position,
                    rotation,
                    scale,
                    profile.Lifetime,
                    sortingLayerId,
                    sortingOrder,
                    cancellationToken);
            }

            return TransientVfxPool.PlayAsync(
                profile.Prefab,
                position,
                rotation,
                scale,
                profile.Lifetime,
                sortingLayerId,
                sortingOrder,
                cancellationToken);
        }

        internal static Task PlayFromSnapshotAsync(
            IUnit caster,
            IUnit primaryTarget,
            VisualCueProfile profile,
            Vector3 sourceWorldPosition,
            Vector3 targetWorldPosition,
            Vector3 targetGroundWorldPosition,
            CancellationToken cancellationToken)
        {
            if (profile == null || profile.Prefab == null)
                return Task.CompletedTask;

            Vector3 position = profile.Anchor switch
            {
                VisualCueAnchor.Caster => sourceWorldPosition,
                VisualCueAnchor.PrimaryTargetGround => targetGroundWorldPosition,
                _ => targetWorldPosition
            };
            var referenceRenderer = ResolveRenderer(primaryTarget) ?? ResolveRenderer(caster);
            int sortingLayerId = referenceRenderer != null ? referenceRenderer.sortingLayerID : 0;
            int sortingOrder = (referenceRenderer != null ? referenceRenderer.sortingOrder : 0) +
                profile.SortingOrderOffset;
            Quaternion rotation = VisualCueTransformUtility.ResolveRotation(
                profile,
                sourceWorldPosition,
                targetWorldPosition);
            Vector3 scale = VisualCueTransformUtility.ResolveScale(
                profile,
                sourceWorldPosition,
                targetWorldPosition);
            return profile.CompletionPolicy == VisualCueCompletionPolicy.FireAndForget
                ? TransientVfxPool.PlayOneShot(
                    profile.Prefab,
                    position,
                    rotation,
                    scale,
                    profile.Lifetime,
                    sortingLayerId,
                    sortingOrder,
                    cancellationToken)
                : TransientVfxPool.PlayAsync(
                    profile.Prefab,
                    position,
                    rotation,
                    scale,
                    profile.Lifetime,
                    sortingLayerId,
                    sortingOrder,
                    cancellationToken);
        }

        private static bool TryResolvePosition(
            IUnit caster,
            IUnit primaryTarget,
            ICell targetPoint,
            VisualCueAnchor anchor,
            out Vector3 position)
        {
            switch (anchor)
            {
                case VisualCueAnchor.Caster:
                    return TryResolveUnitCenter(caster, out position);
                case VisualCueAnchor.PrimaryTarget:
                    return TryResolveUnitCenter(primaryTarget, out position);
                case VisualCueAnchor.PrimaryTargetGround:
                    return TryResolveUnitGround(primaryTarget, out position);
                default:
                    if (!IsMissingUnityObject(targetPoint))
                    {
                        position = targetPoint.WorldPosition.ToVector3();
                        return true;
                    }

                    if (!IsMissingUnityObject(primaryTarget) &&
                        !IsMissingUnityObject(primaryTarget.CurrentCell))
                    {
                        position = primaryTarget.CurrentCell.WorldPosition.ToVector3();
                        return true;
                    }

                    return TryResolveUnitCenter(primaryTarget, out position);
            }
        }

        private static bool TryResolveUnitCenter(IUnit unit, out Vector3 position)
        {
            if (IsMissingUnityObject(unit))
            {
                position = default;
                return false;
            }

            var renderer = ResolveRenderer(unit);
            if (renderer != null)
            {
                position = renderer.bounds.center;
                return true;
            }

            position = unit.WorldPosition.ToVector3();
            return true;
        }

        private static Vector3 ResolveUnitCenter(IUnit unit, Vector3 fallback)
        {
            return TryResolveUnitCenter(unit, out Vector3 position) ? position : fallback;
        }

        private static Vector3 ResolveDirectionTarget(
            IUnit primaryTarget,
            ICell targetPoint,
            Vector3 fallback)
        {
            if (TryResolveUnitCenter(primaryTarget, out Vector3 position))
                return position;
            if (!IsMissingUnityObject(targetPoint))
                return targetPoint.WorldPosition.ToVector3();
            return fallback;
        }

        private static bool TryResolveUnitGround(IUnit unit, out Vector3 position)
        {
            if (IsMissingUnityObject(unit))
            {
                position = default;
                return false;
            }

            position = SkillVfxPositionUtility.ResolveUnitGround(unit);
            return true;
        }

        private static bool IsMissingUnityObject(object value)
        {
            if (value == null)
                return true;
            return value is Object unityObject && unityObject == null;
        }

        private static SpriteRenderer ResolveRenderer(IUnit unit)
        {
            if (unit is not Component component)
                return null;

            var directional = component.GetComponent<FourDirectionSpriteVisual>();
            if (directional?.TargetRenderer != null)
                return directional.TargetRenderer;

            foreach (var renderer in component.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.gameObject.name == "Sprite")
                    return renderer;
            }

            return null;
        }
    }
}
