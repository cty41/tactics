using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Tactics.Common.Battle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Tactics.Tests.Editor
{
    public sealed class Test1BattleMapLayoutEditorTests
    {
        private const string ScenePath = "Assets/Tactics/Scenes/Test1.unity";
        private Scene _scene;
        private bool _openedForTest;

        [SetUp]
        public void SetUp()
        {
            _scene = SceneManager.GetSceneByPath(ScenePath);
            if (_scene.isLoaded)
                return;

            _scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            _openedForTest = true;
        }

        [TearDown]
        public void TearDown()
        {
            if (_openedForTest && _scene.isLoaded)
                EditorSceneManager.CloseScene(_scene, true);
        }

        [Test]
        public void Test1_UsesUnscaledGridAndTheFixedTenByTenBattlefield()
        {
            Transform grid = FindInScene("Grid").transform;
            Transform unitManager = FindInScene("UnitManager").transform;
            Tilemap background = FindInScene("BackgroundLayer").GetComponent<Tilemap>();
            Tilemap foreground = FindInScene("ForegroundLayer").GetComponent<Tilemap>();
            Tilemap highlight = FindInScene("HighlightLayer").GetComponent<Tilemap>();

            Assert.That(grid.parent, Is.Null);
            Assert.That(grid.position, Is.EqualTo(Vector3.zero));
            Assert.That(grid.localScale, Is.EqualTo(Vector3.one));
            Assert.That(unitManager.parent, Is.Null);

            AssertSquareBattlefield(background);
            Assert.That(CountTiles(background), Is.EqualTo(BattleBoardSpec.CellCount));
            AssertAllTilesStayInsideBattlefield(foreground);
            Assert.That(highlight.GetUsedTilesCount(), Is.EqualTo(0));
        }

        [Test]
        public void Test1_BattleSpawnConfigsStayInsideTheCroppedBattlefield()
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

            AssertSpawnCellsInside(party.Slots.Select(slot => slot.SpawnCell));
            AssertSpawnCellsInside(encounter.Slots.Select(slot => slot.SpawnCell));
            AssertSpawnCellsInside(corpseEncounter.Slots.Select(slot => slot.SpawnCell));
            AssertSpawnCellsInside(corpseEncounter.CorpseSlots.Select(slot => slot.SpawnCell));

            MatchCollection basicMeleeSpawns = Regex.Matches(
                basicMeleeAsset.text,
                "\"spawnCellX\"\\s*:\\s*(\\d+)\\s*,\\s*\"spawnCellY\"\\s*:\\s*(\\d+)");
            Assert.That(basicMeleeSpawns, Is.Not.Empty);
            AssertSpawnCellsInside(basicMeleeSpawns
                .Select(match => new Vector2Int(
                    int.Parse(match.Groups[1].Value),
                    int.Parse(match.Groups[2].Value))));

            foreach (string recipeId in new[] { "N1", "N2", "N3", "N4", "N5", "N6", "E1", "E2", "Special" })
            {
                var resolved = EncounterResolver.Resolve(recipeId, 1203);
                AssertSpawnCellsInside(resolved.Units.Select(unit => new Vector2Int(unit.SpawnCell.X, unit.SpawnCell.Y)));
                AssertSpawnCellsInside(resolved.Layout.BlockedCells.Select(cell => new Vector2Int(cell.X, cell.Y)));
            }
        }

        [Test]
        public void Test1_ContainsOneConfiguredCameraFitterAndReusableBackdropBehindTheMap()
        {
            Camera[] cameras = _scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                .ToArray();
            BattleBoardCameraFitter[] cameraFitters = _scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<BattleBoardCameraFitter>(true))
                .ToArray();
            BattleBackdropFitter[] backdrops = _scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<BattleBackdropFitter>(true))
                .ToArray();

            Assert.That(cameras, Has.Length.EqualTo(1));
            Assert.That(cameraFitters, Has.Length.EqualTo(1));
            Assert.That(backdrops, Has.Length.EqualTo(1));

            Camera camera = cameras[0];
            Tilemap background = FindInScene("BackgroundLayer").GetComponent<Tilemap>();
            var boardFitter = new SerializedObject(cameraFitters[0]);
            Assert.That(boardFitter.FindProperty("_targetCamera").objectReferenceValue, Is.SameAs(camera));
            Assert.That(boardFitter.FindProperty("_boardTilemap").objectReferenceValue, Is.SameAs(background));

            BattleBackdropFitter backdrop = backdrops[0];
            MeshRenderer backdropRenderer = backdrop.GetComponent<MeshRenderer>();
            TilemapRenderer mapRenderer = FindInScene("BackgroundLayer").GetComponent<TilemapRenderer>();

            Assert.That(backdropRenderer, Is.Not.Null);
            Assert.That(backdropRenderer.sharedMaterial, Is.Not.Null);
            Assert.That(backdropRenderer.sharedMaterial.shader.name, Is.EqualTo("Tactics/Battle/BackdropGradient"));
            Assert.That(mapRenderer.sharedMaterial, Is.Not.Null);
            Assert.That(
                backdropRenderer.sharedMaterial.renderQueue,
                Is.LessThan(mapRenderer.sharedMaterial.renderQueue));
            Assert.That(
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(backdrop.gameObject),
                Is.EqualTo("Assets/Tactics/Arts/Prefabs/BattleBackdrop.prefab"));
        }

        private void AssertSquareBattlefield(Tilemap tilemap)
        {
            BoundsInt bounds = tilemap.cellBounds;
            Assert.That(bounds.position, Is.EqualTo(Vector3Int.zero));
            Assert.That(bounds.size, Is.EqualTo(new Vector3Int(
                BattleBoardSpec.Width,
                BattleBoardSpec.Height,
                1)));
        }

        private void AssertAllTilesStayInsideBattlefield(Tilemap tilemap)
        {
            foreach (Vector3Int position in tilemap.cellBounds.allPositionsWithin)
            {
                if (tilemap.GetTile(position) == null)
                    continue;

                Assert.That(BattleBoardSpec.Contains(position.x, position.y), Is.True,
                    $"Tile {position} is outside the fixed board.");
            }
        }

        private void AssertSpawnCellsInside(IEnumerable<Vector2Int> cells)
        {
            foreach (Vector2Int cell in cells)
            {
                Assert.That(BattleBoardSpec.Contains(cell), Is.True,
                    $"Spawn {cell} is outside the fixed board.");
            }
        }

        private static int CountTiles(Tilemap tilemap)
        {
            int count = 0;
            foreach (Vector3Int position in tilemap.cellBounds.allPositionsWithin)
            {
                if (tilemap.GetTile(position) != null)
                    count++;
            }

            return count;
        }

        private GameObject FindInScene(string name)
        {
            foreach (GameObject root in _scene.GetRootGameObjects())
            {
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (transform.name == name)
                        return transform.gameObject;
                }
            }

            Assert.Fail($"Could not find '{name}' in {ScenePath}.");
            return null;
        }
    }
}
