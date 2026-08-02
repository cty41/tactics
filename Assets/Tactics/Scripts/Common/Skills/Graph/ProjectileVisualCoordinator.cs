using System.Threading;
using System.Threading.Tasks;
using DG.Tweening;
using Tactics.Common.Battle.Runtime;
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
        public static async Task PlayAsync(
            IUnit caster,
            IUnit target,
            ICell targetCell,
            ProjectileVisualProfile profile,
            float speed,
            float fallbackTravelTime,
            CancellationToken cancellationToken,
            IBattleRuntimeScope runtimeScope = null)
        {
            Vector3 start = SkillVfxPositionUtility.ResolveUnitCenter(caster);
            Vector3 end = target != null
                ? SkillVfxPositionUtility.ResolveUnitCenter(target)
                : targetCell.WorldPosition.ToVector3() + Vector3.up * 0.45f;
            Vector3 towardTarget = (end - start).normalized;
            start += towardTarget * 0.12f;
            float duration = ProjectileVisualFactory.ResolveDuration(
                Vector3.Distance(start, end), speed, fallbackTravelTime);

            if (profile == null ||
                (profile.FlightPrefab == null && !ProjectileVisualFactory.CanRender(profile)))
            {
                await global::Tactics.GameTimeService.DelayScaledAsync(duration, cancellationToken);
                return;
            }

            var sourceRenderer = SkillVfxPositionUtility.ResolveRenderer(caster);
            int sortingLayerId = sourceRenderer != null ? sourceRenderer.sortingLayerID : 0;
            int sortingOrder = (sourceRenderer != null ? sourceRenderer.sortingOrder : 0) +
                profile.SortingOrderOffset;
            bool pooled = profile.FlightPrefab != null;
            GameObject projectileObject;
            Renderer renderer;
            if (pooled)
            {
                projectileObject = TransientVfxPool.Rent(
                    profile.FlightPrefab,
                    start,
                    Quaternion.identity,
                    1f,
                    sortingLayerId,
                    sortingOrder);
                projectileObject.name = "ProjectileVisual";
                var spriteRenderer = projectileObject.GetComponent<SpriteRenderer>();
                if (profile.Sprite != null)
                {
                    if (spriteRenderer == null)
                        spriteRenderer = projectileObject.AddComponent<SpriteRenderer>();
                    projectileObject.GetComponent<TransientVfxPoolMember>()?
                        .RegisterRuntimeSpriteRenderer(spriteRenderer);
                    spriteRenderer.enabled = true;
                    spriteRenderer.sprite = profile.Sprite;
                    spriteRenderer.color = profile.Tint;
                    if (profile.Material != null)
                        spriteRenderer.sharedMaterial = profile.Material;
                    spriteRenderer.sortingLayerID = sortingLayerId;
                    spriteRenderer.sortingOrder = sortingOrder;
                }

                TransientVfxPool.ApplySorting(projectileObject, sortingLayerId, sortingOrder);
                renderer = spriteRenderer != null
                    ? spriteRenderer
                    : projectileObject.GetComponentInChildren<Renderer>(true);
            }
            else
            {
                ProjectileVisualHandle handle = ProjectileVisualFactory.CreateProjectile(profile, sourceRenderer);
                projectileObject = handle.GameObject;
                renderer = handle.Renderer;
            }

            Tween tween = null;
            ProjectileTrailRuntime trail = null;
            var completion = new TaskCompletionSource<bool>();
            try
            {
                trail = new ProjectileTrailRuntime(profile, renderer, start);
                tween = ProjectileTweenBuilder.Build(projectileObject.transform, profile, start, end, duration)
                    .OnUpdate(() => trail.Sample(projectileObject.transform))
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
                if (profile.ImpactPrefab != null)
                {
                    Task impactTask = TransientVfxPool.PlayOneShot(
                        profile.ImpactPrefab,
                        end,
                        Quaternion.identity,
                        profile.ImpactScale,
                        profile.ImpactLifetime,
                        sortingLayerId,
                        sortingOrder,
                        cancellationToken);

                    if (runtimeScope == null)
                    {
                        await impactTask;
                    }
                    else
                    {
                        runtimeScope.Track(impactTask);
                        if (runtimeScope.IsCancelling)
                            await impactTask;
                    }
                }
            }
            finally
            {
                trail?.Stop(cancellationToken);
                if (tween != null && tween.IsActive())
                    tween.Kill(false);
                if (pooled)
                    TransientVfxPool.Return(projectileObject);
                else if (projectileObject != null)
                    Object.Destroy(projectileObject);
            }
        }

        /// <summary>
        /// Resolves projectile travel time using the shared runtime and editor-preview rules.
        /// </summary>
        public static float ResolveDuration(float worldDistance, float speed, float fallbackTravelTime)
        {
            return ProjectileVisualFactory.ResolveDuration(worldDistance, speed, fallbackTravelTime);
        }

    }
}
