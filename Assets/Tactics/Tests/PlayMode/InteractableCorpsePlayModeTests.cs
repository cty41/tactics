using System.Collections;
using System.Linq;
using NUnit.Framework;
using Tactics.Common.Cells;
using Tactics.Common.Interactables;
using Tactics.Common.Units;
using Tactics.Common.Utilities;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tactics.Tests.PlayMode
{
    public class InteractableCorpsePlayModeTests
    {
        private GameObject _cellManagerRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;

            _cellManagerRoot = new GameObject("TestCellManager");
            var cellMgr = _cellManagerRoot.AddComponent<RegularCellManager>();
            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    var cellGo = new GameObject($"Cell_{x}_{y}");
                    cellGo.transform.SetParent(_cellManagerRoot.transform);
                    var square = cellGo.AddComponent<Square>();
                    square.GridCoordinates = new Vector2IntImpl(x, y);
                    square.WorldPosition = new Vector3Impl(x, y, 0);
                    square.MovementCost = 1f;
                }
            }

            cellMgr.Initialize(null);

            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_cellManagerRoot != null)
            {
                Object.DestroyImmediate(_cellManagerRoot);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Corpse_OccupiesCell_CanBeConsumed()
        {
            var cell = FindCell(0, 0);
            Assert.IsNotNull(cell, "Cell should exist.");

            var corpseGo = new GameObject("TestCorpse");
            var corpse = corpseGo.AddComponent<Corpse>();
            cell.AddInteractable(corpse);

            Assert.IsTrue(cell.IsTaken, "Cell should be occupied by corpse.");
            Assert.IsTrue(cell.CurrentInteractables.Any(i => i is Corpse), "Cell should have a Corpse interactable.");
            Assert.IsTrue(corpse.OccupiesCell, "Corpse should occupy cell.");

            corpse.Consume();

            Assert.IsFalse(cell.CurrentInteractables.Any(i => i is Corpse), "Corpse should be consumed.");
            Assert.IsFalse(cell.IsTaken, "Cell should be free after corpse is consumed.");
            yield break;
        }

        [UnityTest]
        public IEnumerator Corpse_MultipleOnSameCell_AllTracked()
        {
            var cell = FindCell(1, 0);
            Assert.IsNotNull(cell, "Cell should exist.");

            var corpseGo1 = new GameObject("TestCorpse1");
            var corpse1 = corpseGo1.AddComponent<Corpse>();
            var corpseGo2 = new GameObject("TestCorpse2");
            var corpse2 = corpseGo2.AddComponent<Corpse>();
            cell.AddInteractable(corpse1);
            cell.AddInteractable(corpse2);

            Assert.IsTrue(cell.CurrentInteractables.Count(i => i is Corpse) == 2, "Both corpses should be tracked.");
            Assert.IsTrue(cell.IsTaken, "Cell should be occupied.");

            corpse1.Consume();

            Assert.IsTrue(cell.CurrentInteractables.Count(i => i is Corpse) == 1, "One corpse should remain.");
            Assert.IsTrue(cell.IsTaken, "Cell should still be occupied.");
            yield break;
        }

        [UnityTest]
        public IEnumerator Corpse_AuthoredVisual_ReplacesGenericPresentation()
        {
            var corpseGo = new GameObject("TestCorpse");
            var corpse = corpseGo.AddComponent<Corpse>();
            var spriteObject = new GameObject("Sprite");
            spriteObject.transform.SetParent(corpseGo.transform);
            spriteObject.transform.localPosition = new Vector3(0f, -0.15f, 0f);
            spriteObject.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            spriteObject.transform.localScale = new Vector3(2f, 2f, 1f);

            var renderer = spriteObject.AddComponent<SpriteRenderer>();
            renderer.color = new Color(0.4f, 0.4f, 0.4f, 0.8f);
            renderer.flipX = true;

            var texture = new Texture2D(2, 2);
            var deathSprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.25f, 0.75f));
            var material = new Material(Shader.Find("Sprites/Default"));
            var sourceColor = new Color(0.8f, 0.7f, 0.6f, 1f);

            corpse.ApplyVisual(deathSprite, material, sourceColor);

            Assert.That(renderer.sprite, Is.EqualTo(deathSprite));
            Assert.That(renderer.sharedMaterial, Is.EqualTo(material));
            Assert.That(renderer.color, Is.EqualTo(sourceColor));
            Assert.That(renderer.flipX, Is.False);
            Assert.That(spriteObject.transform.localPosition,
                Is.EqualTo(new Vector3(-deathSprite.bounds.center.x, -deathSprite.bounds.center.y, 0f)));
            Assert.That(spriteObject.transform.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(spriteObject.transform.localScale, Is.EqualTo(Vector3.one));

            Object.Destroy(material);
            Object.Destroy(deathSprite);
            Object.Destroy(texture);
            Object.Destroy(corpseGo);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Corpse_MissingAuthoredVisual_KeepsGenericPresentation()
        {
            var corpseGo = new GameObject("TestCorpse");
            var corpse = corpseGo.AddComponent<Corpse>();
            var spriteObject = new GameObject("Sprite");
            spriteObject.transform.SetParent(corpseGo.transform);
            spriteObject.transform.localPosition = new Vector3(0f, -0.15f, 0f);
            spriteObject.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var renderer = spriteObject.AddComponent<SpriteRenderer>();
            var genericColor = new Color(0.4f, 0.4f, 0.4f, 0.8f);
            renderer.color = genericColor;

            corpse.ApplyVisual(null, null, Color.white);

            Assert.That(renderer.sprite, Is.Null);
            Assert.That(renderer.color, Is.EqualTo(genericColor));
            Assert.That(spriteObject.transform.localPosition, Is.EqualTo(new Vector3(0f, -0.15f, 0f)));
            Assert.That(Quaternion.Angle(spriteObject.transform.localRotation, Quaternion.Euler(0f, 0f, 90f)),
                Is.LessThan(0.001f));

            Object.Destroy(corpseGo);
            yield return null;
        }

        private ICell FindCell(int x, int y)
        {
            return _cellManagerRoot.GetComponentsInChildren<Square>()
                .FirstOrDefault(c => c.GridCoordinates.x == x && c.GridCoordinates.y == y);
        }
    }
}
