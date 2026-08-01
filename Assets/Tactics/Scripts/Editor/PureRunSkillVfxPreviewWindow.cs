using System.Collections.Generic;
using System.Linq;
using Tactics.Common.Skills.Graph;
using UnityEditor;
using UnityEngine;

namespace Tactics.EditorTools
{
    /// <summary>
    /// Deterministic scrub preview for finite Pure Run skill VFX recipes.
    /// </summary>
    public sealed class PureRunSkillVfxPreviewWindow : EditorWindow
    {
        private const float TileWidth = 64f;
        private const float TileHeight = 32f;
        private const float PixelsPerWorldUnit = 64f;

        private SkillVfxRecipe _recipe;
        private SkillVfxCueKind _cue;
        private int _level = 1;
        private int _pathLength = 2;
        private int _hitCount = 1;
        private bool _isPlaying;
        private float _time;
        private double _lastEditorTime;
        private GameObject _particlePreviewObject;
        private ParticleSystem _particlePreviewSystem;
        private ParticleSystem.Particle[] _particleBuffer = new ParticleSystem.Particle[64];

        [MenuItem("Tactics/Pure Run/Skill VFX Preview")]
        private static void Open()
        {
            GetWindow<PureRunSkillVfxPreviewWindow>("Skill VFX Preview");
        }

        private void OnEnable()
        {
            EditorApplication.update += UpdatePlayback;
            _lastEditorTime = EditorApplication.timeSinceStartup;
        }

        private void OnDisable()
        {
            EditorApplication.update -= UpdatePlayback;
            if (_particlePreviewObject != null)
                DestroyImmediate(_particlePreviewObject);
        }

        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            _recipe = (SkillVfxRecipe)EditorGUILayout.ObjectField("Recipe", _recipe, typeof(SkillVfxRecipe), false);
            _cue = (SkillVfxCueKind)EditorGUILayout.EnumPopup("Cue", _cue);
            _level = EditorGUILayout.IntSlider("Level", _level, 1, 3);
            _pathLength = EditorGUILayout.IntSlider("Path Length", _pathLength, 1, 4);
            _hitCount = EditorGUILayout.IntSlider("Hit Count", _hitCount, 1, 8);
            if (EditorGUI.EndChangeCheck())
            {
                _time = Mathf.Min(_time, ResolveDuration());
                Repaint();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(_isPlaying ? "Pause" : "Play"))
                {
                    _isPlaying = !_isPlaying;
                    _lastEditorTime = EditorApplication.timeSinceStartup;
                }
                if (GUILayout.Button("Replay"))
                {
                    _time = 0f;
                    _isPlaying = true;
                    _lastEditorTime = EditorApplication.timeSinceStartup;
                }
            }

