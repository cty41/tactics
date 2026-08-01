using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tactics.Common.Skills.Graph
{
    /// <summary>
    /// Represents a running primitive layer. Gameplay waits only for <see cref="BlockingTask"/>.
    /// </summary>
    public sealed class SkillVfxPrimitivePlayback
    {
        public static readonly SkillVfxPrimitivePlayback Completed = new(
            Task.CompletedTask,
            Task.CompletedTask);

        public Task BlockingTask { get; }
        public Task CompletionTask { get; }

        public SkillVfxPrimitivePlayback(Task blockingTask, Task completionTask)
        {
            BlockingTask = blockingTask ?? Task.CompletedTask;
            CompletionTask = completionTask ?? Task.CompletedTask;
        }
    }

    /// <summary>
    /// Deterministic layer sample shared by runtime tween construction and the editor scrub preview.
    /// </summary>
    public readonly struct SkillVfxPrimitivePreviewState
    {
        public bool IsVisible { get; }
        public float Size { get; }
        public float Alpha { get; }
        public float WidthScale { get; }

        public SkillVfxPrimitivePreviewState(bool isVisible, float size, float alpha, float widthScale)
        {
            IsVisible = isVisible;
            Size = size;
            Alpha = alpha;
            WidthScale = widthScale;
        }
    }

    /// <summary>
    /// Builds the six finite Pure Run VFX primitives for runtime and editor preview.
    /// </summary>
    public static class SkillVfxPrimitiveBuilder
    {
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        private static readonly int EmissionId = Shader.PropertyToID("_Emission");
        private static readonly int ShapeModeId = Shader.PropertyToID("_ShapeMode");
        private static readonly int RadialInnerId = Shader.PropertyToID("_RadialInner");
        private static readonly int RadialOuterId = Shader.PropertyToID("_RadialOuter");
        private static readonly int SoftnessId = Shader.PropertyToID("_Softness");

        private static Mesh _quadMesh;
        private static Mesh _diamondMesh;
        private static readonly Dictionary<int, Mesh> TaperedMeshes = new();

        public static SkillVfxPrimitivePlayback Play(
            SkillVfxRecipe recipe,
            SkillVfxPrimitiveLayer layer,
            SkillVfxCueContext context,
            int sortingLayerId,
            int baseSortingOrder,
            Transform parent,
            CancellationToken cancellationToken)
        {
            if (recipe == null || layer == null || context == null)
                return SkillVfxPrimitivePlayback.Completed;

            Material material = recipe.ResolveMaterial(layer.BlendMode);
            if (material == null)
                return SkillVfxPrimitivePlayback.Completed;

            var completionTasks = layer.PrimitiveKind switch
            {
                SkillVfxPrimitiveKind.RadialCore => PlayRadial(
                    material, layer, context, sortingLayerId, baseSortingOrder, parent, cancellationToken),
                SkillVfxPrimitiveKind.RadialRing => PlayRadial(
                    material, layer, context, sortingLayerId, baseSortingOrder, parent, cancellationToken),
                SkillVfxPrimitiveKind.TaperedLine => PlayTaperedLine(
                    material, layer, context, sortingLayerId, baseSortingOrder, parent, cancellationToken),
                SkillVfxPrimitiveKind.CrossFlash => PlayCrossFlash(
                    material, layer, context, sortingLayerId, baseSortingOrder, parent, cancellationToken),
                SkillVfxPrimitiveKind.ParticleBurst => PlayParticleBurst(
                    material, layer, context, sortingLayerId, baseSortingOrder, parent, cancellationToken),
                SkillVfxPrimitiveKind.ProjectileGhostTrail => Array.Empty<Task>(),
                _ => Array.Empty<Task>()
            };

            if (completionTasks.Count == 0)
                return SkillVfxPrimitivePlayback.Completed;

            Task blockingTask = layer.BlockingMarker > 0f
                ? global::Tactics.GameTimeService.DelayScaledAsync(layer.BlockingMarker, cancellationToken)
                : Task.CompletedTask;
            return new SkillVfxPrimitivePlayback(blockingTask, Task.WhenAll(completionTasks));
        }

        internal static Mesh SharedQuadMesh => QuadMesh;
        internal static Mesh SharedDiamondMesh => DiamondMesh;

        /// <summary>
        /// Samples the authored timeline without creating renderers. Editor previews use this exact
        /// interpolation so scrubbing cannot drift from runtime primitive timing.
        /// </summary>
        public static SkillVfxPrimitivePreviewState EvaluatePreviewState(
            SkillVfxPrimitiveLayer layer,
            float absoluteTime)
        {
            if (layer == null || absoluteTime < 0f || absoluteTime > layer.Duration)
                return new SkillVfxPrimitivePreviewState(false, 0f, 0f, 1f);

            float progress = Mathf.Clamp01(absoluteTime / layer.Duration);
            float size = EvaluateLayerTimeline(
                progress, layer, layer.StartSize, layer.MiddleSize, layer.PeakSize, layer.EndSize);
            float alpha = EvaluateLayerTimeline(
                progress, layer, layer.StartAlpha, layer.MiddleAlpha, layer.PeakAlpha, layer.EndAlpha);
            float widthScale = absoluteTime <= layer.PeakTime
                ? 1f
                : Mathf.Lerp(1f, 0.8f, Mathf.InverseLerp(layer.PeakTime, layer.Duration, absoluteTime));
            return new SkillVfxPrimitivePreviewState(alpha > 0.001f, size, alpha, widthScale);
        }

        internal static void ApplyStandaloneProperties(
            Renderer renderer,
            MaterialPropertyBlock propertyBlock,
            Color tint,
            float alpha,
            float emission,
            SkillVfxShapeMode shapeMode,
            float radialInner = 0.5f,
            float radialOuter = 1f,
            float softness = 0.12f)
        {
            propertyBlock.Clear();
            propertyBlock.SetColor(TintId, tint);
            propertyBlock.SetFloat(AlphaId, Mathf.Clamp01(alpha));
            propertyBlock.SetFloat(EmissionId, emission);
            propertyBlock.SetFloat(ShapeModeId, (float)shapeMode);
            propertyBlock.SetFloat(RadialInnerId, radialInner);
            propertyBlock.SetFloat(RadialOuterId, radialOuter);
            propertyBlock.SetFloat(SoftnessId, softness);
            renderer.SetPropertyBlock(propertyBlock);
        }

        private static IReadOnlyList<Task> PlayRadial(
            Material material,
            SkillVfxPrimitiveLayer layer,
            SkillVfxCueContext context,
            int sortingLayerId,
            int baseSortingOrder,
            Transform parent,
            CancellationToken cancellationToken)
        {
            var tasks = new List<Task>();
            foreach (Vector3 position in ResolveEffectPositions(context).Take(layer.MaximumInstances))
            {
                var visual = CreateMeshVisual(
                    $"SkillVfx_{layer.PrimitiveKind}",
                    QuadMesh,
                    material,
                    position,
                    0f,
                    sortingLayerId,
                    baseSortingOrder + layer.SortingOrderOffset,
                    parent);
                var propertyBlock = new MaterialPropertyBlock();
                tasks.Add(PlayTweenAndDestroyAsync(
                    visual.gameObject,
                    layer.Duration,
                    progress =>
                    {
                        float radius = EvaluateLayerTimeline(
                            progress, layer, layer.StartSize, layer.MiddleSize, layer.PeakSize, layer.EndSize);
                        float alpha = EvaluateLayerTimeline(
                            progress, layer, layer.StartAlpha, layer.MiddleAlpha, layer.PeakAlpha, layer.EndAlpha);
                        visual.transform.localScale = Vector3.one * (Mathf.Max(0f, radius) * 2f);
                        ApplyProperties(visual, propertyBlock, layer, alpha);
                    },
                    cancellationToken));
            }
            return tasks;
        }

        private static IReadOnlyList<Task> PlayTaperedLine(
            Material material,
            SkillVfxPrimitiveLayer layer,
            SkillVfxCueContext context,
            int sortingLayerId,
            int baseSortingOrder,
            Transform parent,
            CancellationToken cancellationToken)
        {
            Vector3 delta = context.TargetWorldPosition - context.SourceWorldPosition;
            float distance = delta.magnitude;
            if (distance <= 0.0001f)
                return Array.Empty<Task>();

            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            float tipRatio = layer.TipWidth / layer.RootWidth;
            var visual = CreateMeshVisual(
                "SkillVfx_TaperedLine",
                GetTaperedMesh(tipRatio),
                material,
                context.SourceWorldPosition,
                angle,
                sortingLayerId,
                baseSortingOrder + layer.SortingOrderOffset,
                parent);
            var propertyBlock = new MaterialPropertyBlock();
            Task task = PlayTweenAndDestroyAsync(
                visual.gameObject,
                layer.Duration,
                progress =>
                {
                    float lengthRatio = EvaluateLayerTimeline(
                        progress, layer, layer.StartSize, layer.MiddleSize, layer.PeakSize, layer.EndSize);
                    float alpha = EvaluateLayerTimeline(
                        progress, layer, layer.StartAlpha, layer.MiddleAlpha, layer.PeakAlpha, layer.EndAlpha);
                    float normalizedTime = progress * layer.Duration;
                    float widthCompression = normalizedTime <= layer.PeakTime
                        ? 1f
                        : Mathf.Lerp(1f, 0.8f, Mathf.InverseLerp(layer.PeakTime, layer.Duration, normalizedTime));
                    visual.transform.localScale = new Vector3(
                        distance * Mathf.Max(0f, lengthRatio),
                        layer.RootWidth * widthCompression,
                        1f);
                    ApplyProperties(visual, propertyBlock, layer, alpha);
                },
                cancellationToken);
            return new[] { task };
        }

        private static IReadOnlyList<Task> PlayCrossFlash(
            Material material,
            SkillVfxPrimitiveLayer layer,
            SkillVfxCueContext context,
            int sortingLayerId,
            int baseSortingOrder,
            Transform parent,
            CancellationToken cancellationToken)
        {
            var tasks = new List<Task>();
            float baseAngle = Mathf.Atan2(context.Direction.y, context.Direction.x) * Mathf.Rad2Deg;
            foreach (Vector3 position in ResolveEffectPositions(context).Take(layer.MaximumInstances))
            {
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    var visual = CreateMeshVisual(
                        "SkillVfx_CrossFlash",
                        QuadMesh,
                        material,
                        position,
                        baseAngle + sign * layer.Angle,
                        sortingLayerId,
                        baseSortingOrder + layer.SortingOrderOffset,
                        parent);
                    var propertyBlock = new MaterialPropertyBlock();
                    tasks.Add(PlayTweenAndDestroyAsync(
                        visual.gameObject,
                        layer.Duration,
                        progress =>
                        {
                            float length = EvaluateLayerTimeline(
                                progress, layer, layer.StartSize, layer.MiddleSize, layer.PeakSize, layer.EndSize);
                            float alpha = EvaluateLayerTimeline(
                                progress, layer, layer.StartAlpha, layer.MiddleAlpha, layer.PeakAlpha, layer.EndAlpha);
                            visual.transform.localScale = new Vector3(
                                Mathf.Max(0f, length),
                                layer.RootWidth,
                                1f);
                            ApplyProperties(visual, propertyBlock, layer, alpha);
                        },
                        cancellationToken));
                }
            }
            return tasks;
        }

        private static IReadOnlyList<Task> PlayParticleBurst(
            Material material,
            SkillVfxPrimitiveLayer layer,
            SkillVfxCueContext context,
            int sortingLayerId,
            int baseSortingOrder,
            Transform parent,
            CancellationToken cancellationToken)
        {
            if (layer.ParticleCount <= 0)
                return Array.Empty<Task>();

            var tasks = new List<Task>();
            foreach (Vector3 position in ResolveEffectPositions(context).Take(layer.MaximumInstances))
            {
                var particleObject = new GameObject("SkillVfx_ParticleBurst");
                if (parent != null)
                    particleObject.transform.SetParent(parent, true);
                particleObject.transform.position = position;

                var particles = particleObject.AddComponent<ParticleSystem>();
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particles.useAutoRandomSeed = false;
                particles.randomSeed = layer.RandomSeed;
                var main = particles.main;
                main.playOnAwake = false;
                main.loop = false;
                main.duration = layer.Duration;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = layer.ParticleCount;
                main.startLifetime = new ParticleSystem.MinMaxCurve(
                    layer.ParticleLifetimeMin,
                    layer.ParticleLifetimeMax);
                main.startSize = layer.ParticleSize;
                main.startSpeed = layer.ParticleSpeed;
                main.startColor = layer.Color;

                var emission = particles.emission;
                emission.enabled = false;
                var shape = particles.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 0.001f;
                shape.radiusThickness = 1f;
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
                        new GradientAlphaKey(1f, 0f),
                        new GradientAlphaKey(0f, 1f)
                    });
                colorOverLifetime.color = gradient;

                if (layer.ParticleDrag > 0f)
                {
                    var limitVelocity = particles.limitVelocityOverLifetime;
                    limitVelocity.enabled = true;
                    limitVelocity.drag = layer.ParticleDrag;
                    limitVelocity.dampen = 1f;
                }

                var renderer = particleObject.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Mesh;
                renderer.mesh = DiamondMesh;
                renderer.sharedMaterial = material;
                renderer.sortingLayerID = sortingLayerId;
                renderer.sortingOrder = baseSortingOrder + layer.SortingOrderOffset;
                var propertyBlock = new MaterialPropertyBlock();
                ApplyProperties(renderer, propertyBlock, layer, 1f);

                particles.Emit(layer.ParticleCount);
                particles.Play();
                float cleanupDelay = Mathf.Max(layer.Duration, layer.ParticleLifetimeMax) + 0.02f;
                tasks.Add(DestroyAfterDelayAsync(particleObject, cleanupDelay, cancellationToken));
            }
            return tasks;
        }

        private static IEnumerable<Vector3> ResolveEffectPositions(SkillVfxCueContext context)
        {
            if (context.HitWorldPositions.Count > 0)
                return context.HitWorldPositions;
            return new[] { context.PrimaryHitWorldPosition };
        }

        private static MeshRenderer CreateMeshVisual(
            string name,
            Mesh mesh,
            Material material,
            Vector3 position,
            float angle,
            int sortingLayerId,
            int sortingOrder,
            Transform parent)
        {
            var gameObject = new GameObject(name);
            if (parent != null)
                gameObject.transform.SetParent(parent, true);
            gameObject.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, angle));
            var filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingLayerID = sortingLayerId;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private static void ApplyProperties(
            Renderer renderer,
            MaterialPropertyBlock propertyBlock,
            SkillVfxPrimitiveLayer layer,
            float alpha)
        {
            propertyBlock.Clear();
            propertyBlock.SetColor(TintId, layer.Color);
            propertyBlock.SetFloat(AlphaId, Mathf.Clamp01(alpha));
            propertyBlock.SetFloat(EmissionId, layer.Emission);
            propertyBlock.SetFloat(ShapeModeId, (float)layer.ShapeMode);
            propertyBlock.SetFloat(RadialInnerId, layer.RadialInner);
            propertyBlock.SetFloat(RadialOuterId, layer.RadialOuter);
            propertyBlock.SetFloat(SoftnessId, layer.Softness);
            renderer.SetPropertyBlock(propertyBlock);
        }

        private static async Task PlayTweenAndDestroyAsync(
            GameObject visual,
            float duration,
            Action<float> update,
            CancellationToken cancellationToken)
        {
            Tween tween = null;
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            try
            {
                update(0f);
                tween = DOTween.To(
                        () => 0f,
                        progress => update(Mathf.Clamp01(progress)),
                        1f,
                        duration)
                    .SetEase(Ease.Linear)
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
            catch (OperationCanceledException)
            {
                // Cancellation is expected when the skill or battle scope ends.
            }
            finally
            {
                if (tween != null && tween.IsActive())
                    tween.Kill(false);
                DestroyObject(visual);
            }
        }

        private static async Task DestroyAfterDelayAsync(
            GameObject visual,
            float delay,
            CancellationToken cancellationToken)
        {
            try
            {
                await global::Tactics.GameTimeService.DelayScaledAsync(delay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Cancellation is expected when the skill or battle scope ends.
            }
            finally
            {
                DestroyObject(visual);
            }
        }

        private static float EvaluateThreePoint(
            float normalizedProgress,
            float start,
            float peak,
            float end,
            float peakTime,
            float duration)
        {
            float time = Mathf.Clamp01(normalizedProgress) * duration;
            if (peakTime <= 0f)
                return Mathf.Lerp(peak, end, duration <= 0f ? 1f : time / duration);
            if (time <= peakTime)
                return Mathf.Lerp(start, peak, time / peakTime);
            return Mathf.Lerp(peak, end, Mathf.InverseLerp(peakTime, duration, time));
        }

        private static float EvaluateLayerTimeline(
            float normalizedProgress,
            SkillVfxPrimitiveLayer layer,
            float start,
            float middle,
            float peak,
            float end)
        {
            if (!layer.UseMiddleKey)
                return EvaluateThreePoint(normalizedProgress, start, peak, end, layer.PeakTime, layer.Duration);

            float time = Mathf.Clamp01(normalizedProgress) * layer.Duration;
            if (layer.MiddleTime <= 0f)
                return EvaluateThreePoint(normalizedProgress, middle, peak, end, layer.PeakTime, layer.Duration);
            if (time <= layer.MiddleTime)
                return Mathf.Lerp(start, middle, time / layer.MiddleTime);
            if (time <= layer.PeakTime)
                return Mathf.Lerp(middle, peak, Mathf.InverseLerp(layer.MiddleTime, layer.PeakTime, time));
            return Mathf.Lerp(peak, end, Mathf.InverseLerp(layer.PeakTime, layer.Duration, time));
        }

        private static Mesh QuadMesh => _quadMesh != null ? _quadMesh : _quadMesh = CreateQuadMesh();
        private static Mesh DiamondMesh => _diamondMesh != null ? _diamondMesh : _diamondMesh = CreateDiamondMesh();

        private static Mesh GetTaperedMesh(float tipRatio)
        {
            int key = Mathf.RoundToInt(Mathf.Clamp01(tipRatio) * 1000f);
            if (TaperedMeshes.TryGetValue(key, out var mesh) && mesh != null)
                return mesh;

            float tipHalfWidth = key / 1000f * 0.5f;
            mesh = CreateMesh(
                $"SkillVfxTapered_{key}",
                new[]
                {
                    new Vector3(0f, -0.5f),
                    new Vector3(0f, 0.5f),
                    new Vector3(1f, -tipHalfWidth),
                    new Vector3(1f, tipHalfWidth)
                });
            TaperedMeshes[key] = mesh;
            return mesh;
        }

        private static Mesh CreateQuadMesh()
        {
            return CreateMesh(
                "SkillVfxQuad",
                new[]
                {
                    new Vector3(-0.5f, -0.5f),
                    new Vector3(-0.5f, 0.5f),
                    new Vector3(0.5f, -0.5f),
                    new Vector3(0.5f, 0.5f)
                });
        }

        private static Mesh CreateDiamondMesh()
        {
            return CreateMesh(
                "SkillVfxDiamond",
                new[]
                {
                    new Vector3(-0.5f, 0f),
                    new Vector3(0f, 0.5f),
                    new Vector3(0f, -0.5f),
                    new Vector3(0.5f, 0f)
                });
        }

        private static Mesh CreateMesh(string name, Vector3[] vertices)
        {
            var mesh = new Mesh
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
                vertices = vertices,
                uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(0f, 1f),
                    new Vector2(1f, 0f),
                    new Vector2(1f, 1f)
                },
                triangles = new[] { 0, 1, 2, 2, 1, 3 }
            };
            mesh.RecalculateBounds();
            return mesh;
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
    }
}
