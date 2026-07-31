using System.Collections;
using NUnit.Framework;
using Tactics.Common.Players;
using Tactics.Common.Skills.Graph.Testing;
using Tactics.Common.Units;
using Tactics.Units;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tactics.Tests.PlayMode
{
    public sealed class FourDirectionSpriteVisualPlayModeTests
    {
        private GameObject _root;
        private Texture2D _texture;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _root = new GameObject("FourDirectionVisualTest");
            _texture = new Texture2D(2, 2);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_root);
            Object.Destroy(_texture);
            yield return null;
        }

        [UnityTest]
        public IEnumerator UnitFacing_MapsTwoNativeSpritesAcrossFourDirections()
        {
            var unit = _root.AddComponent<TilemapUnit>();
            var spriteObject = new GameObject("Sprite");
            spriteObject.transform.SetParent(_root.transform);
            var renderer = spriteObject.AddComponent<SpriteRenderer>();
            var visual = _root.AddComponent<FourDirectionSpriteVisual>();
            var downRight = Sprite.Create(_texture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f));
            var upLeft = Sprite.Create(_texture, new Rect(1, 1, 1, 1), new Vector2(.5f, .5f));

            visual.Configure(renderer, downRight, upLeft);

            AssertFacing(unit, renderer, FacingDirection.East, upLeft, true);
            AssertFacing(unit, renderer, FacingDirection.West, downRight, true);
            AssertFacing(unit, renderer, FacingDirection.North, upLeft, false);
            AssertFacing(unit, renderer, FacingDirection.South, downRight, false);
            yield return null;
        }

        [Test]
        public void UnitInitialization_KeepsHumanEastAndAutomatedEnemyWest()
        {
            using var world = new SkillGraphTestWorld();
            world.PlayerManager.AddPlayer(new AIPlayer { PlayerNumber = 2 });

            var human = world.CreateUnit("Human", 0);
            var enemy = world.CreateUnit("Enemy", 2);

            Assert.That(human.Facing, Is.EqualTo(FacingDirection.East));
            Assert.That(enemy.Facing, Is.EqualTo(FacingDirection.West));
        }

        [UnityTest]
        public IEnumerator UnitFacing_WithoutComponent_KeepsLegacyEastWestFlip()
        {
            var unit = _root.AddComponent<TilemapUnit>();
            var spriteObject = new GameObject("Sprite");
            spriteObject.transform.SetParent(_root.transform);
            var renderer = spriteObject.AddComponent<SpriteRenderer>();

            unit.Facing = FacingDirection.East;
            Assert.That(renderer.flipX, Is.False);
            unit.Facing = FacingDirection.West;
            Assert.That(renderer.flipX, Is.True);
            yield return null;
        }

        private static void AssertFacing(TilemapUnit unit, SpriteRenderer renderer, FacingDirection direction, Sprite expectedSprite, bool expectedFlip)
        {
            unit.Facing = direction;
            Assert.That(renderer.sprite, Is.EqualTo(expectedSprite), direction.ToString());
            Assert.That(renderer.flipX, Is.EqualTo(expectedFlip), direction.ToString());
        }
    }
}
