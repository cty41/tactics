using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
