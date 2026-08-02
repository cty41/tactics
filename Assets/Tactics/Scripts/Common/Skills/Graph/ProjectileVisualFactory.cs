using UnityEngine;

namespace Tactics.Common.Skills.Graph
{
    /// <summary>
    /// Builds transient projectile renderers and trail renderers shared by runtime and editor preview.
    /// </summary>
    /// <remarks>
    /// This factory owns visual construction only. Callers retain lifecycle, playback and gameplay timing.
    /// Keeping construction here prevents the offline preview from silently diverging from battle rendering.
    /// </remarks>
    internal static class ProjectileVisualFactory
    {
        private const float MinTravelDuration = 0.12f;
        private const float MaxTravelDuration = 0.75f;

        internal static bool CanRender(ProjectileVisualProfile profile)
        {
            if (profile == null)
                return false;

            return profile.VisualKind switch
            {
                ProjectileVisualKind.Sprite => profile.Sprite != null,
                ProjectileVisualKind.SoftDisc => profile.Material != null,
                _ => false
            };
        }

        internal static ProjectileVisualHandle CreateProjectile(
            ProjectileVisualProfile profile,
            Renderer sourceRenderer,
            string objectName = "ProjectileVisual")
        {
            if (!CanRender(profile))
                return default;

            var projectileObject = new GameObject(objectName);
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
                // Preserve Unity's compatible default material when the profile does not
                // explicitly override it. Assigning null produces the magenta error shader.
                if (profile.Material != null)
                    spriteRenderer.sharedMaterial = profile.Material;
                renderer = spriteRenderer;
            }

            ApplySorting(renderer, sourceRenderer, profile.SortingOrderOffset);
            return new ProjectileVisualHandle(projectileObject, renderer);
        }

        internal static ParticleSystem CreateParticleTrail(
            ProjectileVisualProfile profile,
            Renderer sourceRenderer,
            Vector3 start,
            string objectName = "ProjectileParticleTrail")
        {
            ProjectileParticleTrailSettings settings = profile?.ParticleTrail;
            if (settings?.Enabled != true || profile.Material == null)
                return null;

            var particleObject = new GameObject(objectName);
            particleObject.transform.position = start;
            var particles = particleObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.useAutoRandomSeed = false;
            particles.randomSeed = settings.RandomSeed;

            var main = particles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = Mathf.Max(0.1f, settings.LifetimeMax);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = settings.MaximumParticles;
            main.startLifetime = new ParticleSystem.MinMaxCurve(settings.LifetimeMin, settings.LifetimeMax);
            main.startSize = new ParticleSystem.MinMaxCurve(settings.SizeMin, settings.SizeMax);
            main.startSpeed = 0.01f;
            main.startColor = settings.Color;

            var emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 1f / settings.EmissionInterval;
            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.002f;
            shape.randomDirectionAmount = 1f;

            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.75f, 0f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = gradient;

            var particleRenderer = particleObject.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Mesh;
            particleRenderer.mesh = SkillVfxPrimitiveBuilder.SharedDiamondMesh;
            particleRenderer.sharedMaterial = profile.Material;
            ApplySorting(particleRenderer, sourceRenderer, -1);
            var block = new MaterialPropertyBlock();
            SkillVfxPrimitiveBuilder.ApplyStandaloneProperties(
                particleRenderer,
                block,
                Color.white,
                1f,
                1f,
                SkillVfxShapeMode.Solid);
            return particles;
        }

        internal static SpriteRenderer CreateGhost(
            ProjectileVisualProfile profile,
            SpriteRenderer sourceRenderer,
            string objectName = "ProjectileGhostTrail")
        {
            if (profile == null || sourceRenderer == null || sourceRenderer.sprite == null)
                return null;

            var ghost = new GameObject(objectName);
            var renderer = ghost.AddComponent<SpriteRenderer>();
            renderer.sprite = sourceRenderer.sprite;
            if (profile.Material != null)
                renderer.sharedMaterial = sourceRenderer.sharedMaterial;
            renderer.sortingLayerID = sourceRenderer.sortingLayerID;
            renderer.sortingOrder = sourceRenderer.sortingOrder - 1;
            renderer.color = sourceRenderer.color;
            return renderer;
        }

        internal static float ResolveDuration(float worldDistance, float speed, float fallbackTravelTime)
        {
            if (speed <= 0f)
                return Mathf.Max(0.05f, fallbackTravelTime);

            return Mathf.Clamp(worldDistance / speed, MinTravelDuration, MaxTravelDuration);
        }

        private static void ApplySorting(Renderer renderer, Renderer sourceRenderer, int orderOffset)
        {
            if (renderer == null)
                return;

            if (sourceRenderer != null)
            {
                renderer.sortingLayerID = sourceRenderer.sortingLayerID;
                renderer.sortingOrder = sourceRenderer.sortingOrder + orderOffset;
            }
            else
            {
                renderer.sortingOrder = orderOffset;
            }
        }
    }

    /// <summary>
    /// Identifies one transient projectile object and its primary renderer.
    /// </summary>
    internal readonly struct ProjectileVisualHandle
    {
        internal ProjectileVisualHandle(GameObject gameObject, Renderer renderer)
        {
            GameObject = gameObject;
            Renderer = renderer;
        }

        internal GameObject GameObject { get; }
        internal Renderer Renderer { get; }
        internal bool IsValid => GameObject != null && Renderer != null;
    }
}
