using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Interactables;
using Tactics.Common.Skills.Graph.Testing;
using Tactics.Common.Units;
using Tactics.Common.Units.Tween;
using Tactics.Common.Utilities;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;

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
            var sourceObject = new GameObject("SourceSprite");
            var sourceRenderer = sourceObject.AddComponent<SpriteRenderer>();
            sourceRenderer.sortingLayerID = renderer.sortingLayerID;
            sourceRenderer.sortingOrder = 19;

            corpse.InheritSortingFrom(sourceRenderer);
            corpse.ApplyVisual(deathSprite, material, sourceColor);

            Assert.That(renderer.sprite, Is.EqualTo(deathSprite));
            Assert.That(renderer.sharedMaterial, Is.EqualTo(material));
            Assert.That(renderer.color, Is.EqualTo(sourceColor));
            Assert.That(renderer.flipX, Is.False);
            Assert.That(renderer.sortingLayerID, Is.EqualTo(sourceRenderer.sortingLayerID));
            Assert.That(renderer.sortingOrder, Is.EqualTo(sourceRenderer.sortingOrder));
            Assert.That(spriteObject.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(spriteObject.transform.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(spriteObject.transform.localScale, Is.EqualTo(Vector3.one));

            Object.Destroy(material);
            Object.Destroy(deathSprite);
            Object.Destroy(texture);
            Object.Destroy(sourceObject);
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
            var sourceObject = new GameObject("SourceSprite");
            var sourceRenderer = sourceObject.AddComponent<SpriteRenderer>();
            sourceRenderer.sortingLayerID = renderer.sortingLayerID;
            sourceRenderer.sortingOrder = 13;

            corpse.InheritSortingFrom(sourceRenderer);
            corpse.ApplyVisual(null, null, Color.white);

            Assert.That(renderer.sprite, Is.Null);
            Assert.That(renderer.color, Is.EqualTo(genericColor));
            Assert.That(renderer.sortingLayerID, Is.EqualTo(sourceRenderer.sortingLayerID));
            Assert.That(renderer.sortingOrder, Is.EqualTo(sourceRenderer.sortingOrder));
            Assert.That(spriteObject.transform.localPosition, Is.EqualTo(new Vector3(0f, -0.15f, 0f)));
            Assert.That(Quaternion.Angle(spriteObject.transform.localRotation, Quaternion.Euler(0f, 0f, 90f)),
                Is.LessThan(0.001f));

            Object.Destroy(sourceObject);
            Object.Destroy(corpseGo);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LethalDamage_RegistersHiddenCorpseImmediately_ThenHandsOffPresentation()
        {
            ICell cell = FindCell(1, 1);
            var unitManager = new SkillGraphTestUnitManager();
            var controller = new GridController
            {
                UnitManager = unitManager,
                CorpsePrefabPath = null
            };

            var unitObject = new GameObject("DoomedUnit");
            var spriteObject = new GameObject("Sprite");
            spriteObject.transform.SetParent(unitObject.transform);
            var sourceRenderer = spriteObject.AddComponent<SpriteRenderer>();
            var shadowObject = new GameObject("Shadow");
            shadowObject.transform.SetParent(unitObject.transform);
            var shadowRenderer = shadowObject.AddComponent<SpriteRenderer>();
            var sourceMaterial = new Material(Shader.Find("Sprites/Default"));
            var sourceColor = new Color(0.35f, 0.7f, 0.55f, 1f);
            sourceRenderer.sharedMaterial = sourceMaterial;
            sourceRenderer.color = sourceColor;
            sourceRenderer.sortingOrder = 17;
            int expectedSortingLayerId = sourceRenderer.sortingLayerID;
            int expectedSortingOrder = sourceRenderer.sortingOrder;

            var texture = new Texture2D(8, 8);
            var deathSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 8f, 8f),
                new Vector2(0.25f, 0.75f),
                8f);
            var profile = ScriptableObject.CreateInstance<StandardUnitTweenProfile>();
            var directional = unitObject.AddComponent<FourDirectionSpriteVisual>();
            directional.Configure(sourceRenderer, deathSprite, deathSprite);
            typeof(FourDirectionSpriteVisual)
                .GetField("_deathSprite", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(directional, deathSprite);
            var tweenVisual = unitObject.AddComponent<UnitTweenVisual>();
            tweenVisual.ConfigureForPreview(spriteObject.transform, sourceRenderer, profile);
            var unit = unitObject.AddComponent<Unit>();
            unit.CurrentCell = cell;
            cell.CurrentUnits.Add(unit);
            cell.IsTaken = true;
            unitManager.AddUnit(unit);

            MethodInfo registerUnit = typeof(GridController).GetMethod(
                "RegisterUnit",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(registerUnit, Is.Not.Null);
            registerUnit.Invoke(controller, new object[] { unit });

            var attackerObject = new GameObject("Attacker");
            attackerObject.transform.position = Vector3.right;
            var attacker = attackerObject.AddComponent<Unit>();
            unit.ModifyHealth(-unit.Health, attacker);

            Corpse corpse = cell.CurrentInteractables.OfType<Corpse>().SingleOrDefault();
            Assert.That(corpse, Is.Not.Null);
            Assert.That(unitManager.GetUnits().Contains(unit), Is.False);
            Assert.That(corpse.transform.position,
                Is.EqualTo(cell.WorldPosition.ToVector3())
                    .Using(Vector3ComparerWithEqualsOperator.Instance));
            SpriteRenderer corpseRenderer = corpse.GetComponentsInChildren<SpriteRenderer>(true)
                .Single(value => value.gameObject.name == "Sprite");
            Assert.That(corpseRenderer.sprite, Is.SameAs(deathSprite));
            Assert.That(corpseRenderer.sharedMaterial, Is.SameAs(sourceMaterial));
            Assert.That(corpseRenderer.color, Is.EqualTo(sourceColor));
            Assert.That(corpseRenderer.flipX, Is.False);
            Assert.That(corpseRenderer.sortingLayerID, Is.EqualTo(expectedSortingLayerId));
            Assert.That(corpseRenderer.sortingOrder, Is.EqualTo(expectedSortingOrder));
            Assert.That(corpseRenderer.enabled, Is.False);
            Assert.That(sourceRenderer.enabled, Is.True);
            Assert.That(shadowRenderer.enabled, Is.True);
            Assert.That(corpse.OccupiesCell, Is.True);

            float handoffTime = profile.HitRecoilDuration +
                profile.LethalShakeDuration +
                profile.LethalCollapseDuration;
            yield return new WaitForSeconds(handoffTime + 0.02f);
            Assert.That(corpseRenderer.enabled, Is.True);
            Assert.That(corpseRenderer.sortingLayerID, Is.EqualTo(expectedSortingLayerId));
            Assert.That(corpseRenderer.sortingOrder, Is.EqualTo(expectedSortingOrder));
            Assert.That(unitObject == null, Is.True);

            yield return new WaitForSeconds(
                profile.CorpseDropDuration + profile.CorpseImpactDuration +
                profile.CorpseSettleDuration + 0.02f);
            Assert.That(corpseRenderer.transform.localPosition,
                Is.EqualTo(Vector3.zero).Using(Vector3ComparerWithEqualsOperator.Instance));
            Assert.That(corpseRenderer.transform.localScale,
                Is.EqualTo(Vector3.one).Using(Vector3ComparerWithEqualsOperator.Instance));

            corpse.Consume();
            Object.Destroy(profile);
            Object.Destroy(deathSprite);
            Object.Destroy(texture);
            Object.Destroy(sourceMaterial);
            Object.Destroy(attackerObject);
            yield return null;
        }

        private ICell FindCell(int x, int y)
        {
            return _cellManagerRoot.GetComponentsInChildren<Square>()
                .FirstOrDefault(c => c.GridCoordinates.x == x && c.GridCoordinates.y == y);
        }
    }
}
