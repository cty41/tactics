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
        public static async Task PlayAsync(
            IUnit caster,
            IUnit target,
            ICell targetCell,
            ProjectileVisualProfile profile,
            float speed,
            float fallbackTravelTime,
            CancellationToken cancellationToken)
        {
            Vector3 start = SkillVfxPositionUtility.ResolveUnitCenter(caster);
            Vector3 end = target != null
                ? SkillVfxPositionUtility.ResolveUnitCenter(target)
                : targetCell.WorldPosition.ToVector3() + Vector3.up * 0.45f;
            Vector3 towardTarget = (end - start).normalized;
            start += towardTarget * 0.12f;
            float duration = ProjectileVisualFactory.ResolveDuration(
                Vector3.Distance(start, end), speed, fallbackTravelTime);

            if (!ProjectileVisualFactory.CanRender(profile))
            {
                await global::Tactics.GameTimeService.DelayScaledAsync(duration, cancellationToken);
                return;
            }

            var sourceRenderer = SkillVfxPositionUtility.ResolveRenderer(caster);
            ProjectileVisualHandle handle = ProjectileVisualFactory.CreateProjectile(profile, sourceRenderer);
            GameObject projectileObject = handle.GameObject;
            Renderer renderer = handle.Renderer;

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
            }
            finally
            {
                trail?.Stop(cancellationToken);
                if (tween != null && tween.IsActive())
                    tween.Kill(false);
                if (projectileObject != null)
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
