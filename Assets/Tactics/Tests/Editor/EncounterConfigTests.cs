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
using UnityEngine;
using Tactics.RoguelikeMap;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;

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
                AiBrainAssetPath = "Assets/Tactics/AI/BasicMeleeBrain.asset",
                SpawnCellX = 1,
                SpawnCellY = 2
            });
            config.Units.Add(new EncounterUnitEntry
            {
                UnitName = "UnitB",
                UnitPrefabPath = "Assets/Tactics/Arts/Prefabs/Units/Infantry Blue.prefab",
                AiBrainAssetPath = "Assets/Tactics/AI/BasicMeleeBrain.asset",
                SpawnCellX = 1,
                SpawnCellY = 2
            });

            LogAssert.Expect(LogType.Error,
                new Regex(@"\[EncounterConfigLoader\] Duplicate spawn cell '1,2' in encounter: duplicate\.json"));
            Assert.IsFalse(EncounterConfigLoader.Validate(config, "duplicate.json"));
        }

        [TestCase("N1", "open", "charger:2,ranged:1")]
        [TestCase("N2", "open", "ranged:2,support:1")]
        [TestCase("N3", "center_blocker", "aoe:1,charger:2,support:1")]
        [TestCase("N4", "split_flank", "aoe:1,charger:1,ranged:2")]
        [TestCase("N5", "center_blocker", "aoe:1,charger:1,support:2")]
        [TestCase("N6", "split_flank", "aoe:1,charger:2,ranged:1")]
        public void Resolve_NormalRecipe_UsesAuthoredCompositionAndLayout(
            string recipeId,
            string expectedLayoutId,
            string expectedComposition)
        {
            var resolved = EncounterResolver.Resolve(recipeId, 12345);

            Assert.AreEqual(expectedLayoutId, resolved.Layout.LayoutId);
            Assert.AreEqual(1f, resolved.HealthMultiplier);
            Assert.AreEqual(1f, resolved.OutputMultiplier);
            Assert.AreEqual(expectedComposition, FormatComposition(resolved));
        }

        [TestCase("E1", "center_blocker")]
        [TestCase("E2", "split_flank")]
        public void Resolve_EliteRecipe_AppliesExplicitMultipliers(string recipeId, string expectedLayoutId)
        {
            var resolved = EncounterResolver.Resolve(recipeId, 9);

            Assert.AreEqual(expectedLayoutId, resolved.Layout.LayoutId);
            Assert.AreEqual(1.3f, resolved.HealthMultiplier, 0.0001f);
            Assert.AreEqual(1.15f, resolved.OutputMultiplier, 0.0001f);
            Assert.That(resolved.Units, Has.All.Matches<ResolvedEncounterUnit>(unit =>
                Math.Abs(unit.HealthMultiplier - 1.3f) < 0.0001f &&
                Math.Abs(unit.OutputMultiplier - 1.15f) < 0.0001f));
        }

        [Test]
        public void Resolve_SpecialRecipe_IsStableForSeedAndUsesSpecialMultipliers()
        {
            var first = EncounterResolver.Resolve("Special", 7788);
            var second = EncounterResolver.Resolve("Special", 7788);

            Assert.AreEqual("open", first.Layout.LayoutId);
            Assert.AreEqual(1.8f, first.HealthMultiplier, 0.0001f);
            Assert.AreEqual(1.25f, first.OutputMultiplier, 0.0001f);
            Assert.AreEqual(1, first.Units.Count);
            Assert.AreEqual(first.Units[0].Monster.MonsterId, second.Units[0].Monster.MonsterId);
            Assert.That(
                first.Units[0].Monster.MonsterId,
                Is.EqualTo(EncounterCatalog.EliteChargerId).Or.EqualTo(EncounterCatalog.ElitePoisonCasterId));
        }

        [Test]
        public void ResolvedEncounter_ToEncounterConfig_PreservesRecipeDataForLegacySpawner()
        {
            var resolved = EncounterResolver.Resolve("E1", 44);

            var config = resolved.ToEncounterConfig();

            Assert.AreEqual("E1", config.EncounterId);
            Assert.AreEqual("E1", config.RecipeId);
            Assert.AreEqual(44, config.RunSeed);
            Assert.AreEqual(resolved.Units.Count, config.Units.Count);
            Assert.That(config.Units, Has.All.Matches<EncounterUnitEntry>(unit =>
                !string.IsNullOrWhiteSpace(unit.MonsterId) &&
                !string.IsNullOrWhiteSpace(unit.UnitPrefabPath) &&
                !string.IsNullOrWhiteSpace(unit.AiBrainAssetPath) &&
                unit.AbilityConfigPaths != null &&
                unit.AbilityConfigPaths.Count >= 2 &&
                Math.Abs(unit.HealthMultiplier - 1.3f) < 0.0001f &&
                Math.Abs(unit.OutputMultiplier - 1.15f) < 0.0001f));
            Assert.IsTrue(EncounterConfigLoader.Validate(config, "resolved-e1"));
        }

        [Test]
        public void EncounterRuntime_DoesNotExposeThreatValue()
        {
            Assert.IsNull(typeof(EncounterRecipe).GetProperty("ThreatValue"));
            Assert.IsNull(typeof(ResolvedEncounter).GetProperty("ThreatValue"));
            Assert.IsNull(typeof(EncounterUnitEntry).GetField("ThreatValue"));
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

        [TestCase(0, 0, 0.10f, 0.10f)]
        [TestCase(0, 0, 0.75f, 0.75f)]
        [TestCase(-3, 2, 0.25f, 0.80f)]
        [TestCase(4, -2, 0.90f, 0.20f)]
        public void TilemapCellGeometry_WorldToCell_PreservesIsometricCellBoundaries(
            int cellX,
            int cellY,
            float offsetX,
            float offsetY)
        {
            var gridObject = new GameObject("Grid");
            var tilemapObject = new GameObject("Tilemap");

            try
            {
                var grid = gridObject.AddComponent<Grid>();
                grid.cellLayout = GridLayout.CellLayout.Isometric;
                grid.cellSize = new Vector3(1f, 0.5f, 1f);

                tilemapObject.transform.SetParent(gridObject.transform, false);
                var tilemap = tilemapObject.AddComponent<Tilemap>();

                Vector3 interpolatedCell = new Vector3(cellX + offsetX, cellY + offsetY, 0f);
                Vector3 worldPosition = tilemap.transform.TransformPoint(
                    tilemap.CellToLocalInterpolated(interpolatedCell));

                Vector3Int result = TilemapCellGeometry.WorldToCell(tilemap, worldPosition);

                Assert.That(result, Is.EqualTo(new Vector3Int(cellX, cellY, 0)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tilemapObject);
                UnityEngine.Object.DestroyImmediate(gridObject);
            }
        }

        [Test]
        public void TilemapCellGeometry_GroundCenterAndDiamondRemainAlignedUnderTransform()
        {
            var gridObject = new GameObject("Grid");
            var tilemapObject = new GameObject("Tilemap");

            try
            {
                gridObject.transform.SetPositionAndRotation(
                    new Vector3(3.25f, -2.5f, 0f),
                    Quaternion.Euler(0f, 0f, 11f));
                var grid = gridObject.AddComponent<Grid>();
                grid.cellLayout = GridLayout.CellLayout.Isometric;
                grid.cellSize = new Vector3(1f, 0.5f, 1f);

                tilemapObject.transform.SetParent(gridObject.transform, false);
                var tilemap = tilemapObject.AddComponent<Tilemap>();
                var coordinates = new Vector3Int(2, -3, 0);

                Vector3 center = TilemapCellGeometry.GetGroundCenterWorld(tilemap, coordinates);
                Assert.That(center, Is.EqualTo(tilemap.GetCellCenterWorld(coordinates)));
                Assert.That(TilemapCellGeometry.WorldToCell(tilemap, center), Is.EqualTo(coordinates));

                TilemapCellGeometry.GetCellBasisWorld(
                    tilemap,
                    coordinates,
                    out Vector3 xAxis,
                    out Vector3 yAxis);
                TilemapCellGeometry.GetDiamondVerticesWorld(
                    tilemap,
                    coordinates,
                    out Vector3 top,
                    out Vector3 right,
                    out Vector3 bottom,
                    out Vector3 left);

                AssertVectorApproximately((top + right + bottom + left) * 0.25f, center);
                AssertVectorApproximately(top - center, (xAxis + yAxis) * 0.5f);
                AssertVectorApproximately(right - center, (xAxis - yAxis) * 0.5f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tilemapObject);
                UnityEngine.Object.DestroyImmediate(gridObject);
            }
        }

        [Test]
        public void ProceduralTileHighlightRenderer_MeshUsesCanonicalGroundCenter()
        {
            var gridObject = new GameObject("Grid");
            var tilemapObject = new GameObject("Tilemap");
            var rendererObject = new GameObject("HighlightRenderer");

            try
            {
                var grid = gridObject.AddComponent<Grid>();
                grid.cellLayout = GridLayout.CellLayout.Isometric;
                grid.cellSize = new Vector3(1f, 0.5f, 1f);
                tilemapObject.transform.SetParent(gridObject.transform, false);
                var tilemap = tilemapObject.AddComponent<Tilemap>();

                rendererObject.transform.position = new Vector3(1.25f, -0.75f, 0f);
                var highlightRenderer = rendererObject.AddComponent<ProceduralTileHighlightRenderer>();
                typeof(ProceduralTileHighlightRenderer)
                    .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(highlightRenderer, null);
                highlightRenderer.SetDataLayer(tilemap);

                var coordinates = new Vector2IntImpl(2, 3);
                Vector3 groundCenter = TilemapCellGeometry.GetGroundCenterWorld(
                    tilemap,
                    new Vector3Int(coordinates.x, coordinates.y, 0));
                var cell = new VirtualSquareCell(
                    coordinates,
                    groundCenter.ToIVector3(),
                    1,
                    false,
                    null);

                highlightRenderer.AddHighlights(new[] { cell }, TileHighlightType.Highlighted);

                Mesh mesh = rendererObject.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(mesh, Is.Not.Null);
                Assert.That(mesh.vertexCount, Is.EqualTo(4));
                AssertVectorApproximately(
                    rendererObject.transform.TransformPoint(mesh.bounds.center),
                    groundCenter);

                TilemapCellGeometry.GetDiamondVerticesWorld(
                    tilemap,
                    new Vector3Int(coordinates.x, coordinates.y, 0),
                    out Vector3 top,
                    out Vector3 right,
                    out Vector3 bottom,
                    out Vector3 left);
                Vector3[] vertices = mesh.vertices;
                AssertVectorApproximately(rendererObject.transform.TransformPoint(vertices[0]), top);
                AssertVectorApproximately(rendererObject.transform.TransformPoint(vertices[1]), right);
                AssertVectorApproximately(rendererObject.transform.TransformPoint(vertices[2]), bottom);
                AssertVectorApproximately(rendererObject.transform.TransformPoint(vertices[3]), left);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rendererObject);
                UnityEngine.Object.DestroyImmediate(tilemapObject);
                UnityEngine.Object.DestroyImmediate(gridObject);
            }
        }

        [Test]
        public void ProceduralTileHighlightRenderer_UnitStateMeshFollowsWorldAnchorWithoutChangingStaticMesh()
        {
            var gridObject = new GameObject("Grid");
            var tilemapObject = new GameObject("Tilemap");
            var rendererObject = new GameObject("HighlightRenderer");
            var unitRoot = new GameObject("UnitRoot");

            try
            {
                var grid = gridObject.AddComponent<Grid>();
                grid.cellLayout = GridLayout.CellLayout.Isometric;
                grid.cellSize = new Vector3(1f, 0.5f, 1f);
                tilemapObject.transform.SetParent(gridObject.transform, false);
                var tilemap = tilemapObject.AddComponent<Tilemap>();

                var highlightRenderer = rendererObject.AddComponent<ProceduralTileHighlightRenderer>();
                typeof(ProceduralTileHighlightRenderer)
                    .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(highlightRenderer, null);
                highlightRenderer.SetDataLayer(tilemap);

                Vector3 staticCenter = TilemapCellGeometry.GetGroundCenterWorld(tilemap, Vector3Int.zero);
                var staticCell = new VirtualSquareCell(
                    new Vector2IntImpl(0, 0),
                    staticCenter.ToIVector3(),
                    1,
                    false,
                    null);
                highlightRenderer.AddHighlights(new[] { staticCell }, TileHighlightType.Highlighted);
                Mesh staticMesh = rendererObject.GetComponent<MeshFilter>().sharedMesh;
                Vector3[] staticVertices = staticMesh.vertices;

                unitRoot.transform.position = staticCenter + new Vector3(0.37f, 0.11f, 0f);
                highlightRenderer.SetUnitStateHighlight(
                    unitRoot.GetInstanceID(),
                    unitRoot.transform,
                    TileHighlightType.UnitSelected);

                Mesh unitStateMesh = highlightRenderer.UnitStateHighlightMesh;
                Assert.That(unitStateMesh.vertexCount, Is.EqualTo(4));
                AssertVectorApproximately(
                    rendererObject.transform.TransformPoint(unitStateMesh.bounds.center),
                    unitRoot.transform.position);

                TilemapCellGeometry.GetCellBasisWorld(
                    tilemap,
                    Vector3Int.zero,
                    out Vector3 xAxis,
                    out Vector3 yAxis);
                Vector3[] dynamicVertices = unitStateMesh.vertices;
                AssertVectorApproximately(
                    rendererObject.transform.TransformPoint(dynamicVertices[0]),
                    unitRoot.transform.position + (xAxis + yAxis) * 0.5f);

                unitRoot.transform.position += new Vector3(0.18f, -0.09f, 0f);
                typeof(ProceduralTileHighlightRenderer)
                    .GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(highlightRenderer, null);
                AssertVectorApproximately(
                    rendererObject.transform.TransformPoint(unitStateMesh.bounds.center),
                    unitRoot.transform.position);
                CollectionAssert.AreEqual(staticVertices, staticMesh.vertices);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(unitRoot);
                UnityEngine.Object.DestroyImmediate(rendererObject);
                UnityEngine.Object.DestroyImmediate(tilemapObject);
                UnityEngine.Object.DestroyImmediate(gridObject);
            }
        }

        private static void AssertVectorApproximately(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.0001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.0001f));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.0001f));
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

#pragma warning disable CS0067 // Events are never used — interface stubs
            public event Action<ICell> CellAdded;
            public event Action<ICell> CellRemoved;
#pragma warning restore CS0067

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
            public Task MarkAsGuidance(IEnumerable<ICell> cells, CellGuidanceType guidanceType) => Task.CompletedTask;
            public Task UnMarkGuidance(IEnumerable<ICell> cells, CellGuidanceType guidanceType) => Task.CompletedTask;
            public void SetColor(ICell cell, float r, float g, float b, float a) { }
            public bool IsCellWalkable(ICell cell) => true;
        }

        private static string FormatComposition(ResolvedEncounter resolved)
        {
            return string.Join(",", resolved.Units
                .GroupBy(unit => unit.Monster.MonsterId)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => $"{group.Key}:{group.Count()}"));
        }

        private sealed class FakeUnitManager : IUnitManager
        {
            private readonly List<string> _order;

            public FakeUnitManager(List<string> order)
            {
                _order = order;
            }

#pragma warning disable CS0067 // Events are never used — interface stubs
            public event Action<IUnit> UnitAdded;
            public event Action<IUnit> UnitRemoved;
#pragma warning restore CS0067

            public Transform ContainerTransform => null;

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
