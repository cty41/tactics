#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using DG.Tweening;
using Tactics.Common.Skills.Graph;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tactics.EditorTools
{
    /// <summary>
    /// Samples procedural VFX recipes inside a PreviewRenderUtility scene without starting gameplay.
    /// </summary>
    internal sealed class ProceduralVfxPreviewAdapter : IDisposable
    {
        private readonly Action<GameObject> _addToPreviewScene;
        private readonly List<GameObject> _objects = new();

        internal ProceduralVfxPreviewAdapter(Action<GameObject> addToPreviewScene)
        {
            _addToPreviewScene = addToPreviewScene;
        }

        internal Sequence Build(
            SkillVfxRecipe recipe,
            SkillVfxCueKind cue,
            Vector3 source,
            Vector3 target)
        {
            var sequence = DOTween.Sequence();
            if (recipe == null)
                return sequence.AppendInterval(0.01f);

            foreach (SkillVfxPrimitiveLayer layer in recipe.GetLayers(cue))
            {
                if (layer == null || layer.PrimitiveKind == SkillVfxPrimitiveKind.ProjectileGhostTrail)
                    continue;
                PreviewLayer preview = CreateLayer(recipe, layer, source, target);
                if (preview == null)
                    continue;
                sequence.Insert(0f, DOTween.To(
                        () => 0f,
                        elapsed => preview.Sample(elapsed),
                        layer.Duration,
                        layer.Duration)
                    .SetEase(Ease.Linear));
            }
            if (sequence.Duration(false) <= 0f)
                sequence.AppendInterval(0.01f);
            return sequence;
        }

        public void Dispose()
        {
            foreach (GameObject value in _objects)
            {
                if (value != null)
                    Object.DestroyImmediate(value);
            }
            _objects.Clear();
        }

        private PreviewLayer CreateLayer(
            SkillVfxRecipe recipe,
            SkillVfxPrimitiveLayer layer,
            Vector3 source,
            Vector3 target)
        {
            Material material = recipe.ResolveMaterial(layer.BlendMode);
            if (material == null)
                return null;

            return layer.PrimitiveKind switch
            {
                SkillVfxPrimitiveKind.TaperedLine => CreateLine(layer, material, source, target),
                SkillVfxPrimitiveKind.CrossFlash => CreateCross(layer, material, target),
                SkillVfxPrimitiveKind.ParticleBurst => CreateParticles(layer, material, target),
                _ => CreateRadial(layer, material, target)
            };
        }

        private PreviewLayer CreateRadial(
            SkillVfxPrimitiveLayer layer,
            Material material,
            Vector3 position)
        {
            RendererState renderer = CreateRenderer(
                $"PresentationPreview_{layer.PrimitiveKind}",
                SkillVfxPrimitiveBuilder.SharedQuadMesh,
                material,
                position,
                Quaternion.identity);
            return new PreviewLayer(layer, elapsed =>
            {
                SkillVfxPrimitivePreviewState state = SkillVfxPrimitiveBuilder.EvaluatePreviewState(layer, elapsed);
                renderer.GameObject.SetActive(state.IsVisible);
                renderer.Transform.localScale = Vector3.one * Mathf.Max(0f, state.Size * 2f);
                Apply(renderer, layer, state.Alpha);
            });
        }

        private PreviewLayer CreateLine(
            SkillVfxPrimitiveLayer layer,
            Material material,
            Vector3 source,
            Vector3 target)
        {
            Vector3 delta = target - source;
            float distance = Mathf.Max(0.01f, delta.magnitude);
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            RendererState renderer = CreateRenderer(
                "PresentationPreview_TaperedLine",
                SkillVfxPrimitiveBuilder.GetSharedTaperedMesh(layer.RootWidth, layer.TipWidth),
                material,
                source,
                Quaternion.Euler(0f, 0f, angle));
            return new PreviewLayer(layer, elapsed =>
            {
                SkillVfxPrimitivePreviewState state = SkillVfxPrimitiveBuilder.EvaluatePreviewState(layer, elapsed);
                renderer.GameObject.SetActive(state.IsVisible);
                renderer.Transform.localScale = new Vector3(
                    distance * Mathf.Max(0f, state.Size),
                    Mathf.Max(0.001f, layer.RootWidth * state.WidthScale),
                    1f);
                Apply(renderer, layer, state.Alpha);
            });
        }

        private PreviewLayer CreateCross(
            SkillVfxPrimitiveLayer layer,
            Material material,
            Vector3 position)
        {
            RendererState first = CreateRenderer(
                "PresentationPreview_CrossA",
                SkillVfxPrimitiveBuilder.SharedDiamondMesh,
                material,
                position,
                Quaternion.Euler(0f, 0f, layer.Angle));
            RendererState second = CreateRenderer(
                "PresentationPreview_CrossB",
                SkillVfxPrimitiveBuilder.SharedDiamondMesh,
                material,
                position,
                Quaternion.Euler(0f, 0f, -layer.Angle));
            return new PreviewLayer(layer, elapsed =>
            {
                SkillVfxPrimitivePreviewState state = SkillVfxPrimitiveBuilder.EvaluatePreviewState(layer, elapsed);
                foreach (RendererState renderer in new[] { first, second })
                {
                    renderer.GameObject.SetActive(state.IsVisible);
                    renderer.Transform.localScale = new Vector3(
                        Mathf.Max(0f, state.Size * 2f),
                        Mathf.Max(0.001f, layer.RootWidth * state.WidthScale),
                        1f);
                    Apply(renderer, layer, state.Alpha);
                }
            });
        }

        private PreviewLayer CreateParticles(
            SkillVfxPrimitiveLayer layer,
            Material material,
            Vector3 position)
        {
            int count = Mathf.Clamp(layer.ParticleCount, 1, layer.MaximumInstances);
            var renderers = new List<RendererState>(count);
            var directions = new List<Vector3>(count);
            var random = new System.Random(unchecked((int)layer.RandomSeed));
            for (int index = 0; index < count; index++)
            {
                float angle = (float)(random.NextDouble() * Math.PI * 2d);
                directions.Add(new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f));
                renderers.Add(CreateRenderer(
                    $"PresentationPreview_Particle_{index}",
                    SkillVfxPrimitiveBuilder.SharedDiamondMesh,
                    material,
                    position,
                    Quaternion.identity));
            }
            return new PreviewLayer(layer, elapsed =>
            {
                SkillVfxPrimitivePreviewState state = SkillVfxPrimitiveBuilder.EvaluatePreviewState(layer, elapsed);
                float progress = Mathf.Clamp01(elapsed / layer.Duration);
                for (int index = 0; index < renderers.Count; index++)
                {
                    RendererState renderer = renderers[index];
                    renderer.GameObject.SetActive(state.IsVisible);
                    renderer.Transform.position = position +
                        directions[index] * layer.ParticleSpeed * elapsed;
                    renderer.Transform.localScale = Vector3.one *
                        Mathf.Max(0.001f, layer.ParticleSize * (1f - progress * 0.35f));
                    Apply(renderer, layer, state.Alpha);
                }
            });
        }

        private RendererState CreateRenderer(
            string name,
            Mesh mesh,
            Material material,
            Vector3 position,
            Quaternion rotation)
        {
            var gameObject = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            var filter = gameObject.AddComponent<MeshFilter>();
            var renderer = gameObject.AddComponent<MeshRenderer>();
            filter.sharedMesh = mesh;
            renderer.sharedMaterial = material;
            gameObject.transform.SetPositionAndRotation(position, rotation);
            _addToPreviewScene?.Invoke(gameObject);
            _objects.Add(gameObject);
            return new RendererState(gameObject, renderer);
        }

        private static void Apply(
            RendererState renderer,
            SkillVfxPrimitiveLayer layer,
            float alpha)
        {
            SkillVfxPrimitiveBuilder.ApplyStandaloneProperties(
                renderer.Renderer,
                renderer.PropertyBlock,
                layer.Color,
                alpha,
                layer.Emission,
                layer.ShapeMode,
                layer.RadialInner,
                layer.RadialOuter,
                layer.Softness);
        }

        private sealed class PreviewLayer
        {
            private readonly Action<float> _sample;

            internal PreviewLayer(SkillVfxPrimitiveLayer layer, Action<float> sample)
            {
                _sample = sample;
                _sample(0f);
            }

            internal void Sample(float elapsed) => _sample(elapsed);
        }

        private sealed class RendererState
        {
            internal RendererState(GameObject gameObject, Renderer renderer)
            {
                GameObject = gameObject;
                Renderer = renderer;
            }

            internal GameObject GameObject { get; }
            internal Transform Transform => GameObject.transform;
            internal Renderer Renderer { get; }
            internal MaterialPropertyBlock PropertyBlock { get; } = new();
        }
    }
}
#endif
