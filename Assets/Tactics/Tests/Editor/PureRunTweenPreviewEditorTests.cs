using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DG.Tweening;
using NUnit.Framework;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Units.Tween;
using Tactics.EditorTools;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tactics.Tests.Editor
{
    /// <summary>
    /// Covers the shared projectile factory and lifecycle boundaries used by Tween Preview.
    /// </summary>
    public sealed class PureRunTweenPreviewEditorTests
    {
        private readonly List<Object> _objectsToDestroy = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object value in _objectsToDestroy)
            {
                if (value != null)
                    Object.DestroyImmediate(value);
            }
            _objectsToDestroy.Clear();
        }

        [Test]
        public void ProjectileFactory_SpriteWithoutExplicitMaterial_PreservesUnityDefault()
        {
            ProjectileVisualProfile profile = LoadProfile("BoneSpear");
            ProjectileVisualHandle handle = ProjectileVisualFactory.CreateProjectile(profile, null);
            _objectsToDestroy.Add(handle.GameObject);

            Assert.That(handle.IsValid, Is.True);
            Assert.That(handle.Renderer, Is.TypeOf<SpriteRenderer>());
            Assert.That(((SpriteRenderer)handle.Renderer).sprite, Is.SameAs(profile.Sprite));
            Assert.That(handle.Renderer.sharedMaterial, Is.Not.Null);
        }

        [Test]
        public void ProjectileFactory_SoftDisc_UsesAuthoredMaterialAndSharedMesh()
        {
            ProjectileVisualProfile profile = LoadProfile("Fire");
            ProjectileVisualHandle handle = ProjectileVisualFactory.CreateProjectile(profile, null);
            _objectsToDestroy.Add(handle.GameObject);

            Assert.That(handle.IsValid, Is.True);
            Assert.That(handle.Renderer, Is.TypeOf<MeshRenderer>());
            Assert.That(handle.Renderer.sharedMaterial, Is.SameAs(profile.Material));
            Assert.That(handle.GameObject.GetComponent<MeshFilter>().sharedMesh, Is.Not.Null);
        }

        [Test]
        public void ProjectileFactory_Duration_UsesRuntimeClampAndFallback()
        {
            Assert.That(ProjectileVisualFactory.ResolveDuration(0.1f, 10f, 0.3f), Is.EqualTo(0.12f));
            Assert.That(ProjectileVisualFactory.ResolveDuration(100f, 10f, 0.3f), Is.EqualTo(0.75f));
            Assert.That(ProjectileVisualFactory.ResolveDuration(4f, 0f, 0.3f), Is.EqualTo(0.3f));
        }

        [Test]
        public void ProjectilePreviewAdapter_BuildsRuntimeParticleAndGhostKinds_ThenCleansThem()
        {
            var registered = new List<GameObject>();
            var sourceObject = new GameObject("PreviewSource");
            _objectsToDestroy.Add(sourceObject);
            var sourceRenderer = sourceObject.AddComponent<SpriteRenderer>();

            using (var particleAdapter = new ProjectileVisualPreviewAdapter(registered.Add, null))
            {
                particleAdapter.Build(
                    LoadProfile("Fire"),
                    sourceRenderer,
                    Vector3.zero,
                    Vector3.right,
                    0.25f);
                Assert.That(registered.Any(value => value.name == "PreviewProjectile"), Is.True);
                Assert.That(
                    registered.Any(value => value.name == "PreviewProjectileParticleTrail"),
                    Is.True);
            }
            Assert.That(registered.All(value => value == null), Is.True);

            registered.Clear();
            using (var ghostAdapter = new ProjectileVisualPreviewAdapter(registered.Add, null))
            {
                ghostAdapter.Build(
                    LoadProfile("BoneSpear"),
                    sourceRenderer,
                    Vector3.zero,
                    Vector3.right,
                    0.25f);
                Assert.That(
                    registered.Any(value => value != null && value.name.StartsWith("PreviewProjectileGhost_")),
                    Is.True);
            }
            Assert.That(registered.All(value => value == null), Is.True);
        }

        [Test]
        public void ProceduralPreviewAdapter_BuildsRecipePrimitives_ThenCleansThem()
        {
            var recipe = AssetDatabase.LoadAssetAtPath<SkillVfxRecipe>(
                "Assets/Tactics/Arts/PureRun/Tween/SkillVfx/Recipes/DefaultCastSkillVfxRecipe.asset");
            Assert.That(recipe, Is.Not.Null);
            var registered = new List<GameObject>();
            using (var adapter = new ProceduralVfxPreviewAdapter(registered.Add))
            {
                var sequence = adapter.Build(
                    recipe,
                    SkillVfxCueKind.CastCharge,
                    Vector3.zero,
                    Vector3.right);
                Assert.That(sequence.Duration(false), Is.GreaterThan(0f));
                Assert.That(registered, Is.Not.Empty);
                sequence.Kill(false);
            }
            Assert.That(registered.All(value => value == null), Is.True);
        }

        [Test]
        public void PreviewSpriteState_RestoresStandingSpriteFlipAndColor()
        {
            var rendererObject = new GameObject("Sprite");
            _objectsToDestroy.Add(rendererObject);
            var renderer = rendererObject.AddComponent<SpriteRenderer>();
            Sprite standing = LoadProfile("BoneSpear").Sprite;
            renderer.sprite = standing;
            renderer.flipX = true;
            renderer.color = Color.cyan;
            PreviewSpriteState state = PreviewSpriteState.Capture(renderer);

            renderer.sprite = null;
            renderer.flipX = false;
            renderer.color = Color.magenta;
            state.Restore(renderer);

            Assert.That(renderer.sprite, Is.SameAs(standing));
            Assert.That(renderer.flipX, Is.True);
            Assert.That(renderer.color, Is.EqualTo(Color.cyan));
        }

        [Test]
        public void PresentationPreview_ResolvesCueAnchorsFromSpriteCentersAndTilePoint()
        {
            var actor = CreateSpriteAnchor("Actor", new Vector3(-1f, 0.25f, 0f));
            var target = CreateSpriteAnchor("Target", new Vector3(2f, -0.5f, 0f));
            var profile = ScriptableObject.CreateInstance<VisualCueProfile>();
            _objectsToDestroy.Add(profile);
            var serialized = new SerializedObject(profile);
            Vector3 targetPoint = new Vector3(3f, -1f, 0f);

            SetAnchor(serialized, VisualCueAnchor.Caster);
            Assert.That(PureRunTweenPreviewWindow.ResolveVisualCueAnchor(
                profile, actor, target, targetPoint), Is.EqualTo(FindSprite(actor).bounds.center));

            SetAnchor(serialized, VisualCueAnchor.PrimaryTarget);
            Assert.That(PureRunTweenPreviewWindow.ResolveVisualCueAnchor(
                profile, actor, target, targetPoint), Is.EqualTo(FindSprite(target).bounds.center));

            SetAnchor(serialized, VisualCueAnchor.PrimaryTargetGround);
            Assert.That(PureRunTweenPreviewWindow.ResolveVisualCueAnchor(
                profile, actor, target, targetPoint), Is.EqualTo(target.transform.position));

            SetAnchor(serialized, VisualCueAnchor.TargetPoint);
            Assert.That(PureRunTweenPreviewWindow.ResolveVisualCueAnchor(
                profile, actor, target, targetPoint), Is.EqualTo(targetPoint));
        }

        [Test]
        public void PresentationPreview_AppliesRuntimeSortingToPrefabFx()
        {
            var graph = AssetDatabase.LoadAssetAtPath<BattlePresentationGraph>(
                "Assets/Tactics/Arts/PureRun/Presentation/Curse_Presentation.asset");
            Assert.That(graph, Is.Not.Null);
            VisualCueProfile[] profiles = graph.Nodes.OfType<PresentationPrefabFxNodeRecord>()
                .Select(node => node.Profile)
                .ToArray();
            Assert.That(profiles, Has.Length.EqualTo(3));
            Assert.That(profiles, Has.All.Not.Null);

            var window = ScriptableObject.CreateInstance<PureRunTweenPreviewWindow>();
            try
            {
                SetField(window, "_presentationGraph", graph);
                SetField(window, "_presentationCue", PresentationCueKind.PrimaryTargetHit);
                Invoke(window, "RebuildStage");

                GameObject target = GetField<GameObject>(window, "_targetInstance");
                SpriteRenderer targetRenderer = FindSprite(target);
                var previewObjects = GetField<List<GameObject>>(window, "_presentationPreviewObjects");
                Assert.That(previewObjects, Has.Count.EqualTo(3));
                foreach (VisualCueProfile profile in profiles)
                {
                    GameObject effect = previewObjects.Single(candidate =>
                        candidate.name.StartsWith(profile.Prefab.name, StringComparison.Ordinal));
                    foreach (Renderer renderer in effect.GetComponentsInChildren<Renderer>(true))
                    {
                        Assert.That(renderer.sortingLayerID, Is.EqualTo(targetRenderer.sortingLayerID));
                        Assert.That(
                            renderer.sortingOrder,
                            Is.EqualTo(targetRenderer.sortingOrder + profile.SortingOrderOffset));
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void CursePresentationPreview_SimulatesVisibleParticlesOverTarget()
        {
            var graph = AssetDatabase.LoadAssetAtPath<BattlePresentationGraph>(
                "Assets/Tactics/Arts/PureRun/Presentation/Curse_Presentation.asset");
            Assert.That(graph, Is.Not.Null);

            var window = ScriptableObject.CreateInstance<PureRunTweenPreviewWindow>();
            try
            {
                SetField(window, "_presentationGraph", graph);
                SetField(window, "_presentationCue", PresentationCueKind.PrimaryTargetHit);
                Invoke(window, "RebuildStage");

                Sequence sequence = GetField<Sequence>(window, "_previewSequence");
                Assert.That(sequence, Is.Not.Null);
                sequence.Goto(0.4f, false);

                GameObject target = GetField<GameObject>(window, "_targetInstance");
                Bounds targetBounds = FindSprite(target).bounds;
                List<GameObject> effects = GetField<List<GameObject>>(
                    window,
                    "_presentationPreviewObjects");
                Assert.That(effects, Has.Count.EqualTo(3));
                Assert.That(effects.All(effect => effect.activeInHierarchy), Is.True);
                GameObject ground = effects.Single(effect =>
                    effect.name.StartsWith("AmplifyDamageSigilGroundV2", StringComparison.Ordinal));
                GameObject rearFlames = effects.Single(effect =>
                    effect.name.StartsWith("AmplifyDamageSigilRearFlamesV2", StringComparison.Ordinal));
                GameObject foregroundFlames = effects.Single(effect =>
                    effect.name.StartsWith(
                        "AmplifyDamageSigilForegroundFlamesV2",
                        StringComparison.Ordinal));
                Assert.That(ground, Is.Not.Null);
                Assert.That(
                    rearFlames.GetComponentsInChildren<ParticleSystem>(true)
                        .Sum(system => system.particleCount),
                    Is.GreaterThan(0));
                Assert.That(
                    foregroundFlames.GetComponentsInChildren<ParticleSystem>(true)
                        .Sum(system => system.particleCount),
                    Is.GreaterThan(0));
                ParticleSystem[] systems = effects
                    .SelectMany(effect => effect.GetComponentsInChildren<ParticleSystem>(true))
                    .ToArray();
                Assert.That(systems.Sum(system => system.particleCount), Is.GreaterThan(0));

                Bounds effectBounds = default;
                bool hasBounds = false;
                foreach (ParticleSystemRenderer renderer in
                         effects.SelectMany(effect =>
                             effect.GetComponentsInChildren<ParticleSystemRenderer>(true)))
                {
                    if (!renderer.enabled || renderer.bounds.size.sqrMagnitude < 0.0001f)
                        continue;
                    if (hasBounds)
                        effectBounds.Encapsulate(renderer.bounds);
                    else
                    {
                        effectBounds = renderer.bounds;
                        hasBounds = true;
                    }
                }

                Assert.That(hasBounds, Is.True);
                Assert.That(effectBounds.max.x, Is.GreaterThan(targetBounds.min.x), effectBounds.ToString());
                Assert.That(effectBounds.min.x, Is.LessThan(targetBounds.max.x), effectBounds.ToString());
                Assert.That(effectBounds.max.y, Is.GreaterThan(targetBounds.min.y), effectBounds.ToString());
                Assert.That(effectBounds.min.y, Is.LessThan(targetBounds.max.y), effectBounds.ToString());
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void TweenPreviewWindow_RebuildAndDestroy_LeavesNoPreviewObjects()
        {
            int before = CountPreviewObjects();
            var window = ScriptableObject.CreateInstance<PureRunTweenPreviewWindow>();
            MethodInfo rebuild = typeof(PureRunTweenPreviewWindow).GetMethod(
                "RebuildStage",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(rebuild, Is.Not.Null);
            rebuild.Invoke(window, null);
            Assert.That(CountPreviewObjects(), Is.GreaterThan(before));

            Object.DestroyImmediate(window);
            Assert.That(CountPreviewObjects(), Is.EqualTo(before));
        }

        [Test]
        public void TweenPreviewWindow_RepeatedPlayStopAndRebuild_DoesNotCorruptTweenManager()
        {
            var window = ScriptableObject.CreateInstance<PureRunTweenPreviewWindow>();
            try
            {
                Invoke(window, "RebuildStage");
                for (int iteration = 0; iteration < 3; iteration++)
                {
                    Assert.DoesNotThrow(() => Invoke(window, "PlayPreview"));
                    Assert.DoesNotThrow(() => Invoke(window, "StopPreview", true));
                    Assert.DoesNotThrow(() => Invoke(window, "RebuildSequence", false));
                }
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void TweenPreviewWindow_RebuildStopAndActionChange_PreserveVisibleSpriteScale()
        {
            var window = ScriptableObject.CreateInstance<PureRunTweenPreviewWindow>();
            try
            {
                Invoke(window, "RebuildStage");
                AssertPreviewSpriteIsAuthoredAndVisible(window, "_actorInstance");
                AssertPreviewSpriteIsAuthoredAndVisible(window, "_targetInstance");

                Invoke(window, "StopPreview", true);
                AssertPreviewSpriteIsAuthoredAndVisible(window, "_actorInstance");
                AssertPreviewSpriteIsAuthoredAndVisible(window, "_targetInstance");

                Invoke(window, "RebuildSequence", false);
                AssertPreviewSpriteIsAuthoredAndVisible(window, "_actorInstance");
                AssertPreviewSpriteIsAuthoredAndVisible(window, "_targetInstance");
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void TweenPreviewWindow_ApplySupportsUndo_AndRevertRestoresSandbox()
        {
            var source = ScriptableObject.CreateInstance<StandardUnitTweenProfile>();
            _objectsToDestroy.Add(source);
            float originalDuration = source.IdleDuration;
            var window = ScriptableObject.CreateInstance<PureRunTweenPreviewWindow>();
            Invoke(window, "SetUnitProfile", source);

            var sandbox = GetField<StandardUnitTweenProfile>(window, "_unitSandbox");
            SetSerializedFloat(sandbox, "_idleDuration", originalDuration + 0.5f);
            SetField(window, "_unitSandboxDirty", true);
            Invoke(window, "ApplyUnitSandbox");
            Assert.That(source.IdleDuration, Is.EqualTo(originalDuration + 0.5f).Within(0.0001f));

            Undo.PerformUndo();
            Assert.That(source.IdleDuration, Is.EqualTo(originalDuration).Within(0.0001f));

            SetSerializedFloat(sandbox, "_idleDuration", originalDuration + 0.75f);
            SetField(window, "_unitSandboxDirty", true);
            Invoke(window, "RevertUnitSandbox");
            StandardUnitTweenProfile reverted = GetField<StandardUnitTweenProfile>(window, "_unitSandbox");
            Assert.That(reverted.IdleDuration, Is.EqualTo(source.IdleDuration).Within(0.0001f));

            Object.DestroyImmediate(window);
        }

        [Test]
        public void TweenPreviewWindow_Destroy_CleansTransientProjectileProfile()
        {
            var window = ScriptableObject.CreateInstance<PureRunTweenPreviewWindow>();
            var transient = (ProjectileVisualProfile)Invoke(window, "CreateTransientProjectileProfile");
            Assert.That(transient, Is.Not.Null);

            Object.DestroyImmediate(window);
            Assert.That(transient == null, Is.True);
        }

        [Test]
        public void TweenPreviewWindow_UsesCompliantMenuAndCurrentActionApi()
        {
            const string path = "Assets/Tactics/Scripts/Editor/PureRunTweenPreviewWindow.cs";
            string source = File.ReadAllText(path);
            StringAssert.Contains("[MenuItem(\"Tactics/Pure Run/Tween Preview\")]", source);
            StringAssert.DoesNotContain("GlowOverlay", source);
            StringAssert.DoesNotContain("Tactics/Tools/Pure Run/Tween Preview", source);
        }

        private static int CountPreviewObjects()
        {
            return Resources.FindObjectsOfTypeAll<GameObject>().Count(value =>
                value != null &&
                (value.name.StartsWith("PreviewTile_") ||
                 value.name.StartsWith("PreviewProjectile") ||
                 value.name == "PureRunHunter(Clone)" ||
                 value.name == "PureRunGoatCharger(Clone)"));
        }

        private GameObject CreateSpriteAnchor(string name, Vector3 position)
        {
            var root = new GameObject(name);
            _objectsToDestroy.Add(root);
            root.transform.position = position;

            var spriteObject = new GameObject("Sprite");
            spriteObject.transform.SetParent(root.transform, false);
            SpriteRenderer renderer = spriteObject.AddComponent<SpriteRenderer>();
            renderer.sprite = LoadProfile("BoneSpear").Sprite;
            return root;
        }

        private static SpriteRenderer FindSprite(GameObject root)
        {
            return root
                .GetComponentsInChildren<SpriteRenderer>(true)
                .Single(value => value.gameObject.name == "Sprite");
        }

        private static void SetAnchor(SerializedObject serializedObject, VisualCueAnchor anchor)
        {
            SerializedProperty property = serializedObject.FindProperty("_anchor");
            Assert.That(property, Is.Not.Null, "_anchor");
            property.enumValueIndex = (int)anchor;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssertPreviewSpriteIsAuthoredAndVisible(
            PureRunTweenPreviewWindow window,
            string instanceFieldName)
        {
            GameObject instance = GetField<GameObject>(window, instanceFieldName);
            Assert.That(instance, Is.Not.Null, instanceFieldName);
            SpriteRenderer renderer = instance
                .GetComponentsInChildren<SpriteRenderer>(true)
                .Single(value => value.gameObject.name == "Sprite");
            UnitTweenVisual visual = instance.GetComponent<UnitTweenVisual>();

            Assert.That(renderer.sprite, Is.Not.Null);
            Assert.That(renderer.transform.localScale.x, Is.GreaterThan(0f));
            Assert.That(renderer.transform.localScale.y, Is.GreaterThan(0f));
            Assert.That(renderer.bounds.size.x, Is.GreaterThan(0f));
            Assert.That(renderer.bounds.size.y, Is.GreaterThan(0f));
            Assert.That(visual, Is.Not.Null);
            Assert.That(
                visual.VisualRoot == renderer.transform ||
                renderer.transform.IsChildOf(visual.VisualRoot),
                Is.True,
                "Tween root must own the visible Sprite hierarchy.");
        }

        private static ProjectileVisualProfile LoadProfile(string name)
        {
            var profile = AssetDatabase.LoadAssetAtPath<ProjectileVisualProfile>(
                $"Assets/Tactics/Arts/PureRun/Tween/Projectiles/{name}.asset");
            Assert.That(profile, Is.Not.Null, name);
            return profile;
        }

        private static object Invoke(Object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            return method.Invoke(target, arguments);
        }

        private static T GetField<T>(Object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return (T)field.GetValue(target);
        }

        private static void SetField<T>(Object target, string fieldName, T value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private static void SetSerializedFloat(Object target, string propertyName, float value)
        {
            var serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            Assert.That(property, Is.Not.Null, propertyName);
            property.floatValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
