using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using Tactics.Common.AI.MonsterAI;
using Tactics.Common.Battle;
using Tactics.Common.Cells;
using Tactics.Common.Interactables;
using Tactics.Common.Players;
using Tactics.Common.Units;
using Tactics.Common.Units.Buffs;
using Tactics.Common.Units.Classes;
using Tactics.Common.Utilities;
using Tactics.Roster;
using Tactics.Units;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tactics.Tests.PlayMode
{
    public sealed class BattleControllerEncounterSpawnTests
    {
        private GameObject _cellRoot;
        private GameObject _battleRoot;
        private GameObject _unitContainer;
        private GameObject _unitTemplate;
        private RuntimeWalkabilityCellManager _cellManager;
        private BattleController _controller;

        [SetUp]
        public void SetUp()
        {
            _cellRoot = new GameObject("EncounterSpawnCells");
            _cellManager = _cellRoot.AddComponent<RuntimeWalkabilityCellManager>();
            for (int x = 0; x < BattleBoardSpec.Width; x++)
            {
                for (int y = 0; y < BattleBoardSpec.Height; y++)
                {
                    var cellObject = new GameObject($"Cell_{x}_{y}");
                    cellObject.transform.SetParent(_cellRoot.transform);
                    var cell = cellObject.AddComponent<Square>();
                    cell.GridCoordinates = new Vector2IntImpl(x, y);
                    cell.WorldPosition = new Vector3Impl(x, y, 0f);
                    cell.MovementCost = 1f;
                }
            }
            _cellManager.Initialize(null);

            _battleRoot = new GameObject("EncounterSpawnBattle");
            _battleRoot.SetActive(false);
            _controller = _battleRoot.AddComponent<BattleController>();
            _unitContainer = new GameObject("UnitContainer");
            _unitContainer.transform.SetParent(_battleRoot.transform);
            SetRequiredPrivateField(_controller, "_cellManager", _cellManager);
            SetRequiredPrivateField(_controller, "_unitContainer", _unitContainer.transform);
            SetRequiredPrivateField(_controller, "_startImmediatelly", false);
            SetRequiredPrivateField(_controller, "_humanPlayerNumber", 1);
            _battleRoot.SetActive(true);

            _unitTemplate = new GameObject("RuntimeUnitTemplate");
            _unitTemplate.SetActive(false);
            _unitTemplate.AddComponent<TilemapUnit>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_battleRoot != null)
                UnityEngine.Object.DestroyImmediate(_battleRoot);
            if (_cellRoot != null)
                UnityEngine.Object.DestroyImmediate(_cellRoot);
            if (_unitTemplate != null)
                UnityEngine.Object.DestroyImmediate(_unitTemplate);
        }

        [Test]
        public void ProductionPreparation_LoadsOnce_AndSpawnsBlockersThenPartyThenEnemies()
        {
            var encounter = CreateEncounter(
                new[] { Cell(1, 1), Cell(1, 2), Cell(1, 3) },
                new[] { Cell(4, 4) });
            var state = CreatePartyState("third", "first", "second");
            ConfigureProduction(encounter, state);

            int loadCount = 0;
            SetRequiredPrivateField(_controller, "_encounterLoaderOverrideForTests",
                new Func<EncounterConfig>(() => { loadCount++; return encounter; }));

            var order = new List<string>();
            SetRequiredPrivateField(_controller, "_encounterSpawnStepObserverForTests",
                new Action<string>(step =>
                {
                    if (step.StartsWith("party:", StringComparison.Ordinal) ||
                        step.StartsWith("enemy:", StringComparison.Ordinal))
                    {
                        Assert.That(GetCell(4, 4).IsTaken, Is.True,
                            $"Blocker must already be active before '{step}'.");
                    }
                    order.Add(step);
                }));

            bool result = InvokeProductionPreparation();

            Assert.That(result, Is.True);
            Assert.That(loadCount, Is.EqualTo(1), "One battle preparation may resolve/load its encounter only once.");
            Assert.That(order, Is.EqualTo(new[] { "blocked", "party:0", "party:1", "party:2", "enemy:0" }));

            var partyByCell = SpawnedUnits()
                .Where(unit => unit.PlayerNumber == 1)
                .ToDictionary(unit => (unit.CurrentCell.GridCoordinates.x, unit.CurrentCell.GridCoordinates.y));
            Assert.That(partyByCell[(1, 1)].GetComponent<RosterCharacterLink>().CharacterId, Is.EqualTo("third"));
            Assert.That(partyByCell[(1, 2)].GetComponent<RosterCharacterLink>().CharacterId, Is.EqualTo("first"));
            Assert.That(partyByCell[(1, 3)].GetComponent<RosterCharacterLink>().CharacterId, Is.EqualTo("second"));
            Assert.That(SpawnedUnits().Count(unit => unit.PlayerNumber == 2), Is.EqualTo(1));

            UnityEngine.Object.DestroyImmediate(_battleRoot);
            _battleRoot = null;
            _controller = null;
            Assert.That(GetCell(4, 4).IsTaken, Is.False,
                "Destroying the battle must restore each encounter blocker's original state.");
        }

        [Test]
        public void EndBattleBeforeBattleBecomesActive_RestoresPreparedBlockers()
        {
            ConfigureProduction(
                CreateEncounter(new[] { Cell(1, 1), Cell(1, 2), Cell(1, 3) }, new[] { Cell(4, 4) }),
                CreatePartyState("first", "second", "third"));
            Assert.That(InvokeProductionPreparation(), Is.True);
            Assert.That(GetCell(4, 4).IsTaken, Is.True);

            _controller.EndBattle(default);

            Assert.That(GetCell(4, 4).IsTaken, Is.False);
        }

        [Test]
        public async Task EndBattleAsyncBeforeBattleBecomesActive_RestoresPreparedBlockers()
        {
            ConfigureProduction(
                CreateEncounter(new[] { Cell(1, 1), Cell(1, 2), Cell(1, 3) }, new[] { Cell(4, 4) }),
                CreatePartyState("first", "second", "third"));
            Assert.That(InvokeProductionPreparation(), Is.True);
            Assert.That(GetCell(4, 4).IsTaken, Is.True);

            await _controller.EndBattleAsync(default);

            Assert.That(GetCell(4, 4).IsTaken, Is.False);
        }

        [Test]
        public void PendingRecipeResolutionFailure_DoesNotFallBackToLegacyEncounterPath()
        {
            bool hadPath = PlayerPrefs.HasKey(EncounterRuntimeState.PendingEncounterPrefsKey);
            bool hadRecipe = PlayerPrefs.HasKey(EncounterRuntimeState.PendingEncounterRecipePrefsKey);
            bool hadSeed = PlayerPrefs.HasKey(EncounterRuntimeState.PendingEncounterSeedPrefsKey);
            string previousPath = PlayerPrefs.GetString(EncounterRuntimeState.PendingEncounterPrefsKey, string.Empty);
            string previousRecipe = PlayerPrefs.GetString(EncounterRuntimeState.PendingEncounterRecipePrefsKey, string.Empty);
            int previousSeed = PlayerPrefs.GetInt(EncounterRuntimeState.PendingEncounterSeedPrefsKey, 0);
            try
            {
                PlayerPrefs.SetString(EncounterRuntimeState.PendingEncounterPrefsKey, "Assets/ShouldNotLoad.json");
                PlayerPrefs.SetString(EncounterRuntimeState.PendingEncounterRecipePrefsKey, "MissingRecipe");
                PlayerPrefs.SetInt(EncounterRuntimeState.PendingEncounterSeedPrefsKey, 123);
                LogAssert.Expect(LogType.Error, new Regex(".*Failed to resolve recipe:MissingRecipe@123.*"));

                bool result = EncounterRuntimeState.TryLoadPendingEncounter(
                    null,
                    out var encounter,
                    out string source);

                Assert.That(result, Is.False);
                Assert.That(encounter, Is.Null);
                Assert.That(source, Is.EqualTo("recipe:MissingRecipe@123"));
            }
            finally
            {
                RestoreStringPref(EncounterRuntimeState.PendingEncounterPrefsKey, hadPath, previousPath);
                RestoreStringPref(EncounterRuntimeState.PendingEncounterRecipePrefsKey, hadRecipe, previousRecipe);
                if (hadSeed)
                    PlayerPrefs.SetInt(EncounterRuntimeState.PendingEncounterSeedPrefsKey, previousSeed);
                else
                    PlayerPrefs.DeleteKey(EncounterRuntimeState.PendingEncounterSeedPrefsKey);
                PlayerPrefs.Save();
            }
        }

        [TestCase("missing")]
        [TestCase("outside")]
        [TestCase("duplicate")]
        [TestCase("non-walkable")]
        [TestCase("occupied")]
        public void InvalidProductionPartyCells_FailClosedWithoutFallbackOrPartialParty(string invalidCase)
        {
            var partyCells = new List<BattleLayoutCell> { Cell(1, 1), Cell(1, 2), Cell(1, 3) };
            switch (invalidCase)
            {
                case "missing":
                    partyCells.RemoveAt(2);
                    break;
                case "outside":
                    partyCells[1] = Cell(BattleBoardSpec.Width, 2);
                    break;
                case "duplicate":
                    partyCells[2] = Cell(1, 1);
                    break;
                case "non-walkable":
                    _cellManager.NonWalkable.Add(new Vector2Int(1, 2));
                    break;
                case "occupied":
                    GetCell(1, 2).IsTaken = true;
                    break;
            }

            var encounter = CreateEncounter(partyCells, new[] { Cell(4, 4) });
            ConfigureProduction(encounter, CreatePartyState("first", "second", "third"));

            string expectedLogFragment = invalidCase switch
            {
                "missing" => "requires at least",
                "outside" => "outside BattleBoardSpec",
                "duplicate" => "duplicates cell",
                "non-walkable" => "not walkable",
                "occupied" => "is occupied",
                _ => throw new ArgumentOutOfRangeException(nameof(invalidCase), invalidCase, null)
            };
            LogAssert.Expect(LogType.Error, new Regex($".*{Regex.Escape(expectedLogFragment)}.*"));

            bool result = InvokeProductionPreparation();

            Assert.That(result, Is.False, $"'{invalidCase}' must abort production initialization.");
            Assert.That(SpawnedUnits(), Is.Empty,
                $"'{invalidCase}' must not leave a partial party or spawn enemies on substitute cells.");
            Assert.That(GetCell(4, 4).IsTaken, Is.False,
                $"'{invalidCase}' must roll back blockers applied by the failed transaction.");
        }

        [Test]
        public void ProductionPartyCellWithReplaceableUnitAndOccupyingCorpse_FailsClosedAndPreservesBoth()
        {
            var occupiedCell = GetCell(0, 0);
            var existingObject = UnityEngine.Object.Instantiate(_unitTemplate, _unitContainer.transform);
            existingObject.name = "ExistingUnitSharingCorpseCell";
            existingObject.SetActive(true);
            var existingUnit = existingObject.GetComponent<TilemapUnit>();
            existingUnit.PlayerNumber = 1;
            existingUnit.CurrentCell = occupiedCell;
            occupiedCell.CurrentUnits.Add(existingUnit);
            occupiedCell.IsTaken = true;
            ((IUnitManager)_controller).Initialize(_controller);

            var existingCorpseObject = new GameObject("ExistingCorpseSharingUnitCell");
            existingCorpseObject.transform.SetParent(_unitContainer.transform);
            var existingCorpse = existingCorpseObject.AddComponent<Corpse>();
            occupiedCell.AddInteractable(existingCorpse);

            ConfigureProduction(
                CreateEncounter(new[] { Cell(0, 0), Cell(1, 2), Cell(1, 3) }, Array.Empty<BattleLayoutCell>()),
                CreatePartyState("first", "second", "third"));
            LogAssert.Expect(LogType.Error, new Regex(".*Party spawn cell \\(0,0\\) is occupied.*"));

            bool result = InvokeProductionPreparation();

            Assert.That(result, Is.False);
            Assert.That(existingUnit == null, Is.False);
            Assert.That(existingUnit.transform.parent, Is.EqualTo(_unitContainer.transform));
            Assert.That(existingUnit.CurrentCell, Is.SameAs(occupiedCell));
            Assert.That(occupiedCell.CurrentUnits, Does.Contain(existingUnit));
            Assert.That(existingCorpse == null, Is.False);
            Assert.That(existingCorpse.transform.parent, Is.EqualTo(_unitContainer.transform));
            Assert.That(existingCorpse.CurrentCell, Is.SameAs(occupiedCell));
            Assert.That(occupiedCell.CurrentInteractables, Does.Contain(existingCorpse));
            Assert.That(occupiedCell.IsTaken, Is.True);
        }

        [Test]
        public void ProductionStageFailure_RestoresEveryUnitStagedBeforeTheException()
        {
            var existingUnits = new List<(TilemapUnit Unit, Square Cell)>();
            foreach (var coordinates in new[] { new Vector2Int(0, 0), new Vector2Int(0, 1) })
            {
                var cell = GetCell(coordinates.x, coordinates.y);
                var existingObject = UnityEngine.Object.Instantiate(_unitTemplate, _unitContainer.transform);
                existingObject.name = $"ExistingUnit_{coordinates.x}_{coordinates.y}";
                existingObject.SetActive(true);
                var unit = existingObject.GetComponent<TilemapUnit>();
                unit.PlayerNumber = 1;
                unit.CurrentCell = cell;
                cell.CurrentUnits.Add(unit);
                cell.IsTaken = true;
                existingUnits.Add((unit, cell));
            }
            ((IUnitManager)_controller).Initialize(_controller);

            ConfigureProduction(
                CreateEncounter(new[] { Cell(1, 1), Cell(1, 2), Cell(1, 3) }, Array.Empty<BattleLayoutCell>()),
                CreatePartyState("first", "second", "third"));
            int stagedCount = 0;
            SetRequiredPrivateField(_controller, "_stageSceneUnitObserverForTests", new Action<TilemapUnit>(_ =>
            {
                stagedCount++;
                if (stagedCount == 2)
                    throw new InvalidOperationException("Injected staging failure after one unit was detached.");
            }));
            LogAssert.Expect(LogType.Error, new Regex(".*Encounter spawn transaction failed and was rolled back.*"));

            bool result = InvokeProductionPreparation();

            Assert.That(result, Is.False);
            Assert.That(stagedCount, Is.EqualTo(2));
            foreach (var (unit, cell) in existingUnits)
            {
                Assert.That(unit == null, Is.False);
                Assert.That(unit.transform.parent, Is.EqualTo(_unitContainer.transform));
                Assert.That(unit.CurrentCell, Is.SameAs(cell));
                Assert.That(cell.CurrentUnits, Does.Contain(unit));
                Assert.That(cell.IsTaken, Is.True);
                Assert.That(((IUnitManager)_controller).GetUnits(), Does.Contain(unit));
                Assert.That(unit.gameObject.activeSelf, Is.True);
            }
        }

        [Test]
        public void EnemySpawnAtMaximumBoardCell_CommitsExactOccupancy()
        {
            var encounter = CreateEncounter(
                new[] { Cell(1, 1), Cell(1, 2), Cell(1, 3) },
                new[] { Cell(4, 4) });
            encounter.Units[0].SpawnCellX = 9;
            encounter.Units[0].SpawnCellY = 9;
            ConfigureProduction(encounter, CreatePartyState("first", "second", "third"));

            bool result = InvokeProductionPreparation();

            Assert.That(result, Is.True);
            var enemy = SpawnedUnits().Single(unit => unit.PlayerNumber == 2);
            Assert.That(GetCell(9, 9).CurrentUnits, Is.EqualTo(new IUnit[] { enemy }));
            Assert.That(GetCell(9, 9).IsTaken, Is.True);
            Assert.That(enemy.CurrentCell.GridCoordinates.x, Is.EqualTo(9));
            Assert.That(enemy.CurrentCell.GridCoordinates.y, Is.EqualTo(9));
        }

        [Test]
        public void EnemySpawnBeyondMaximumBoardCell_AbortsWithoutFallbackAndRollsBack()
        {
            var encounter = CreateEncounter(
                new[] { Cell(1, 1), Cell(1, 2), Cell(1, 3) },
                new[] { Cell(4, 4) });
            encounter.Units[0].SpawnCellX = 10;
            encounter.Units[0].SpawnCellY = 9;
            ConfigureProduction(encounter, CreatePartyState("first", "second", "third"));
            LogAssert.Expect(LogType.Error, new Regex(".*cannot spawn.*\\(10,9\\).*cell does not exist.*"));

            bool result = InvokeProductionPreparation();

            Assert.That(result, Is.False);
            Assert.That(SpawnedUnits(), Is.Empty);
            Assert.That(GetCell(4, 4).IsTaken, Is.False);
            Assert.That(GetCell(9, 9).CurrentUnits, Is.Empty);
            Assert.That(GetCell(9, 9).IsTaken, Is.False);
        }

        [Test]
        public void EnemySpawnFailure_RollsBackPartyEnemiesBlockersAndOccupancy()
        {
            var encounter = CreateEncounter(
                new[] { Cell(1, 1), Cell(1, 2), Cell(1, 3) },
                new[] { Cell(4, 4) });
            encounter.Units.Add(new EncounterUnitEntry
            {
                MonsterId = "invalid-enemy",
                UnitName = "InvalidEnemy",
                UnitPrefabPath = "test://invalid-enemy",
                AiBrainAssetPath = string.Empty,
                AbilityConfigPaths = new List<string>(),
                PlayerNumber = 2,
                SpawnCellX = 1,
                SpawnCellY = 1
            });
            ConfigureProduction(encounter, CreatePartyState("first", "second", "third"));
            LogAssert.Expect(LogType.Error, new Regex(".*cell is blocked or occupied.*"));

            bool result = InvokeProductionPreparation();

            Assert.That(result, Is.False);
            Assert.That(SpawnedUnits(), Is.Empty);
            Assert.That(GetCell(4, 4).IsTaken, Is.False);
            Assert.That(GetCell(1, 1).CurrentUnits, Is.Empty);
            Assert.That(GetCell(8, 8).CurrentUnits, Is.Empty);
        }

        [Test]
        public void EnemySpawnFailure_RestoresPreExistingSceneUnitAfterReplacementWasStaged()
        {
            var existingObject = UnityEngine.Object.Instantiate(_unitTemplate, _unitContainer.transform);
            existingObject.name = "ExistingSceneUnit";
            existingObject.SetActive(true);
            var existingUnit = existingObject.GetComponent<TilemapUnit>();
            var originalCell = GetCell(1, 1);
            existingUnit.CurrentCell = originalCell;
            originalCell.CurrentUnits.Add(existingUnit);
            originalCell.IsTaken = true;
            ((IUnitManager)_controller).Initialize(_controller);

            var state = CreatePartyState("first", "second", "third");
            var encounter = CreateEncounter(
                new[] { Cell(1, 1), Cell(1, 2), Cell(1, 3) },
                new[] { Cell(4, 4) });
            encounter.Units.Add(new EncounterUnitEntry
            {
                MonsterId = "invalid-enemy",
                UnitName = "InvalidEnemy",
                UnitPrefabPath = "test://invalid-enemy",
                AiBrainAssetPath = string.Empty,
                AbilityConfigPaths = new List<string>(),
                PlayerNumber = 2,
                SpawnCellX = 7,
                SpawnCellY = 8
            });
            var steps = new List<string>();
            ConfigureProduction(encounter, state);
            SetRequiredPrivateField(_controller, "_encounterSpawnStepObserverForTests",
                new Action<string>(steps.Add));
            SetRequiredPrivateField(_controller, "_runtimePrefabLoadOverrideForTests",
                new Func<string, GameObject>(path => path == "test://invalid-enemy" ? null : _unitTemplate));
            LogAssert.Expect(LogType.Error, new Regex(".*prefab not found.*test://invalid-enemy.*"));

            Assert.That(InvokeProductionPreparation(), Is.False);
            Assert.That(steps, Does.Contain("blocked"), "Replacement must pass prevalidation before the enemy failure.");
            Assert.That(existingUnit == null, Is.False, "The staged scene unit must not be destroyed on rollback.");
            Assert.That(existingObject.activeSelf, Is.True);
            Assert.That(existingUnit.transform.parent, Is.EqualTo(_unitContainer.transform));
            Assert.That(existingUnit.CurrentCell, Is.SameAs(originalCell));
            Assert.That(originalCell.CurrentUnits, Does.Contain(existingUnit));
            Assert.That(originalCell.IsTaken, Is.True);
            Assert.That(((IUnitManager)_controller).GetUnits(), Does.Contain(existingUnit));
        }

        [Test]
        public void BasicMelee_EnemiesLoadAiBrainAndCreateAutomatedPlayerTwo()
        {
            string jsonPath = Path.Combine(
                Application.dataPath,
                "Tactics/GameData/Encounters/basic_melee.json");
            var json = JsonUtility.FromJson<BasicMeleeJson>(File.ReadAllText(jsonPath));
            var encounter = new EncounterConfig
            {
                EncounterId = json.encounterId,
                PartySpawnCells = json.partySpawnCells
                    .Select(cell => new BattleLayoutCell(cell.X, cell.Y))
                    .ToList(),
                Units = json.units.Select(unit => new EncounterUnitEntry
                {
                    UnitName = unit.unitName,
                    UnitPrefabPath = unit.unitPrefabPath,
                    AiBrainAssetPath = unit.aiBrainAssetPath,
                    PlayerNumber = unit.playerNumber,
                    SpawnCellX = unit.spawnCellX,
                    SpawnCellY = unit.spawnCellY
                }).ToList()
            };
            var brain = ScriptableObject.CreateInstance<AiBrainAsset>();
            var decisionGraph = ScriptableObject.CreateInstance<AiDecisionGraph>();
            SetRequiredPrivateField(brain, "_decisionGraph", decisionGraph);

            try
            {
                ConfigureProduction(encounter, CreatePartyState("first", "second", "third"));
                int brainLoadCount = 0;
                SetRequiredPrivateField(
                    _controller,
                    "_runtimeAiBrainLoadOverrideForTests",
                    new Func<string, AiBrainAsset>(_ =>
                    {
                        brainLoadCount++;
                        return brain;
                    }));

                Assert.That(InvokeProductionPreparation(), Is.True);
                foreach (var spawnedUnit in SpawnedUnits())
                    spawnedUnit.gameObject.SetActive(true);
                ((IUnitManager)_controller).Initialize(_controller);
                InvokeRequiredPrivateMethod(_controller, "EnsurePlayersCoverSpawnedUnits");

                var enemies = SpawnedUnits().Where(unit => unit.PlayerNumber == 2).ToList();
                Assert.That(brainLoadCount, Is.EqualTo(1),
                    "The shared basic-melee AI brain must be loaded for the enemy faction.");
                Assert.That(enemies, Has.Count.EqualTo(2));
                Assert.That(enemies.All(unit => unit.AiBrainAsset == brain), Is.True);
                Assert.That(
                    ((IPlayerManager)_controller).GetPlayers().Single(player => player.PlayerNumber == 2).PlayerType,
                    Is.EqualTo(PlayerType.AutomatedPlayer));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(brain);
                UnityEngine.Object.DestroyImmediate(decisionGraph);
            }
        }

        [Test]
        public void ProductionPreparation_CachesRepeatedPrefabLoadsPerAssetPath()
        {
            var state = CreatePartyState("first", "second", "third");
            foreach (var definition in state.Roster)
                definition.PrefabPath = "Assets/Tactics/Tests/Fixtures/SharedParty.prefab";
            var encounter = CreateEncounter(
                new[] { Cell(1, 1), Cell(1, 2), Cell(1, 3) },
                Array.Empty<BattleLayoutCell>());
            encounter.Units.Add(new EncounterUnitEntry
            {
                MonsterId = "second-enemy",
                UnitName = "SecondEnemy",
                UnitPrefabPath = "Assets/Tactics/Tests/Fixtures/SharedEnemy.prefab",
                AiBrainAssetPath = string.Empty,
                AbilityConfigPaths = new List<string>(),
                PlayerNumber = 2,
                SpawnCellX = 7,
                SpawnCellY = 8
            });
            ConfigureProduction(encounter, state);
            var loadCounts = new Dictionary<string, int>();
            var releasedPaths = new List<string>();
            SetRequiredPrivateField(_controller, "_runtimePrefabLoadOverrideForTests", new Func<string, GameObject>(path =>
            {
                loadCounts[path] = loadCounts.TryGetValue(path, out int count) ? count + 1 : 1;
                return _unitTemplate;
            }));
            SetRequiredPrivateField(_controller, "_runtimeAssetReleaseOverrideForTests",
                new Action<string>(releasedPaths.Add));

            Assert.That(InvokeProductionPreparation(), Is.True);
            Assert.That(loadCounts["Assets/Tactics/Tests/Fixtures/SharedParty.prefab"], Is.EqualTo(1));
            Assert.That(loadCounts["Assets/Tactics/Tests/Fixtures/SharedEnemy.prefab"], Is.EqualTo(1));

            UnityEngine.Object.DestroyImmediate(_battleRoot);
            _battleRoot = null;
            _controller = null;
            Assert.That(releasedPaths.Count(path => path == "Assets/Tactics/Tests/Fixtures/SharedParty.prefab"), Is.EqualTo(1));
            Assert.That(releasedPaths.Count(path => path == "Assets/Tactics/Tests/Fixtures/SharedEnemy.prefab"), Is.EqualTo(1));
        }

        [Test]
        public void TestEncounterSpawn_CachesRepeatedAiBrainLoadsPerAssetPath()
        {
            const string sharedBrainPath = "Assets/Tactics/Tests/Fixtures/SharedBrain.asset";
            var brain = ScriptableObject.CreateInstance<AiBrainAsset>();
            var decisionGraph = ScriptableObject.CreateInstance<AiDecisionGraph>();
            SetRequiredPrivateField(brain, "_decisionGraph", decisionGraph);
            EncounterTestSlot CreateSlot(Vector2Int cell, string name)
            {
                var slot = new EncounterTestSlot();
                SetRequiredPrivateField(slot, "_spawnCell", cell);
                SetRequiredPrivateField(slot, "_unitPrefab", _unitTemplate);
                SetRequiredPrivateField(slot, "_aiBrainAssetPath", sharedBrainPath);
                SetRequiredPrivateField(slot, "_displayName", name);
                SetRequiredPrivateField(slot, "_playerNumber", 2);
                return slot;
            }

            var encounterConfig = ScriptableObject.CreateInstance<BattleEncounterTestConfig>();
            SetRequiredPrivateField(
                encounterConfig,
                "_slots",
                new List<EncounterTestSlot>
                {
                    CreateSlot(new Vector2Int(6, 4), "TestEnemyA"),
                    CreateSlot(new Vector2Int(7, 3), "TestEnemyB")
                });
            SetRequiredPrivateField(_controller, "_testEncounterConfig", encounterConfig);

            int loadCount = 0;
            var releasedPaths = new List<string>();
            SetRequiredPrivateField(_controller, "_runtimeAiBrainLoadOverrideForTests", new Func<string, AiBrainAsset>(_ =>
            {
                loadCount++;
                return brain;
            }));
            SetRequiredPrivateField(_controller, "_runtimeAssetReleaseOverrideForTests",
                new Action<string>(releasedPaths.Add));

            InvokeRequiredPrivateMethod(_controller, "SpawnTestEncounterUnits");

            Assert.That(loadCount, Is.EqualTo(1));
            Assert.That(SpawnedUnits().Count(unit => unit.PlayerNumber == 2), Is.EqualTo(2));

            UnityEngine.Object.DestroyImmediate(_battleRoot);
            _battleRoot = null;
            _controller = null;
            Assert.That(releasedPaths.Count(path => path == sharedBrainPath), Is.EqualTo(1));
            UnityEngine.Object.DestroyImmediate(encounterConfig);
            UnityEngine.Object.DestroyImmediate(brain);
            UnityEngine.Object.DestroyImmediate(decisionGraph);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void DirectTestEncounterSpawn_InvalidAiBrain_DetachesCellBeforeDeferredDestroy(bool brainIsMissing)
        {
            const string brainPath = "Assets/Tactics/Tests/Fixtures/InvalidBrain.asset";
            var invalidBrain = ScriptableObject.CreateInstance<AiBrainAsset>();
            var slot = new EncounterTestSlot();
            SetRequiredPrivateField(slot, "_spawnCell", new Vector2Int(6, 4));
            SetRequiredPrivateField(slot, "_unitPrefab", _unitTemplate);
            SetRequiredPrivateField(slot, "_aiBrainAssetPath", brainPath);
            SetRequiredPrivateField(slot, "_displayName", "InvalidBrainEnemy");
            SetRequiredPrivateField(slot, "_playerNumber", 2);

            var encounterConfig = ScriptableObject.CreateInstance<BattleEncounterTestConfig>();
            SetRequiredPrivateField(encounterConfig, "_slots", new List<EncounterTestSlot> { slot });
            SetRequiredPrivateField(_controller, "_testEncounterConfig", encounterConfig);
            SetRequiredPrivateField(
                _controller,
                "_runtimeAiBrainLoadOverrideForTests",
                new Func<string, AiBrainAsset>(_ => brainIsMissing ? null : invalidBrain));
            string failureKind = brainIsMissing ? "not found" : "is invalid";
            LogAssert.Expect(LogType.Error, new Regex($".*AI brain {failureKind}.*Destroying AI unit.*"));

            try
            {
                InvokeRequiredPrivateMethod(_controller, "SpawnTestEncounterUnits");

                var spawnCell = GetCell(6, 4);
                Assert.That(spawnCell.CurrentUnits, Is.Empty,
                    "A direct helper failure must detach the doomed unit before deferred destruction.");
                Assert.That(spawnCell.IsTaken, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(encounterConfig);
                UnityEngine.Object.DestroyImmediate(invalidBrain);
            }
        }

        [Test]
        public void EnemySpawnFailure_PreservesPendingBuffsForTheNextBattleAttempt()
        {
            var state = CreatePartyState("first", "second", "third");
            foreach (var definition in state.Roster)
                definition.PrefabPath = $"Assets/Tactics/Tests/Fixtures/{definition.Id}.prefab";
            var pendingBuff = ScriptableObject.CreateInstance<BuffConfig>();
            SetRequiredPrivateField(pendingBuff, "_buffName", "Task7PendingBuff");
            state.Roster[0].PendingBuffs.Add(pendingBuff);
            var encounter = CreateEncounter(
                new[] { Cell(1, 1), Cell(1, 2), Cell(1, 3) },
                new[] { Cell(4, 4) });
            encounter.Units.Add(new EncounterUnitEntry
            {
                MonsterId = "invalid-enemy",
                UnitName = "InvalidEnemy",
                UnitPrefabPath = "test://invalid-enemy",
                AiBrainAssetPath = string.Empty,
                AbilityConfigPaths = new List<string>(),
                PlayerNumber = 2,
                SpawnCellX = 1,
                SpawnCellY = 1
            });
            ConfigureProduction(encounter, state);
            var releasedPaths = new List<string>();
            SetRequiredPrivateField(_controller, "_runtimeAssetReleaseOverrideForTests",
                new Action<string>(releasedPaths.Add));
            LogAssert.Expect(LogType.Error, new Regex(".*cell is blocked or occupied.*"));

            try
            {
                Assert.That(InvokeProductionPreparation(), Is.False);
                Assert.That(state.Roster[0].PendingBuffs, Has.Count.EqualTo(1));
                var loadedPaths = (HashSet<string>)GetRequiredPrivateField(_controller, "_loadedPaths");
                Assert.That(loadedPaths, Does.Not.Contain("Assets/Tactics/Tests/Fixtures/first.prefab"));
                Assert.That(loadedPaths, Does.Not.Contain("Assets/Tactics/Tests/Fixtures/second.prefab"));
                Assert.That(loadedPaths, Does.Not.Contain("Assets/Tactics/Tests/Fixtures/third.prefab"));
                Assert.That(releasedPaths, Does.Contain("Assets/Tactics/Tests/Fixtures/first.prefab"));
                Assert.That(releasedPaths, Does.Contain("Assets/Tactics/Tests/Fixtures/second.prefab"));
                Assert.That(releasedPaths, Does.Contain("Assets/Tactics/Tests/Fixtures/third.prefab"));
                Assert.That(releasedPaths, Does.Contain("test://enemy"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pendingBuff);
            }
        }

        [Test]
        public void TestMode_CorpseSlotFailure_RollsBackWholePreparationAndPreservesPreExistingUnit()
        {
            const string transactionBrainPath = "Assets/Tactics/Tests/Fixtures/TransactionBrain.asset";
            const string preExistingAssetPath = "Assets/Tactics/Tests/Fixtures/PreExisting.asset";

            var existingObject = UnityEngine.Object.Instantiate(_unitTemplate, _unitContainer.transform);
            existingObject.name = "PreExistingUnit";
            existingObject.SetActive(true);
            var existingUnit = existingObject.GetComponent<TilemapUnit>();
            existingUnit.PlayerNumber = 1;
            var existingCell = GetCell(0, 0);
            existingUnit.CurrentCell = existingCell;
            existingCell.CurrentUnits.Add(existingUnit);
            existingCell.IsTaken = true;
            ((IUnitManager)_controller).Initialize(_controller);

            var existingCorpseObject = new GameObject("PreExistingCorpse");
            existingCorpseObject.transform.SetParent(_unitContainer.transform);
            var existingCorpse = existingCorpseObject.AddComponent<Corpse>();
            var existingCorpseCell = GetCell(8, 8);
            existingCorpseCell.AddInteractable(existingCorpse);

            var partyConfig = ScriptableObject.CreateInstance<BattlePartyTestConfig>();
            var partySlot = new PartyTestSlot();
            SetRequiredPrivateField(partySlot, "_spawnCell", new Vector2Int(2, 2));
            SetRequiredPrivateField(partySlot, "_unitPrefab", _unitTemplate);
            SetRequiredPrivateField(partySlot, "_displayName", "TransactionParty");
            SetRequiredPrivateField(partyConfig, "_slots", new List<PartyTestSlot> { partySlot });

            var encounterConfig = ScriptableObject.CreateInstance<BattleEncounterTestConfig>();
            var enemySlot = new EncounterTestSlot();
            SetRequiredPrivateField(enemySlot, "_spawnCell", new Vector2Int(6, 4));
            SetRequiredPrivateField(enemySlot, "_unitPrefab", _unitTemplate);
            SetRequiredPrivateField(enemySlot, "_aiBrainAssetPath", transactionBrainPath);
            SetRequiredPrivateField(enemySlot, "_displayName", "TransactionEnemy");
            SetRequiredPrivateField(enemySlot, "_playerNumber", 2);
            var invalidCorpseSlot = new CorpseTestSlot();
            SetRequiredPrivateField(invalidCorpseSlot, "_spawnCell", new Vector2Int(99, 99));
            SetRequiredPrivateField(invalidCorpseSlot, "_unitPrefab", _unitTemplate);
            SetRequiredPrivateField(encounterConfig, "_slots", new List<EncounterTestSlot> { enemySlot });
            SetRequiredPrivateField(encounterConfig, "_corpseSlots", new List<CorpseTestSlot> { invalidCorpseSlot });

            var brain = ScriptableObject.CreateInstance<AiBrainAsset>();
            var decisionGraph = ScriptableObject.CreateInstance<AiDecisionGraph>();
            SetRequiredPrivateField(brain, "_decisionGraph", decisionGraph);
            var releasedPaths = new List<string>();
            var loadedPaths = (HashSet<string>)GetRequiredPrivateField(_controller, "_loadedPaths");
            loadedPaths.Add(preExistingAssetPath);

            SetRequiredPrivateField(_controller, "_useTestSetup", true);
            SetRequiredPrivateField(_controller, "_testPartyConfig", partyConfig);
            SetRequiredPrivateField(_controller, "_testEncounterConfig", encounterConfig);
            SetRequiredPrivateField(_controller, "_runtimeAiBrainLoadOverrideForTests",
                new Func<string, AiBrainAsset>(_ => brain));
            SetRequiredPrivateField(_controller, "_runtimeAssetReleaseOverrideForTests",
                new Action<string>(releasedPaths.Add));
            LogAssert.Expect(LogType.Error, new Regex(".*CorpseTestSlot\\[0\\].*did not map to a grid cell.*"));

            try
            {
                bool result = InvokeProductionPreparation();

                Assert.That(result, Is.False, "A later corpse-slot failure must abort the complete test-mode transaction.");
                Assert.That(SpawnedUnits(), Is.EqualTo(new[] { existingUnit }),
                    "Only the pre-existing unit may remain after transaction rollback.");
                Assert.That(existingUnit == null, Is.False);
                Assert.That(existingUnit.transform.parent, Is.EqualTo(_unitContainer.transform));
                Assert.That(existingUnit.CurrentCell, Is.SameAs(existingCell));
                Assert.That(existingCell.CurrentUnits, Is.EqualTo(new IUnit[] { existingUnit }));
                Assert.That(existingCell.IsTaken, Is.True);
                Assert.That(((IUnitManager)_controller).GetUnits(), Does.Contain(existingUnit));
                Assert.That(existingCorpse == null, Is.False,
                    "A corpse that predates the transaction must not be destroyed during rollback.");
                Assert.That(existingCorpse.transform.parent, Is.EqualTo(_unitContainer.transform));
                Assert.That(existingCorpse.CurrentCell, Is.SameAs(existingCorpseCell));
                Assert.That(existingCorpseCell.CurrentInteractables, Does.Contain(existingCorpse));
                Assert.That(existingCorpseCell.IsTaken, Is.True);
                Assert.That(GetCell(2, 2).CurrentUnits, Is.Empty);
                Assert.That(GetCell(2, 2).IsTaken, Is.False);
                Assert.That(GetCell(6, 4).CurrentUnits, Is.Empty);
                Assert.That(GetCell(6, 4).IsTaken, Is.False);
                Assert.That(GetCell(2, 2).CurrentInteractables, Is.Empty);
                Assert.That(GetCell(6, 4).CurrentInteractables, Is.Empty);
                Assert.That(loadedPaths, Does.Contain(preExistingAssetPath));
                Assert.That(loadedPaths, Does.Not.Contain(transactionBrainPath));
                Assert.That(releasedPaths, Does.Contain(transactionBrainPath));
                Assert.That(releasedPaths, Does.Not.Contain(preExistingAssetPath));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(partyConfig);
                UnityEngine.Object.DestroyImmediate(encounterConfig);
                UnityEngine.Object.DestroyImmediate(brain);
                UnityEngine.Object.DestroyImmediate(decisionGraph);
            }
        }

        [Test]
        public void TestMode_UsesBattlePartyTestConfigWithoutProductionPartyCells()
        {
            var partyConfig = ScriptableObject.CreateInstance<BattlePartyTestConfig>();
            var partySlot = new PartyTestSlot();
            SetRequiredPrivateField(partySlot, "_spawnCell", new Vector2Int(2, 2));
            SetRequiredPrivateField(partySlot, "_unitPrefab", _unitTemplate);
            SetRequiredPrivateField(partySlot, "_displayName", "ConfiguredTestParty");
            SetRequiredPrivateField(partyConfig, "_slots", new List<PartyTestSlot> { partySlot });

            var encounterConfig = ScriptableObject.CreateInstance<BattleEncounterTestConfig>();
            SetRequiredPrivateField(encounterConfig, "_slots", new List<EncounterTestSlot>());
            SetRequiredPrivateField(_controller, "_useTestSetup", true);
            SetRequiredPrivateField(_controller, "_testPartyConfig", partyConfig);
            SetRequiredPrivateField(_controller, "_testEncounterConfig", encounterConfig);
            SetRequiredPrivateField(_controller, "_encounterLoaderOverrideForTests",
                new Func<EncounterConfig>(() => throw new AssertionException(
                    "Test mode must not load a production encounter or require production party cells.")));

            bool result = InvokeProductionPreparation();

            Assert.That(result, Is.True);
            Assert.That(SpawnedUnits().Count(unit => unit.PlayerNumber == 1), Is.EqualTo(1));
            Assert.That(SpawnedUnits().Single(unit => unit.PlayerNumber == 1).CurrentCell.GridCoordinates,
                Is.EqualTo(new Vector2IntImpl(2, 2)));

            UnityEngine.Object.DestroyImmediate(partyConfig);
            UnityEngine.Object.DestroyImmediate(encounterConfig);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TestMode_MissingConfig_FailsClosedWithoutProductionFallback(bool missingParty)
        {
            var partyConfig = ScriptableObject.CreateInstance<BattlePartyTestConfig>();
            SetRequiredPrivateField(partyConfig, "_slots", new List<PartyTestSlot>());
            var encounterConfig = ScriptableObject.CreateInstance<BattleEncounterTestConfig>();
            SetRequiredPrivateField(encounterConfig, "_slots", new List<EncounterTestSlot>());

            SetRequiredPrivateField(_controller, "_useTestSetup", true);
            SetRequiredPrivateField(_controller, "_testPartyConfig", missingParty ? null : partyConfig);
            SetRequiredPrivateField(_controller, "_testEncounterConfig", missingParty ? encounterConfig : null);
            SetRequiredPrivateField(_controller, "_encounterLoaderOverrideForTests",
                new Func<EncounterConfig>(() => throw new AssertionException(
                    "Missing test config must never fall back to production.")));
            LogAssert.Expect(LogType.Error, new Regex(".*Test setup is authoritative.*config is missing.*"));

            Assert.That(InvokeProductionPreparation(), Is.False);
            Assert.That(SpawnedUnits(), Is.Empty);

            UnityEngine.Object.DestroyImmediate(partyConfig);
            UnityEngine.Object.DestroyImmediate(encounterConfig);
        }

        private void ConfigureProduction(EncounterConfig encounter, PlayerAdventureState state)
        {
            SetRequiredPrivateField(_controller, "_useTestSetup", false);
            SetRequiredPrivateField(_controller, "_partyStateLoaderOverrideForTests",
                new Func<PlayerAdventureState>(() => state));
            SetRequiredPrivateField(_controller, "_encounterLoaderOverrideForTests",
                new Func<EncounterConfig>(() => encounter));
            SetRequiredPrivateField(_controller, "_runtimePrefabLoadOverrideForTests",
                new Func<string, GameObject>(_ => _unitTemplate));
            SetRequiredPrivateField(_controller, "_rolePrefabMappings", new List<RolePrefabMapping>
            {
                new RolePrefabMapping { RoleType = RoleType.Barbarian, Prefab = _unitTemplate }
            });
        }

        private static EncounterConfig CreateEncounter(
            IEnumerable<BattleLayoutCell> partyCells,
            IEnumerable<BattleLayoutCell> blockedCells)
        {
            return new EncounterConfig
            {
                EncounterId = "task7-direct-10x10",
                PartySpawnCells = partyCells.ToList(),
                BlockedCells = blockedCells.ToList(),
                Units = new List<EncounterUnitEntry>
                {
                    new EncounterUnitEntry
                    {
                        MonsterId = "test-enemy",
                        UnitName = "TestEnemy",
                        UnitPrefabPath = "test://enemy",
                        AiBrainAssetPath = string.Empty,
                        AbilityConfigPaths = new List<string>(),
                        PlayerNumber = 2,
                        SpawnCellX = 8,
                        SpawnCellY = 8
                    }
                }
            };
        }

        private static PlayerAdventureState CreatePartyState(params string[] activeIds)
        {
            var roster = new[] { "first", "second", "third" }
                .Select(id => CharacterDefinition.CreateDefault(id, id, roleType: RoleType.Barbarian))
                .ToList();
            foreach (var character in roster)
                character.PrefabPath = null;
            return new PlayerAdventureState
            {
                Roster = roster,
                ActivePartyCharacterIds = activeIds.ToList()
            };
        }

        [Test]
        public void EnsurePlayersCoverSpawnedUnits_RepairsConfiguredHumanPlayerType()
        {
            var partyObject = new GameObject("RuntimePartyUnit");
            partyObject.transform.SetParent(_unitContainer.transform);
            var partyUnit = partyObject.AddComponent<TilemapUnit>();
            partyUnit.PlayerNumber = 1;
            SetRequiredPrivateField(_controller, "_units", new List<IUnit> { partyUnit });
            SetRequiredPrivateField(_controller, "_players", new[]
            {
                new BattleController.PlayerEntry
                {
                    PlayerNumber = 1,
                    Type = PlayerType.AutomatedPlayer
                }
            });

            InvokeRequiredPrivateMethod(_controller, "EnsurePlayersCoverSpawnedUnits");

            var players = (BattleController.PlayerEntry[])GetRequiredPrivateField(_controller, "_players");
            Assert.That(players.Single(entry => entry.PlayerNumber == 1).Type, Is.EqualTo(PlayerType.HumanPlayer));
        }

        private bool InvokeProductionPreparation()
        {
            var method = typeof(BattleController).GetMethod(
                "PrepareEncounterAndSpawnUnits",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null,
                "BattleController must expose one private preparation transaction at BeforeUnitManagerInitialize.");
            return (bool)method.Invoke(_controller, null);
        }

        private Square GetCell(int x, int y)
        {
            return _cellRoot.GetComponentsInChildren<Square>()
                .Single(cell => cell.GridCoordinates.x == x && cell.GridCoordinates.y == y);
        }

        private List<TilemapUnit> SpawnedUnits()
        {
            return _unitContainer.GetComponentsInChildren<TilemapUnit>(true).ToList();
        }

        private static BattleLayoutCell Cell(int x, int y) => new BattleLayoutCell(x, y);

        [Serializable]
        private sealed class BasicMeleeJson
        {
            public string encounterId;
            public List<BasicMeleeCellJson> partySpawnCells;
            public List<BasicMeleeUnitJson> units;
        }

        [Serializable]
        private sealed class BasicMeleeCellJson
        {
            public int X;
            public int Y;
        }

        [Serializable]
        private sealed class BasicMeleeUnitJson
        {
            public string unitName;
            public string unitPrefabPath;
            public string aiBrainAssetPath;
            public int playerNumber;
            public int spawnCellX;
            public int spawnCellY;
        }

        private static void RestoreStringPref(string key, bool existed, string value)
        {
            if (existed)
                PlayerPrefs.SetString(key, value);
            else
                PlayerPrefs.DeleteKey(key);
        }

        private static void SetRequiredPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing required private seam '{fieldName}'.");
            field.SetValue(target, value);
        }

        private static object GetRequiredPrivateField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing required private field '{fieldName}'.");
            return field.GetValue(target);
        }

        private static object InvokeRequiredPrivateMethod(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Missing required private method '{methodName}'.");
            return method.Invoke(target, null);
        }

        private sealed class RuntimeWalkabilityCellManager : RegularCellManager
        {
            public readonly HashSet<Vector2Int> NonWalkable = new();

            public override bool IsCellWalkable(ICell cell)
            {
                return cell != null && !NonWalkable.Contains(
                    new Vector2Int(cell.GridCoordinates.x, cell.GridCoordinates.y));
            }
        }
    }
}
