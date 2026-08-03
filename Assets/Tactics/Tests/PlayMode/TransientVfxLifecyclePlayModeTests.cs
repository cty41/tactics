using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tactics.Tests.PlayMode
{
    public class TransientVfxLifecyclePlayModeTests
    {
        private const string ProjectileProfilePath =
            "Assets/Tactics/Arts/PureRun/Tween/Projectiles/AmazonPoisonSpear.asset";

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GameTimeService.ForceResume();
            GameTimeService.SetPlaybackSpeed(GamePlaybackSpeed.Normal);
            ResetTransientVfxPoolForTests();

            Task<GameAssetManager> initializeTask = TestGameAssetHelper.EnsureInitialized();
            yield return WaitForTask(initializeTask, 10d, "Initialize GameAssetManager");
            Assert.That(initializeTask.IsFaulted, Is.False);
            Assert.That(initializeTask.Result, Is.Not.Null);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameTimeService.ForceResume();
            GameTimeService.SetPlaybackSpeed(GamePlaybackSpeed.Normal);
            ResetTransientVfxPoolForTests();
            TestGameAssetHelper.Cleanup();
            yield return null;
        }

        [UnityTest]
        public IEnumerator FireAndForgetVisualCue_CancelledBeforeTrack_PropagatesCancellationAndRecycles()
        {
            var graph = GameAssetManager.Instance.Load<SkillGraphAsset>(
                "Assets/Tactics/Battle/Abilities/SkillGraphs/Lightning_Graph.asset");
            var world = new SkillGraphTestWorld();
            using var scope = new BattleRuntimeScope();
            try
            {
                var casterCell = world.CreateSquareCell("CancelledCueCaster", 0, 0);
                var targetCell = world.CreateSquareCell("CancelledCueTarget", 1, 0);
                var caster = world.CreateUnit("CancelledCueCasterUnit", 0, casterCell);
                var target = world.CreateUnit("CancelledCueTargetUnit", 1, targetCell);
                var cue = CreateLegacyLightningCueFixture();
                var context = new SkillExecutionContext(
                    caster,
                    graph,
                    SkillGraphRuntimeDefinition.FromAsset(graph),
                    world.GridController)
                {
                    PrimaryTarget = target,
                    TargetPoint = targetCell,
                    RuntimeScope = scope
                };

                scope.Cancel();
                Task<SkillNodeExecutionResult> executionTask =
                    SkillNodeExecutorRegistry.Get(SkillGraphNodeType.PlayVisualCue).Execute(cue, context);

                yield return WaitForTask(executionTask, 2d, "Observe pre-cancelled visual cue");
                Assert.That(executionTask.IsCanceled, Is.True,
                    "A scope that cancels before Track must leave playback owned by the executor and propagate cancellation.");
                Assert.That(GameObject.Find("LightningImpact_Vfx"), Is.Null,
                    "Pre-cancelled playback must not leave a transient VFX instance in the scene.");
            }
            finally
            {
                scope.Cancel();
                world.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator ProjectileImpact_RuntimeScopeDrainReturnsItToPool()
        {
            var profile = GameAssetManager.Instance.Load<ProjectileVisualProfile>(ProjectileProfilePath);
            var world = new SkillGraphTestWorld();
            using var scope = new BattleRuntimeScope();
            try
            {
                var casterCell = world.CreateSquareCell("ScopedImpactCaster", 0, 0);
                var targetCell = world.CreateSquareCell("ScopedImpactTarget", 1, 0);
                var caster = world.CreateUnit("ScopedImpactCasterUnit", 0, casterCell);
                var target = world.CreateUnit("ScopedImpactTargetUnit", 1, targetCell);

                Task projectileTask = ProjectileVisualCoordinator.PlayAsync(
                    caster,
                    target,
                    targetCell,
                    profile,
                    100f,
                    0.05f,
                    scope.Token,
                    scope);

                yield return WaitForTask(projectileTask, 2d, "Complete projectile flight");
                Assert.That(projectileTask.IsFaulted, Is.False);
                Assert.That(projectileTask.IsCanceled, Is.False);
                Assert.That(GameObject.Find("PoisonSpearImpact_Vfx"), Is.Not.Null,
                    "The impact must still be active after the scoped projectile flight returns.");

                scope.Cancel();
                Task drainTask = scope.WhenIdleAsync();
                yield return WaitForTask(drainTask, 2d, "Drain scoped projectile impact");

                Assert.That(drainTask.IsFaulted, Is.False,
                    "Cancellation is normal teardown and must not become a scope fault.");
                Assert.That(GameObject.Find("PoisonSpearImpact_Vfx"), Is.Null,
                    "Scope drain must return the active impact before teardown completes.");
                Assert.That(GetCachedCount(profile.ImpactPrefab), Is.EqualTo(1),
                    "The canceled impact must be returned to the pool exactly once.");
            }
            finally
            {
                scope.Cancel();
                world.Dispose();
            }
        }

        private static IEnumerator WaitForTask(Task task, double timeoutSeconds, string label)
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

        private static int GetCachedCount(GameObject prefab)
        {
            object cache = typeof(TransientVfxPool).GetField(
                    "Available",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                ?.GetValue(null);
            object stack = cache?.GetType().GetProperty("Item")?.GetValue(
                cache,
                new object[] { prefab.GetInstanceID() });
            return stack == null
                ? 0
                : (int)stack.GetType().GetProperty("Count").GetValue(stack);
        }

        private static void ResetTransientVfxPoolForTests()
        {
            typeof(TransientVfxPool).GetMethod(
                    "ResetStatics",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                ?.Invoke(null, null);
        }
        [UnityTest]
        public System.Collections.IEnumerator SkillExecutionContext_InheritsBattleRuntimeScopeFromBattleController()
        {
            var graph = GameAssetManager.Instance.Load<SkillGraphAsset>(
                "Assets/Tactics/Battle/Abilities/SkillGraphs/Lightning_Graph.asset");
            var controllerObject = new GameObject("ScopedBattleController");
            controllerObject.SetActive(false);
            var controller = controllerObject.AddComponent<BattleController>();

            try
            {
                Task startTask = controller.StartBattleAsync();
                yield return WaitForTask(startTask, 10d, "Complete battle startup");
                Assert.That(startTask.IsFaulted, Is.False);
                Assert.That(controller.RuntimeScope, Is.Not.Null);

                var context = new SkillExecutionContext(
                    null,
                    graph,
                    SkillGraphRuntimeDefinition.FromAsset(graph),
                    controller);

                Assert.That(context.RuntimeScope, Is.SameAs(controller.RuntimeScope));
            }
            finally
            {
                controller.EndBattle(default);
                Object.Destroy(controllerObject);
            }

            yield return null;
        }

        [UnityTest]
        public System.Collections.IEnumerator FireAndForgetVisualCue_TracksRuntimeScopeAndCancelsCleanly()
        {
            var graph = GameAssetManager.Instance.Load<SkillGraphAsset>(
                "Assets/Tactics/Battle/Abilities/SkillGraphs/Lightning_Graph.asset");
            var world = new SkillGraphTestWorld();
            using var scope = new BattleRuntimeScope();
            try
            {
                var casterCell = world.CreateSquareCell("ScopedCueCaster", 0, 0);
                var targetCell = world.CreateSquareCell("ScopedCueTarget", 1, 0);
                var caster = world.CreateUnit("ScopedCueCasterUnit", 0, casterCell);
                var target = world.CreateUnit("ScopedCueTargetUnit", 1, targetCell);
                var cue = CreateLegacyLightningCueFixture();
                var context = new SkillExecutionContext(
                    caster,
                    graph,
                    SkillGraphRuntimeDefinition.FromAsset(graph),
                    world.GridController)
                {
                    PrimaryTarget = target,
                    TargetPoint = targetCell,
                    RuntimeScope = scope
                };

                Task<SkillNodeExecutionResult> task =
                    SkillNodeExecutorRegistry.Get(SkillGraphNodeType.PlayVisualCue).Execute(cue, context);
                yield return WaitForTask(task, 10d, "Complete async test operation");
                Assert.That(task.IsFaulted, Is.False);
                yield return null;
                Assert.That(GameObject.Find("LightningImpact_Vfx"), Is.Not.Null);

                Task idleTask = scope.WhenIdleAsync();
                Assert.That(idleTask.IsCompleted, Is.False,
                    "Fire-and-forget playback must remain tracked until it is recycled.");
                scope.Cancel();
                yield return WaitForTask(idleTask, 10d, "Drain runtime scope");
                yield return null;
                Assert.That(GameObject.Find("LightningImpact_Vfx"), Is.Null);
            }
            finally
            {
                scope.Cancel();
                world.Dispose();
            }
        }

        [UnityTest]
        public System.Collections.IEnumerator FireAndForgetVisualCue_WithoutRuntimeScope_AwaitsPlaybackOwner()
        {
            var graph = GameAssetManager.Instance.Load<SkillGraphAsset>(
                "Assets/Tactics/Battle/Abilities/SkillGraphs/Lightning_Graph.asset");
            var world = new SkillGraphTestWorld();
            try
            {
                var casterCell = world.CreateSquareCell("UnscopedCueCaster", 0, 0);
                var targetCell = world.CreateSquareCell("UnscopedCueTarget", 1, 0);
                var caster = world.CreateUnit("UnscopedCueCasterUnit", 0, casterCell);
                var target = world.CreateUnit("UnscopedCueTargetUnit", 1, targetCell);
                var cue = CreateLegacyLightningCueFixture();
                var context = new SkillExecutionContext(
                    caster,
                    graph,
                    SkillGraphRuntimeDefinition.FromAsset(graph),
                    world.GridController)
                {
                    PrimaryTarget = target,
                    TargetPoint = targetCell,
                    RuntimeScope = null
                };

                Task<SkillNodeExecutionResult> task =
                    SkillNodeExecutorRegistry.Get(SkillGraphNodeType.PlayVisualCue).Execute(cue, context);
                Assert.That(task.IsCompleted, Is.False,
                    "Without a runtime scope, the executor must own and await fire-and-forget playback.");

                yield return WaitForTask(task, 10d, "Complete async test operation");
                Assert.That(task.IsFaulted, Is.False);
                Assert.That(GameObject.Find("LightningImpact_Vfx"), Is.Null);
            }
            finally
            {
                world.Dispose();
                ResetTransientVfxPoolForTests();
            }
        }

        [TestCase(VisualCueAnchor.Caster)]
        [TestCase(VisualCueAnchor.PrimaryTarget)]
        [TestCase(VisualCueAnchor.TargetPoint)]
        public void VisualCueCoordinator_MissingAnchor_DoesNotRentAtWorldOrigin(
            VisualCueAnchor anchor)
        {
            var prefab = CreateParticlePrefab($"Missing{anchor}AnchorPrefab");
            var profile = ScriptableObject.CreateInstance<VisualCueProfile>();
            using var cancellation = new CancellationTokenSource();
            try
            {
                SetVisualCueProfileField(profile, "_prefab", prefab);
                SetVisualCueProfileField(profile, "_anchor", anchor);
                cancellation.Cancel();

                Task playback = VisualCueCoordinator.PlayAsync(
                    null, null, null, profile, cancellation.Token);

                Assert.That(playback.IsCompleted, Is.True);
                Assert.That(GetTransientVfxCacheKeyCount(), Is.Zero,
                    "A missing cue anchor must skip playback instead of renting at Vector3.zero.");
            }
            finally
            {
                ResetTransientVfxPoolForTests();
                Object.DestroyImmediate(profile);
                Object.DestroyImmediate(prefab);
            }
        }

        private static PlayVisualCueNodeRecord CreateLegacyLightningCueFixture()
        {
            var presentation = GameAssetManager.Instance.Load<BattlePresentationGraph>(
                "Assets/Tactics/Arts/PureRun/Presentation/Lightning_Presentation.asset");
            VisualCueProfile profile = presentation.Nodes
                .OfType<PresentationPrefabFxNodeRecord>()
                .Single()
                .Profile;
            return new PlayVisualCueNodeRecord
            {
                NodeId = "legacy-lightning-cue-fixture",
                Profile = profile
            };
        }

        [UnityTest]
        public System.Collections.IEnumerator ProjectileVisualCoordinator_ReusedPrefabDoesNotLeakSpriteAcrossProfiles()
        {
            var flightPrefab = CreateParticlePrefab("SharedFlightPrefab");
            var spriteProfile = ScriptableObject.CreateInstance<ProjectileVisualProfile>();
            var prefabOnlyProfile = ScriptableObject.CreateInstance<ProjectileVisualProfile>();
            var texture = new Texture2D(2, 2);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f));
            var world = new SkillGraphTestWorld();
            using var cancellation = new CancellationTokenSource();
            try
            {
                SetProjectileVisualProfileField(spriteProfile, "_flightPrefab", flightPrefab);
                SetProjectileVisualProfileField(spriteProfile, "_sprite", sprite);
                SetProjectileVisualProfileField(prefabOnlyProfile, "_flightPrefab", flightPrefab);
                var casterCell = world.CreateSquareCell("SpriteLeakCaster", 0, 0);
                var targetCell = world.CreateSquareCell("SpriteLeakTarget", 1, 0);
                var caster = world.CreateUnit("SpriteLeakCasterUnit", 0, casterCell);
                var target = world.CreateUnit("SpriteLeakTargetUnit", 1, targetCell);

                Task firstPlayback = ProjectileVisualCoordinator.PlayAsync(
                    caster, target, targetCell, spriteProfile, 100f, 0.01f, CancellationToken.None);
                yield return WaitForTask(firstPlayback, 10d, "Complete first VFX playback");
                Assert.That(firstPlayback.IsFaulted, Is.False);

                Task secondPlayback = ProjectileVisualCoordinator.PlayAsync(
                    caster, target, targetCell, prefabOnlyProfile, 1f, 0.75f, cancellation.Token);
                var reusedProjectile = GameObject.Find("ProjectileVisual");
                Assert.That(reusedProjectile, Is.Not.Null);
                var spriteRenderer = reusedProjectile.GetComponent<SpriteRenderer>();
                Assert.That(spriteRenderer == null || !spriteRenderer.enabled || spriteRenderer.sprite == null,
                    Is.True,
                    "A prefab-only profile must not render the previous profile's runtime sprite.");

                cancellation.Cancel();
                yield return WaitForTask(secondPlayback, 10d, "Complete second VFX playback");
            }
            finally
            {
                cancellation.Cancel();
                world.Dispose();
                ResetTransientVfxPoolForTests();
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(spriteProfile);
                Object.DestroyImmediate(prefabOnlyProfile);
                Object.DestroyImmediate(flightPrefab);
            }
        }

        [Test]
        public void TransientVfxPool_DuplicateReturn_DoesNotRentSameInstanceTwice()
        {
            var prefab = CreateParticlePrefab("DuplicateReturnPrefab");
            GameObject first = null;
            GameObject second = null;
            try
            {
                var rented = TransientVfxPool.Rent(prefab, Vector3.zero, Quaternion.identity, 1f, 0, 0);
                TransientVfxPool.Return(rented);
                TransientVfxPool.Return(rented);

                first = TransientVfxPool.Rent(prefab, Vector3.zero, Quaternion.identity, 1f, 0, 0);
                second = TransientVfxPool.Rent(prefab, Vector3.one, Quaternion.identity, 1f, 0, 0);

                Assert.That(second, Is.Not.SameAs(first),
                    "A duplicate Return must not enqueue the same live instance twice.");
            }
            finally
            {
                if (first != null)
                    Object.Destroy(first);
                if (second != null && !ReferenceEquals(second, first))
                    Object.Destroy(second);
                Object.Destroy(prefab);
                ResetTransientVfxPoolForTests();
            }
        }

        [Test]
        public void TransientVfxPool_Return_CapsEachPrefabAtEightInstances()
        {
            var prefab = CreateParticlePrefab("CapacityPrefab");
            var rented = new GameObject[12];
            try
            {
                for (int index = 0; index < rented.Length; index++)
                {
                    rented[index] = TransientVfxPool.Rent(
                        prefab, Vector3.zero, Quaternion.identity, 1f, 0, 0);
                }

                foreach (GameObject instance in rented)
                    TransientVfxPool.Return(instance);

                Assert.That(GetTransientVfxCachedCount(prefab), Is.EqualTo(8),
                    "The ordinary-skill concurrency budget allows at most eight cached instances per prefab.");
            }
            finally
            {
                Object.Destroy(prefab);
                ResetTransientVfxPoolForTests();
            }
        }

        [Test]
        public void TransientVfxPoolMember_CachesParticlesAndRenderers()
        {
            var prefab = CreateParticlePrefab("ComponentCachePrefab");
            try
            {
                var instance = TransientVfxPool.Rent(
                    prefab, Vector3.zero, Quaternion.identity, 1f, 0, 0);
                var marker = instance.GetComponent<TransientVfxPoolMember>();
                Assert.That(marker, Is.Not.Null);

                var particleSystems = typeof(TransientVfxPoolMember).GetField(
                    "_particleSystems",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?.GetValue(marker) as ParticleSystem[];
                var renderers = typeof(TransientVfxPoolMember).GetField(
                    "_renderers",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?.GetValue(marker) as Renderer[];

                Assert.That(particleSystems, Is.Not.Null.And.Length.EqualTo(1));
                Assert.That(renderers, Is.Not.Null.And.Length.EqualTo(1));
                TransientVfxPool.Return(instance);
            }
            finally
            {
                Object.Destroy(prefab);
                ResetTransientVfxPoolForTests();
            }
        }

        [UnityTest]
        public System.Collections.IEnumerator TransientVfxPool_HasSubsystemRegistrationReset()
        {
            var prefab = CreateParticlePrefab("SubsystemResetPrefab");
            GameObject replacement = null;
            try
            {
                var instance = TransientVfxPool.Rent(
                    prefab, Vector3.zero, Quaternion.identity, 1f, 0, 0);
                TransientVfxPool.Return(instance);
                Assert.That(GetTransientVfxCachedCount(prefab), Is.EqualTo(1));
                Transform oldRoot = GetTransientVfxPoolRoot();
                Assert.That(oldRoot, Is.Not.Null);

                var resetMethod = typeof(TransientVfxPool).GetMethod(
                    "ResetStatics",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                Assert.That(resetMethod, Is.Not.Null,
                    "Disable-Domain-Reload play sessions require a SubsystemRegistration reset.");
                var attributes = resetMethod.GetCustomAttributes(
                    typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                Assert.That(attributes, Has.Length.EqualTo(1));
                Assert.That(((RuntimeInitializeOnLoadMethodAttribute)attributes[0]).loadType,
                    Is.EqualTo(RuntimeInitializeLoadType.SubsystemRegistration));

                resetMethod.Invoke(null, null);

                Assert.That(GetTransientVfxCacheKeyCount(), Is.Zero,
                    "SubsystemRegistration must clear all stale prefab instance IDs.");
                Assert.That(GetTransientVfxPoolRoot(), Is.Null,
                    "SubsystemRegistration must release the persistent pool root.");

                yield return null;
                Assert.That(oldRoot == null, Is.True,
                    "The previous persistent root must be destroyed before the next play session uses the pool.");
                Assert.That(instance == null, Is.True,
                    "Cached children from the previous session must be destroyed with their pool root.");

                replacement = TransientVfxPool.Rent(
                    prefab, Vector3.zero, Quaternion.identity, 1f, 0, 0);
                Assert.That(ReferenceEquals(replacement, instance), Is.False,
                    "A new play session must not reuse a member from the reset session.");
            }
            finally
            {
                if (replacement != null)
                    TransientVfxPool.Return(replacement);
                Object.Destroy(prefab);
                ResetTransientVfxPoolForTests();
            }
        }

        [Test]
        public void TransientVfxPool_WarmedRentReturn_AllocatesZeroManagedBytes()
        {
            var prefab = CreateParticlePrefab("AllocationPrefab");
            try
            {
                for (int index = 0; index < 8; index++)
                {
                    var warmInstance = TransientVfxPool.Rent(
                        prefab, Vector3.zero, Quaternion.identity, 1f, 0, 0);
                    TransientVfxPool.Return(warmInstance);
                }

                System.GC.Collect();
                System.GC.WaitForPendingFinalizers();
                long before = System.GC.GetAllocatedBytesForCurrentThread();
                for (int index = 0; index < 64; index++)
                {
                    var instance = TransientVfxPool.Rent(
                        prefab, Vector3.zero, Quaternion.identity, 1f, 0, 0);
                    TransientVfxPool.Return(instance);
                }
                long allocatedBytes = System.GC.GetAllocatedBytesForCurrentThread() - before;

                Assert.That(allocatedBytes, Is.Zero,
                    $"A warmed Rent/Return loop allocated {allocatedBytes} managed bytes.");
            }
            finally
            {
                Object.Destroy(prefab);
                ResetTransientVfxPoolForTests();
            }
        }

        private static GameObject CreateParticlePrefab(string name)
        {
            var prefab = new GameObject(name);
            prefab.AddComponent<ParticleSystem>();
            prefab.SetActive(false);
            return prefab;
        }

        private static void SetVisualCueProfileField<T>(
            VisualCueProfile profile,
            string fieldName,
            T value)
        {
            var field = typeof(VisualCueProfile).GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(profile, value);
        }

        private static void SetProjectileVisualProfileField<T>(
            ProjectileVisualProfile profile,
            string fieldName,
            T value)
        {
            var field = typeof(ProjectileVisualProfile).GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(profile, value);
        }

        private static int GetTransientVfxCachedCount(GameObject prefab)
        {
            object cache = GetTransientVfxCache();
            object stack = cache.GetType().GetProperty("Item")?.GetValue(
                cache, new object[] { prefab.GetInstanceID() });
            return stack == null
                ? 0
                : (int)stack.GetType().GetProperty("Count").GetValue(stack);
        }

        private static int GetTransientVfxCacheKeyCount()
        {
            object cache = GetTransientVfxCache();
            return (int)cache.GetType().GetProperty("Count").GetValue(cache);
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
    }
}
