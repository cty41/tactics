using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Tactics.Common.Battle;
using Tactics.Common.Skills.Graph.Testing;
using Tactics.Common.Units;
using Tactics.Common.Units.Tween;
using Tactics.Units;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tactics.Tests.PlayMode
{
    /// <summary>
    /// Verifies single-frame action poses independently from production artwork assets.
    /// </summary>
    public sealed class UnitActionPosePlayModeTests
    {
        private readonly List<Object> _objectsToDestroy = new();
        private Texture2D _texture;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _texture = Track(new Texture2D(8, 8));
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (Object value in _objectsToDestroy)
            {
                if (value != null)
                    Object.Destroy(value);
            }
            _objectsToDestroy.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator DirectionStateAndFallback_ResolveWithoutChangingRendererContract()
        {
            Sprite idleDownRight = CreateSprite(0, 0);
            Sprite idleUpLeft = CreateSprite(1, 0);
            Sprite unarmedDownRight = CreateSprite(2, 0);
            Sprite unarmedUpLeft = CreateSprite(3, 0);
            Sprite castDownRight = CreateSprite(4, 0);
            Sprite castUpLeft = CreateSprite(5, 0);
            Sprite unarmedCastDownRight = CreateSprite(6, 0);
            Sprite unarmedCastUpLeft = CreateSprite(7, 0);
            UnitPoseFamily castFamily = CreateFamily("Cast", UnitPoseExitPolicy.RecoveryStart);
            UnitPoseFamily missingFutureFamily = CreateFamily("ShotAttack", UnitPoseExitPolicy.Release);
            UnitActionPoseProfile profile = CreateProfile(castFamily: castFamily);
            profile.SetIdleSprites(UnitVisualState.Unarmed, unarmedDownRight, unarmedUpLeft);
            profile.SetPoseSprites(castFamily, UnitVisualState.Default, castDownRight, castUpLeft);

            var root = Track(new GameObject("PoseResolutionUnit"));
            var renderer = CreateRenderer(root);
            var material = Track(new Material(Shader.Find("Sprites/Default")));
            renderer.sharedMaterial = material;
            renderer.color = new Color(0.31f, 0.62f, 0.74f, 0.83f);
            renderer.sortingLayerID = 0;
            renderer.sortingOrder = 17;
            Color originalColor = renderer.color;
            int originalSortingOrder = renderer.sortingOrder;
            Vector3 originalScale = renderer.transform.localScale;
            var visual = root.AddComponent<FourDirectionSpriteVisual>();
            visual.Configure(renderer, idleDownRight, idleUpLeft, profile);

            visual.SetVisualState(UnitVisualState.Unarmed, FacingDirection.South);
            Assert.That(renderer.sprite, Is.SameAs(unarmedDownRight));
            Assert.That(visual.LastResolution, Is.EqualTo(UnitPoseResolution.StateIdle));

            visual.SetPose(castFamily, FacingDirection.North);
            Assert.That(renderer.sprite, Is.SameAs(castUpLeft));
            Assert.That(renderer.flipX, Is.False);
            Assert.That(visual.LastResolution, Is.EqualTo(UnitPoseResolution.DefaultPoseState));

            profile.SetPoseSprites(
                castFamily,
                UnitVisualState.Unarmed,
                unarmedCastDownRight,
                unarmedCastUpLeft);
            AssertFacing(visual, renderer, FacingDirection.East, unarmedCastUpLeft, true);
            AssertFacing(visual, renderer, FacingDirection.West, unarmedCastDownRight, true);
            AssertFacing(visual, renderer, FacingDirection.North, unarmedCastUpLeft, false);
            AssertFacing(visual, renderer, FacingDirection.South, unarmedCastDownRight, false);
            Assert.That(visual.LastResolution, Is.EqualTo(UnitPoseResolution.ExactPoseState));

            visual.SetPose(missingFutureFamily, FacingDirection.South);
            Assert.That(renderer.sprite, Is.SameAs(unarmedDownRight),
                "An explicit missing family must fall back to idle, not another action family.");
            Assert.That(visual.LastResolution, Is.EqualTo(UnitPoseResolution.StateIdle));
            Assert.That(renderer.sharedMaterial, Is.SameAs(material));
            Assert.That(renderer.color, Is.EqualTo(originalColor));
            Assert.That(renderer.sortingOrder, Is.EqualTo(originalSortingOrder));
            Assert.That(renderer.transform.localScale, Is.EqualTo(originalScale));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ThrownPose_ClearsBeforeRelease_AndCastPoseClearsAtRecovery()
        {
            Sprite idleDownRight = CreateSprite(0, 1);
            Sprite idleUpLeft = CreateSprite(1, 1);
            Sprite thrownDownRight = CreateSprite(2, 1);
            Sprite thrownUpLeft = CreateSprite(3, 1);
            Sprite castDownRight = CreateSprite(4, 1);
            Sprite castUpLeft = CreateSprite(5, 1);
            UnitPoseFamily thrownFamily = CreateFamily("ThrownAttack", UnitPoseExitPolicy.Release);
            UnitPoseFamily castFamily = CreateFamily("Cast", UnitPoseExitPolicy.RecoveryStart);
            UnitActionPoseProfile poseProfile = CreateProfile(
                rangedAttackFamily: thrownFamily,
                castFamily: castFamily);
            poseProfile.SetPoseSprites(
                thrownFamily, UnitVisualState.Default, thrownDownRight, thrownUpLeft);
            poseProfile.SetPoseSprites(castFamily, UnitVisualState.Default, castDownRight, castUpLeft);
            StandardUnitTweenProfile tweenProfile = Track(
                ScriptableObject.CreateInstance<StandardUnitTweenProfile>());

            var root = Track(new GameObject("PoseTimingUnit"));
            var renderer = CreateRenderer(root);
            var directional = root.AddComponent<FourDirectionSpriteVisual>();
            directional.Configure(renderer, idleDownRight, idleUpLeft, poseProfile);
            directional.TryApply(FacingDirection.South);
            var tween = root.AddComponent<UnitTweenVisual>();
            tween.ConfigureForPreview(renderer.transform, renderer, tweenProfile);

            int thrownReleases = 0;
            Sprite spriteAtThrownRelease = null;
            Task thrownTask = tween.PlayActionAsync(
                UnitVisualAction.Ranged,
                null,
                Vector3.right,
                null,
                () =>
                {
                    thrownReleases++;
                    spriteAtThrownRelease = renderer.sprite;
                },
                CancellationToken.None);
            Assert.That(renderer.sprite, Is.SameAs(thrownDownRight));
            yield return WaitForTask(thrownTask);
            Assert.That(thrownTask.IsFaulted, Is.False);
            Assert.That(thrownReleases, Is.EqualTo(1));
            Assert.That(spriteAtThrownRelease, Is.SameAs(idleDownRight));
            Assert.That(renderer.sprite, Is.SameAs(idleDownRight));

            Sprite spriteAtCastRelease = null;
            Task castTask = tween.PlayActionAsync(
                UnitVisualAction.Cast,
                null,
                Vector3.right,
                null,
                () => spriteAtCastRelease = renderer.sprite,
                CancellationToken.None);
            Assert.That(renderer.sprite, Is.SameAs(castDownRight));
            yield return WaitForTask(castTask);
            Assert.That(castTask.IsFaulted, Is.False);
            Assert.That(spriteAtCastRelease, Is.SameAs(castDownRight));
            Assert.That(renderer.sprite, Is.SameAs(idleDownRight));
        }

        [UnityTest]
        public IEnumerator MoveAndCancellationInterrupts_RestoreCurrentStateIdle()
        {
            Sprite idleDownRight = CreateSprite(0, 2);
            Sprite idleUpLeft = CreateSprite(1, 2);
            Sprite unarmedDownRight = CreateSprite(2, 2);
            Sprite unarmedUpLeft = CreateSprite(3, 2);
            Sprite castDownRight = CreateSprite(4, 2);
            Sprite castUpLeft = CreateSprite(5, 2);
            UnitPoseFamily castFamily = CreateFamily("Cast", UnitPoseExitPolicy.RecoveryStart);
            UnitActionPoseProfile poseProfile = CreateProfile(castFamily: castFamily);
            poseProfile.SetIdleSprites(UnitVisualState.Unarmed, unarmedDownRight, unarmedUpLeft);
            poseProfile.SetPoseSprites(castFamily, UnitVisualState.Default, castDownRight, castUpLeft);
            StandardUnitTweenProfile tweenProfile = Track(
                ScriptableObject.CreateInstance<StandardUnitTweenProfile>());

            var root = Track(new GameObject("InterruptedPoseUnit"));
            var renderer = CreateRenderer(root);
            var directional = root.AddComponent<FourDirectionSpriteVisual>();
            directional.Configure(renderer, idleDownRight, idleUpLeft, poseProfile);
            directional.SetVisualState(UnitVisualState.Unarmed, FacingDirection.South);
            var tween = root.AddComponent<UnitTweenVisual>();
            tween.ConfigureForPreview(renderer.transform, renderer, tweenProfile);

            Task movedTask = tween.PlayActionAsync(
                UnitVisualAction.Cast,
                Vector3.right,
                () => { },
                CancellationToken.None);
            Assert.That(renderer.sprite, Is.SameAs(castDownRight),
                "Missing Unarmed Cast falls back to the same family's Default artwork.");
            tween.BeginMoveStep(Vector3.right);
            yield return WaitForTask(movedTask);
            Assert.That(renderer.sprite, Is.SameAs(unarmedDownRight));
            tween.StopAllVisualTweens();

            using var cancellation = new CancellationTokenSource();
            Task cancelledTask = tween.PlayActionAsync(
                UnitVisualAction.Cast,
                Vector3.right,
                () => { },
                cancellation.Token);
            Assert.That(renderer.sprite, Is.SameAs(castDownRight));
            cancellation.Cancel();
            yield return WaitForTask(cancelledTask);
            Assert.That(cancelledTask.IsCanceled, Is.True);
            Assert.That(renderer.sprite, Is.SameAs(unarmedDownRight));
        }

        [UnityTest]
        public IEnumerator AmazonDropAndRecover_ReconcilesUnarmedAndHeldIdle()
        {
            using var world = new SkillGraphTestWorld();
            var ownerCell = world.CreateSquareCell("AmazonPoseOwner", 0, 0);
            var dropCell = world.CreateSquareCell("AmazonPoseDrop", 1, 0);
            var owner = world.CreateUnit("AmazonPoseUnit", 0, ownerCell);
            owner.Facing = FacingDirection.South;
            var ownerObject = ((Component)owner).gameObject;
            Sprite heldDownRight = CreateSprite(0, 3);
            Sprite heldUpLeft = CreateSprite(1, 3);
            Sprite unarmedDownRight = CreateSprite(2, 3);
            Sprite unarmedUpLeft = CreateSprite(3, 3);
            UnitActionPoseProfile poseProfile = CreateProfile();
            poseProfile.SetIdleSprites(UnitVisualState.Unarmed, unarmedDownRight, unarmedUpLeft);
            var renderer = CreateRenderer(ownerObject);
            var directional = ownerObject.AddComponent<FourDirectionSpriteVisual>();
            directional.Configure(renderer, heldDownRight, heldUpLeft, poseProfile);
            directional.TryApply(FacingDirection.South);

            AmazonBattleState state = AmazonBattleState.For(world.GridController);
            Assert.That(state.DropSpear(owner, dropCell), Is.True);
            Assert.That(directional.VisualState, Is.EqualTo(UnitVisualState.Unarmed));
            Assert.That(renderer.sprite, Is.SameAs(unarmedDownRight));

            Assert.That(state.RecoverSpear(owner), Is.True);
            Assert.That(directional.VisualState, Is.EqualTo(UnitVisualState.Default));
            Assert.That(renderer.sprite, Is.SameAs(heldDownRight));
            state.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator PureRunHunterAsset_ResolvesApprovedDirectionalPoseSlice()
        {
            const string prefabPath =
                "Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunHunter.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null, prefabPath);
            GameObject instance = Track(Object.Instantiate(prefab));
            FourDirectionSpriteVisual visual = instance.GetComponent<FourDirectionSpriteVisual>();
            SpriteRenderer renderer = visual.TargetRenderer;
            UnitActionPoseProfile profile = visual.ActionPoseProfile;
            Assert.That(profile, Is.Not.Null);

            UnitPoseFamily melee = profile.ResolveFamily(UnitVisualAction.Melee);
            UnitPoseFamily thrown = profile.ResolveFamily(UnitVisualAction.Ranged);
            UnitPoseFamily cast = profile.ResolveFamily(UnitVisualAction.Cast);
            UnitPoseFamily hit = profile.HitFamily;
            Sprite meleeDownRight = LoadSprite("doge_hunter_melee_attack_dr.png");
            Sprite meleeUpLeft = LoadSprite("doge_hunter_melee_attack_ul.png");
            Sprite castDownRight = LoadSprite("doge_hunter_cast_dr.png");
            Sprite castUpLeft = LoadSprite("doge_hunter_cast_ul.png");
            Sprite unarmedDownRight = LoadSprite("doge_hunter_idle_unarmed_dr.png");
            Sprite hitDownRight = LoadSprite("doge_hunter_hit_dr.png");
            Sprite hitUpLeft = LoadSprite("doge_hunter_hit_ul.png");
            Assert.That(hit, Is.Not.Null);

            visual.SetPose(melee, FacingDirection.South);
            Assert.That(renderer.sprite, Is.SameAs(meleeDownRight));
            Assert.That(renderer.flipX, Is.False);
            visual.TryApply(FacingDirection.West);
            Assert.That(renderer.sprite, Is.SameAs(meleeDownRight));
            Assert.That(renderer.flipX, Is.True);
            visual.TryApply(FacingDirection.North);
            Assert.That(renderer.sprite, Is.SameAs(meleeUpLeft));
            Assert.That(renderer.flipX, Is.False);
            visual.TryApply(FacingDirection.East);
            Assert.That(renderer.sprite, Is.SameAs(meleeUpLeft));
            Assert.That(renderer.flipX, Is.True);

            visual.SetPose(thrown, FacingDirection.South);
            Assert.That(renderer.sprite, Is.SameAs(meleeDownRight),
                "ThrownAttack intentionally reuses the approved melee pair for this trial slice.");
            visual.SetVisualState(UnitVisualState.Unarmed, FacingDirection.South);
            visual.ClearPose(FacingDirection.South);
            Assert.That(renderer.sprite, Is.SameAs(unarmedDownRight));
            visual.SetPose(cast, FacingDirection.South);
            Assert.That(renderer.sprite, Is.SameAs(castDownRight));
            visual.TryApply(FacingDirection.North);
            Assert.That(renderer.sprite, Is.SameAs(castUpLeft));

            visual.SetVisualState(UnitVisualState.Default, FacingDirection.South);
            visual.SetPose(hit, FacingDirection.South);
            Assert.That(renderer.sprite, Is.SameAs(hitDownRight));
            visual.TryApply(FacingDirection.West);
            Assert.That(renderer.sprite, Is.SameAs(hitDownRight));
            Assert.That(renderer.flipX, Is.True);
            visual.TryApply(FacingDirection.North);
            Assert.That(renderer.sprite, Is.SameAs(hitUpLeft));
            Assert.That(renderer.flipX, Is.False);
            visual.TryApply(FacingDirection.East);
            Assert.That(renderer.sprite, Is.SameAs(hitUpLeft));
            Assert.That(renderer.flipX, Is.True);
            visual.SetVisualState(UnitVisualState.Unarmed, FacingDirection.South);
            Assert.That(renderer.sprite, Is.SameAs(hitDownRight),
                "Held and Unarmed hit states intentionally share the spear-hidden pair.");
            visual.ClearPose(FacingDirection.South);
            Assert.That(renderer.sprite, Is.SameAs(unarmedDownRight));
            yield return null;
        }

        [UnityTest]
        public IEnumerator PureRunHunterHitPose_RestoresAuthoritativeIdle_AndExplicitStopClearsPose()
        {
            const string prefabPath =
                "Assets/Tactics/Arts/PureRun/Prefabs/Units/PureRunHunter.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null, prefabPath);
            GameObject instance = Track(Object.Instantiate(prefab));
            TilemapUnit unit = instance.GetComponent<TilemapUnit>();
            FourDirectionSpriteVisual visual = instance.GetComponent<FourDirectionSpriteVisual>();
            UnitTweenVisual tween = instance.GetComponent<UnitTweenVisual>();
            SpriteRenderer renderer = visual.TargetRenderer;
            Sprite heldIdle = renderer.sprite;
            Sprite unarmedIdle = LoadSprite("doge_hunter_idle_unarmed_dr.png");
            Sprite hitDownRight = LoadSprite("doge_hunter_hit_dr.png");

            unit.Facing = FacingDirection.South;
            visual.SetVisualState(UnitVisualState.Default, unit.Facing);
            tween.PlayHit(instance.transform.position - Vector3.right);
            Assert.That(renderer.sprite, Is.SameAs(hitDownRight));
            yield return new WaitForSeconds(0.16f);
            Assert.That(renderer.sprite, Is.SameAs(heldIdle),
                "Hit pose must clear when the recovery segment begins.");

            visual.SetVisualState(UnitVisualState.Unarmed, unit.Facing);
            tween.PlayHit(instance.transform.position - Vector3.right);
            Assert.That(renderer.sprite, Is.SameAs(hitDownRight));
            tween.PlayHit(instance.transform.position - Vector3.left);
            Assert.That(renderer.sprite, Is.SameAs(hitDownRight));
            tween.StopAllVisualTweens();
            Assert.That(renderer.sprite, Is.SameAs(unarmedIdle),
                "Explicit interruption must restore the current authoritative visual state.");
        }

        [Test]
        public void SharedPlans_ExposeReleaseAndPoseRestoreMarkers()
        {
            var target = Track(new GameObject("PosePlanTarget"));
            var profile = Track(ScriptableObject.CreateInstance<StandardUnitTweenProfile>());
            UnitTweenActionPlan melee = UnitTweenSequenceBuilder.BuildAction(
                UnitVisualAction.Melee,
                target.transform,
                profile,
                Vector3.zero,
                Quaternion.identity,
                Vector3.one,
                Vector3.right);
            UnitTweenActionPlan thrown = UnitTweenSequenceBuilder.BuildAction(
                UnitVisualAction.Ranged,
                target.transform,
                profile,
                Vector3.zero,
                Quaternion.identity,
                Vector3.one,
                Vector3.right,
                UnitPoseExitPolicy.Release);
            UnitTweenPosePlan hit = UnitTweenSequenceBuilder.BuildHitPlan(
                target.transform,
                profile,
                Vector3.zero,
                Quaternion.identity,
                Vector3.one,
                Vector3.right);

            Assert.That(melee.PoseRestoreTime, Is.GreaterThan(melee.ReleaseTime));
            Assert.That(thrown.PoseRestoreTime, Is.EqualTo(thrown.ReleaseTime).Within(0.0001f));
            Assert.That(hit.PoseRestoreTime, Is.GreaterThan(0f));
        }

        private UnitPoseFamily CreateFamily(string stableId, UnitPoseExitPolicy exitPolicy)
        {
            var family = Track(ScriptableObject.CreateInstance<UnitPoseFamily>());
            family.Configure(stableId, exitPolicy);
            return family;
        }

        private UnitActionPoseProfile CreateProfile(
            UnitPoseFamily meleeAttackFamily = null,
            UnitPoseFamily rangedAttackFamily = null,
            UnitPoseFamily castFamily = null,
            UnitPoseFamily hitFamily = null)
        {
            var profile = Track(ScriptableObject.CreateInstance<UnitActionPoseProfile>());
            profile.ConfigureDefaultFamilies(
                meleeAttackFamily,
                rangedAttackFamily,
                castFamily,
                hitFamily);
            return profile;
        }

        private SpriteRenderer CreateRenderer(GameObject root)
        {
            var spriteObject = new GameObject("Sprite");
            spriteObject.transform.SetParent(root.transform, false);
            return spriteObject.AddComponent<SpriteRenderer>();
        }

        private Sprite CreateSprite(int x, int y)
        {
            return Track(Sprite.Create(
                _texture,
                new Rect(x, y, 1, 1),
                new Vector2(0.5f, 0.5f)));
        }

        private static Sprite LoadSprite(string fileName)
        {
            const string root = "Assets/Tactics/Arts/PureRun/Textures/Actions/Amazon";
            return LoadSprite(root, fileName);
        }

        private static Sprite LoadSprite(string root, string fileName)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{root}/{fileName}");
            Assert.That(sprite, Is.Not.Null, fileName);
            return sprite;
        }

        private T Track<T>(T value) where T : Object
        {
            _objectsToDestroy.Add(value);
            return value;
        }

        private static IEnumerator WaitForTask(Task task)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!task.IsCompleted && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(task.IsCompleted, Is.True, $"Task timed out with status {task.Status}.");
        }

        private static void AssertFacing(
            FourDirectionSpriteVisual visual,
            SpriteRenderer renderer,
            FacingDirection facing,
            Sprite expectedSprite,
            bool expectedFlip)
        {
            visual.TryApply(facing);
            Assert.That(renderer.sprite, Is.SameAs(expectedSprite), facing.ToString());
            Assert.That(renderer.flipX, Is.EqualTo(expectedFlip), facing.ToString());
        }
    }
}
