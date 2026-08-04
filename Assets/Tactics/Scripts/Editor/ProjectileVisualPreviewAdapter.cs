#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using DG.Tweening;
using Tactics.Common.Skills.Graph;
using UnityEngine;
using Object = UnityEngine.Object;

[assembly: InternalsVisibleTo("Tactics.Tests.Editor")]

namespace Tactics.EditorTools
{
    /// <summary>
    /// Adapts runtime projectile construction to deterministic editor-only sequence preview.
    /// </summary>
    /// <remarks>
    /// The runtime factory remains the source of truth for renderers, materials, sorting and
    /// trail configuration. This adapter only replaces wall-clock sampling with scrub-safe
    /// sequence evaluation and owns every temporary object it creates.
    /// </remarks>
    internal sealed class ProjectileVisualPreviewAdapter : IDisposable
    {
        private const float ParticleSimulationStep = 1f / 60f;

        private readonly Action<GameObject> _registerObject;
        private readonly Sprite _placeholderSprite;
        private readonly List<GameObject> _ownedObjects = new();

        internal ProjectileVisualPreviewAdapter(
            Action<GameObject> registerObject,
            Sprite placeholderSprite)
        {
            _registerObject = registerObject;
            _placeholderSprite = placeholderSprite;
        }

        internal Renderer PrimaryRenderer { get; private set; }
        internal bool UsesPlaceholder { get; private set; }

        internal Sequence Build(
            ProjectileVisualProfile profile,
            Renderer sourceRenderer,
            Vector3 start,
            Vector3 end,
            float duration)
        {
            DisposeObjects();
            UsesPlaceholder = false;

            ProjectileVisualHandle handle = profile?.FlightPrefab != null
                ? CreatePrefabProjectile(profile, sourceRenderer)
                : ProjectileVisualFactory.CreateProjectile(
                    profile,
                    sourceRenderer,
                    "PreviewProjectile");
            GameObject projectileObject;
            if (handle.IsValid)
            {
                projectileObject = handle.GameObject;
                PrimaryRenderer = handle.Renderer;
            }
            else
            {
                UsesPlaceholder = true;
                projectileObject = CreatePlaceholder(sourceRenderer);
                PrimaryRenderer = projectileObject.GetComponent<SpriteRenderer>();
            }

            Register(projectileObject);
            var sequence = DOTween.Sequence();
            sequence.Append(ProjectileTweenBuilder.Build(
                projectileObject.transform,
                profile,
                start,
                end,
                duration));

            if (!UsesPlaceholder)
            {
                AddPrefabParticleSimulation(sequence, profile, projectileObject, duration);
                AddParticleTrail(sequence, profile, PrimaryRenderer, start, end, duration);
                AddGhostTrail(sequence, profile, PrimaryRenderer as SpriteRenderer, start, end, duration);
            }

            return sequence;
        }

        private static ProjectileVisualHandle CreatePrefabProjectile(
            ProjectileVisualProfile profile,
            Renderer sourceRenderer)
        {
            var projectileObject = Object.Instantiate(profile.FlightPrefab);
            projectileObject.name = "PreviewProjectile";
            Renderer renderer = ProjectileVisualFactory.ConfigurePrefabProjectile(
                profile,
                projectileObject);

            int sortingLayerId = sourceRenderer != null ? sourceRenderer.sortingLayerID : 0;
            int sortingOrder = (sourceRenderer != null ? sourceRenderer.sortingOrder : 0) +
                profile.SortingOrderOffset;
            TransientVfxPool.ApplySorting(projectileObject, sortingLayerId, sortingOrder);
            return new ProjectileVisualHandle(projectileObject, renderer);
        }

