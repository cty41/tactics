using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Tactics.Cells;
using Tactics.Common.Battle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Tactics.Tests.Editor
{
    public sealed class Probe10x10BattleMapLayoutEditorTests
    {
        private const string ProbeScenePath = "Assets/Tactics/Scenes/Test1_10x10_Probe.unity";
        private const string Test1ScenePath = "Assets/Tactics/Scenes/Test1.unity";
        private const string PartyPath = "Assets/Tactics/ScriptableObjects/BattleTest/Probe10x10Party.asset";
        private const string EncounterPath = "Assets/Tactics/ScriptableObjects/BattleTest/Probe10x10Encounter.asset";

        [Test]
        public void ProbeScene_HasExactBoardGridAndIsolatedBackgroundLogic()
        {
            WithProbeScene(scene =>
            {
                Tilemap[] tilemaps = ComponentsInScene<Tilemap>(scene);
                Tilemap[] logicalBackgrounds = tilemaps.Where(tilemap =>
                    tilemap.name == "BackgroundLayer" || tilemap.gameObject.tag == "Background").ToArray();
                Assert.That(logicalBackgrounds, Has.Length.EqualTo(1));

                Tilemap background = logicalBackgrounds[0];
                Assert.That(background.name, Is.EqualTo("BackgroundLayer"));
                Assert.That(background.cellBounds.position, Is.EqualTo(Vector3Int.zero));
                Assert.That(background.cellBounds.size, Is.EqualTo(new Vector3Int(10, 10, 1)));
                Assert.That(CountTiles(background), Is.EqualTo(100));
                for (int y = 0; y < 10; y++)
                for (int x = 0; x < 10; x++)
                    Assert.That(background.HasTile(new Vector3Int(x, y, 0)), Is.True, $"Missing tile ({x},{y}).");

                Grid grid = ComponentsInScene<Grid>(scene).Single(component => component.name == "Grid");
                Assert.That(grid.transform.parent, Is.Null);
                Assert.That(grid.transform.position, Is.EqualTo(Vector3.zero));
                Assert.That(grid.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(grid.cellSize, Is.EqualTo(new Vector3(1f, 0.5f, 1f)));

                Assert.That(tilemaps.Where(tilemap => tilemap != background),
                    Has.None.Matches<Tilemap>(tilemap => tilemap.name == "BackgroundLayer" || tilemap.gameObject.tag == "Background"));
            });
        }

        [Test]
        public void ProbeScene_HasExactlyOneConfiguredCameraAndBackdropFitter()
        {
            WithProbeScene(scene =>
            {
                Tilemap[] backgrounds = ComponentsInScene<Tilemap>(scene)
                    .Where(tilemap => tilemap.name == "BackgroundLayer").ToArray();
                TilemapCellManager[] cellManagers = ComponentsInScene<TilemapCellManager>(scene);
                Camera[] cameras = ComponentsInScene<Camera>(scene);
                BattleBoardCameraFitter[] boardFitters = ComponentsInScene<BattleBoardCameraFitter>(scene);
                BattleBackdropFitter[] backdropFitters = ComponentsInScene<BattleBackdropFitter>(scene);

                Assert.That(backgrounds, Has.Length.EqualTo(1));
                Assert.That(cellManagers, Has.Length.EqualTo(1));
                Assert.That(cameras, Has.Length.EqualTo(1));
                Assert.That(boardFitters, Has.Length.EqualTo(1));
                Assert.That(backdropFitters, Has.Length.EqualTo(1));

                Tilemap background = backgrounds[0];
                Camera camera = cameras[0];
                Assert.That(camera.gameObject.scene, Is.EqualTo(scene));

                var cellManager = new SerializedObject(cellManagers[0]);
                Assert.That(RequiredProperty(cellManager, "_gridLayer").objectReferenceValue, Is.SameAs(background));

                var board = new SerializedObject(boardFitters[0]);
                Assert.That(RequiredProperty(board, "_targetCamera").objectReferenceValue, Is.SameAs(camera));
                Assert.That(RequiredProperty(board, "_boardTilemap").objectReferenceValue, Is.SameAs(background));

                var backdrop = new SerializedObject(backdropFitters[0]);
                Assert.That(RequiredProperty(backdrop, "_targetCamera").objectReferenceValue, Is.SameAs(camera));

                var backdropRenderer = RequiredProperty(backdrop, "_meshRenderer").objectReferenceValue as MeshRenderer;
                Assert.That(backdropRenderer, Is.Not.Null);
                Assert.That(backdropRenderer.gameObject, Is.SameAs(backdropFitters[0].gameObject));
                Assert.That(backdropRenderer.gameObject.scene, Is.EqualTo(scene));
            });
        }

        [Test]
        public void ProbeAssets_HaveExactIsolatedSpawnCoordinatesAndValidReferences()
        {
            var party = AssetDatabase.LoadAssetAtPath<BattlePartyTestConfig>(PartyPath);
            var encounter = AssetDatabase.LoadAssetAtPath<BattleEncounterTestConfig>(EncounterPath);
            Assert.That(party, Is.Not.Null, $"Missing {PartyPath}");
            Assert.That(encounter, Is.Not.Null, $"Missing {EncounterPath}");

            CollectionAssert.AreEqual(new[] { new Vector2Int(1, 4), new Vector2Int(1, 5), new Vector2Int(2, 4) },
                party.Slots.Select(slot => slot.SpawnCell).ToArray());
            Assert.That(party.Slots, Has.All.Matches<PartyTestSlot>(slot => slot.UnitPrefab != null));

            CollectionAssert.AreEqual(new[] { new Vector2Int(6, 4), new Vector2Int(7, 3), new Vector2Int(7, 5), new Vector2Int(8, 4) },
                encounter.Slots.Select(slot => slot.SpawnCell).ToArray());
            Assert.That(encounter.Slots, Has.All.Matches<EncounterTestSlot>(slot => slot.UnitPrefab != null));
            Assert.That(encounter.Slots.Select(slot => slot.PlayerNumber), Is.All.EqualTo(2));
        }

        [Test]
        public void ProbeScene_UsesProbeConfigsWithoutEnteringBuildSettingsOrReplacingTest1()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(Test1ScenePath), Is.Not.Null);
            Assert.That(ProbeScenePath, Is.Not.EqualTo(Test1ScenePath));
            Assert.That(EditorBuildSettings.scenes.Select(scene => scene.path), Does.Not.Contain(ProbeScenePath));

            WithProbeScene(scene =>
            {
                Component[] controllers = ComponentsInScene<Component>(scene)
                    .Where(component => component.GetType().FullName == "Tactics.Common.Battle.BattleController")
                    .ToArray();
                Assert.That(controllers, Has.Length.EqualTo(1));
                var serialized = new SerializedObject(controllers[0]);
                Assert.That(RequiredProperty(serialized, "_useTestSetup").boolValue, Is.True);
                Assert.That(AssetDatabase.GetAssetPath(RequiredProperty(serialized, "_testPartyConfig").objectReferenceValue), Is.EqualTo(PartyPath));
                Assert.That(AssetDatabase.GetAssetPath(RequiredProperty(serialized, "_testEncounterConfig").objectReferenceValue), Is.EqualTo(EncounterPath));
            });
        }

        [TestCaseSource(nameof(LayoutContractCases))]
        public void LayoutContract_HasExactLegalConnectedNonDeadEndSpawns_AndAuditableOpenRangeProxy(
            string layoutName,
            Vector2Int[] expectedEnemies,
            Vector2Int[] expectedBlockers)
        {
            LayoutData layout;
            if (!LayoutDataByName.TryGetValue(layoutName, out layout))
                Assert.Fail($"missing layout data: {layoutName}");

            CollectionAssert.AreEquivalent(FixedParty, layout.Party, $"{layoutName}: exact party set drifted.");
            CollectionAssert.AreEquivalent(expectedEnemies, layout.Enemies, $"{layoutName}: exact enemy set drifted.");
            CollectionAssert.AreEquivalent(expectedBlockers, layout.Blockers, $"{layoutName}: exact blocker set drifted.");

            AssertUnique(layoutName, "party", layout.Party);
            AssertUnique(layoutName, "enemy", layout.Enemies);
            AssertUnique(layoutName, "blocker", layout.Blockers);
            AssertDisjoint(layoutName, "party", layout.Party, "enemy", layout.Enemies);
            AssertDisjoint(layoutName, "party", layout.Party, "blocker", layout.Blockers);
            AssertDisjoint(layoutName, "enemy", layout.Enemies, "blocker", layout.Blockers);

            Vector2Int[] allSpawns = layout.Party.Concat(layout.Enemies).ToArray();
            foreach (Vector2Int cell in layout.Party.Concat(layout.Enemies).Concat(layout.Blockers))
                Assert.That(BattleBoardSpec.Contains(cell), Is.True, $"{layoutName}: {cell} is outside the 0..9 board.");
            foreach (Vector2Int spawn in allSpawns)
            {
                Assert.That(layout.Blockers.Contains(spawn), Is.False, $"{layoutName}: spawn {spawn} is blocked.");
                Assert.That(WalkableDegree(spawn, layout.Blockers), Is.GreaterThanOrEqualTo(2),
                    $"{layoutName}: spawn {spawn} has fewer than two walkable four-way neighbors (dead-end contract).");
            }

            var walkableCells = new HashSet<Vector2Int>(
                Enumerable.Range(0, BattleBoardSpec.Width)
                    .SelectMany(x => Enumerable.Range(0, BattleBoardSpec.Height).Select(y => new Vector2Int(x, y)))
                    .Except(layout.Blockers));
            Vector2Int origin = allSpawns.Length > 0 ? allSpawns[0] : walkableCells.First();
            HashSet<Vector2Int> reachable = ReachableFourWay(origin, layout.Blockers);
            Assert.That(allSpawns.All(reachable.Contains), Is.True,
                $"{layoutName}: every party/enemy spawn must be reachable from {origin}; " +
                $"unreachable spawns=[{string.Join(",", allSpawns.Where(spawn => !reachable.Contains(spawn)))}].");
            Assert.That(reachable.SetEquals(walkableCells), Is.True,
                $"{layoutName}: all non-blocker cells must form one connected battlefield component from {origin}; " +
                $"reachable={reachable.Count}, walkable={walkableCells.Count}, " +
                $"unreachable=[{string.Join(",", walkableCells.Where(cell => !reachable.Contains(cell)))}].");

            if (layoutName == "open")
            {
                int totalPairs = layout.Party.Length * layout.Enemies.Length;
                int nearPairs = layout.Party.Sum(party => layout.Enemies.Count(enemy => ManhattanDistance(party, enemy) <= 4));
                Assert.That(nearPairs, Is.GreaterThanOrEqualTo(1),
                    "open: audit proxy requires at least one party/enemy pair within Manhattan distance 4; this does not bind formal skill range.");
                Assert.That(nearPairs * 2, Is.LessThan(totalPairs),
                    $"open: audit proxy requires strictly fewer than half of party/enemy pairs to be within Manhattan distance 4; observed {nearPairs} of {totalPairs}; this does not bind formal skill range.");
            }
        }

        private static readonly Vector2Int[] FixedParty =
        {
            new Vector2Int(1, 4), new Vector2Int(1, 5), new Vector2Int(2, 4)
        };

        private static readonly Dictionary<string, LayoutData> LayoutDataByName =
            new Dictionary<string, LayoutData>(StringComparer.Ordinal)
            {
                ["open"] = new LayoutData(
                    new[] { new Vector2Int(6, 4), new Vector2Int(7, 3), new Vector2Int(7, 5), new Vector2Int(8, 4) },
                    new Vector2Int[0]),
                ["center_blocker"] = new LayoutData(
                    new[] { new Vector2Int(6, 3), new Vector2Int(6, 6), new Vector2Int(7, 4), new Vector2Int(7, 5) },
                    new[] { new Vector2Int(4, 4), new Vector2Int(4, 5), new Vector2Int(5, 4), new Vector2Int(5, 5) }),
                ["split_flank"] = new LayoutData(
                    new[] { new Vector2Int(6, 2), new Vector2Int(6, 7), new Vector2Int(7, 2), new Vector2Int(7, 7) },
                    new[] { new Vector2Int(4, 3), new Vector2Int(5, 4), new Vector2Int(4, 6), new Vector2Int(5, 5) }),
                ["Special"] = new LayoutData(
                    new[] { new Vector2Int(7, 4) },
                    new Vector2Int[0])
            };

        private static IEnumerable LayoutContractCases
        {
            get
            {
                yield return new TestCaseData("open",
                    new[] { new Vector2Int(6, 4), new Vector2Int(7, 3), new Vector2Int(7, 5), new Vector2Int(8, 4) },
                    new Vector2Int[0]).SetName("LayoutContract_open");
                yield return new TestCaseData("center_blocker",
                    new[] { new Vector2Int(6, 3), new Vector2Int(6, 6), new Vector2Int(7, 4), new Vector2Int(7, 5) },
                    new[] { new Vector2Int(4, 4), new Vector2Int(4, 5), new Vector2Int(5, 4), new Vector2Int(5, 5) })
                    .SetName("LayoutContract_center_blocker");
                yield return new TestCaseData("split_flank",
                    new[] { new Vector2Int(6, 2), new Vector2Int(6, 7), new Vector2Int(7, 2), new Vector2Int(7, 7) },
                    new[] { new Vector2Int(4, 3), new Vector2Int(5, 4), new Vector2Int(4, 6), new Vector2Int(5, 5) })
                    .SetName("LayoutContract_split_flank");
                yield return new TestCaseData("Special",
                    new[] { new Vector2Int(7, 4) },
                    new Vector2Int[0]).SetName("LayoutContract_Special");
            }
        }

        private static void AssertUnique(string layoutName, string groupName, Vector2Int[] cells)
        {
            Assert.That(cells.Distinct().Count(), Is.EqualTo(cells.Length), $"{layoutName}: {groupName} cells must be unique.");
        }

        private static void AssertDisjoint(
            string layoutName, string firstName, Vector2Int[] first, string secondName, Vector2Int[] second)
        {
            Assert.That(first.Intersect(second), Is.Empty, $"{layoutName}: {firstName} and {secondName} cells overlap.");
        }

        private static int WalkableDegree(Vector2Int cell, Vector2Int[] blockers)
        {
            return FourWayNeighbors(cell).Count(neighbor => !blockers.Contains(neighbor));
        }

        private static HashSet<Vector2Int> ReachableFourWay(Vector2Int start, Vector2Int[] blockers)
        {
            var blocked = new HashSet<Vector2Int>(blockers);
            var visited = new HashSet<Vector2Int> { start };
            var frontier = new Queue<Vector2Int>();
            frontier.Enqueue(start);
            while (frontier.Count > 0)
            {
                Vector2Int current = frontier.Dequeue();
                foreach (Vector2Int neighbor in FourWayNeighbors(current))
                    if (!blocked.Contains(neighbor) && visited.Add(neighbor)) frontier.Enqueue(neighbor);
            }

            return visited;
        }

        private static IEnumerable<Vector2Int> FourWayNeighbors(Vector2Int cell)
        {
            var offsets = new[] { Vector2Int.left, Vector2Int.right, Vector2Int.down, Vector2Int.up };
            return offsets.Select(offset => cell + offset).Where(BattleBoardSpec.Contains);
        }

        private static int ManhattanDistance(Vector2Int first, Vector2Int second)
        {
            return Mathf.Abs(first.x - second.x) + Mathf.Abs(first.y - second.y);
        }

        private sealed class LayoutData
        {
            public LayoutData(Vector2Int[] enemies, Vector2Int[] blockers)
            {
                Party = FixedParty;
                Enemies = enemies;
                Blockers = blockers;
            }

            public Vector2Int[] Party { get; }
            public Vector2Int[] Enemies { get; }
            public Vector2Int[] Blockers { get; }
        }

        private static void WithProbeScene(Action<Scene> assertion)
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ProbeScenePath), Is.Not.Null, $"Missing {ProbeScenePath}");
            Scene activeBefore = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(ProbeScenePath);
            bool openedForTest = false;
            try
            {
                if (!scene.isLoaded)
                {
                    scene = EditorSceneManager.OpenScene(ProbeScenePath, OpenSceneMode.Additive);
                    openedForTest = true;
                }

                assertion(scene);
            }
            finally
            {
                if (openedForTest && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
                if (activeBefore.IsValid() && activeBefore.isLoaded)
                    SceneManager.SetActiveScene(activeBefore);
            }
        }

        private static SerializedProperty RequiredProperty(SerializedObject serializedObject, string propertyName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            Assert.That(property, Is.Not.Null,
                $"Missing serialized property '{propertyName}' on {serializedObject.targetObject.GetType().FullName}.");
            return property;
        }

        private static T[] ComponentsInScene<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        private static int CountTiles(Tilemap tilemap)
        {
            int count = 0;
            foreach (Vector3Int position in tilemap.cellBounds.allPositionsWithin)
                if (tilemap.HasTile(position)) count++;
            return count;
        }
    }
}
