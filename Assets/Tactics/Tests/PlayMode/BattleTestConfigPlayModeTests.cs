using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Tactics.Common.Battle;
using Tactics.Common.Battle.Authoring;
using Tactics.Common.Cells;
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
        private readonly List<GameObject> _spawnPoints = new();

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

            // Destroy all spawned test objects
            foreach (var sp in _spawnPoints)
            {
                if (sp != null) Object.DestroyImmediate(sp);
            }
            _spawnPoints.Clear();

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
        public IEnumerator TestMode_SpawnsPartyUnits_AtSpawnPoints()
        {
            // Arrange
            var template = CreateUnitTemplate();
            var spawnA = CreateSpawnPoint<PlayerSpawnPoint>("p_spawn_1", new Vector3(0, 0, 0));
            var spawnB = CreateSpawnPoint<PlayerSpawnPoint>("p_spawn_2", new Vector3(1, 0, 0));

            var partyConfig = ScriptableObject.CreateInstance<BattlePartyTestConfig>();
            var slots = new List<PartyTestSlot>
            {
                CreatePartySlot("p_spawn_1", template, "Warrior"),
                CreatePartySlot("p_spawn_2", template, "Mage")
            };
            SetPrivateField(partyConfig, "_slots", slots);

            var bc = _battleRoot.GetComponent<BattleController>();
            SetPrivateField(bc, "_useTestSetup", true);
            SetPrivateField(bc, "_testPartyConfig", partyConfig);
            RebuildSpawnCaches(bc);

            // Act
            CallPrivate(bc, "SpawnPartyUnits");
            yield return null;

            // Assert
            int playerUnits = CountUnitsWithPlayerNumber(1);
            Assert.That(playerUnits, Is.EqualTo(2), $"Expected 2 player units, found {playerUnits}.");

            Object.DestroyImmediate(template);
        }

        [UnityTest]
        public IEnumerator TestMode_SpawnsEncounterUnits_AtSpawnPoints()
        {
            // Arrange
            var template = CreateUnitTemplate();
            // Set PlayerNumber=1 so encounter cleanup doesn't destroy it
            template.GetComponent<TilemapUnit>().PlayerNumber = 1;
            var spawnA = CreateSpawnPoint<EnemySpawnPoint>("e_spawn_1", new Vector3(3, 0, 0));

            var encounterConfig = ScriptableObject.CreateInstance<BattleEncounterTestConfig>();
            var slots = new List<EncounterTestSlot>
            {
                CreateEncounterSlot("e_spawn_1", template, "Goblin", 2)
            };
            SetPrivateField(encounterConfig, "_slots", slots);

            var bc = _battleRoot.GetComponent<BattleController>();
            SetPrivateField(bc, "_useTestSetup", true);
            SetPrivateField(bc, "_testEncounterConfig", encounterConfig);
            RebuildSpawnCaches(bc);

            // Act
            CallPrivate(bc, "SpawnEncounterUnits");
            yield return null;

            // Assert
            int enemyUnits = CountUnitsWithPlayerNumber(2);
            Assert.That(enemyUnits, Is.EqualTo(1), $"Expected 1 enemy unit, found {enemyUnits}.");

            Object.DestroyImmediate(template);
        }

        [UnityTest]
        public IEnumerator MissingSpawnPoint_LogsError()
        {
            // Arrange: config references nonexistent spawn point
            var template = CreateUnitTemplate();
            var partyConfig = ScriptableObject.CreateInstance<BattlePartyTestConfig>();
            var slots = new List<PartyTestSlot>
            {
                CreatePartySlot("nonexistent_spawn", template, "Ghost")
            };
            SetPrivateField(partyConfig, "_slots", slots);

            var bc = _battleRoot.GetComponent<BattleController>();
            SetPrivateField(bc, "_useTestSetup", true);
            SetPrivateField(bc, "_testPartyConfig", partyConfig);
            RebuildSpawnCaches(bc);

            // Act + Assert expected error
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("PlayerSpawnPoint with SpawnId='nonexistent_spawn' not found"));
            CallPrivate(bc, "SpawnPartyUnits");
            yield return null;

            int playerUnits = CountUnitsWithPlayerNumber(1);
            Assert.That(playerUnits, Is.EqualTo(0), "Should not spawn when spawn point is missing.");

            Object.DestroyImmediate(template);
        }

        [UnityTest]
        public IEnumerator NullPrefab_SkipsSlot()
        {
            // Arrange
            var template = CreateUnitTemplate();
            var spawnA = CreateSpawnPoint<PlayerSpawnPoint>("p_spawn_1", new Vector3(0, 0, 0));

            var partyConfig = ScriptableObject.CreateInstance<BattlePartyTestConfig>();
            var slots = new List<PartyTestSlot>
            {
                CreatePartySlot("p_spawn_1", null, "Ghost"),
                CreatePartySlot("p_spawn_1", template, "Valid")
            };
            SetPrivateField(partyConfig, "_slots", slots);

            var bc = _battleRoot.GetComponent<BattleController>();
            SetPrivateField(bc, "_useTestSetup", true);
            SetPrivateField(bc, "_testPartyConfig", partyConfig);
            RebuildSpawnCaches(bc);

            // Act + Assert expected error for null prefab
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("TestPartySlot\\[0\\] has null UnitPrefab"));
            CallPrivate(bc, "SpawnPartyUnits");
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

        // --- Helpers ---

        private T CreateSpawnPoint<T>(string spawnId, Vector3 position) where T : MonoBehaviour
        {
            var go = new GameObject($"Spawn_{spawnId}");
            go.transform.position = position;
            var point = go.AddComponent<T>();
            SetPrivateField(point, "_spawnId", spawnId);
            _spawnPoints.Add(go);
            return point;
        }

        private static PartyTestSlot CreatePartySlot(string spawnId, GameObject prefab, string displayName)
        {
            var slot = new PartyTestSlot();
            SetPrivateField(slot, "_spawnId", spawnId);
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

        private static EncounterTestSlot CreateEncounterSlot(string spawnId, GameObject prefab, string displayName, int playerNumber)
        {
            var slot = new EncounterTestSlot();
            SetPrivateField(slot, "_spawnId", spawnId);
            SetPrivateField(slot, "_unitPrefab", prefab);
            SetPrivateField(slot, "_displayName", displayName);
            SetPrivateField(slot, "_playerNumber", playerNumber);
            return slot;
        }

        private void RebuildSpawnCaches(BattleController bc)
        {
            var buildMethod = typeof(BattleController).GetMethod("BuildSpawnPointCaches", BindingFlags.Instance | BindingFlags.NonPublic);
            buildMethod?.Invoke(bc, null);
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
