using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Tactics.AssetPipeline;
using Tactics.Common.Battle;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Skills.Graph.Testing;
using Tactics.Common.Testing.Gameplay;
using Tactics.Common.Units.Abilities;
using Tactics.Common.Units.Buffs;
using Tactics.Common.Units.Tween;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tactics.Tests.PlayMode
{
    public class PureRunTweenPlayModeTests
    {
        private const string GlowOverlayMaterialPath =
            "Assets/Tactics/Arts/PureRun/Tween/PureRunGlowOverlay.mat";

        [UnitySetUp]
        public System.Collections.IEnumerator SetUp()
        {
            var task = TestGameAssetHelper.EnsureInitialized();
            yield return new WaitUntil(() => task.IsCompleted);
            Assert.That(task.Result, Is.Not.Null);
        }

        [UnityTearDown]
        public System.Collections.IEnumerator TearDown()
        {
            TestGameAssetHelper.Cleanup();
            yield return null;
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
            Assert.That(spriteObject.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(spriteObject.transform.localScale, Is.EqualTo(Vector3.one));

            Object.Destroy(root);
            Object.Destroy(profile);
        }

        [UnityTest]
        public System.Collections.IEnumerator CastOverlay_IsLazyAndResetsAfterCompletion()
        {
            var root = new GameObject("CastUnit");
            var spriteObject = new GameObject("Sprite");
            spriteObject.transform.SetParent(root.transform, false);
            var renderer = spriteObject.AddComponent<SpriteRenderer>();
            var texture = new Texture2D(2, 2);
            renderer.sprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), Vector2.one * 0.5f);
            var profile = ScriptableObject.CreateInstance<StandardUnitTweenProfile>();
            var material = GameAssetManager.Instance.Load<Material>(GlowOverlayMaterialPath);
            var visual = root.AddComponent<UnitTweenVisual>();
            visual.ConfigureForPreview(spriteObject.transform, renderer, profile, material);

            Assert.That(visual.GlowOverlay, Is.Null);
            Assert.That(spriteObject.transform.Find("GlowOverlay"), Is.Null);
            int releases = 0;
            Task task = visual.PlayActionAsync(
                UnitVisualAction.Cast,
                Vector3.right,
                () => releases++,
                CancellationToken.None);
            yield return null;

            var overlay = spriteObject.transform.Find("GlowOverlay")?.GetComponent<SpriteRenderer>();
            Assert.That(overlay, Is.Not.Null);
            Assert.That(visual.GlowOverlay, Is.SameAs(overlay));
            Assert.That(overlay.enabled, Is.True);
            Assert.That(overlay.sharedMaterial, Is.SameAs(material));
            yield return new WaitForSeconds(0.05f);
            Assert.That(overlay.color.r, Is.EqualTo(profile.CastGlowColor.r).Within(0.001f));
            Assert.That(overlay.color.g, Is.EqualTo(profile.CastGlowColor.g).Within(0.001f));
            Assert.That(overlay.color.b, Is.EqualTo(profile.CastGlowColor.b).Within(0.001f));
            Assert.That(overlay.color.a, Is.GreaterThan(0f));

            yield return new WaitUntil(() => task.IsCompleted);
            Assert.That(task.IsFaulted, Is.False);
            Assert.That(releases, Is.EqualTo(1));
            Assert.That(overlay.enabled, Is.False);
            Assert.That(overlay.color.a, Is.EqualTo(0f));

            Object.Destroy(root);
            Object.Destroy(profile);
            Object.Destroy(renderer.sprite);
            Object.Destroy(texture);
        }

        [UnityTest]
        public System.Collections.IEnumerator CancelledCast_DisablesOverlayAndCancelsTask()
        {
            var root = new GameObject("CancelledCastUnit");
            var spriteObject = new GameObject("Sprite");
            spriteObject.transform.SetParent(root.transform, false);
            var renderer = spriteObject.AddComponent<SpriteRenderer>();
            var profile = ScriptableObject.CreateInstance<StandardUnitTweenProfile>();
            var material = GameAssetManager.Instance.Load<Material>(GlowOverlayMaterialPath);
            var visual = root.AddComponent<UnitTweenVisual>();
            visual.ConfigureForPreview(spriteObject.transform, renderer, profile, material);
            using var cancellation = new CancellationTokenSource();

            Task task = visual.PlayActionAsync(
                UnitVisualAction.Cast,
                Vector3.right,
                () => { },
                cancellation.Token);
            yield return null;
            var overlay = spriteObject.transform.Find("GlowOverlay")?.GetComponent<SpriteRenderer>();
            Assert.That(overlay, Is.Not.Null);
            Assert.That(overlay.enabled, Is.True);

            cancellation.Cancel();
            yield return new WaitUntil(() => task.IsCompleted);
            Assert.That(task.IsCanceled, Is.True);
            Assert.That(overlay.enabled, Is.False);
            Assert.That(overlay.color.a, Is.EqualTo(0f));

            Object.Destroy(root);
            Object.Destroy(profile);
        }

        [UnityTest]
        public System.Collections.IEnumerator MoveRequestDuringCast_LeavesNoOverlayResidue()
        {
            var root = new GameObject("InterruptedCastUnit");
            var spriteObject = new GameObject("Sprite");
            spriteObject.transform.SetParent(root.transform, false);
            var renderer = spriteObject.AddComponent<SpriteRenderer>();
            var profile = ScriptableObject.CreateInstance<StandardUnitTweenProfile>();
            var material = GameAssetManager.Instance.Load<Material>(GlowOverlayMaterialPath);
            var visual = root.AddComponent<UnitTweenVisual>();
            visual.ConfigureForPreview(spriteObject.transform, renderer, profile, material);

            Task task = visual.PlayActionAsync(
                UnitVisualAction.Cast,
                Vector3.right,
                () => { },
                CancellationToken.None);
            yield return null;
            var overlay = spriteObject.transform.Find("GlowOverlay")?.GetComponent<SpriteRenderer>();
            Assert.That(overlay, Is.Not.Null);
            Assert.That(overlay.enabled, Is.True);

            visual.BeginMoveStep(Vector3.right);
            yield return new WaitUntil(() => task.IsCompleted);
            Assert.That(task.IsCompleted, Is.True);
            Assert.That(overlay.enabled, Is.False);
            Assert.That(overlay.color.a, Is.EqualTo(0f));

            visual.StopAllVisualTweens();
            Object.Destroy(root);
            Object.Destroy(profile);
        }

        [UnityTest]
        public System.Collections.IEnumerator MissingGlowMaterial_SkipsOverlayButStillReleases()
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
            yield return new WaitUntil(() => task.IsCompleted);

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
                    world.GridController);
                yield return null;

                Assert.That(task.IsCompleted, Is.False);
                Assert.That(target.BuffComponent.HasBuff(BuffEffectType.Poison), Is.False);
                Assert.That(GameObject.Find("ProjectileVisual"), Is.Not.Null);

                yield return new WaitUntil(() => task.IsCompleted);
                Assert.That(task.IsFaulted, Is.False);
                Assert.That(task.Result.ExecutionState, Is.EqualTo(SkillGraphExecutionState.Completed));
                Assert.That(target.BuffComponent.HasBuff(BuffEffectType.Poison), Is.True);
                yield return null;
                Assert.That(GameObject.Find("ProjectileVisual"), Is.Null);
            }
            finally
            {
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
                yield return new WaitUntil(() => task.IsCompleted);
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
                "Assets/Tactics/Arts/PureRun/Tween/Projectiles/AmazonSpear.asset");
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
                Assert.That(GameObject.Find("ProjectileVisual"), Is.Not.Null);

                cancellation.Cancel();
                yield return new WaitUntil(() => task.IsCompleted);
                Assert.That(task.IsCanceled, Is.True);
                yield return null;
                Assert.That(GameObject.Find("ProjectileVisual"), Is.Null);
            }
            finally
            {
                world.Dispose();
            }
        }
    }
}
