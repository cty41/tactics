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
using UnityEngine.TestTools.Utils;
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
                GameObject projectile = registered.Single(value => value.name == "PreviewProjectile");
                Assert.That(projectile.GetComponentsInChildren<ParticleSystem>(true), Is.Not.Empty);
                Assert.That(particleAdapter.PrimaryRenderer, Is.TypeOf<MeshRenderer>());
                Assert.That(
                    particleAdapter.PrimaryRenderer.gameObject.name,
                    Is.EqualTo("RuntimeProjectileCore"));
                Assert.That(
                    particleAdapter.PrimaryRenderer.sharedMaterial,
                    Is.SameAs(LoadProfile("Fire").Material));
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
        public void VisualCueTransform_SourceToTargetMatchesFourDirectionsAndDistance()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VisualCueProfile>(
                "Assets/Tactics/Arts/PureRun/VFX/PilotoAdapted/Profiles/ThrustStrikeLv1.asset");
            Assert.That(profile, Is.Not.Null);

            var cases = new[]
            {
                (Vector3.right * 3f, 0f),
                (Vector3.up * 3f, 90f),
                (Vector3.left * 3f, 180f),
                (Vector3.down * 3f, -90f)
            };
            foreach ((Vector3 target, float expectedAngle) in cases)
            {
                Quaternion rotation = VisualCueTransformUtility.ResolveRotation(
                    profile,
                    Vector3.zero,
                    target);
                Assert.That(Mathf.DeltaAngle(rotation.eulerAngles.z, expectedAngle),
                    Is.EqualTo(0f).Within(0.001f));
                Vector3 scale = VisualCueTransformUtility.ResolveScale(
                    profile,
                    Vector3.zero,
                    target);
                Assert.That(scale.x, Is.EqualTo(3f).Within(0.001f));
                Assert.That(scale.y, Is.EqualTo(1f).Within(0.001f));
            }
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
        public void ProceduralPreviewAdapter_ThrustUsesRuntimeTaperedGeometryAndTimeline()
        {
            var recipe = AssetDatabase.LoadAssetAtPath<SkillVfxRecipe>(
                "Assets/Tactics/Arts/PureRun/Tween/SkillVfx/Recipes/ThrustSkillVfxRecipe.asset");
            Assert.That(recipe, Is.Not.Null);
            SkillVfxPrimitiveLayer[] layers = recipe.GetLayers(SkillVfxCueKind.DirectionalStrike)
                .ToArray();
            Assert.That(layers, Has.Length.EqualTo(2));
            Assert.That(layers.All(layer =>
                layer.PrimitiveKind == SkillVfxPrimitiveKind.TaperedLine), Is.True);

            var registered = new List<GameObject>();
            Vector3 source = new Vector3(-0.4f, 0.25f, 0f);
            Vector3 target = new Vector3(1.1f, 0.85f, 0f);
            float distance = Vector3.Distance(source, target);
            float angle = Mathf.Atan2(target.y - source.y, target.x - source.x) * Mathf.Rad2Deg;
            using (var adapter = new ProceduralVfxPreviewAdapter(registered.Add))
            {
                Sequence sequence = adapter.Build(
                    recipe,
                    SkillVfxCueKind.DirectionalStrike,
                    source,
                    target);
                sequence.Pause();
                Assert.That(registered, Has.Count.EqualTo(2));

                for (int index = 0; index < registered.Count; index++)
                {
                    GameObject line = registered[index];
                    SkillVfxPrimitiveLayer layer = layers[index];
                    Mesh mesh = line.GetComponent<MeshFilter>().sharedMesh;
                    Vector3[] vertices = mesh.vertices;

                    Assert.That(mesh, Is.Not.SameAs(SkillVfxPrimitiveBuilder.SharedQuadMesh));
                    Assert.That(vertices[1].y - vertices[0].y, Is.EqualTo(1f).Within(0.001f));
                    Assert.That(
                        vertices[3].y - vertices[2].y,
                        Is.EqualTo(layer.TipWidth / layer.RootWidth).Within(0.001f));
                    Assert.That(line.transform.position, Is.EqualTo(source));
                    Assert.That(line.transform.eulerAngles.z, Is.EqualTo(angle).Within(0.001f));
                }

                sequence.Goto(0f, false);
                AssertLineScales(registered, layers, 0f, distance, 1f, true);
                sequence.Goto(0.0325f, false);
                AssertLineScales(registered, layers, 0.5f, distance, 1f, true);
                sequence.Goto(0.065f, false);
                AssertLineScales(registered, layers, 1f, distance, 1f, true);
                for (int index = 0; index < registered.Count; index++)
                {
                    var propertyBlock = new MaterialPropertyBlock();
                    registered[index].GetComponent<MeshRenderer>().GetPropertyBlock(propertyBlock);
                    Assert.That(
                        propertyBlock.GetFloat(Shader.PropertyToID("_Alpha")),
                        Is.EqualTo(layers[index].PeakAlpha).Within(0.001f));
                }
                sequence.Goto(0.16f, false);
                AssertLineScales(registered, layers, 1f, distance, 0.8f, false);
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
        public void PresentationPreview_DirectionalStrikeUsesRuntimeRepresentativeAnchors()
        {
            var actor = CreateSpriteAnchor("Actor", new Vector3(-1f, 0.25f, 0f));
            var target = CreateSpriteAnchor("Target", new Vector3(2f, -0.5f, 0f));
            Vector3 actorCenter = FindSprite(actor).bounds.center;
            Vector3 expectedTarget = target.transform.position + Vector3.up * 0.45f;
            Vector3 expectedDirection = (expectedTarget - actorCenter).normalized;

            PureRunTweenPreviewWindow.ResolveProceduralVfxAnchors(
                SkillVfxCueKind.DirectionalStrike,
                actor,
                target,
                new Vector3(9f, 9f, 0f),
                out Vector3 source,
                out Vector3 targetPosition);

            Assert.That(targetPosition, Is.EqualTo(expectedTarget));
            Assert.That(source, Is.EqualTo(actorCenter + expectedDirection * 0.10f));

            PureRunTweenPreviewWindow.ResolveProceduralVfxAnchors(
                SkillVfxCueKind.PrimaryTargetHit,
                actor,
                target,
                Vector3.zero,
                out source,
                out targetPosition);
            Assert.That(source, Is.EqualTo(actor.transform.position + Vector3.up * 0.45f));
            Assert.That(targetPosition, Is.EqualTo(expectedTarget));
        }

        [Test]
        public void PresentationPreview_UsesGraphDefaultEntry()
        {
            BattlePresentationGraph graph = ScriptableObject.CreateInstance<BattlePresentationGraph>();
            _objectsToDestroy.Add(graph);
            graph.DefaultPreviewEntry = PresentationCueKind.DirectionalStrike;
            var action = graph.AddNode(PresentationNodeType.Entry, Vector2.zero)
                as PresentationEntryNodeRecord;
            action.Cue = PresentationCueKind.Action;
            var strike = graph.AddNode(PresentationNodeType.Entry, Vector2.right)
                as PresentationEntryNodeRecord;
            strike.Cue = PresentationCueKind.DirectionalStrike;

            Assert.That(
                PureRunTweenPreviewWindow.ResolveDefaultPreviewCue(graph),
                Is.EqualTo(PresentationCueKind.DirectionalStrike));
        }

        [Test]
        public void PresentationPreview_DoesNotKeepEntryOverrideState()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;

            Assert.That(typeof(PureRunTweenPreviewWindow).GetField("_presentationCue", flags), Is.Null);
            Assert.That(
                typeof(PureRunTweenPreviewWindow).GetField("_overridePresentationCue", flags),
                Is.Null);
        }

        [Test]
        public void FullThrustPreview_OverlapsActionStrikeContactAndTargetHit()
        {
            var graph = AssetDatabase.LoadAssetAtPath<BattlePresentationGraph>(
                "Assets/Tactics/Arts/PureRun/Presentation/Thrust_Presentation.asset");
            Assert.That(graph, Is.Not.Null);
            Assert.That(graph.HasPreviewScenario, Is.True);

            var window = ScriptableObject.CreateInstance<PureRunTweenPreviewWindow>();
            try
            {
                SetField(window, "_presentationGraph", graph);
                SetField(window, "_actorPrefab", graph.PreviewActorPrefab);
                SetField(window, "_targetPrefab", graph.PreviewTargetPrefab);
                Invoke(window, "RebuildStage");

                Sequence sequence = GetField<Sequence>(window, "_previewSequence");
                float release = GetField<float>(window, "_releaseTime");
                float poseRestore = GetField<float>(window, "_poseRestoreTime");
                float contact = GetField<float>(window, "_blockingTime");
                float hit = GetField<float>(window, "_hitTime");

                Assert.That(release, Is.GreaterThan(0f));
                Assert.That(poseRestore, Is.GreaterThan(release),
                    "The action pose tail must keep restoring while the strike phase has started.");
                Assert.That(contact, Is.EqualTo(release + 0.065f).Within(0.003f));
                Assert.That(hit, Is.EqualTo(contact).Within(0.003f));
                Assert.That(sequence.Duration(false), Is.GreaterThan(hit));

                sequence.Goto(release + 0.0325f, false);
                GameObject[] taperedLines = Resources.FindObjectsOfTypeAll<GameObject>()
                    .Where(effect => effect != null && effect.name == "PresentationPreview_TaperedLine")
                    .ToArray();
                Assert.That(taperedLines, Has.Length.EqualTo(2));
                Assert.That(taperedLines.All(line => line.activeSelf), Is.True);

                GameObject target = GetField<GameObject>(window, "_targetInstance");
                UnitTweenVisual targetVisual = target.GetComponent<UnitTweenVisual>();
                Vector3 basePosition = targetVisual.BasePosition;
                sequence.Goto(hit + 0.02f, false);
                Assert.That(targetVisual.VisualRoot.localPosition, Is.Not.EqualTo(basePosition));

                Invoke(window, "StopPreview", true);
                Assert.That(targetVisual.VisualRoot.localPosition, Is.EqualTo(basePosition));
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        [TestCase("Fireball", false)]
        [TestCase("Fireball_Lv3", true)]
        [TestCase("BoneSpear", false)]
        public void FullCastProjectilePreview_UsesReleaseImpactAndFinalHitOrder(
            string presentationName,
            bool hasConditionalDetonation)
        {
            var graph = AssetDatabase.LoadAssetAtPath<BattlePresentationGraph>(
                $"Assets/Tactics/Arts/PureRun/Presentation/{presentationName}_Presentation.asset");
            Assert.That(graph, Is.Not.Null);

            var window = ScriptableObject.CreateInstance<PureRunTweenPreviewWindow>();
            try
            {
                SetField(window, "_presentationGraph", graph);
                SetField(window, "_actorPrefab", graph.PreviewActorPrefab);
                SetField(window, "_targetPrefab", graph.PreviewTargetPrefab);
                Invoke(window, "RebuildStage");

                float release = GetField<float>(window, "_releaseTime");
                float impact = GetField<float>(window, "_impactTime");
                float blocking = GetField<float>(window, "_blockingTime");
                float hit = GetField<float>(window, "_hitTime");
                Assert.That(release, Is.GreaterThan(0f), presentationName);
                Assert.That(impact, Is.GreaterThan(release), presentationName);
                Assert.That(hit, Is.GreaterThanOrEqualTo(impact), presentationName);

                if (presentationName.StartsWith("Fireball", StringComparison.Ordinal))
                {
                    Assert.That(blocking, Is.GreaterThan(impact), presentationName);
                    Assert.That(hit, hasConditionalDetonation
                        ? Is.GreaterThan(blocking)
                        : Is.EqualTo(blocking).Within(0.003f), presentationName);
                    Assert.That(
                        graph.PreviewPhases.Any(phase =>
                            phase.Cues.Contains(PresentationCueKind.ConditionalDetonation)),
                        Is.EqualTo(hasConditionalDetonation));
                }
                else
                {
                    Assert.That(blocking, Is.LessThan(0f), presentationName);
                    Assert.That(hit, Is.EqualTo(impact).Within(0.003f), presentationName);
                }
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void FullCursePreview_DoesNotPlayDamageHitReaction()
        {
            var graph = AssetDatabase.LoadAssetAtPath<BattlePresentationGraph>(
                "Assets/Tactics/Arts/PureRun/Presentation/Curse_Presentation.asset");
            var window = ScriptableObject.CreateInstance<PureRunTweenPreviewWindow>();
            try
            {
                SetField(window, "_presentationGraph", graph);
                SetField(window, "_actorPrefab", graph.PreviewActorPrefab);
                SetField(window, "_targetPrefab", graph.PreviewTargetPrefab);
                Invoke(window, "RebuildStage");

                Assert.That(GetField<float>(window, "_hitTime"), Is.LessThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void PresentationPreview_MissingDefaultEntry_FallsBackToFirstEnabledEntry()
        {
            BattlePresentationGraph graph = ScriptableObject.CreateInstance<BattlePresentationGraph>();
            _objectsToDestroy.Add(graph);
            graph.DefaultPreviewEntry = PresentationCueKind.Projectile;
            var disabled = graph.AddNode(PresentationNodeType.Entry, Vector2.zero)
                as PresentationEntryNodeRecord;
            disabled.Cue = PresentationCueKind.Action;
            disabled.Enabled = false;
            var enabled = graph.AddNode(PresentationNodeType.Entry, Vector2.right)
                as PresentationEntryNodeRecord;
            enabled.Cue = PresentationCueKind.PrimaryTargetHit;

            Assert.That(
                PureRunTweenPreviewWindow.ResolveDefaultPreviewCue(graph),
                Is.EqualTo(PresentationCueKind.PrimaryTargetHit));
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
                Invoke(window, "RebuildStage");

                Sequence sequence = GetField<Sequence>(window, "_previewSequence");
                Assert.That(sequence, Is.Not.Null);
                float release = GetField<float>(window, "_releaseTime");
                sequence.Goto(release + 0.4f, false);

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
        public void TweenPreviewWindow_CorpseLanding_UsesIndependentRuntimeCorpsePresentation()
        {
            var window = ScriptableObject.CreateInstance<PureRunTweenPreviewWindow>();
            try
            {
                SetField(window, "_loop", false);
                SetField(window, "_action", PureRunTweenPreviewWindow.PreviewAction.CorpseLanding);
                Invoke(window, "RebuildStage");

                GameObject actor = GetField<GameObject>(window, "_actorInstance");
                GameObject corpse = GetField<GameObject>(window, "_corpseInstance");
                Sequence sequence = GetField<Sequence>(window, "_previewSequence");
                StandardUnitTweenProfile profile = GetField<StandardUnitTweenProfile>(window, "_unitSandbox");
                SpriteRenderer actorRenderer = actor.GetComponentsInChildren<SpriteRenderer>(true)
                    .Single(value => value.gameObject.name == "Sprite");
                SpriteRenderer corpseRenderer = corpse.GetComponentsInChildren<SpriteRenderer>(true)
                    .Single(value => value.gameObject.name == "Sprite");
                Sprite deathSprite = actor.GetComponent<Tactics.Common.Units.FourDirectionSpriteVisual>().DeathSprite;
                Vector3 basePosition = Vector3.zero;

                Assert.That(actor.activeSelf, Is.False);
                Assert.That(corpse, Is.Not.SameAs(actor));
                Assert.That(corpseRenderer.sprite, Is.SameAs(deathSprite));
                Assert.That(corpseRenderer.sharedMaterial, Is.SameAs(actorRenderer.sharedMaterial));
                Assert.That(corpseRenderer.color, Is.EqualTo(actorRenderer.color));
                Assert.That(corpseRenderer.flipX, Is.False);
                Assert.That(corpseRenderer.sortingLayerID, Is.EqualTo(actorRenderer.sortingLayerID));
                Assert.That(corpseRenderer.sortingOrder, Is.EqualTo(actorRenderer.sortingOrder));
                Assert.That(GetField<float>(window, "_corpseDropTime"), Is.Zero);
                Assert.That(GetField<float>(window, "_corpseImpactTime"),
                    Is.EqualTo(profile.CorpseDropDuration).Within(0.0001f));
                Assert.That(GetField<float>(window, "_corpseImpactEndTime"),
                    Is.EqualTo(profile.CorpseDropDuration + profile.CorpseImpactDuration)
                        .Within(0.0001f));
                Assert.That(GetField<float>(window, "_corpseSettledTime"),
                    Is.EqualTo(profile.CorpseDropDuration + profile.CorpseImpactDuration +
                        profile.CorpseSettleDuration).Within(0.0001f));

                sequence.Goto(0f, false);
                Assert.That(corpseRenderer.transform.localPosition,
                    Is.EqualTo(basePosition + Vector3.up * profile.CorpseStartHeight)
                        .Using(Vector3ComparerWithEqualsOperator.Instance));
                Assert.That(corpseRenderer.transform.localScale,
                    Is.EqualTo(new Vector3(0.85f, 0.85f, 1f))
                        .Using(Vector3ComparerWithEqualsOperator.Instance));

                sequence.Goto(profile.CorpseDropDuration, false);
                Assert.That(corpseRenderer.transform.localPosition,
                    Is.EqualTo(basePosition).Using(Vector3ComparerWithEqualsOperator.Instance));
                Assert.That(corpseRenderer.transform.localScale,
                    Is.EqualTo(Vector3.one).Using(Vector3ComparerWithEqualsOperator.Instance));

                sequence.Goto(profile.CorpseDropDuration + profile.CorpseImpactDuration, false);
                Assert.That(corpseRenderer.transform.localScale,
                    Is.EqualTo(new Vector3(1.08f, 0.88f, 1f))
                        .Using(Vector3ComparerWithEqualsOperator.Instance));

                sequence.Goto(sequence.Duration(false), false);
                Assert.That(corpseRenderer.transform.localScale,
                    Is.EqualTo(Vector3.one).Using(Vector3ComparerWithEqualsOperator.Instance));

                Invoke(window, "StopPreview", true);
                Assert.That(actor.activeSelf, Is.True);
                Assert.That(corpse == null, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void TweenPreviewWindow_LethalHitToCorpse_UsesRuntimeHandoffAndCombinedTimeline()
        {
            var window = ScriptableObject.CreateInstance<PureRunTweenPreviewWindow>();
            try
            {
                SetField(window, "_loop", false);
                SetField(window, "_action", PureRunTweenPreviewWindow.PreviewAction.LethalHitToCorpse);
                Invoke(window, "RebuildStage");

                GameObject actor = GetField<GameObject>(window, "_actorInstance");
                GameObject corpse = GetField<GameObject>(window, "_corpseInstance");
                Sequence sequence = GetField<Sequence>(window, "_previewSequence");
                StandardUnitTweenProfile profile = GetField<StandardUnitTweenProfile>(window, "_unitSandbox");
                UnitTweenVisual actorVisual = actor.GetComponent<UnitTweenVisual>();
                SpriteRenderer actorRenderer = actor.GetComponentsInChildren<SpriteRenderer>(true)
                    .Single(value => value.gameObject.name == "Sprite");
                SpriteRenderer corpseRenderer = corpse.GetComponentsInChildren<SpriteRenderer>(true)
                    .Single(value => value.gameObject.name == "Sprite");
                SpriteRenderer[] actorRenderers = actor.GetComponentsInChildren<SpriteRenderer>(true);
                Dictionary<SpriteRenderer, bool> originalVisibility = actorRenderers
                    .ToDictionary(value => value, value => value.enabled);
                Vector3 corpseBasePosition = Vector3.zero;
                float shakeTime = profile.HitRecoilDuration;
                float collapseTime = shakeTime + profile.LethalShakeDuration;
                float handoffTime = collapseTime + profile.LethalCollapseDuration;
                float impactTime = handoffTime + profile.CorpseDropDuration;
                float impactEndTime = impactTime + profile.CorpseImpactDuration;
                float settledTime = impactEndTime + profile.CorpseSettleDuration;

                Assert.That(actorVisual.Lifecycle, Is.EqualTo(UnitPresentationLifecycle.Dying));
                Assert.That(corpseRenderer.enabled, Is.False);
                Assert.That(corpseRenderer.sortingLayerID, Is.EqualTo(actorRenderer.sortingLayerID));
                Assert.That(corpseRenderer.sortingOrder, Is.EqualTo(actorRenderer.sortingOrder));
                Assert.That(GetField<float>(window, "_lethalRecoilTime"), Is.Zero);
                Assert.That(GetField<float>(window, "_lethalShakeTime"),
                    Is.EqualTo(shakeTime).Within(0.0001f));
                Assert.That(GetField<float>(window, "_lethalCollapseTime"),
                    Is.EqualTo(collapseTime).Within(0.0001f));
                Assert.That(GetField<float>(window, "_deathHandoffTime"),
                    Is.EqualTo(handoffTime).Within(0.0001f));
                Assert.That(GetField<float>(window, "_corpseDropTime"),
                    Is.EqualTo(handoffTime).Within(0.0001f));
                Assert.That(GetField<float>(window, "_corpseImpactTime"),
                    Is.EqualTo(impactTime).Within(0.0001f));
                Assert.That(GetField<float>(window, "_corpseImpactEndTime"),
                    Is.EqualTo(impactEndTime).Within(0.0001f));
                Assert.That(GetField<float>(window, "_corpseSettledTime"),
                    Is.EqualTo(settledTime).Within(0.0001f));

                sequence.Goto(profile.HitRecoilDuration, false);
                Assert.That(Vector3.Distance(
                    actorVisual.VisualRoot.localPosition,
                    actorVisual.BasePosition), Is.GreaterThan(0.001f));
                Assert.That(corpseRenderer.enabled, Is.False);

                sequence.Goto(collapseTime, false);
                Assert.That(corpseRenderer.enabled, Is.False);

                sequence.Goto(handoffTime, false);
                Assert.That(actorVisual.Lifecycle, Is.EqualTo(UnitPresentationLifecycle.Removed));
                Assert.That(actorRenderers.All(value => !value.enabled), Is.True);
                Assert.That(actorVisual.VisualRoot.localPosition,
                    Is.EqualTo(actorVisual.BasePosition).Using(Vector3ComparerWithEqualsOperator.Instance));
                Assert.That(Quaternion.Angle(
                    actorVisual.VisualRoot.localRotation,
                    actorVisual.BaseRotation), Is.LessThan(0.001f));
                Assert.That(actorVisual.VisualRoot.localScale,
                    Is.EqualTo(new Vector3(
                        actorVisual.BaseScale.x * profile.LethalCollapseScaleX,
                        actorVisual.BaseScale.y * profile.LethalCollapseScaleY,
                        actorVisual.BaseScale.z)).Using(Vector3ComparerWithEqualsOperator.Instance));
                Assert.That(corpseRenderer.enabled, Is.True);
                Assert.That(corpseRenderer.transform.localPosition,
                    Is.EqualTo(corpseBasePosition + Vector3.up * profile.CorpseStartHeight)
                        .Using(Vector3ComparerWithEqualsOperator.Instance));
                Assert.That(corpseRenderer.transform.localScale,
                    Is.EqualTo(new Vector3(0.85f, 0.85f, 1f))
                        .Using(Vector3ComparerWithEqualsOperator.Instance));

                sequence.Goto(impactTime, false);
                Assert.That(corpseRenderer.transform.localPosition,
                    Is.EqualTo(corpseBasePosition).Using(Vector3ComparerWithEqualsOperator.Instance));
                Assert.That(corpseRenderer.transform.localScale,
                    Is.EqualTo(Vector3.one).Using(Vector3ComparerWithEqualsOperator.Instance));

                sequence.Goto(impactEndTime, false);
                Assert.That(corpseRenderer.transform.localScale,
                    Is.EqualTo(new Vector3(1.08f, 0.88f, 1f))
                        .Using(Vector3ComparerWithEqualsOperator.Instance));

                sequence.Goto(settledTime, false);
                Assert.That(corpseRenderer.transform.localScale,
                    Is.EqualTo(Vector3.one).Using(Vector3ComparerWithEqualsOperator.Instance));

                Invoke(window, "StopPreview", true);
                Assert.That(actorVisual.Lifecycle, Is.EqualTo(UnitPresentationLifecycle.Alive));
                Assert.That(originalVisibility.All(entry => entry.Key.enabled == entry.Value), Is.True);
                Assert.That(corpse == null, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void UnitTweenVisual_DyingPreemptsForegroundAndCompletesHandoffOnce()
        {
            var unitObject = new GameObject("DyingUnit");
            var spriteObject = new GameObject("Sprite");
            spriteObject.transform.SetParent(unitObject.transform);
            var renderer = spriteObject.AddComponent<SpriteRenderer>();
            var profile = ScriptableObject.CreateInstance<StandardUnitTweenProfile>();
            var visual = unitObject.AddComponent<UnitTweenVisual>();
            visual.ConfigureForPreview(spriteObject.transform, renderer, profile);
            _objectsToDestroy.Add(unitObject);
            _objectsToDestroy.Add(profile);

            visual.BeginMoveStep(Vector3.right);
            int handoffCount = 0;
            Sequence sequence = visual.PlayDying(Vector3.right, () => handoffCount++);
            int releaseCount = 0;
            visual.PlayActionAsync(
                UnitVisualAction.Melee,
                Vector3.right,
                () => releaseCount++,
                default).GetAwaiter().GetResult();
            visual.PlayHit(Vector3.right);
            visual.BeginMoveStep(Vector3.right);

            Assert.That(sequence, Is.Not.Null);
            Assert.That(visual.Lifecycle, Is.EqualTo(UnitPresentationLifecycle.Dying));
            Assert.That(releaseCount, Is.Zero);

            sequence.Kill(false);
            visual.StopAllVisualTweens();
            Assert.That(handoffCount, Is.EqualTo(1));
            Assert.That(visual.Lifecycle, Is.EqualTo(UnitPresentationLifecycle.Removed));
            Assert.That(renderer.enabled, Is.False);

            visual.ResetPresentationForPreview();
            Assert.That(visual.Lifecycle, Is.EqualTo(UnitPresentationLifecycle.Alive));
            Assert.That(renderer.enabled, Is.True);
        }

        [TestCase(1f, 0f)]
        [TestCase(-1f, 0f)]
        [TestCase(0f, 1f)]
        [TestCase(0f, -1f)]
        public void UnitTweenVisual_LethalRecoilMovesAwayFromAttacker(float attackerX, float attackerY)
        {
            var unitObject = new GameObject("DirectionalDyingUnit");
            var spriteObject = new GameObject("Sprite");
            spriteObject.transform.SetParent(unitObject.transform);
            var renderer = spriteObject.AddComponent<SpriteRenderer>();
            var profile = ScriptableObject.CreateInstance<StandardUnitTweenProfile>();
            var visual = unitObject.AddComponent<UnitTweenVisual>();
            visual.ConfigureForPreview(spriteObject.transform, renderer, profile);
            _objectsToDestroy.Add(unitObject);
            _objectsToDestroy.Add(profile);

            var attackerPosition = new Vector3(attackerX, attackerY, 0f);
            Sequence sequence = visual.PlayDying(attackerPosition, null);
            sequence.Goto(profile.HitRecoilDuration, false);

            Vector3 expectedDirection = -attackerPosition.normalized;
            Vector3 displacement = visual.VisualRoot.localPosition - visual.BasePosition;
            Assert.That(Vector3.Dot(displacement.normalized, expectedDirection),
                Is.EqualTo(1f).Within(0.001f));
            Assert.That(displacement.magnitude,
                Is.EqualTo(profile.HitRecoilDistance).Within(0.001f));
        }

        [Test]
        public void UnitTweenVisualEditor_UsesReadOnlyRuntimeDebugSnapshot()
        {
            var unitObject = new GameObject("DebugUnit");
            var spriteObject = new GameObject("Sprite");
            spriteObject.transform.SetParent(unitObject.transform);
            var renderer = spriteObject.AddComponent<SpriteRenderer>();
            var profile = ScriptableObject.CreateInstance<StandardUnitTweenProfile>();
            var visual = unitObject.AddComponent<UnitTweenVisual>();
            visual.ConfigureForPreview(spriteObject.transform, renderer, profile);
            _objectsToDestroy.Add(unitObject);
            _objectsToDestroy.Add(profile);

            int handoffCount = 0;
            Sequence sequence = visual.PlayDying(Vector3.right, () => handoffCount++);
            UnitTweenVisualDebugSnapshot dying = visual.GetDebugSnapshot();
            UnityEditor.Editor editor = UnityEditor.Editor.CreateEditor(visual);
            _objectsToDestroy.Add(editor);

            Assert.That(editor, Is.TypeOf<Tactics.Editor.UnitTweenVisualEditor>());
            Assert.That(editor.RequiresConstantRepaint(), Is.False);
            Assert.That(dying.Lifecycle, Is.EqualTo(UnitPresentationLifecycle.Dying));
            Assert.That(dying.ForegroundPriority, Is.EqualTo("Corpse"));
            Assert.That(dying.IsForegroundTweenActive, Is.True);
            Assert.That(dying.IsDeathHandoffComplete, Is.False);

            sequence.Goto(sequence.Duration(false), false);
            UnitTweenVisualDebugSnapshot removed = visual.GetDebugSnapshot();
            Assert.That(handoffCount, Is.EqualTo(1));
            Assert.That(removed.Lifecycle, Is.EqualTo(UnitPresentationLifecycle.Removed));
            Assert.That(removed.IsDeathHandoffComplete, Is.True);
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

        private static void AssertLineScales(
            IReadOnlyList<GameObject> lines,
            IReadOnlyList<SkillVfxPrimitiveLayer> layers,
            float expectedSize,
            float distance,
            float expectedWidthScale,
            bool expectedVisible)
        {
            for (int index = 0; index < lines.Count; index++)
            {
                Assert.That(lines[index].activeSelf, Is.EqualTo(expectedVisible));
                Assert.That(
                    lines[index].transform.localScale.x,
                    Is.EqualTo(distance * expectedSize).Within(0.001f));
                Assert.That(
                    lines[index].transform.localScale.y,
                    Is.EqualTo(layers[index].RootWidth * expectedWidthScale).Within(0.001f));
            }
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
