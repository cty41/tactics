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
            Vector3 start = SkillVfxPositionUtility.ResolveUnitCenter(caster);
            Vector3 end = target != null
                ? SkillVfxPositionUtility.ResolveUnitCenter(target)
                : targetCell.WorldPosition.ToVector3() + Vector3.up * 0.45f;
            Vector3 towardTarget = (end - start).normalized;
            start += towardTarget * 0.12f;
            float duration = ResolveDuration(Vector3.Distance(start, end), speed, fallbackTravelTime);

            bool missingSprite = profile?.VisualKind == ProjectileVisualKind.Sprite && profile.Sprite == null;
            bool missingProceduralMaterial = profile?.VisualKind == ProjectileVisualKind.SoftDisc && profile.Material == null;
            if (profile == null || missingSprite || missingProceduralMaterial)
            {
                await global::Tactics.GameTimeService.DelayScaledAsync(duration, cancellationToken);
                return;
            }

            var projectileObject = new GameObject("ProjectileVisual");
            Renderer renderer;
            if (profile.VisualKind == ProjectileVisualKind.SoftDisc)
            {
                var filter = projectileObject.AddComponent<MeshFilter>();
                filter.sharedMesh = SkillVfxPrimitiveBuilder.SharedQuadMesh;
                var meshRenderer = projectileObject.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = profile.Material;
                var propertyBlock = new MaterialPropertyBlock();
                SkillVfxPrimitiveBuilder.ApplyStandaloneProperties(
                    meshRenderer,
                    propertyBlock,
                    profile.Tint,
                    1f,
                    1.8f,
                    SkillVfxShapeMode.SoftDisc,
                    radialInner: 0f,
                    radialOuter: 1f,
                    softness: 0.24f);
                renderer = meshRenderer;
            }
            else
            {
                var spriteRenderer = projectileObject.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = profile.Sprite;
                spriteRenderer.color = profile.Tint;
                // A newly created SpriteRenderer already owns Unity's compatible default
                // sprite material. Assigning a null profile material replaces that fallback
                // and can render the sprite with the magenta error shader.
                if (profile.Material != null)
                    spriteRenderer.sharedMaterial = profile.Material;
                renderer = spriteRenderer;
            }

            var sourceRenderer = SkillVfxPositionUtility.ResolveRenderer(caster);
            if (sourceRenderer != null)
            {
                renderer.sortingLayerID = sourceRenderer.sortingLayerID;
                renderer.sortingOrder = sourceRenderer.sortingOrder + profile.SortingOrderOffset;
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

        public static float ResolveDuration(float worldDistance, float speed, float fallbackTravelTime)
        {
            if (speed <= 0f)
                return Mathf.Max(0.05f, fallbackTravelTime);

            return Mathf.Clamp(worldDistance / speed, MinTravelDuration, MaxTravelDuration);
        }

    }
}
