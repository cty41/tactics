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
using UnityEditor;
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
            var config = CreateValidConfig();
            config.EncounterId = "broken";
            config.Units[0].UnitName = "BrokenUnit";
            config.Units[0].UnitPrefabPath = null;

            LogAssert.Expect(LogType.Error,
                new Regex(@".*\[EncounterConfigLoader\] Encounter unit #0 is missing unitPrefabPath: broken\.json.*"));
            Assert.IsFalse(EncounterConfigLoader.Validate(config, "broken.json"));
        }

        [TestCase("party")]
        [TestCase("enemy")]
        [TestCase("blocked")]
        public void Validate_ReturnsFalse_WhenAnyCellIsOutsideBattleBoard(string group)
        {
            var config = CreateValidConfig();
            const string source = "bounds.json";
            switch (group)
            {
                case "party":
                    config.PartySpawnCells[0] = new BattleLayoutCell(10, 2);
                    break;
                case "enemy":
                    config.Units[0].SpawnCellX = 10;
                    config.Units[0].SpawnCellY = 2;
                    break;
                case "blocked":
                    config.BlockedCells.Add(new BattleLayoutCell(10, 2));
                    break;
            }

            LogAssert.Expect(
                LogType.Error,
                new Regex(@".*'10,2'.*bounds\.json.*"));
            Assert.IsFalse(EncounterConfigLoader.Validate(config, source));
        }

        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(2)]
        public void Validate_ReturnsFalse_WhenPartySpawnCellsAreMissingOrFewerThanThree(int count)
        {
            var config = CreateValidConfig();
            const string source = "party-count.json";
            config.PartySpawnCells = count < 0
                ? null
                : config.PartySpawnCells.Take(count).ToList();

            LogAssert.Expect(LogType.Error, new Regex(@".*party spawn cells.*party-count\.json.*"));
            Assert.IsFalse(EncounterConfigLoader.Validate(config, source));
        }

        [TestCase("party")]
        [TestCase("enemy")]
        [TestCase("blocked")]
        public void Validate_ReturnsFalse_WhenCellGroupContainsDuplicate(string group)
        {
            var config = CreateValidConfig();
            const string source = "duplicate-group.json";
            const string coordinate = "1,4";
            switch (group)
            {
                case "party":
                    config.PartySpawnCells[1] = new BattleLayoutCell(1, 4);
                    break;
                case "enemy":
                    config.Units.Add(CreateValidUnit("EnemyDuplicate", 7, 4));
                    break;
                case "blocked":
                    config.BlockedCells.Add(new BattleLayoutCell(4, 4));
                    config.BlockedCells.Add(new BattleLayoutCell(4, 4));
                    break;
            }

            string expectedCoordinate = group == "enemy" ? "7,4" : group == "blocked" ? "4,4" : coordinate;
            LogAssert.Expect(
                LogType.Error,
                new Regex($@".*uplicate.*'{expectedCoordinate}'.*duplicate-group\.json.*"));
            Assert.IsFalse(EncounterConfigLoader.Validate(config, source));
        }

        [TestCase("party-enemy")]
        [TestCase("party-blocked")]
        [TestCase("enemy-blocked")]
        public void Validate_ReturnsFalse_WhenCellGroupsOverlap(string overlap)
        {
            var config = CreateValidConfig();
            const string source = "overlap.json";
            string coordinate;
            switch (overlap)
            {
                case "party-enemy":
                    coordinate = "1,4";
                    config.Units[0].SpawnCellX = 1;
                    config.Units[0].SpawnCellY = 4;
                    break;
                case "party-blocked":
                    coordinate = "1,4";
                    config.BlockedCells.Add(new BattleLayoutCell(1, 4));
                    break;
                default:
                    coordinate = "7,4";
                    config.BlockedCells.Add(new BattleLayoutCell(7, 4));
                    break;
            }

            LogAssert.Expect(
                LogType.Error,
                new Regex($@".*overlap.*'{coordinate}'.*overlap\.json.*"));
            Assert.IsFalse(EncounterConfigLoader.Validate(config, source));
        }

        [Test]
        public void Validate_ReturnsFalse_WhenSpawnCellsDuplicate()
        {
            var config = CreateValidConfig();
            config.EncounterId = "duplicate-spawn";
            config.Units[0].SpawnCellX = 1;
            config.Units[0].SpawnCellY = 2;
            config.Units.Add(CreateValidUnit("UnitB", 1, 2));

            LogAssert.Expect(LogType.Error,
                new Regex(@".*\[EncounterConfigLoader\] Duplicate spawn cell '1,2' in encounter: duplicate\.json.*"));
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

            Assert.AreEqual("special_open", first.Layout.LayoutId);
            Assert.AreEqual(1.8f, first.HealthMultiplier, 0.0001f);
            Assert.AreEqual(1.25f, first.OutputMultiplier, 0.0001f);
            Assert.AreEqual(1, first.Units.Count);
            Assert.That(CellKey(first.Units[0].SpawnCell), Is.EqualTo("7,4"));
            Assert.AreEqual(first.Units[0].Monster.MonsterId, second.Units[0].Monster.MonsterId);
            Assert.That(
                first.Units[0].Monster.MonsterId,
                Is.EqualTo(EncounterCatalog.EliteChargerId).Or.EqualTo(EncounterCatalog.ElitePoisonCasterId));
        }

        [TestCase("open", "6,4|7,3|7,5|8,4", "")]
        [TestCase("center_blocker", "6,3|6,6|7,4|7,5", "4,4|4,5|5,4|5,5")]
        [TestCase("split_flank", "6,2|6,7|7,2|7,7", "4,3|5,4|4,6|5,5")]
        public void EncounterCatalog_LayoutsMatchApprovedTenByTenMatrix(
            string layoutId,
            string expectedEnemyCells,
            string expectedBlockedCells)
        {
            Assert.That(EncounterCatalog.TryGetLayout(layoutId, out var layout), Is.True);
            Assert.That(
                string.Join("|", layout.PartySpawnCells.Select(CellKey)),
                Is.EqualTo("1,4|1,5|2,4"));
            Assert.That(
                string.Join("|", layout.SpawnCells.Select(CellKey)),
                Is.EqualTo(expectedEnemyCells));
            Assert.That(
                string.Join("|", layout.BlockedCells.Select(CellKey)),
                Is.EqualTo(expectedBlockedCells));
        }

        [Test]
        public void ProductionSpawnConfigs_MatchApprovedTenByTenCoordinates()
        {
            var party = AssetDatabase.LoadAssetAtPath<BattlePartyTestConfig>(
                "Assets/Tactics/ScriptableObjects/BattleTest/DefaultTestParty.asset");
            var encounter = AssetDatabase.LoadAssetAtPath<BattleEncounterTestConfig>(
                "Assets/Tactics/ScriptableObjects/BattleTest/DefaultTestEncounter.asset");
            var corpseEncounter = AssetDatabase.LoadAssetAtPath<BattleEncounterTestConfig>(
                "Assets/Tactics/ScriptableObjects/BattleTest/CorpseTestEncounter.asset");
            var basicMeleeAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Tactics/GameData/Encounters/basic_melee.json");

            Assert.That(party, Is.Not.Null);
            Assert.That(encounter, Is.Not.Null);
            Assert.That(corpseEncounter, Is.Not.Null);
            Assert.That(basicMeleeAsset, Is.Not.Null);
            Assert.That(
                string.Join("|", party.Slots.Select(slot => $"{slot.SpawnCell.x},{slot.SpawnCell.y}")),
                Is.EqualTo("1,4"));
            Assert.That(
                string.Join("|", encounter.Slots.Select(slot => $"{slot.SpawnCell.x},{slot.SpawnCell.y}")),
                Is.EqualTo("6,4|7,3"));
            Assert.That(
                string.Join("|", corpseEncounter.Slots.Select(slot => $"{slot.SpawnCell.x},{slot.SpawnCell.y}")),
                Is.EqualTo("6,4"));
            Assert.That(
                string.Join("|", corpseEncounter.CorpseSlots.Select(slot => $"{slot.SpawnCell.x},{slot.SpawnCell.y}")),
                Is.EqualTo("4,4"));

            string compactJson = Regex.Replace(basicMeleeAsset.text, @"\s+", string.Empty);
            Assert.That(
                compactJson,
                Does.Contain("\"partySpawnCells\":[{\"X\":1,\"Y\":4},{\"X\":1,\"Y\":5},{\"X\":2,\"Y\":4}]"));
            Assert.That(compactJson, Does.Contain("\"spawnCellX\":6,\"spawnCellY\":4"));
            Assert.That(compactJson, Does.Contain("\"spawnCellX\":7,\"spawnCellY\":3"));
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
                unit.PlayerNumber == 2 &&
                Math.Abs(unit.HealthMultiplier - 1.3f) < 0.0001f &&
                Math.Abs(unit.OutputMultiplier - 1.15f) < 0.0001f));
            Assert.IsTrue(EncounterConfigLoader.Validate(config, "resolved-e1"));
        }

        [Test]
        public void ResolvedEncounter_ToEncounterConfig_DeepCopiesAllLayoutCellGroups()
        {
            var layoutPartyProperty = typeof(BattleLayout).GetProperty("PartySpawnCells");
            var configPartyField = typeof(EncounterConfig).GetField("PartySpawnCells");
            Assert.That(layoutPartyProperty, Is.Not.Null, "BattleLayout.PartySpawnCells contract is missing.");
            Assert.That(configPartyField, Is.Not.Null, "EncounterConfig.PartySpawnCells contract is missing.");

            var resolved = EncounterResolver.Resolve("E1", 44);
            var config = resolved.ToEncounterConfig();
            var layoutParty = (List<BattleLayoutCell>)layoutPartyProperty.GetValue(resolved.Layout);
            var configParty = (List<BattleLayoutCell>)configPartyField.GetValue(config);

            Assert.That(configParty.Select(CellKey), Is.EqualTo(layoutParty.Select(CellKey)));
            Assert.That(config.BlockedCells.Select(CellKey), Is.EqualTo(resolved.Layout.BlockedCells.Select(CellKey)));
            Assert.That(config.Units.Select(unit => $"{unit.SpawnCellX},{unit.SpawnCellY}"),
                Is.EqualTo(resolved.Units.Select(unit => CellKey(unit.SpawnCell))));
            Assert.That(configParty.Zip(layoutParty, ReferenceEquals).Any(referenceEquals => referenceEquals), Is.False);
            Assert.That(config.BlockedCells.Zip(resolved.Layout.BlockedCells, ReferenceEquals).Any(referenceEquals => referenceEquals), Is.False);

            string originalParty = CellKey(layoutParty[0]);
            string originalBlocked = CellKey(resolved.Layout.BlockedCells[0]);
            string originalEnemy = CellKey(resolved.Units[0].SpawnCell);
            configParty[0].X = 99;
            config.BlockedCells[0].X = 99;
            config.Units[0].SpawnCellX = 99;

            Assert.That(CellKey(layoutParty[0]), Is.EqualTo(originalParty));
            Assert.That(CellKey(resolved.Layout.BlockedCells[0]), Is.EqualTo(originalBlocked));
            Assert.That(CellKey(resolved.Units[0].SpawnCell), Is.EqualTo(originalEnemy));
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

        private static string CellKey(BattleLayoutCell cell)
        {
            return $"{cell.X},{cell.Y}";
        }

        private static EncounterConfig CreateValidConfig()
        {
            var config = new EncounterConfig
            {
                EncounterId = "valid",
                PartySpawnCells = new List<BattleLayoutCell>
                {
                    new BattleLayoutCell(1, 4),
                    new BattleLayoutCell(1, 5),
                    new BattleLayoutCell(2, 4)
                }
            };
            config.Units.Add(CreateValidUnit("Enemy", 7, 4));
            return config;
        }

        private static EncounterUnitEntry CreateValidUnit(string unitName, int x, int y)
        {
            return new EncounterUnitEntry
            {
                UnitName = unitName,
                UnitPrefabPath = "Assets/Tactics/Arts/Prefabs/Units/HunterBlue.prefab",
                AiBrainAssetPath = "Assets/Tactics/AI/BasicMeleeBrain.asset",
                SpawnCellX = x,
                SpawnCellY = y
            };
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
