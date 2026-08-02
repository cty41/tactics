using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Tactics.AssetPipeline;
using Tactics.Common.Battle;
using Tactics.Common.Battle.Runtime;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Skills.Graph.Testing;
using Tactics.Common.Testing.Gameplay;
using Tactics.Common.Units.Abilities;
using Tactics.Common.Units.Buffs;
using Tactics.Common.Units.Tween;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tactics.Tests.PlayMode
{
    public class PureRunTweenPlayModeTests
    {
        [UnitySetUp]
        public System.Collections.IEnumerator SetUp()
        {
            GameTimeService.ForceResume();
            GameTimeService.SetPlaybackSpeed(GamePlaybackSpeed.Normal);
            var task = TestGameAssetHelper.EnsureInitialized();
            yield return WaitForTask(task, 10d, "Complete async test operation");
            Assert.That(task.Result, Is.Not.Null);
        }

        [UnityTearDown]
        public System.Collections.IEnumerator TearDown()
        {
            bool runtimeTeardownCompleted = true;
            Exception runtimeTeardownException = null;
            bool managerRestoreCompleted = true;
            bool managerRestoreFaulted = false;
            var cleanupFailures = new List<Exception>();
            BattleController controller = null;
            RecordCleanupFailure(cleanupFailures, "Resolve BattleController", () =>
                controller = BattleController.Instance);
            if (controller != null)
            {
                Task teardownTask = null;
                RecordCleanupFailure(cleanupFailures, "Start BattleController runtime teardown", () =>
                    teardownTask = controller.TeardownRuntimeScopeAsync());
                if (teardownTask == null)
                {
                    runtimeTeardownCompleted = false;
                }
                else
                {
                    for (int frame = 0; frame < 30 && !teardownTask.IsCompleted; frame++)
                        yield return null;

                    runtimeTeardownCompleted = teardownTask.IsCompleted;
                    RecordCleanupFailure(cleanupFailures, "Read BattleController teardown result", () =>
                        runtimeTeardownException = controller.RuntimeScopeTeardownException);
                }

                RecordCleanupFailure(cleanupFailures, "Destroy BattleController", () =>
                    Object.Destroy(controller.gameObject));
                yield return null;
            }

            GameAssetManager manager = null;
            RecordCleanupFailure(cleanupFailures, "Resolve GameAssetManager", () =>
                manager = GameAssetManager.Instance);
            if (manager == null)
            {
                Task<GameAssetManager> restoreManagerTask = null;
                RecordCleanupFailure(cleanupFailures, "Start GameAssetManager restore", () =>
                    restoreManagerTask = TestGameAssetHelper.EnsureInitialized());
                if (restoreManagerTask == null)
                {
                    managerRestoreCompleted = false;
                    managerRestoreFaulted = true;
                }
                else
                {
                    for (int frame = 0; frame < 30 && !restoreManagerTask.IsCompleted; frame++)
                        yield return null;

                    managerRestoreCompleted = restoreManagerTask.IsCompleted;
                    managerRestoreFaulted = restoreManagerTask.IsFaulted;
                }
            }

            for (int frame = 0; frame < 3; frame++)
                yield return null;

            RecordCleanupFailure(cleanupFailures, "Destroy Battle UI", () =>
                UIManager.Instance?.Destroy(UIManager.UIId.Battle));
            RecordCleanupFailure(cleanupFailures, "Destroy CheatConsole UI", () =>
                UIManager.Instance?.Destroy(UIManager.UIId.CheatConsole));
            yield return null;

            RecordCleanupFailure(cleanupFailures, "Reset transient VFX pool", ResetTransientVfxPoolForTests);
            RecordCleanupFailure(cleanupFailures, "Resume game time", GameTimeService.ForceResume);
            RecordCleanupFailure(cleanupFailures, "Reset playback speed", () =>
                GameTimeService.SetPlaybackSpeed(GamePlaybackSpeed.Normal));
            RecordCleanupFailure(cleanupFailures, "Cleanup test assets", TestGameAssetHelper.Cleanup);
            yield return null;

            Assert.That(runtimeTeardownCompleted, Is.True,
                "BattleController runtime scope teardown must complete within 30 frames during fixture cleanup.");
            Assert.That(runtimeTeardownException, Is.Null,
                "BattleController runtime scope teardown must not observe tracked-task, cancellation, or disposal failures.");
            Assert.That(managerRestoreCompleted, Is.True,
                "Fixture cleanup must restore GameAssetManager within 30 frames.");
            Assert.That(managerRestoreFaulted, Is.False,
                "Fixture cleanup must restore GameAssetManager without faulting.");
            Assert.That(BattleController.Instance, Is.Null,
                "Fixture cleanup must leave no BattleController singleton behind.");
            Assert.That(cleanupFailures, Is.Empty,
                "Fixture cleanup steps failed after all remaining cleanup steps were attempted: " +
                string.Join(" | ", cleanupFailures.Select(exception => exception.Message)));
        }

        [UnityTest]
        public System.Collections.IEnumerator ActionRelease_IsInvokedExactlyOnce()
        {
            var root = new GameObject("Unit");
            var spriteObject = new GameObject("Sprite");
            spriteObject.transform.SetParent(root.transform, false);
            var renderer = spriteObject.AddComponent<SpriteRenderer>();
            var profile = ScriptableObject.CreateInstance<StandardUnitTweenProfile>();
            var visual = root.AddComponent<UnitTweenVisual>();
            visual.ConfigureForPreview(spriteObject.transform, renderer, profile);

            int releases = 0;
            Task task = visual.PlayActionAsync(
                UnitVisualAction.Ranged,
                Vector3.right,
                () => releases++,
                CancellationToken.None);
            while (!task.IsCompleted)
                yield return null;

            Assert.That(task.IsFaulted, Is.False);
            Assert.That(releases, Is.EqualTo(1));
            Assert.That(spriteObject.transform.localPosition.sqrMagnitude, Is.LessThan(0.00001f));
            Assert.That((spriteObject.transform.localScale - Vector3.one).sqrMagnitude, Is.LessThan(0.00001f));

            Object.Destroy(root);
            Object.Destroy(profile);
        }

        [UnityTest]
        public System.Collections.IEnumerator CastTween_PreservesSpriteAndReleasesExactlyOnce()
        {
            var root = new GameObject("CastUnit");
            var spriteObject = new GameObject("Sprite");
            spriteObject.transform.SetParent(root.transform, false);
            var renderer = spriteObject.AddComponent<SpriteRenderer>();
            var texture = new Texture2D(2, 2);
            renderer.sprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), Vector2.one * 0.5f);
            renderer.color = new Color(0.42f, 0.57f, 0.81f, 0.93f);
            Sprite originalSprite = renderer.sprite;
            Color originalColor = renderer.color;
            Material originalMaterial = renderer.sharedMaterial;
            var profile = ScriptableObject.CreateInstance<StandardUnitTweenProfile>();
            var visual = root.AddComponent<UnitTweenVisual>();
            visual.ConfigureForPreview(spriteObject.transform, renderer, profile);

            int releases = 0;
            Task task = visual.PlayActionAsync(
                UnitVisualAction.Cast,
                Vector3.right,
                () => releases++,
                CancellationToken.None);
            yield return null;

            Assert.That(spriteObject.transform.Find("GlowOverlay"), Is.Null);
            Assert.That(renderer.sprite, Is.SameAs(originalSprite));
            Assert.That(renderer.color, Is.EqualTo(originalColor));
            Assert.That(renderer.sharedMaterial, Is.SameAs(originalMaterial));

            yield return WaitForTask(task, 10d, "Complete async test operation");
            Assert.That(task.IsFaulted, Is.False);
            Assert.That(releases, Is.EqualTo(1));
            Assert.That(spriteObject.transform.Find("GlowOverlay"), Is.Null);
            Assert.That(renderer.sprite, Is.SameAs(originalSprite));
            Assert.That(renderer.color, Is.EqualTo(originalColor));
            Assert.That(renderer.sharedMaterial, Is.SameAs(originalMaterial));

            Object.Destroy(root);
            Object.Destroy(profile);
            Object.Destroy(renderer.sprite);
            Object.Destroy(texture);
        }

        [UnityTest]
        public System.Collections.IEnumerator CancelledCast_RestoresPoseWithoutCreatingOverlay()
        {
            var root = new GameObject("CancelledCastUnit");
            var spriteObject = new GameObject("Sprite");
            spriteObject.transform.SetParent(root.transform, false);
            var renderer = spriteObject.AddComponent<SpriteRenderer>();
            var profile = ScriptableObject.CreateInstance<StandardUnitTweenProfile>();
            var visual = root.AddComponent<UnitTweenVisual>();
            visual.ConfigureForPreview(spriteObject.transform, renderer, profile);
            using var cancellation = new CancellationTokenSource();

            Task task = visual.PlayActionAsync(
                UnitVisualAction.Cast,
                Vector3.right,
                () => { },
                cancellation.Token);
            yield return null;
            Assert.That(spriteObject.transform.Find("GlowOverlay"), Is.Null);

            cancellation.Cancel();
            yield return WaitForTask(task, 10d, "Complete async test operation");
            Assert.That(task.IsCanceled, Is.True);
            Assert.That(spriteObject.transform.Find("GlowOverlay"), Is.Null);
            Assert.That(spriteObject.transform.localPosition.sqrMagnitude, Is.LessThan(0.00001f));
            Assert.That((spriteObject.transform.localScale - Vector3.one).sqrMagnitude, Is.LessThan(0.00001f));

            Object.Destroy(root);
            Object.Destroy(profile);
        }

        [UnityTest]
        public System.Collections.IEnumerator MoveRequestDuringCast_LeavesNoCastResidue()
        {
            var root = new GameObject("InterruptedCastUnit");
            var spriteObject = new GameObject("Sprite");
            spriteObject.transform.SetParent(root.transform, false);
            var renderer = spriteObject.AddComponent<SpriteRenderer>();
            var profile = ScriptableObject.CreateInstance<StandardUnitTweenProfile>();
            var visual = root.AddComponent<UnitTweenVisual>();
            visual.ConfigureForPreview(spriteObject.transform, renderer, profile);

            Task task = visual.PlayActionAsync(
                UnitVisualAction.Cast,
                Vector3.right,
                () => { },
                CancellationToken.None);
            yield return null;
            Assert.That(spriteObject.transform.Find("GlowOverlay"), Is.Null);

            visual.BeginMoveStep(Vector3.right);
            yield return WaitForTask(task, 10d, "Complete async test operation");
            Assert.That(task.IsCompleted, Is.True);
            Assert.That(spriteObject.transform.Find("GlowOverlay"), Is.Null);

            visual.StopAllVisualTweens();
            Object.Destroy(root);
            Object.Destroy(profile);
        }

        [UnityTest]
        public System.Collections.IEnumerator CastWithoutVfxRecipe_StillReleases()
        {
            var root = new GameObject("MissingGlowMaterialUnit");
            var spriteObject = new GameObject("Sprite");
            spriteObject.transform.SetParent(root.transform, false);
            var renderer = spriteObject.AddComponent<SpriteRenderer>();
            var profile = ScriptableObject.CreateInstance<StandardUnitTweenProfile>();
            var visual = root.AddComponent<UnitTweenVisual>();
            visual.ConfigureForPreview(spriteObject.transform, renderer, profile);

            int releases = 0;
            Task task = visual.PlayActionAsync(
                UnitVisualAction.Cast,
                Vector3.right,
                () => releases++,
                CancellationToken.None);
            yield return WaitForTask(task, 10d, "Complete async test operation");

            Assert.That(task.IsFaulted, Is.False);
            Assert.That(releases, Is.EqualTo(1));
            Assert.That(spriteObject.transform.Find("GlowOverlay"), Is.Null);

            Object.Destroy(root);
            Object.Destroy(profile);
        }

        [TestCase(10f, 10f, 0.3f, 0.75f)]
        [TestCase(0.01f, 10f, 0.3f, 0.12f)]
        [TestCase(1f, 0f, 0.02f, 0.05f)]
        public void ProjectileDuration_UsesSpeedClampAndFallback(
            float distance,
            float speed,
            float fallback,
            float expected)
        {
            Assert.That(
                ProjectileVisualCoordinator.ResolveDuration(distance, speed, fallback),
                Is.EqualTo(expected).Within(0.0001f));
        }

        [UnityTest]
        public System.Collections.IEnumerator PoisonSpear_WaitsForVisibleProjectileBeforeApplyingEffect()
        {
            var config = GameAssetManager.Instance.Load<SkillGraphAbilityConfig>(
                "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/PoisonSpear_Graph_Ability.asset");
            var world = new SkillGraphTestWorld();
            using var runtimeScope = new BattleRuntimeScope();
            try
            {
                var casterCell = world.CreateSquareCell("TweenPoisonCaster", 0, 0);
                world.CreateSquareCell("TweenPoisonLine1", 1, 0);
                world.CreateSquareCell("TweenPoisonLine2", 2, 0);
                var targetCell = world.CreateSquareCell("TweenPoisonTarget", 3, 0);
                world.CreateSquareCell("TweenPoisonDrop", 4, 0);
                var caster = world.CreateUnit("TweenPoisonCasterUnit", 0, casterCell);
                var target = world.CreateUnit("TweenPoisonTargetUnit", 1, targetCell);
                caster.Mana = 20f;
                caster.MaxMana = 20f;
                world.SetTurnContext(world.PlayerOne, new[] { caster });
                world.SetTurnContext(world.PlayerTwo, new[] { target });

                var ability = new SkillGraphAbilityImpl(caster, config);
                Task<SkillGraphRuntimeTestResult> task = ability.ExecuteForTestAsync(
                    targetCell,
                    world.GridController,
                    runtimeScope);
                yield return null;

                Assert.That(task.IsCompleted, Is.False);
                Assert.That(target.BuffComponent.HasBuff(BuffEffectType.Poison), Is.False);
                var projectile = GameObject.Find("ProjectileVisual");
                Assert.That(projectile, Is.Not.Null);
                Assert.That(projectile.GetComponentsInChildren<ParticleSystem>(true), Is.Not.Empty);

                yield return WaitForTask(task, 10d, "Complete async test operation");
                Assert.That(task.IsFaulted, Is.False);
                Assert.That(task.Result.ExecutionState, Is.EqualTo(SkillGraphExecutionState.Completed));
                Assert.That(target.BuffComponent.HasBuff(BuffEffectType.Poison), Is.True);
                yield return null;
                Assert.That(GameObject.Find("ProjectileVisual"), Is.Null);
                var impact = GameObject.Find("PoisonSpearImpact_Vfx");
                Assert.That(impact, Is.Not.Null);
                Assert.That(impact.GetComponentsInChildren<ParticleSystem>(true), Is.Not.Empty);
                runtimeScope.Cancel();
                Task drainTask = runtimeScope.WhenIdleAsync();
                yield return WaitForTask(drainTask, 10d, "Drain Poison Spear impact");
                Assert.That(drainTask.IsFaulted, Is.False);
                Assert.That(GameObject.Find("PoisonSpearImpact_Vfx"), Is.Null);
            }
            finally
            {
                runtimeScope.Cancel();
                AmazonBattleState.For(world.GridController).Clear();
                world.Dispose();
            }
        }

        [UnityTest]
        public System.Collections.IEnumerator MissingProjectileProfile_PreservesDelayWithoutCreatingRenderer()
        {
            var world = new SkillGraphTestWorld();
            try
            {
                var casterCell = world.CreateSquareCell("InvisibleProjectileCaster", 0, 0);
                var targetCell = world.CreateSquareCell("InvisibleProjectileTarget", 1, 0);
                var caster = world.CreateUnit("InvisibleProjectileCasterUnit", 0, casterCell);
                var target = world.CreateUnit("InvisibleProjectileTargetUnit", 1, targetCell);

                Task task = ProjectileVisualCoordinator.PlayAsync(
                    caster,
                    target,
                    targetCell,
                    null,
                    0f,
                    0.08f,
                    CancellationToken.None);
                yield return null;

                Assert.That(task.IsCompleted, Is.False);
                Assert.That(GameObject.Find("ProjectileVisual"), Is.Null);
                yield return WaitForTask(task, 10d, "Complete async test operation");
                Assert.That(task.IsFaulted, Is.False);
            }
            finally
            {
                world.Dispose();
            }
        }

        [UnityTest]
        public System.Collections.IEnumerator CancelledProjectile_CleansTemporaryRenderer()
        {
            var profile = GameAssetManager.Instance.Load<ProjectileVisualProfile>(
                "Assets/Tactics/Arts/PureRun/Tween/Projectiles/AmazonPoisonSpear.asset");
            var world = new SkillGraphTestWorld();
            using var cancellation = new CancellationTokenSource();
            try
            {
                var casterCell = world.CreateSquareCell("CancelledProjectileCaster", 0, 0);
                var targetCell = world.CreateSquareCell("CancelledProjectileTarget", 4, 0);
                var caster = world.CreateUnit("CancelledProjectileCasterUnit", 0, casterCell);
                var target = world.CreateUnit("CancelledProjectileTargetUnit", 1, targetCell);

                Task task = ProjectileVisualCoordinator.PlayAsync(
                    caster,
                    target,
                    targetCell,
                    profile,
                    1f,
                    0.3f,
                    cancellation.Token);
                yield return null;
                var projectile = GameObject.Find("ProjectileVisual");
                Assert.That(projectile, Is.Not.Null);
                Assert.That(projectile.GetComponentsInChildren<ParticleSystem>(true), Is.Not.Empty);

                cancellation.Cancel();
                yield return WaitForTask(task, 10d, "Complete async test operation");
                Assert.That(task.IsCanceled, Is.True);
                yield return null;
                Assert.That(GameObject.Find("ProjectileVisual"), Is.Null);
                Assert.That(GameObject.Find("PoisonSpearImpact_Vfx"), Is.Null);
            }
            finally
            {
                world.Dispose();
            }
        }

        [UnityTest]
        public System.Collections.IEnumerator CancelledBoneSpear_CleansProjectileAndGhostTrail()
        {
            var profile = GameAssetManager.Instance.Load<ProjectileVisualProfile>(
                "Assets/Tactics/Arts/PureRun/Tween/Projectiles/BoneSpear.asset");
            var world = new SkillGraphTestWorld();
            using var cancellation = new CancellationTokenSource();
            try
            {
                var casterCell = world.CreateSquareCell("CancelledBoneSpearCaster", 0, 0);
                var targetCell = world.CreateSquareCell("CancelledBoneSpearTarget", 4, 0);
                var caster = world.CreateUnit("CancelledBoneSpearCasterUnit", 0, casterCell);
                var target = world.CreateUnit("CancelledBoneSpearTargetUnit", 1, targetCell);

                Task task = ProjectileVisualCoordinator.PlayAsync(
                    caster,
                    target,
                    targetCell,
                    profile,
                    1f,
                    0.3f,
                    cancellation.Token);
                yield return new WaitUntil(() => GameObject.Find("ProjectileGhostTrail") != null);
                yield return new WaitForSeconds(0.16f);

                var projectile = GameObject.Find("ProjectileVisual");
                Assert.That(projectile, Is.Not.Null);
                var projectileRenderer = projectile.GetComponent<SpriteRenderer>();
                AssertValidSpriteMaterial(projectileRenderer);
                Assert.That(projectileRenderer.sprite, Is.SameAs(profile.Sprite));
                Assert.That(projectileRenderer.color, Is.EqualTo(Color.white));

                SpriteRenderer[] ghostRenderers = Object
                    .FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
                    .Where(renderer => renderer.gameObject.name == "ProjectileGhostTrail")
                    .ToArray();
                Assert.That(ghostRenderers, Is.Not.Empty);
                Assert.That(ghostRenderers.Length, Is.LessThanOrEqualTo(2));
                foreach (SpriteRenderer ghostRenderer in ghostRenderers)
                {
                    AssertValidSpriteMaterial(ghostRenderer);
                    Assert.That(ghostRenderer.sprite, Is.SameAs(profile.Sprite));
                }

                cancellation.Cancel();
                yield return new WaitUntil(() => task.IsCompleted);
                Assert.That(task.IsCanceled, Is.True);
                yield return null;
                Assert.That(GameObject.Find("ProjectileVisual"), Is.Null);
                Assert.That(GameObject.Find("ProjectileGhostTrail"), Is.Null);
            }
            finally
            {
                world.Dispose();
            }
        }

        [UnityTest]
        public System.Collections.IEnumerator SpriteProjectile_WithExplicitMaterial_PreservesItForGhosts()
        {
            var sourceProfile = GameAssetManager.Instance.Load<ProjectileVisualProfile>(
                "Assets/Tactics/Arts/PureRun/Tween/Projectiles/BoneSpear.asset");
            var profile = Object.Instantiate(sourceProfile);
            Shader spriteShader = Shader.Find("Sprites/Default");
            Assert.That(spriteShader, Is.Not.Null);
            var explicitMaterial = new Material(spriteShader);
            FieldInfo materialField = typeof(ProjectileVisualProfile).GetField(
                "_material",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(materialField, Is.Not.Null);
            materialField.SetValue(profile, explicitMaterial);

            var world = new SkillGraphTestWorld();
            using var cancellation = new CancellationTokenSource();
            try
            {
                var casterCell = world.CreateSquareCell("ExplicitMaterialCaster", 0, 0);
                var targetCell = world.CreateSquareCell("ExplicitMaterialTarget", 4, 0);
                var caster = world.CreateUnit("ExplicitMaterialCasterUnit", 0, casterCell);
                var target = world.CreateUnit("ExplicitMaterialTargetUnit", 1, targetCell);

                Task task = ProjectileVisualCoordinator.PlayAsync(
                    caster,
                    target,
                    targetCell,
                    profile,
                    1f,
                    0.3f,
                    cancellation.Token);
                yield return new WaitUntil(() => GameObject.Find("ProjectileGhostTrail") != null);

                var projectileRenderer = GameObject.Find("ProjectileVisual")?.GetComponent<SpriteRenderer>();
                var ghostRenderer = GameObject.Find("ProjectileGhostTrail")?.GetComponent<SpriteRenderer>();
                Assert.That(projectileRenderer, Is.Not.Null);
                Assert.That(ghostRenderer, Is.Not.Null);
                Assert.That(projectileRenderer.sharedMaterial, Is.SameAs(explicitMaterial));
                Assert.That(ghostRenderer.sharedMaterial, Is.SameAs(explicitMaterial));

                cancellation.Cancel();
                yield return new WaitUntil(() => task.IsCompleted);
                Assert.That(task.IsCanceled, Is.True);
            }
            finally
            {
                cancellation.Cancel();
                Object.Destroy(explicitMaterial);
                Object.Destroy(profile);
                world.Dispose();
            }
        }

        [UnityTest]
        public System.Collections.IEnumerator ProjectileImpactCue_CompletesBeforeProjectileHitBlackboard()
        {
            var world = new SkillGraphTestWorld();
            var graph = ScriptableObject.CreateInstance<SkillGraphAsset>();
            graph.DisplayName = "Projectile VFX Ordering";
            graph.AddNode(SkillGraphNodeType.Start, Vector2.zero);
            try
            {
                var casterCell = world.CreateSquareCell("VfxOrderingCaster", 0, 0);
                var targetCell = world.CreateSquareCell("VfxOrderingTarget", 1, 0);
                var caster = world.CreateUnit("VfxOrderingCasterUnit", 0, casterCell);
                var target = world.CreateUnit("VfxOrderingTargetUnit", 1, targetCell);
                var context = new SkillExecutionContext(
                    caster,
                    graph,
                    SkillGraphRuntimeDefinition.FromAsset(graph),
                    world.GridController)
                {
                    PrimaryTarget = target,
                    TargetPoint = targetCell
                };
                var sink = new RecordingVfxSink(context);
                context.VfxSink = sink;
                var node = new ProjectileLaunchNodeRecord
                {
                    NodeId = "projectile-vfx-ordering",
                    Speed = 0f,
                    TravelTime = 0.05f,
                    RequiresLineOfSight = true
                };

                Task<SkillNodeExecutionResult> task = new ProjectileLaunchNodeExecutor().Execute(node, context);
                yield return new WaitUntil(() => sink.WasCalled);
                Assert.That(sink.ProjectileHitWasSetDuringCue, Is.False);
                Assert.That(context.GetBlackboard("ProjectileHit", false), Is.False);

                sink.Release();
                yield return new WaitUntil(() => task.IsCompleted);
                Assert.That(task.IsFaulted, Is.False);
                Assert.That(task.Result.IsSuccess, Is.True);
                Assert.That(context.GetBlackboard("ProjectileHit", false), Is.True);
            }
            finally
            {
                Object.Destroy(graph);
                world.Dispose();
            }
        }

        [UnityTest]
        public System.Collections.IEnumerator CastCharge_CreatesOneRingBehindSourceAndCleansOnCancellation()
        {
            var recipe = GameAssetManager.Instance.Load<SkillVfxRecipe>(
                "Assets/Tactics/Arts/PureRun/Tween/SkillVfx/Recipes/BoneSpearSkillVfxRecipe.asset");
            var world = new SkillGraphTestWorld();
            using var cancellation = new CancellationTokenSource();
            try
            {
                var casterCell = world.CreateSquareCell("CastChargeCaster", 0, 0);
                var caster = world.CreateUnit("CastChargeCasterUnit", 0, casterCell);
                var spriteObject = new GameObject("Sprite");
                spriteObject.transform.SetParent(((Component)caster).transform, false);
                var sourceRenderer = spriteObject.AddComponent<SpriteRenderer>();
                sourceRenderer.sortingOrder = 4;
                sourceRenderer.color = new Color(0.52f, 0.43f, 0.71f, 1f);
                Color originalColor = sourceRenderer.color;
                Material originalMaterial = sourceRenderer.sharedMaterial;
                var coordinator = new SkillVfxCoordinator(recipe, caster);
                Vector3 sourcePosition = SkillVfxPositionUtility.ResolveUnitCenter(caster);
                var cueContext = new SkillVfxCueContext(
                    1,
                    sourcePosition,
                    sourcePosition + Vector3.right,
                    Vector3.right,
                    primaryHitWorldPosition: sourcePosition);

                Task task = coordinator.PlayAsync(
                    SkillVfxCueKind.CastCharge,
                    cueContext,
                    cancellation.Token);
                yield return null;

                Assert.That(task.IsCompleted, Is.True, "Cast charge must never block gameplay timing.");
                MeshRenderer[] rings = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
                    .Where(renderer => renderer.gameObject.name == "SkillVfx_RadialRing")
                    .ToArray();
                Assert.That(rings, Has.Length.EqualTo(1));
                Assert.That(rings[0].sortingOrder, Is.EqualTo(2));
                Assert.That(rings[0].transform.position, Is.EqualTo(sourcePosition));
                Assert.That(sourceRenderer.color, Is.EqualTo(originalColor));
                Assert.That(sourceRenderer.sharedMaterial, Is.SameAs(originalMaterial));
                Assert.That(spriteObject.transform.Find("GlowOverlay"), Is.Null);

                cancellation.Cancel();
                yield return new WaitUntil(() => GameObject.Find("SkillVfx_RadialRing") == null);
            }
            finally
            {
                cancellation.Cancel();
                world.Dispose();
            }
        }

        [UnityTest]
        public System.Collections.IEnumerator CancelledSkillVfx_CleansAllTemporaryPrimitives()
        {
            var recipe = GameAssetManager.Instance.Load<SkillVfxRecipe>(
                "Assets/Tactics/Arts/PureRun/Tween/SkillVfx/Recipes/FireballSkillVfxRecipe.asset");
            var world = new SkillGraphTestWorld();
            using var cancellation = new CancellationTokenSource();
            try
            {
                var casterCell = world.CreateSquareCell("CancelledVfxCaster", 0, 0);
                var caster = world.CreateUnit("CancelledVfxCasterUnit", 0, casterCell);
                var coordinator = new SkillVfxCoordinator(recipe, caster);
                var cueContext = new SkillVfxCueContext(
                    1,
                    Vector3.zero,
                    Vector3.right,
                    Vector3.right,
                    primaryHitWorldPosition: Vector3.right);

                Task task = coordinator.PlayAsync(
                    SkillVfxCueKind.ProjectileImpact,
                    cueContext,
                    cancellation.Token);
                yield return null;
                Assert.That(GameObject.Find("SkillVfx_RadialCore"), Is.Not.Null);

                cancellation.Cancel();
                yield return new WaitUntil(() => task.IsCompleted);
                yield return null;
                Assert.That(GameObject.Find("SkillVfx_RadialCore"), Is.Null);
                Assert.That(GameObject.Find("SkillVfx_RadialRing"), Is.Null);
                Assert.That(GameObject.Find("SkillVfx_ParticleBurst"), Is.Null);
            }
            finally
            {
                world.Dispose();
            }
        }

        private sealed class RecordingVfxSink : ISkillVfxSink
        {
            private readonly SkillExecutionContext _context;
            private readonly TaskCompletionSource<bool> _release = new();

            public bool WasCalled { get; private set; }
            public bool ProjectileHitWasSetDuringCue { get; private set; }

            public RecordingVfxSink(SkillExecutionContext context)
            {
                _context = context;
            }

            public Task PlayAsync(
                SkillVfxCueKind cue,
                SkillVfxCueContext context,
                CancellationToken cancellationToken)
            {
                WasCalled = cue == SkillVfxCueKind.ProjectileImpact;
                ProjectileHitWasSetDuringCue = _context.GetBlackboard("ProjectileHit", false);
                return _release.Task;
            }

            public void Release()
            {
                _release.TrySetResult(true);
            }
        }

        private static void AssertValidSpriteMaterial(SpriteRenderer renderer)
        {
            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.sharedMaterial, Is.Not.Null);
            Assert.That(renderer.sharedMaterial.shader, Is.Not.Null);
            Assert.That(renderer.sharedMaterial.shader.isSupported, Is.True);
            Assert.That(renderer.sharedMaterial.shader.name, Is.Not.EqualTo("Hidden/InternalErrorShader"));
        }

        [UnityTest]
        public System.Collections.IEnumerator PausedProjectile_DoesNotCompleteUntilGameplayResumes()
        {
            var profile = GameAssetManager.Instance.Load<ProjectileVisualProfile>(
                "Assets/Tactics/Arts/PureRun/Tween/Projectiles/AmazonPoisonSpear.asset");
            var world = new SkillGraphTestWorld();
            using var cancellation = new CancellationTokenSource();
            try
            {
                var casterCell = world.CreateSquareCell("PausedProjectileCaster", 0, 0);
                var targetCell = world.CreateSquareCell("PausedProjectileTarget", 4, 0);
                var caster = world.CreateUnit("PausedProjectileCasterUnit", 0, casterCell);
                var target = world.CreateUnit("PausedProjectileTargetUnit", 1, targetCell);

                GameTimeService.Pause();
                Task task = ProjectileVisualCoordinator.PlayAsync(
                    caster, target, targetCell, profile, 1f, 0.3f, cancellation.Token);
                yield return new WaitForSecondsRealtime(0.2f);

                Assert.That(task.IsCompleted, Is.False,
                    "Paused scaled time must not advance projectile arrival.");
                Assert.That(GameObject.Find("ProjectileVisual"), Is.Not.Null);

                GameTimeService.Resume();
                yield return WaitForTask(task, 10d, "Complete async test operation");
                Assert.That(task.IsFaulted, Is.False);
            }
            finally
            {
                cancellation.Cancel();
                GameTimeService.ForceResume();
                world.Dispose();
            }
        }

        [UnityTest]
        public System.Collections.IEnumerator DoubleSpeedProjectile_UsesScaledGameplayTime()
        {
            yield return VerifyProjectilePlaybackSpeed(GamePlaybackSpeed.Double, 0.65d);
        }

        [UnityTest]
        public System.Collections.IEnumerator QuadrupleSpeedProjectile_UsesScaledGameplayTime()
        {
            yield return VerifyProjectilePlaybackSpeed(GamePlaybackSpeed.Quadruple, 0.4d);
        }

        [UnityTest]
        public System.Collections.IEnumerator DestroyedTargetDuringProjectile_DoesNotLeakOrFault()
        {
            var profile = GameAssetManager.Instance.Load<ProjectileVisualProfile>(
                "Assets/Tactics/Arts/PureRun/Tween/Projectiles/AmazonPoisonSpear.asset");
            var world = new SkillGraphTestWorld();
            using var cancellation = new CancellationTokenSource();
            try
            {
                var casterCell = world.CreateSquareCell("DestroyedTargetCaster", 0, 0);
                var targetCell = world.CreateSquareCell("DestroyedTargetCell", 4, 0);
                var caster = world.CreateUnit("DestroyedTargetCasterUnit", 0, casterCell);
                var target = world.CreateUnit("DestroyedTargetUnit", 1, targetCell);

                Task task = ProjectileVisualCoordinator.PlayAsync(
                    caster, target, targetCell, profile, 1f, 0.3f, cancellation.Token);
                yield return null;
                Assert.That(GameObject.Find("ProjectileVisual"), Is.Not.Null);

                Object.Destroy(target.gameObject);
                yield return null;
                yield return WaitForTask(task, 10d, "Complete async test operation");

                Assert.That(task.IsFaulted, Is.False,
                    "Projectile cleanup must use its resolved endpoint, not a destroyed target object.");
                yield return null;
                Assert.That(GameObject.Find("ProjectileVisual"), Is.Null);
            }
            finally
            {
                cancellation.Cancel();
                world.Dispose();
            }
        }

        private static System.Collections.IEnumerator VerifyProjectilePlaybackSpeed(
            GamePlaybackSpeed playbackSpeed,
            double maximumRealtimeSeconds)
        {
            var profile = GameAssetManager.Instance.Load<ProjectileVisualProfile>(
                "Assets/Tactics/Arts/PureRun/Tween/Projectiles/AmazonPoisonSpear.asset");
            var world = new SkillGraphTestWorld();
            using var cancellation = new CancellationTokenSource();
            using var runtimeScope = new BattleRuntimeScope();
            try
            {
                var casterCell = world.CreateSquareCell($"{playbackSpeed}ProjectileCaster", 0, 0);
                var targetCell = world.CreateSquareCell($"{playbackSpeed}ProjectileTarget", 4, 0);
                var caster = world.CreateUnit($"{playbackSpeed}ProjectileCasterUnit", 0, casterCell);
                var target = world.CreateUnit($"{playbackSpeed}ProjectileTargetUnit", 1, targetCell);

                GameTimeService.SetPlaybackSpeed(playbackSpeed);
                double startedAt = Time.realtimeSinceStartupAsDouble;
                Task task = ProjectileVisualCoordinator.PlayAsync(
                    caster,
                    target,
                    targetCell,
                    profile,
                    1f,
                    0.3f,
                    cancellation.Token,
                    runtimeScope);
                yield return WaitForTask(task, 10d, "Complete async test operation");
                double elapsed = Time.realtimeSinceStartupAsDouble - startedAt;

                Assert.That(task.IsFaulted, Is.False);
                Assert.That(elapsed, Is.LessThan(maximumRealtimeSeconds),
                    $"A max-duration projectile took {elapsed:F3}s at {playbackSpeed} speed.");

                runtimeScope.Cancel();
                Task drainTask = runtimeScope.WhenIdleAsync();
                yield return WaitForTask(drainTask, 10d, "Drain projectile impact");
                Assert.That(drainTask.IsFaulted, Is.False);
            }
            finally
            {
                cancellation.Cancel();
                runtimeScope.Cancel();
                GameTimeService.ForceResume();
                GameTimeService.SetPlaybackSpeed(GamePlaybackSpeed.Normal);
                world.Dispose();
            }
        }

        private static object GetTransientVfxCache()
        {
            return typeof(TransientVfxPool).GetField(
                    "Available",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                ?.GetValue(null);
        }

        private static Transform GetTransientVfxPoolRoot()
        {
            return typeof(TransientVfxPool).GetField(
                    "_poolRoot",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                ?.GetValue(null) as Transform;
        }

        private static void RecordCleanupFailure(
            ICollection<Exception> failures,
            string stepName,
            Action cleanup)
        {
            try
            {
                cleanup();
            }
            catch (Exception exception)
            {
                failures.Add(new InvalidOperationException(
                    $"{stepName} failed during fixture cleanup.",
                    exception));
            }
        }

        private static System.Collections.IEnumerator WaitForTask(
            Task task,
            double timeoutSeconds,
            string label)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + timeoutSeconds;
            int frameCount = 0;
            while (!task.IsCompleted && Time.realtimeSinceStartupAsDouble < deadline)
            {
                frameCount++;
                yield return null;
            }

            Assert.That(task.IsCompleted, Is.True,
                $"{label} timed out after {timeoutSeconds:F1}s and {frameCount} frames; status={task.Status}.");
        }

        private static void ResetTransientVfxPoolForTests()
        {
            var resetMethod = typeof(TransientVfxPool).GetMethod(
                "ResetStatics",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (resetMethod != null)
            {
                resetMethod.Invoke(null, null);
                return;
            }

            object cache = GetTransientVfxCache();
            cache?.GetType().GetMethod("Clear")?.Invoke(cache, null);
            Transform root = GetTransientVfxPoolRoot();
            if (root != null)
                Object.Destroy(root.gameObject);
            typeof(TransientVfxPool).GetField(
                    "_poolRoot",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(null, null);
        }
    }
}
