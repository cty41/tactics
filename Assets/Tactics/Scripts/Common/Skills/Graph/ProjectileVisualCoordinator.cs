using System.Threading;
using System.Threading.Tasks;
using DG.Tweening;
using Tactics.Common.Cells;
using Tactics.Common.Units;
using Tactics.Common.Utilities;
using UnityEngine;

namespace Tactics.Common.Skills.Graph
{
    /// <summary>
    /// Creates and awaits transient projectile renderers without introducing gameplay collision.
    /// </summary>
    internal static class ProjectileVisualCoordinator
    {
        private const float MinTravelDuration = 0.12f;
        private const float MaxTravelDuration = 0.75f;

        public static async Task PlayAsync(
            IUnit caster,
            IUnit target,
            ICell targetCell,
            ProjectileVisualProfile profile,
            float speed,
            float fallbackTravelTime,
            CancellationToken cancellationToken)
        {
            Vector3 start = ResolveUnitCenter(caster);
            Vector3 end = target != null
                ? ResolveUnitCenter(target)
                : targetCell.WorldPosition.ToVector3() + Vector3.up * 0.45f;
            Vector3 towardTarget = (end - start).normalized;
            start += towardTarget * 0.12f;
            float duration = ResolveDuration(Vector3.Distance(start, end), speed, fallbackTravelTime);

            if (profile == null || profile.Sprite == null)
            {
                await global::Tactics.GameTimeService.DelayScaledAsync(duration, cancellationToken);
                return;
            }

            var projectileObject = new GameObject("ProjectileVisual");
            var renderer = projectileObject.AddComponent<SpriteRenderer>();
            renderer.sprite = profile.Sprite;
            renderer.color = profile.Tint;
            renderer.sharedMaterial = profile.Material;
            var sourceRenderer = ResolveRenderer(caster);
            if (sourceRenderer != null)
            {
                renderer.sortingLayerID = sourceRenderer.sortingLayerID;
                renderer.sortingOrder = sourceRenderer.sortingOrder + profile.SortingOrderOffset;
            }

            Tween tween = null;
            var completion = new TaskCompletionSource<bool>();
            try
            {
                tween = ProjectileTweenBuilder.Build(projectileObject.transform, profile, start, end, duration)
                    .OnComplete(() => completion.TrySetResult(true))
                    .OnKill(() => completion.TrySetResult(true));
                using var registration = cancellationToken.Register(() =>
                {
                    completion.TrySetCanceled(cancellationToken);
                    if (tween.IsActive())
                        tween.Kill(false);
                });
                tween.Play();
                await completion.Task;
            }
            finally
            {
                if (tween != null && tween.IsActive())
                    tween.Kill(false);
                if (projectileObject != null)
                    Object.Destroy(projectileObject);
            }
        }

        public static float ResolveDuration(float worldDistance, float speed, float fallbackTravelTime)
        {
            if (speed <= 0f)
                return Mathf.Max(0.05f, fallbackTravelTime);

            return Mathf.Clamp(worldDistance / speed, MinTravelDuration, MaxTravelDuration);
        }

        private static Vector3 ResolveUnitCenter(IUnit unit)
        {
            var renderer = ResolveRenderer(unit);
            if (renderer != null)
                return renderer.bounds.center;

            return unit?.WorldPosition.ToVector3() ?? Vector3.zero;
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