            float duration = ResolveDuration();
            _time = EditorGUILayout.Slider("Time", _time, 0f, Mathf.Max(0.01f, duration));
            DrawMarkers(duration);
            Rect previewRect = GUILayoutUtility.GetRect(320f, 420f, 240f, 400f, GUILayout.ExpandWidth(true));
            DrawPreview(previewRect);
        }

        private void UpdatePlayback()
        {
            double now = EditorApplication.timeSinceStartup;
            if (_isPlaying)
            {
                float duration = ResolveDuration();
                _time += (float)(now - _lastEditorTime);
                if (_time > duration)
                {
                    _time = duration;
                    _isPlaying = false;
                }
                Repaint();
            }
            _lastEditorTime = now;
        }

        private float ResolveDuration()
        {
            IReadOnlyList<SkillVfxPrimitiveLayer> layers = _recipe?.GetLayers(_cue);
            return layers == null || layers.Count == 0 ? 0.3f : layers.Max(layer => layer?.Duration ?? 0f);
        }

        private void DrawMarkers(float duration)
        {
            IReadOnlyList<SkillVfxPrimitiveLayer> layers = _recipe?.GetLayers(_cue);
            if (layers == null || layers.Count == 0)
            {
                EditorGUILayout.HelpBox("Select a recipe and a cue with authored layers.", MessageType.Info);
                return;
            }

            string markers = string.Join(", ", layers
                .Where(layer => layer != null && layer.BlockingMarker > 0f)
                .Select(layer => $"{layer.PrimitiveKind} {layer.BlockingMarker:0.000}s")
                .Distinct());
            EditorGUILayout.LabelField("Blocking Markers", string.IsNullOrEmpty(markers) ? "None" : markers);
            EditorGUILayout.LabelField("Duration", $"{duration:0.000}s (fixed seed preview)");
        }

        private void DrawPreview(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.055f, 0.07f, 0.08f, 1f));
            Vector2 center = rect.center;
            DrawTile(center);

            IReadOnlyList<SkillVfxPrimitiveLayer> layers = _recipe?.GetLayers(_cue);
            if (layers == null)
                return;

            float pathPixels = Mathf.Min(_pathLength * TileWidth, rect.width * 0.6f);
            Vector2 source = center - Vector2.right * pathPixels * 0.5f;
            Vector2 target = center + Vector2.right * pathPixels * 0.5f;
            for (int index = 0; index < layers.Count; index++)
            {
                SkillVfxPrimitiveLayer layer = layers[index];
                SkillVfxPrimitivePreviewState state = SkillVfxPrimitiveBuilder.EvaluatePreviewState(layer, _time);
                if (!state.IsVisible && layer.PrimitiveKind != SkillVfxPrimitiveKind.ParticleBurst)
                    continue;
                DrawLayer(layer, state, source, target, index);
            }
        }

        private static void DrawTile(Vector2 center)
        {
            Vector3[] points =
            {
                center + Vector2.up * TileHeight * 0.5f,
                center + Vector2.right * TileWidth * 0.5f,
                center + Vector2.down * TileHeight * 0.5f,
                center + Vector2.left * TileWidth * 0.5f
            };
            Handles.BeginGUI();
            Handles.color = new Color(0.45f, 0.52f, 0.58f, 0.8f);
            Handles.DrawAAPolyLine(2f, points.Concat(new[] { points[0] }).ToArray());
            Handles.EndGUI();
        }

        private void DrawLayer(
            SkillVfxPrimitiveLayer layer,
            SkillVfxPrimitivePreviewState state,
            Vector2 source,
            Vector2 target,
            int layerIndex)
        {
            Color color = layer.Color;
            color.a *= state.Alpha;
            IEnumerable<Vector2> hitPositions = ResolvePreviewHitPositions(target, layer.MaximumInstances);
            switch (layer.PrimitiveKind)
            {
                case SkillVfxPrimitiveKind.RadialCore:
                    foreach (Vector2 position in hitPositions)
                        DrawDisc(position, state.Size * PixelsPerWorldUnit, color);
                    break;
                case SkillVfxPrimitiveKind.RadialRing:
                    foreach (Vector2 position in hitPositions)
                        DrawRing(position, state.Size * PixelsPerWorldUnit, color);
                    break;
                case SkillVfxPrimitiveKind.TaperedLine:
                    DrawTaperedLine(source, target, state.Size, layer, state.WidthScale, color);
                    break;
                case SkillVfxPrimitiveKind.CrossFlash:
                    foreach (Vector2 position in hitPositions)
                        DrawCross(position, state.Size * PixelsPerWorldUnit,
                            layer.RootWidth * PixelsPerWorldUnit, layer.Angle, color);
                    break;
                case SkillVfxPrimitiveKind.ParticleBurst:
                    int positionIndex = 0;
                    foreach (Vector2 position in hitPositions)
                        DrawParticles(position, layer, layerIndex + positionIndex++ * 31);
                    break;
            }
        }

        private IEnumerable<Vector2> ResolvePreviewHitPositions(Vector2 target, int maximumInstances)
        {
            int count = Mathf.Min(_hitCount, maximumInstances);
            for (int index = 0; index < count; index++)
            {
                float centered = index - (count - 1) * 0.5f;
                yield return target + new Vector2(centered * 18f, centered * -9f);
            }
        }

        private static void DrawDisc(Vector2 center, float radius, Color color)
        {
            Handles.BeginGUI();
            Handles.color = color;
            Handles.DrawSolidDisc(center, Vector3.forward, Mathf.Max(0f, radius));
            Handles.EndGUI();
        }

        private static void DrawRing(Vector2 center, float radius, Color color)
        {
            Handles.BeginGUI();
            Handles.color = color;
            Handles.DrawWireDisc(center, Vector3.forward, Mathf.Max(0f, radius), 2f);
            Handles.EndGUI();
        }

        private static void DrawTaperedLine(
            Vector2 source,
            Vector2 target,
            float lengthRatio,
            SkillVfxPrimitiveLayer layer,
            float widthScale,
            Color color)
        {
            Vector2 tip = Vector2.Lerp(source, target, Mathf.Clamp01(lengthRatio));
            Vector2 direction = (tip - source).normalized;
            Vector2 normal = new(-direction.y, direction.x);
            float rootHalf = layer.RootWidth * PixelsPerWorldUnit * widthScale * 0.5f;
            float tipHalf = layer.TipWidth * PixelsPerWorldUnit * widthScale * 0.5f;
            Vector3[] polygon =
            {
                source + normal * rootHalf,
                tip + normal * tipHalf,
                tip - normal * tipHalf,
                source - normal * rootHalf
            };
            Handles.BeginGUI();
            Handles.color = color;
            Handles.DrawAAConvexPolygon(polygon);
            Handles.EndGUI();
        }

        private static void DrawCross(Vector2 center, float length, float width, float angle, Color color)
        {
            Handles.BeginGUI();
            Handles.color = color;
            foreach (float signedAngle in new[] { -angle, angle })
            {
                float radians = signedAngle * Mathf.Deg2Rad;
                Vector2 direction = new(Mathf.Cos(radians), Mathf.Sin(radians));
                Handles.DrawAAPolyLine(Mathf.Max(1f, width), center - direction * length * 0.5f,
                    center + direction * length * 0.5f);
            }
            Handles.EndGUI();
        }

        private void DrawParticles(Vector2 center, SkillVfxPrimitiveLayer layer, int layerIndex)
        {
            if (_time > layer.ParticleLifetimeMax || layer.ParticleCount <= 0)
                return;

            ParticleSystem particles = PrepareParticlePreview(layer, layer.RandomSeed + (uint)(layerIndex * 397));
            particles.Simulate(_time, true, true, true);
            if (_particleBuffer.Length < particles.main.maxParticles)
                _particleBuffer = new ParticleSystem.Particle[particles.main.maxParticles];
            int count = particles.GetParticles(_particleBuffer);
            Handles.BeginGUI();
            for (int index = 0; index < count; index++)
            {
                ParticleSystem.Particle particle = _particleBuffer[index];
                Vector2 position = center + (Vector2)particle.position * PixelsPerWorldUnit;
                float size = Mathf.Max(1f, particle.GetCurrentSize(particles) * PixelsPerWorldUnit);
                Handles.color = particle.GetCurrentColor(particles);
                Vector3[] diamond =
                {
                    position + Vector2.up * size,
                    position + Vector2.right * size,
                    position + Vector2.down * size,
                    position + Vector2.left * size
                };
                Handles.DrawAAConvexPolygon(diamond);
            }
            Handles.EndGUI();
        }

        private ParticleSystem PrepareParticlePreview(SkillVfxPrimitiveLayer layer, uint seed)
        {
            if (_particlePreviewSystem == null)
            {
                _particlePreviewObject = new GameObject("SkillVfxParticleScrubPreview")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                _particlePreviewSystem = _particlePreviewObject.AddComponent<ParticleSystem>();
                _particlePreviewObject.GetComponent<ParticleSystemRenderer>().enabled = false;
            }

            ParticleSystem particles = _particlePreviewSystem;
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.useAutoRandomSeed = false;
            particles.randomSeed = seed == 0 ? 1u : seed;

            var main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = Mathf.Max(layer.Duration, layer.ParticleLifetimeMax);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = Mathf.Max(1, layer.ParticleCount);
            main.startLifetime = new ParticleSystem.MinMaxCurve(
                layer.ParticleLifetimeMin,
                layer.ParticleLifetimeMax);
            main.startSize = layer.ParticleSize;
            main.startSpeed = layer.ParticleSpeed;
            main.startColor = layer.Color;

            var emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)layer.ParticleCount) });
            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.001f;
            shape.randomDirectionAmount = 1f;

            var limitVelocity = particles.limitVelocityOverLifetime;
            limitVelocity.enabled = layer.ParticleDrag > 0f;
            if (limitVelocity.enabled)
            {
                limitVelocity.drag = layer.ParticleDrag;
                limitVelocity.dampen = 1f;
            }

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
            return particles;
        }
    }
}
