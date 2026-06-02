using NUnit.Framework;
using System.Text.RegularExpressions;
using Tactics.Cells;
using Tactics.Common.Battle;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Controllers.TurnResolvers;
using Tactics.Common.Players;
using Tactics.Common.Units;
using Tactics.Common.Utilities;
using Tactics.RoguelikeMap;
using UnityEngine;
using UnityEngine.TestTools;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Tactics.Tests.Editor
{
    public class EncounterConfigTests
    {
        [Test]
        public void DefaultEncounterPath_ReturnsConfiguredPath_ForEnemyNodes()
        {
            Assert.AreEqual(EncounterConfigLoader.DefaultMinorEnemyEncounterPath,
                EncounterConfigLoader.GetDefaultEncounterPath(RoguelikeNodeType.MinorEnemy));
            Assert.AreEqual(EncounterConfigLoader.DefaultMinorEnemyEncounterPath,
                EncounterConfigLoader.GetDefaultEncounterPath(RoguelikeNodeType.EliteEnemy));
            Assert.AreEqual(EncounterConfigLoader.DefaultMinorEnemyEncounterPath,
                EncounterConfigLoader.GetDefaultEncounterPath(RoguelikeNodeType.Boss));
            Assert.AreEqual(string.Empty,
                EncounterConfigLoader.GetDefaultEncounterPath(RoguelikeNodeType.Treasure));
        }

        [Test]
        public void NormalizeEncounterPath_ExpandsShortName()
        {
            Assert.AreEqual("Assets/Tactics/GameData/Encounters/basic_melee.json",
                EncounterConfigLoader.NormalizeEncounterPath("basic_melee"));
        }

        [Test]
        public void Validate_ReturnsFalse_WhenUnitPrefabPathMissing()
        {
            var config = new EncounterConfig
            {
                EncounterId = "broken"
            };
            config.Units.Add(new EncounterUnitEntry
            {
                UnitName = "BrokenUnit"
            });

            LogAssert.Expect(LogType.Error,
                new Regex(@"\[EncounterConfigLoader\] Encounter unit #0 is missing unitPrefabPath: broken\.json"));
            Assert.IsFalse(EncounterConfigLoader.Validate(config, "broken.json"));
        }

        [Test]
        public void Validate_ReturnsFalse_WhenSpawnCellsDuplicate()
        {
            var config = new EncounterConfig
            {
                EncounterId = "duplicate-spawn"
            };
            config.Units.Add(new EncounterUnitEntry
            {
                UnitName = "UnitA",
                UnitPrefabPath = "Assets/Tactics/Arts/Prefabs/Units/HunterBlue.prefab",
                SpawnCellX = 1,
                SpawnCellY = 2
            });
            config.Units.Add(new EncounterUnitEntry
            {
                UnitName = "UnitB",
                UnitPrefabPath = "Assets/Tactics/Arts/Prefabs/Units/Infantry Blue.prefab",
                SpawnCellX = 1,
                SpawnCellY = 2
            });

            LogAssert.Expect(LogType.Error,
                new Regex(@"\[EncounterConfigLoader\] Duplicate spawn cell '1,2' in encounter: duplicate\.json"));
            Assert.IsFalse(EncounterConfigLoader.Validate(config, "duplicate.json"));
        }

        [Test]
        public void Generator_AssignsDefaultEncounterPath_ToEnemyNodes()
        {
            var config = ScriptableObject.CreateInstance<RoguelikeMapConfig>();
            config.gridColumns = 2;
            config.gridRows = 2;
            config.maxReachableDistance = 999f;
            config.randomNodes.Clear();
            config.randomNodes.Add(RoguelikeNodeType.MinorEnemy);

            var map = RoguelikeMapGenerator.GetMap(config);
            Assert.IsNotNull(map);

            foreach (var node in map.nodes)
            {
                if (node.nodeType == RoguelikeNodeType.MinorEnemy ||
                    node.nodeType == RoguelikeNodeType.EliteEnemy ||
                    node.nodeType == RoguelikeNodeType.Boss)
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(node.encounterConfigPath),
                        $"Expected encounter path on node {node.nodeId} ({node.nodeType})");
                }
            }
        }

        [Test]
        public void TilemapCellManager_GetCellAt_ReturnsNull_BeforeInitialize()
        {
            var go = new GameObject("CellManagerTest");
            try
            {
                var manager = go.AddComponent<TilemapCellManager>();
                Assert.IsNull(manager.GetCellAt(new Tactics.Common.Utilities.Vector2IntImpl(0, 0)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void GridController_InvokesPreUnitHook_AfterCellInit_BeforeUnitInit()
        {
            var order = new List<string>();
            var controller = new GridController();
            var cellManager = new FakeCellManager(order);
            var unitManager = new FakeUnitManager(order);
            var playerManager = new FakePlayerManager(order);

            controller.CellManager = cellManager;
            controller.UnitManager = unitManager;
            controller.PlayerManager = playerManager;
            controller.BeforeUnitManagerInitialize = _ => order.Add("hook");

            controller.InitializeGame();

            CollectionAssert.AreEqual(
                new[] { "cell", "hook", "unit", "player" },
                order);
        }

        private sealed class FakeCellManager : ICellManager
        {
            private readonly List<string> _order;

            public FakeCellManager(List<string> order)
            {
                _order = order;
            }

            public event Action<ICell> CellAdded;
            public event Action<ICell> CellRemoved;

            public void Initialize(IGridController gridController)
            {
                _order.Add("cell");
            }

            public IEnumerable<ICell> GetCells()
            {
                return Array.Empty<ICell>();
            }

            public ICell GetCellAt(Vector2IntImpl gridCoordinates)
            {
                return null;
            }

            public Task UnMark(IEnumerable<ICell> cells) => Task.CompletedTask;
            public Task UnMark(ICell cell) => Task.CompletedTask;
            public Task MarkAsHighlighted(ICell cell) => Task.CompletedTask;
            public Task UnMarkAsHighlighted(ICell cell) => Task.CompletedTask;
            public Task MarkAsReachable(IEnumerable<ICell> cells) => Task.CompletedTask;
            public Task MarkAsReachable(ICell cell) => Task.CompletedTask;
            public Task MarkAsPath(IEnumerable<ICell> cells, ICell originCell) => Task.CompletedTask;
            public Task MarkAsAoE(IEnumerable<ICell> cells) => Task.CompletedTask;
            public void SetColor(ICell cell, float r, float g, float b, float a) { }
            public bool IsCellWalkable(ICell cell) => true;
        }

        private sealed class FakeUnitManager : IUnitManager
        {
            private readonly List<string> _order;

            public FakeUnitManager(List<string> order)
            {
                _order = order;
            }

            public event Action<IUnit> UnitAdded;
            public event Action<IUnit> UnitRemoved;

            public void Initialize(IGridController gridController)
            {
                _order.Add("unit");
            }

            public IEnumerable<IUnit> GetUnits() => Array.Empty<IUnit>();
            public IEnumerable<IUnit> GetFriendlyUnits(IPlayer player) => Array.Empty<IUnit>();
            public IEnumerable<IUnit> GetFriendlyUnits(int playerNumber) => Array.Empty<IUnit>();
            public IEnumerable<IUnit> GetEnemyUnits(IPlayer player) => Array.Empty<IUnit>();
            public IEnumerable<IUnit> GetEnemyUnits(int playerNumber) => Array.Empty<IUnit>();
            public void AddUnit(IUnit unit) { }
            public void RemoveUnit(IUnit unit) { }
            public Task UnMark(IEnumerable<IUnit> units) => Task.CompletedTask;
            public Task MarkAsSelected(IUnit unit) => Task.CompletedTask;
            public Task MarkAsFriendly(IEnumerable<IUnit> units) => Task.CompletedTask;
            public Task MarkAsFinished(IEnumerable<IUnit> units) => Task.CompletedTask;
            public Task MarkAsTargetable(IEnumerable<IUnit> units) => Task.CompletedTask;
            public Task MarkAsAttacking(IUnit unit, IUnit target) => Task.CompletedTask;
            public Task MarkAsDefending(IUnit unit, IUnit aggressor) => Task.CompletedTask;
            public Task MarkAsMoving(IUnit unit, ICell source, ICell destination, IEnumerable<ICell> path) => Task.CompletedTask;
            public Task UnMarkAsMoving(IUnit unit, ICell source, ICell destination, IEnumerable<ICell> path) => Task.CompletedTask;
            public Task MarkAsDestroyed(IUnit unit) => Task.CompletedTask;
        }

        private sealed class FakePlayerManager : IPlayerManager
        {
            private readonly List<string> _order;

            public FakePlayerManager(List<string> order)
            {
                _order = order;
            }

            public void Initialize(GridController gridController)
            {
                _order.Add("player");
            }

            public IEnumerable<IPlayer> GetPlayers() => Array.Empty<IPlayer>();
            public IPlayer GetPlayerByNumber(int playerNumber) => null;
        }
    }
}
