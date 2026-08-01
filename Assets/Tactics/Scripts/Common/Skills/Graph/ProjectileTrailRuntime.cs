using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tactics.Common.Skills.Graph
{
    /// <summary>
    /// Owns non-blocking projectile travel decoration without affecting gameplay travel timing.
    /// </summary>
    internal sealed class ProjectileTrailRuntime
    {
        private readonly ProjectileVisualProfile _profile;
        private readonly SpriteRenderer _sourceSprite;
        private readonly Renderer _sourceRenderer;
        private readonly List<GhostPlayback> _ghosts = new();
        private GameObject _particleObject;
        private ParticleSystem _particles;
        private float _lastGhostSampleTime = float.NegativeInfinity;

        public ProjectileTrailRuntime(ProjectileVisualProfile profile, Renderer sourceRenderer, Vector3 start)
        {
            _profile = profile;
            _sourceRenderer = sourceRenderer;
            _sourceSprite = sourceRenderer as SpriteRenderer;
            if (profile?.ParticleTrail?.Enabled == true && profile.Material != null)
                CreateParticleTrail(start);
        }

        public void Sample(Transform projectile)
        {
            if (projectile == null)
                return;

            if (_particleObject != null)
                _particleObject.transform.position = projectile.position;

            ProjectileGhostTrailSettings settings = _profile?.GhostTrail;
            if (settings?.Enabled != true || _sourceSprite == null || _sourceSprite.sprite == null)
                return;
            if (Time.time - _lastGhostSampleTime < settings.SampleInterval)
                return;

            _lastGhostSampleTime = Time.time;
            _ghosts.RemoveAll(ghost => ghost == null || ghost.GameObject == null || !ghost.Tween.IsActive());
            while (_ghosts.Count >= settings.MaximumAlive)
            {
                GhostPlayback oldest = _ghosts[0];
                _ghosts.RemoveAt(0);
                oldest.KillAndDestroy();
            }

            var ghost = new GameObject("ProjectileGhostTrail");
            ghost.transform.SetPositionAndRotation(projectile.position, projectile.rotation);
            ghost.transform.localScale = projectile.localScale * settings.Scale;
            var renderer = ghost.AddComponent<SpriteRenderer>();
            renderer.sprite = _sourceSprite.sprite;
            // Preserve the SpriteRenderer default material for profiles without an
            // explicitly authored sprite material. This mirrors the main projectile
            // and avoids propagating a null/error material to every ghost.
            if (_profile.Material != null)
                renderer.sharedMaterial = _sourceSprite.sharedMaterial;
            renderer.sortingLayerID = _sourceSprite.sortingLayerID;
            renderer.sortingOrder = _sourceSprite.sortingOrder - 1;
            Color color = _sourceSprite.color;
            color.a *= settings.Alpha;
            renderer.color = color;
            Vector3 startScale = ghost.transform.localScale;
            Tween tween = DOTween.To(
                    () => 0f,
                    progress =>
                    {
                        if (renderer == null)
                            return;
                        Color faded = color;
                        faded.a = color.a * (1f - progress);
                        renderer.color = faded;
                        ghost.transform.localScale = Vector3.Lerp(startScale, startScale * 0.86f, progress);
                    },
                    1f,
                    settings.Lifetime)
                .SetEase(Ease.Linear)
                .OnComplete(() => DestroyObject(ghost));
            _ghosts.Add(new GhostPlayback(ghost, tween));
        }

        public void Stop(CancellationToken cancellationToken)
        {
            if (_particles != null)
            {
                var emission = _particles.emission;
                emission.enabled = false;
                _particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                GameObject particlesToDestroy = _particleObject;
                _particleObject = null;
                _particles = null;
                _ = DestroyParticleTrailAsync(particlesToDestroy, cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                foreach (GhostPlayback ghost in _ghosts)
                    ghost?.KillAndDestroy();
                _ghosts.Clear();
            }
        }

        private void CreateParticleTrail(Vector3 start)
        {
            ProjectileParticleTrailSettings settings = _profile.ParticleTrail;
            _particleObject = new GameObject("ProjectileParticleTrail");
            _particleObject.transform.position = start;
            _particles = _particleObject.AddComponent<ParticleSystem>();
            _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _particles.useAutoRandomSeed = false;
            _particles.randomSeed = settings.RandomSeed;

            var main = _particles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = Mathf.Max(0.1f, settings.LifetimeMax);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = settings.MaximumParticles;
            main.startLifetime = new ParticleSystem.MinMaxCurve(settings.LifetimeMin, settings.LifetimeMax);
            main.startSize = new ParticleSystem.MinMaxCurve(settings.SizeMin, settings.SizeMax);
            main.startSpeed = 0.01f;
            main.startColor = settings.Color;

            var emission = _particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 1f / settings.EmissionInterval;
            var shape = _particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.002f;
            shape.randomDirectionAmount = 1f;

            var colorOverLifetime = _particles.colorOverLifetime;
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

            var renderer = _particleObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = SkillVfxPrimitiveBuilder.SharedDiamondMesh;
            renderer.sharedMaterial = _profile.Material;
            renderer.sortingLayerID = _sourceRenderer.sortingLayerID;
            renderer.sortingOrder = _sourceRenderer.sortingOrder - 1;
            var block = new MaterialPropertyBlock();
            SkillVfxPrimitiveBuilder.ApplyStandaloneProperties(
                renderer,
                block,
                Color.white,
                1f,
                1f,
                SkillVfxShapeMode.Solid);
            _particles.Play();
        }

        private async Task DestroyParticleTrailAsync(GameObject particleObject, CancellationToken cancellationToken)
        {
            if (particleObject == null)
                return;
            try
            {
                await global::Tactics.GameTimeService.DelayScaledAsync(
                    _profile.ParticleTrail.LifetimeMax + 0.02f,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // The battle scope owns cancellation.
            }
            finally
            {
                DestroyObject(particleObject);
            }
        }

        private static void DestroyObject(Object target)
        {
            if (target == null)
                return;
            if (Application.isPlaying)
                Object.Destroy(target);
            else
                Object.DestroyImmediate(target);
        }

        private sealed class GhostPlayback
        {
            public GameObject GameObject { get; }
            public Tween Tween { get; }

            public GhostPlayback(GameObject gameObject, Tween tween)
            {
                GameObject = gameObject;
                Tween = tween;
            }

            public void KillAndDestroy()
            {
                if (Tween.IsActive())
                    Tween.Kill(false);
                if (GameObject != null)
                    GameObject.SetActive(false);
                DestroyObject(GameObject);
            }
        }
    }
}