        private static void AddPrefabParticleSimulation(
            Sequence sequence,
            ProjectileVisualProfile profile,
            GameObject projectileObject,
            float duration)
        {
            if (profile?.FlightPrefab == null || projectileObject == null)
                return;

            ParticleSystem[] systems = projectileObject.GetComponentsInChildren<ParticleSystem>(true);
            if (systems.Length == 0)
                return;
            foreach (ParticleSystem system in systems)
            {
                system.useAutoRandomSeed = false;
                system.randomSeed = 1u;
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            Tween simulation = DOTween.To(
                    () => 0f,
                    elapsed => SimulatePrefabParticles(systems, elapsed),
                    duration,
                    duration)
                .SetEase(Ease.Linear);
            sequence.Insert(0f, simulation);
        }

        private static void SimulatePrefabParticles(ParticleSystem[] systems, float elapsed)
        {
            foreach (ParticleSystem system in systems)
            {
                if (system == null)
                    continue;
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                system.Simulate(elapsed, true, true, true);
                system.Pause(true);
            }
        }

        public void Dispose()
        {
            DisposeObjects();
            PrimaryRenderer = null;
        }

        private void AddParticleTrail(
            Sequence sequence,
            ProjectileVisualProfile profile,
            Renderer sourceRenderer,
            Vector3 start,
            Vector3 end,
            float duration)
        {
            ParticleSystem particles = ProjectileVisualFactory.CreateParticleTrail(
                profile,
                sourceRenderer,
                start,
                "PreviewProjectileParticleTrail");
            if (particles == null)
                return;

            Register(particles.gameObject);
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            float linger = profile.ParticleTrail.LifetimeMax + 0.02f;
            float previewDuration = duration + linger;
            Tween simulation = DOTween.To(
                    () => 0f,
                    elapsed => SimulateParticles(particles, profile, start, end, duration, elapsed),
                    previewDuration,
                    previewDuration)
                .SetEase(Ease.Linear);
            sequence.Insert(0f, simulation);
        }

        private static void SimulateParticles(
            ParticleSystem particles,
            ProjectileVisualProfile profile,
            Vector3 start,
            Vector3 end,
            float travelDuration,
            float elapsed)
        {
            if (particles == null)
                return;

            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var emission = particles.emission;
            emission.enabled = true;
            particles.Play(true);

            float simulated = 0f;
            while (simulated < elapsed - 0.0001f)
            {
                float step = Mathf.Min(ParticleSimulationStep, elapsed - simulated);
                simulated += step;
                float travelTime = Mathf.Min(simulated, travelDuration);
                float normalized = travelDuration > 0f ? travelTime / travelDuration : 1f;
                float arcHeight = profile.TrajectoryStyle == ProjectileTrajectoryStyle.MagicStraight
                    ? 0f
                    : profile.ArcHeight;
                particles.transform.position = ProjectileTweenBuilder.EvaluatePosition(
                    start,
                    end,
                    arcHeight,
                    normalized);
                if (simulated >= travelDuration)
                {
                    emission = particles.emission;
                    emission.enabled = false;
                }
                particles.Simulate(step, true, false, false);
            }

            particles.Pause(true);
        }

        private void AddGhostTrail(
            Sequence sequence,
            ProjectileVisualProfile profile,
            SpriteRenderer sourceRenderer,
            Vector3 start,
            Vector3 end,
            float duration)
        {
            ProjectileGhostTrailSettings settings = profile?.GhostTrail;
            if (settings?.Enabled != true || sourceRenderer == null || sourceRenderer.sprite == null)
                return;

            float sampleInterval = settings.SampleInterval;
            float visibleLifetime = Mathf.Min(settings.Lifetime, sampleInterval * settings.MaximumAlive);
            int sampleCount = Mathf.Max(1, Mathf.FloorToInt(duration / sampleInterval) + 1);
            float arcHeight = profile.TrajectoryStyle == ProjectileTrajectoryStyle.MagicStraight
                ? 0f
                : profile.ArcHeight;
            Vector3 projectileBaseScale = Vector3.one * profile.Scale;

            for (int index = 0; index < sampleCount; index++)
            {
                float sampleTime = Mathf.Min(index * sampleInterval, duration);
                float normalized = duration > 0f ? sampleTime / duration : 1f;
                SpriteRenderer ghostRenderer = ProjectileVisualFactory.CreateGhost(
                    profile,
                    sourceRenderer,
                    $"PreviewProjectileGhost_{index}");
                if (ghostRenderer == null)
                    continue;

                Register(ghostRenderer.gameObject);
                Vector3 position = ProjectileTweenBuilder.EvaluatePosition(start, end, arcHeight, normalized);
                Quaternion rotation = profile.RotateAlongTangent
                    ? ProjectileTweenBuilder.EvaluateRotation(start, end, arcHeight, normalized)
                    : Quaternion.identity;
                Vector3 scale = ProjectileTweenBuilder.EvaluateScale(
                    profile,
                    projectileBaseScale,
                    normalized) * settings.Scale;
                Color baseColor = sourceRenderer.color;
                Color hidden = baseColor;
                hidden.a = 0f;
                ghostRenderer.color = hidden;
                ghostRenderer.transform.SetPositionAndRotation(position, rotation);
                ghostRenderer.transform.localScale = scale;

                Tween fade = DOTween.To(
                        () => 0f,
                        progress =>
                        {
                            if (ghostRenderer == null)
                                return;
                            Color color = baseColor;
                            color.a = baseColor.a * settings.Alpha * (1f - progress);
                            ghostRenderer.color = color;
                            ghostRenderer.transform.localScale = Vector3.Lerp(
                                scale,
                                scale * 0.86f,
                                progress);
                        },
                        1f,
                        visibleLifetime)
                    .SetEase(Ease.Linear);
                sequence.Insert(sampleTime, fade);
            }
        }

        private GameObject CreatePlaceholder(Renderer sourceRenderer)
        {
            var placeholder = new GameObject("PreviewProjectilePlaceholder");
            var renderer = placeholder.AddComponent<SpriteRenderer>();
            renderer.sprite = _placeholderSprite;
            renderer.color = new Color(1f, 0.2f, 1f, 1f);
            if (sourceRenderer != null)
            {
                renderer.sortingLayerID = sourceRenderer.sortingLayerID;
                renderer.sortingOrder = sourceRenderer.sortingOrder + 50;
            }
            else
            {
                renderer.sortingOrder = 50;
            }
            return placeholder;
        }

        private void Register(GameObject value)
        {
            if (value == null)
                return;
            value.hideFlags = HideFlags.HideAndDontSave;
            _ownedObjects.Add(value);
            _registerObject?.Invoke(value);
        }

        private void DisposeObjects()
        {
            foreach (GameObject ownedObject in _ownedObjects)
            {
                if (ownedObject != null)
                    Object.DestroyImmediate(ownedObject);
            }
            _ownedObjects.Clear();
        }
    }
}
#endif
