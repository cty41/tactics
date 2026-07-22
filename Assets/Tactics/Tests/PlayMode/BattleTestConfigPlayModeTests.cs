using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Tactics.Common.Battle;
using Tactics.Common.Cells;
using Tactics.Common.Interactables;
using Tactics.Common.Units;
using Tactics.Common.Units.Classes;
using Tactics.Common.Utilities;
using Tactics.Units;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tactics.Tests.PlayMode
{
    public class BattleTestConfigPlayModeTests
    {
        private GameObject _battleRoot;
        private GameObject _cellManagerRoot;
        private GameObject _unitContainer;

        private GameObject CreateUnitTemplate()
        {
            var go = new GameObject("UnitTemplate");
            go.AddComponent<TilemapUnit>();
            return go;
        }
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;

            // Create cell grid
            _cellManagerRoot = new GameObject("TestCellManager");
            var cellMgr = _cellManagerRoot.AddComponent<RegularCellManager>();
            for (int x = 0; x < 4; x++)
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
            // Initialize cell cache so GetCellAt works
            cellMgr.Initialize(null);

            // Create BattleController
            _battleRoot = new GameObject("TestBattleController");
            var bc = _battleRoot.AddComponent<BattleController>();

            var cellMgrField = typeof(BattleController).GetField("_cellManager", BindingFlags.Instance | BindingFlags.NonPublic);
            cellMgrField?.SetValue(bc, cellMgr);

            var startFlag = typeof(BattleController).GetField("_startImmediatelly", BindingFlags.Instance | BindingFlags.NonPublic);
            startFlag?.SetValue(bc, false);

            var humanField = typeof(BattleController).GetField("_humanPlayerNumber", BindingFlags.Instance | BindingFlags.NonPublic);
            humanField?.SetValue(bc, 1);

            // Create unit container
            _unitContainer = new GameObject("UnitContainer");
            _unitContainer.transform.SetParent(_battleRoot.transform);
            var containerField = typeof(BattleController).GetField("_unitContainer", BindingFlags.Instance | BindingFlags.NonPublic);
            containerField?.SetValue(bc, _unitContainer.transform);

            // Call Awake to initialize
            var awake = typeof(BattleController).GetMethod("Awake", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            awake?.Invoke(bc, null);

            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return null;
            yield return null;

            // Destroy any leftover TilemapUnits from tests
            foreach (var unit in Object.FindObjectsByType<TilemapUnit>(FindObjectsSortMode.None))
            {
                if (unit != null && unit.gameObject != null)
                    Object.DestroyImmediate(unit.gameObject);
            }

            if (_cellManagerRoot != null) Object.DestroyImmediate(_cellManagerRoot);
            if (_battleRoot != null) Object.DestroyImmediate(_battleRoot);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TestMode_SpawnsPartyUnits_AtSpawnCells()
        {
            // Arrange
            var template = CreateUnitTemplate();

            var partyConfig = ScriptableObject.CreateInstance<BattlePartyTestConfig>();
            var slots = new List<PartyTestSlot>
            {
                CreatePartySlot(new Vector2Int(0, 0), template, "Warrior"),
                CreatePartySlot(new Vector2Int(1, 0), template, "Mage")
            };
            SetPrivateField(partyConfig, "_slots", slots);

            var bc = _battleRoot.GetComponent<BattleController>();
            SetPrivateField(bc, "_useTestSetup", true);
            SetPrivateField(bc, "_testPartyConfig", partyConfig);

            // Act
            CallPrivate(bc, "SpawnTestPartyUnits");
            yield return null;

            // Assert
            int playerUnits = CountUnitsWithPlayerNumber(1);
            Assert.That(playerUnits, Is.EqualTo(2), $"Expected 2 player units, found {playerUnits}.");

            Object.DestroyImmediate(template);
        }

        [UnityTest]
        public IEnumerator TestMode_SpawnsEncounterUnits_AtSpawnCells()
        {
            // Arrange
            var template = CreateUnitTemplate();
            // Set PlayerNumber=1 so encounter cleanup doesn't destroy it
            template.GetComponent<TilemapUnit>().PlayerNumber = 1;

            var encounterConfig = ScriptableObject.CreateInstance<BattleEncounterTestConfig>();
            var slots = new List<EncounterTestSlot>
            {
                CreateEncounterSlot(new Vector2Int(3, 0), template, "Goblin", 2)
            };
            SetPrivateField(encounterConfig, "_slots", slots);

            var bc = _battleRoot.GetComponent<BattleController>();
            SetPrivateField(bc, "_useTestSetup", true);
            SetPrivateField(bc, "_testEncounterConfig", encounterConfig);

            // Act
            CallPrivate(bc, "SpawnEncounterUnits");
            yield return null;

            // Assert
            int enemyUnits = CountUnitsWithPlayerNumber(2);
            Assert.That(enemyUnits, Is.EqualTo(1), $"Expected 1 enemy unit, found {enemyUnits}.");

            Object.DestroyImmediate(template);
        }

        [UnityTest]
        public IEnumerator MissingSpawnCell_LogsError()
        {
            // Arrange: config references nonexistent spawn cell
            var template = CreateUnitTemplate();
            var partyConfig = ScriptableObject.CreateInstance<BattlePartyTestConfig>();
            var slots = new List<PartyTestSlot>
            {
                CreatePartySlot(new Vector2Int(99, 99), template, "Ghost")
            };
            SetPrivateField(partyConfig, "_slots", slots);

            var bc = _battleRoot.GetComponent<BattleController>();
            SetPrivateField(bc, "_useTestSetup", true);
            SetPrivateField(bc, "_testPartyConfig", partyConfig);

            // Act + Assert expected error
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("TestPartySlot\\[0\\] SpawnCell '\\(99, 99\\)' did not map to a grid cell"));
            CallPrivate(bc, "SpawnTestPartyUnits");
            yield return null;

            int playerUnits = CountUnitsWithPlayerNumber(1);
            Assert.That(playerUnits, Is.EqualTo(0), "Should not spawn when spawn cell is missing.");

            Object.DestroyImmediate(template);
        }

        [UnityTest]
        public IEnumerator NullPrefab_SkipsSlot()
        {
            // Arrange
            var template = CreateUnitTemplate();

            var partyConfig = ScriptableObject.CreateInstance<BattlePartyTestConfig>();
            var slots = new List<PartyTestSlot>
            {
                CreatePartySlot(new Vector2Int(0, 0), null, "Ghost"),
                CreatePartySlot(new Vector2Int(0, 0), template, "Valid")
            };
            SetPrivateField(partyConfig, "_slots", slots);

            var bc = _battleRoot.GetComponent<BattleController>();
            SetPrivateField(bc, "_useTestSetup", true);
            SetPrivateField(bc, "_testPartyConfig", partyConfig);

            // Act + Assert expected error for null prefab
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("TestPartySlot\\[0\\] has null UnitPrefab"));
            CallPrivate(bc, "SpawnTestPartyUnits");
            yield return null;

            int playerUnits = CountUnitsWithPlayerNumber(1);
            Assert.That(playerUnits, Is.EqualTo(1), "Null prefab slot should be skipped, valid slot should spawn.");

            Object.DestroyImmediate(template);
        }

        [UnityTest]
        public IEnumerator TestMode_Disabled_UsesProductionPath()
        {
            // Arrange: test mode disabled, no test configs
            var bc = _battleRoot.GetComponent<BattleController>();
            SetPrivateField(bc, "_useTestSetup", false);
            SetPrivateField(bc, "_testPartyConfig", null);

            // Act: production path runs (will try to load from save data and fail to find prefabs)
            // Multiple error logs expected - suppress them all
            LogAssert.ignoreFailingMessages = true;
            CallPrivate(bc, "SpawnPartyUnits");
            yield return null;

            // Assert: method completed without crash
            Assert.Pass("Production path completed without crash.");
        }

        [UnityTest]
        public IEnumerator TestMode_SpawnsCorpseUnits_WithCorpseInteractable()
        {
            // Arrange: create a corpse prefab (inactive to avoid FindObjectsByType picking it up)
            var corpseGo = new GameObject("CorpsePrefab");
            corpseGo.AddComponent<Corpse>();
            corpseGo.AddComponent<BoxCollider2D>();
            corpseGo.SetActive(false);

            var encounterConfig = ScriptableObject.CreateInstance<BattleEncounterTestConfig>();
            var corpseSlots = new List<CorpseTestSlot>
            {
                CreateCorpseSlot(new Vector2Int(2, 1), corpseGo, "TestCorpse", 2)
            };
            SetPrivateField(encounterConfig, "_corpseSlots", corpseSlots);
            SetPrivateField(encounterConfig, "_slots", new List<EncounterTestSlot>());

            var bc = _battleRoot.GetComponent<BattleController>();
            SetPrivateField(bc, "_useTestSetup", true);
            SetPrivateField(bc, "_testEncounterConfig", encounterConfig);

            // Act
            CallPrivate(bc, "SpawnEncounterUnits");
            yield return null;

            // Assert: Corpse component exists in scene (only active ones)
            var corpses = Object.FindObjectsByType<Corpse>(FindObjectsSortMode.None)
                .Where(c => c.gameObject.activeInHierarchy).ToList();
            Assert.That(corpses.Count, Is.EqualTo(1), $"Expected 1 Corpse, found {corpses.Count}.");

            // Assert: Corpse is on a cell
            var corpse = corpses[0];
            Assert.IsNotNull(corpse.CurrentCell, "Corpse should have a CurrentCell.");
            var cell = corpse.CurrentCell;
            Assert.IsTrue(cell.CurrentInteractables.Any(i => i is Corpse && !i.IsDestroyed), "Cell should have a Corpse interactable.");
            Assert.IsTrue(cell.IsTaken, "Cell should be taken.");

            Object.DestroyImmediate(corpseGo);
        }

        [UnityTest]
        public IEnumerator TestMode_CorpseUnit_IsConsumable()
        {
            // Arrange: create a corpse prefab (inactive to avoid FindObjectsByType picking it up)
            var corpseGo = new GameObject("CorpsePrefab");
            corpseGo.AddComponent<Corpse>();
            corpseGo.AddComponent<BoxCollider2D>();
            corpseGo.SetActive(false);

            var encounterConfig = ScriptableObject.CreateInstance<BattleEncounterTestConfig>();
            var corpseSlots = new List<CorpseTestSlot>
            {
                CreateCorpseSlot(new Vector2Int(2, 1), corpseGo, "TestCorpse", 2)
            };
            SetPrivateField(encounterConfig, "_corpseSlots", corpseSlots);
            SetPrivateField(encounterConfig, "_slots", new List<EncounterTestSlot>());

            var bc = _battleRoot.GetComponent<BattleController>();
            SetPrivateField(bc, "_useTestSetup", true);
            SetPrivateField(bc, "_testEncounterConfig", encounterConfig);

            CallPrivate(bc, "SpawnEncounterUnits");
            yield return null;

            // Find the corpse (only active ones)
            var corpse = Object.FindObjectsByType<Corpse>(FindObjectsSortMode.None)
                .First(c => c.gameObject.activeInHierarchy);
            Assert.IsNotNull(corpse, "Corpse should exist.");
            var cell = corpse.CurrentCell;
            Assert.IsNotNull(cell, "Corpse should have a CurrentCell.");

            // Act: consume the corpse
            corpse.Consume();

            // Assert: corpse consumed, cell freed
            Assert.IsTrue(corpse.IsDestroyed, "Corpse should be destroyed after consume.");
            Assert.IsFalse(cell.CurrentInteractables.Any(i => i is Corpse && !i.IsDestroyed),
                "No living Corpse should remain on cell after consume.");
            Assert.IsFalse(cell.IsTaken, "Cell should be free after corpse is consumed.");

            Object.DestroyImmediate(corpseGo);
        }

        [UnityTest]
        public IEnumerator TestMode_MissingCorpseSpawnCell_LogsError()
        {
            // Arrange: create a corpse prefab (inactive to avoid FindObjectsByType picking it up)
            var corpseGo = new GameObject("CorpsePrefab");
            corpseGo.AddComponent<Corpse>();
            corpseGo.SetActive(false);

            var encounterConfig = ScriptableObject.CreateInstance<BattleEncounterTestConfig>();
            var corpseSlots = new List<CorpseTestSlot>
            {
                CreateCorpseSlot(new Vector2Int(99, 99), corpseGo, "Ghost", 2)
            };
            SetPrivateField(encounterConfig, "_corpseSlots", corpseSlots);
            SetPrivateField(encounterConfig, "_slots", new List<EncounterTestSlot>());

            var bc = _battleRoot.GetComponent<BattleController>();
            SetPrivateField(bc, "_useTestSetup", true);
            SetPrivateField(bc, "_testEncounterConfig", encounterConfig);

            // Act + Assert expected error
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("CorpseTestSlot\\[0\\] SpawnCell '\\(99, 99\\)' did not map to a grid cell"));
            CallPrivate(bc, "SpawnEncounterUnits");
            yield return null;

            // No active corpses should be spawned
            var corpses = Object.FindObjectsByType<Corpse>(FindObjectsSortMode.None)
                .Where(c => c.gameObject.activeInHierarchy).ToList();
            Assert.That(corpses.Count, Is.EqualTo(0), "No corpses should spawn when spawn cell is missing.");

            Object.DestroyImmediate(corpseGo);
        }

        [Test]
        public void EncounterBlockedCells_AreAppliedBeforeSpawnAndRestoreOriginalState()
        {
            var controller = _battleRoot.GetComponent<BattleController>();
            var target = _cellManagerRoot.GetComponentsInChildren<Square>()
                .Single(cell => cell.GridCoordinates.x == 1 && cell.GridCoordinates.y == 1);
            target.IsTaken = false;

            var apply = typeof(BattleController).GetMethod(
                "ApplyEncounterBlockedCells",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var restore = typeof(BattleController).GetMethod(
                "RestoreEncounterBlockedCells",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(apply, Is.Not.Null);
            Assert.That(restore, Is.Not.Null);
            Assert.That(apply.Invoke(controller, new object[]
            {
                new List<BattleLayoutCell> { new BattleLayoutCell(1, 1) }
            }), Is.True);
            Assert.That(target.IsTaken, Is.True);

            restore.Invoke(controller, null);
            Assert.That(target.IsTaken, Is.False);
        }

        // --- Helpers ---

        private static PartyTestSlot CreatePartySlot(Vector2Int spawnCell, GameObject prefab, string displayName)
        {
            var slot = new PartyTestSlot();
            SetPrivateField(slot, "_spawnCell", spawnCell);
            SetPrivateField(slot, "_unitPrefab", prefab);
            SetPrivateField(slot, "_displayName", displayName);
            SetPrivateField(slot, "_roleType", RoleType.Barbarian);
            SetPrivateField(slot, "_strength", 5);
            SetPrivateField(slot, "_agility", 5);
            SetPrivateField(slot, "_constitution", 5);
            SetPrivateField(slot, "_intelligence", 5);
            SetPrivateField(slot, "_charisma", 5);
            SetPrivateField(slot, "_luck", 5);
            SetPrivateField(slot, "_speed", 5f);
            SetPrivateField(slot, "_attackFactor", 1);
            SetPrivateField(slot, "_defenceFactor", 1);
            return slot;
        }

        private static EncounterTestSlot CreateEncounterSlot(Vector2Int spawnCell, GameObject prefab, string displayName, int playerNumber)
        {
            var slot = new EncounterTestSlot();
            SetPrivateField(slot, "_spawnCell", spawnCell);
            SetPrivateField(slot, "_unitPrefab", prefab);
            SetPrivateField(slot, "_displayName", displayName);
            SetPrivateField(slot, "_playerNumber", playerNumber);
            return slot;
        }

        private static CorpseTestSlot CreateCorpseSlot(Vector2Int spawnCell, GameObject prefab, string displayName, int playerNumber)
        {
            var slot = new CorpseTestSlot();
            SetPrivateField(slot, "_spawnCell", spawnCell);
            SetPrivateField(slot, "_unitPrefab", prefab);
            SetPrivateField(slot, "_displayName", displayName);
            SetPrivateField(slot, "_playerNumber", playerNumber);
            return slot;
        }

        private void CallPrivate(BattleController bc, string methodName)
        {
            var method = typeof(BattleController).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method?.Invoke(bc, null);
        }

        private static int CountUnitsWithPlayerNumber(int playerNumber)
        {
            int count = 0;
            foreach (var u in Object.FindObjectsByType<TilemapUnit>(FindObjectsSortMode.None))
            {
                if (u.PlayerNumber == playerNumber) count++;
            }
            return count;
        }

        private static void SetPrivateField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(obj, value);
        }
    }
}
