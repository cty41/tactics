#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using Tactics.Cells;
using Tactics.Common.Battle;
using Tactics.Common.Cells;
using Tactics.Units;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

namespace Tactics.Tests.PlayMode
{
    public sealed class Probe10x10BattleSmokeTests
    {
        private const string ProbeScenePath = "Assets/Tactics/Scenes/Test1_10x10_Probe.unity";
        private const string HomeScenePath = "Assets/Tactics/Scenes/Home.unity";
        private const float InitializationTimeoutSeconds = 5f;

        private bool _originalIgnoreFailingMessages;
        private string _activeScenePathBeforeTest;
        private bool _globalStateCaptured;

        private static readonly Vector2Int[] ExpectedParty =
        {
            new Vector2Int(1, 4),
            new Vector2Int(1, 5),
            new Vector2Int(2, 4),
        };

        private static readonly Vector2Int[] ExpectedEnemies =
        {
            new Vector2Int(6, 4),
            new Vector2Int(7, 3),
            new Vector2Int(7, 5),
            new Vector2Int(8, 4),
        };

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _originalIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            _activeScenePathBeforeTest = SceneManager.GetActiveScene().path;
            _globalStateCaptured = true;
            LogAssert.ignoreFailingMessages = false;
            yield return LoadSceneSingle(ProbeScenePath);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                BattleController battleController = BattleController.Instance;
                Task runtimeTeardownTask = battleController?.TeardownRuntimeScopeAsync();
                if (runtimeTeardownTask != null)
                {
                    float teardownDeadline = Time.realtimeSinceStartup + InitializationTimeoutSeconds;
                    while (!runtimeTeardownTask.IsCompleted && Time.realtimeSinceStartup < teardownDeadline)
                        yield return null;
                }

                bool runtimeTeardownCompleted = runtimeTeardownTask == null || runtimeTeardownTask.IsCompleted;
                bool runtimeTeardownFaulted = runtimeTeardownTask?.IsFaulted == true;
                System.Exception runtimeTeardownException = runtimeTeardownCompleted
                    ? battleController?.RuntimeScopeTeardownException
                    : null;
                bool runtimeScopeReleased = battleController == null || battleController.RuntimeScope == null;
                bool projectileVisualRemaining = GameObject.Find("ProjectileVisual") != null;

                string restorePath = HomeScenePath;
                yield return LoadSceneSingle(restorePath);
                yield return null;

                Assert.That(SceneManager.GetSceneByPath(ProbeScenePath).isLoaded, Is.False,
                    "Probe scene must be unloaded after each smoke test.");
                Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(restorePath),
                    $"Active scene must be restored after the smoke test; captured='{_activeScenePathBeforeTest}', " +
                    $"expected='{restorePath}', active='{SceneManager.GetActiveScene().path}'.");
                Assert.That(runtimeTeardownCompleted, Is.True,
                    "Probe scene teardown must drain battle runtime work before replacing the scene.");
                Assert.That(runtimeTeardownFaulted, Is.False,
                    "Probe scene teardown must observe tracked runtime work without faulting.");
                Assert.That(runtimeTeardownException, Is.Null,
                    "Probe scene teardown must expose no tracked runtime failure.");
                Assert.That(runtimeScopeReleased, Is.True,
                    "Probe scene teardown must release its battle runtime scope before replacing the scene.");
                Assert.That(projectileVisualRemaining, Is.False,
                    "Probe scene teardown must drain ProjectileVisual before replacing the scene.");
            }
            finally
            {
                if (_globalStateCaptured)
                    LogAssert.ignoreFailingMessages = _originalIgnoreFailingMessages;
                _globalStateCaptured = false;
            }
        }

        [UnityTest]
        public IEnumerator RuntimeSmoke_LoadsExactBoardAndSpawnLayout()
        {
            Scene probe = RequireActiveProbeScene();
            TilemapCellManager manager = null;
            TilemapUnit[] units = new TilemapUnit[0];
            ICell[] cells = new ICell[0];
            ICell[] occupiedCells = new ICell[0];
            object[] flattenedUnits = new object[0];
            int observedFrames = 0;
            float deadline = Time.realtimeSinceStartup + InitializationTimeoutSeconds;

            while (true)
            {
                TilemapCellManager[] managers = ComponentsInScene<TilemapCellManager>(probe, true);
                if (managers.Length == 1)
                {
                    manager = managers[0];
                    cells = TryGetCells(manager);
                    occupiedCells = cells.Where(cell => cell.CurrentUnits.Count > 0 || cell.IsTaken).ToArray();
                    flattenedUnits = cells.SelectMany(cell => cell.CurrentUnits).Cast<object>().ToArray();
                }

                units = ComponentsInScene<TilemapUnit>(probe, false);
                if (manager != null && cells.Length == BattleBoardSpec.CellCount &&
                    units.Length == ExpectedParty.Length + ExpectedEnemies.Length &&
                    occupiedCells.Length == units.Length &&
                    flattenedUnits.Length == units.Length &&
                    flattenedUnits.Distinct().Count() == units.Length &&
                    new HashSet<object>(flattenedUnits).SetEquals(units.Cast<object>()) &&
                    occupiedCells.All(cell => cell.CurrentUnits.Count == 1 && cell.IsTaken))
                {
                    break;
                }

                if (Time.realtimeSinceStartup >= deadline)
                    break;
                observedFrames++;
                yield return null;
            }

            TilemapCellManager[] finalManagers = ComponentsInScene<TilemapCellManager>(probe, true);
            Assert.That(finalManagers, Has.Length.EqualTo(1),
                $"Initialization timeout after {InitializationTimeoutSeconds:F1}s wall-clock ({observedFrames} frames): " +
                $"expected one probe-scene TilemapCellManager; found {finalManagers.Length}. " +
                RuntimeDiagnostic(probe, manager, cells, units));
            Assert.That(manager, Is.SameAs(finalManagers[0]));
            Assert.That(cells, Has.Length.EqualTo(BattleBoardSpec.CellCount),
                $"Initialization timeout after {InitializationTimeoutSeconds:F1}s wall-clock ({observedFrames} frames). " +
                RuntimeDiagnostic(probe, manager, cells, units));
            Assert.That(units, Has.Length.EqualTo(7),
                $"Spawn timeout after {InitializationTimeoutSeconds:F1}s wall-clock ({observedFrames} frames). " +
                RuntimeDiagnostic(probe, manager, cells, units));
            Assert.That(occupiedCells, Has.Length.EqualTo(7), RuntimeDiagnostic(probe, manager, cells, units));
            Assert.That(flattenedUnits, Has.Length.EqualTo(7), RuntimeDiagnostic(probe, manager, cells, units));
            Assert.That(flattenedUnits.Distinct().Count(), Is.EqualTo(7), RuntimeDiagnostic(probe, manager, cells, units));
            CollectionAssert.AreEquivalent(units.Cast<object>().ToArray(), flattenedUnits,
                "Cell occupancy must contain exactly the seven scene units before CurrentCell is observed. " +
                RuntimeDiagnostic(probe, manager, cells, units));
            Assert.That(occupiedCells.All(cell => cell.CurrentUnits.Count == 1 && cell.IsTaken), Is.True,
                "Every occupied cell must contain exactly one unit and have IsTaken=true before CurrentCell is observed. " +
                RuntimeDiagnostic(probe, manager, cells, units));

            Vector2Int[] coordinates = cells.Select(ToUnityCoordinates).ToArray();
            Assert.That(coordinates.Distinct().Count(), Is.EqualTo(BattleBoardSpec.CellCount),
                "Runtime cells must have 100 unique coordinates.");
            CollectionAssert.AreEquivalent(
                Enumerable.Range(0, BattleBoardSpec.Width)
                    .SelectMany(x => Enumerable.Range(0, BattleBoardSpec.Height).Select(y => new Vector2Int(x, y)))
                    .ToArray(),
                coordinates,
                "Runtime cell coordinates must be exactly the complete 0..9 x 0..9 board.");

            TilemapUnit[] party = units.Where(unit => unit.PlayerNumber == 1).ToArray();
            TilemapUnit[] enemies = units.Where(unit => unit.PlayerNumber == 2).ToArray();
            Assert.That(party, Has.Length.EqualTo(3));
            Assert.That(enemies, Has.Length.EqualTo(4));

            // CurrentCell is a lazy, occupancy-writing getter. Read it only after the cell-owned
            // occupancy contract above proves the runtime initialized independently of this test.
            CollectionAssert.AreEquivalent(ExpectedParty, party.Select(unit => ToUnityCoordinates(unit.CurrentCell)).ToArray());
            CollectionAssert.AreEquivalent(ExpectedEnemies, enemies.Select(unit => ToUnityCoordinates(unit.CurrentCell)).ToArray());

            Vector2Int[] spawnCoordinates = units.Select(unit => ToUnityCoordinates(unit.CurrentCell)).ToArray();
            Assert.That(spawnCoordinates.Distinct().Count(), Is.EqualTo(7), "All seven runtime spawns must be non-overlapping.");
            Assert.That(spawnCoordinates, Is.All.Matches<Vector2Int>(BattleBoardSpec.Contains),
                "Every runtime spawn must be inside BattleBoardSpec.");

            foreach (TilemapUnit unit in units)
            {
                ICell cell = unit.CurrentCell;
                Assert.That(cell.IsTaken, Is.True,
                    $"Occupied cell {ToUnityCoordinates(cell)} must be taken under the existing cell model.");
                Assert.That(cell.CurrentUnits, Does.Contain(unit),
                    $"CurrentCell {ToUnityCoordinates(cell)} must list its occupying unit {unit.name}.");
            }

            var spawnSet = new HashSet<Vector2Int>(spawnCoordinates);
            foreach (ICell cell in cells.Where(cell => !spawnSet.Contains(ToUnityCoordinates(cell))))
            {
                Assert.That(cell.CurrentUnits, Is.Empty,
                    $"Non-spawn cell {ToUnityCoordinates(cell)} must not contain a unit.");
                Assert.That(cell.IsTaken, Is.False,
                    $"Open-probe non-spawn cell {ToUnityCoordinates(cell)} must not be taken.");
            }

        }

        [UnityTest]
        public IEnumerator ActualSceneCameraGeometry_FitsThreeSupportedAspectRatios()
        {
            Scene probe = RequireActiveProbeScene();
            Camera[] cameras = ComponentsInScene<Camera>(probe, true);
            TilemapRenderer[] backgrounds = ComponentsInScene<TilemapRenderer>(probe, true)
                .Where(renderer => renderer.name == "BackgroundLayer").ToArray();
            BattleBoardCameraFitter[] fitters = ComponentsInScene<BattleBoardCameraFitter>(probe, true);

            Assert.That(cameras, Has.Length.EqualTo(1), "Probe scene must contain exactly one Camera.");
            Assert.That(backgrounds, Has.Length.EqualTo(1), "Probe scene must contain exactly one BackgroundLayer TilemapRenderer.");
            Assert.That(fitters, Has.Length.EqualTo(1), "Probe scene must contain exactly one BattleBoardCameraFitter.");

            Camera camera = cameras[0];
            TilemapRenderer renderer = backgrounds[0];
            BattleBoardCameraFitter fitter = fitters[0];
            float horizontalPadding = ReadPrivateFloat(fitter, "_horizontalPadding");
            float verticalPadding = ReadPrivateFloat(fitter, "_verticalPadding");
            float initialZ = camera.transform.position.z;
            float[] aspects = { 16f / 9f, 16f / 10f, 21f / 9f };

            foreach (float aspect in aspects)
            {
                camera.aspect = aspect;
                yield return null;

                Bounds bounds = renderer.bounds;
                float expectedSize = Mathf.Max(
                    (bounds.size.y + 2f * verticalPadding) * 0.5f,
                    (bounds.size.x + 2f * horizontalPadding) / (2f * aspect));

                Assert.That(camera.orthographic, Is.True, $"aspect={aspect:F6}");
                Assert.That(camera.transform.position.x, Is.EqualTo(bounds.center.x).Within(0.001f), $"aspect={aspect:F6}");
                Assert.That(camera.transform.position.y, Is.EqualTo(bounds.center.y).Within(0.001f), $"aspect={aspect:F6}");
                Assert.That(camera.transform.position.z, Is.EqualTo(initialZ).Within(0.001f), $"aspect={aspect:F6}");
                Assert.That(camera.orthographicSize, Is.EqualTo(expectedSize).Within(0.001f), $"aspect={aspect:F6}");
                AssertPaddedCornersAreVisible(camera, bounds, horizontalPadding, verticalPadding, aspect);

            }
        }

        private static IEnumerator LoadSceneSingle(string scenePath)
        {
            EditorSceneManager.LoadSceneInPlayMode(scenePath, new LoadSceneParameters(LoadSceneMode.Single));
            float deadline = Time.realtimeSinceStartup + InitializationTimeoutSeconds;
            int observedFrames = 0;
            while (true)
            {
                Scene active = SceneManager.GetActiveScene();
                if (active.IsValid() && active.isLoaded && active.path == scenePath)
                    yield break;
                if (Time.realtimeSinceStartup >= deadline)
                    break;
                observedFrames++;
                yield return null;
            }

            Assert.Fail($"Scene '{scenePath}' did not become the active loaded scene within {InitializationTimeoutSeconds:F1}s " +
                $"wall-clock ({observedFrames} frames); " +
                $"active='{SceneManager.GetActiveScene().path}', loaded=[{string.Join(", ", LoadedScenePaths())}].");
        }

        private static Scene RequireActiveProbeScene()
        {
            Scene probe = SceneManager.GetSceneByPath(ProbeScenePath);
            Assert.That(probe.IsValid() && probe.isLoaded, Is.True,
                $"Probe scene must be loaded. Active='{SceneManager.GetActiveScene().path}', loaded=[{string.Join(", ", LoadedScenePaths())}].");
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(probe), "Probe scene must be active and loaded Single.");
            return probe;
        }

        private static T[] ComponentsInScene<T>(Scene scene, bool includeInactive) where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(includeInactive))
                .Where(component => component != null && component.gameObject.scene == scene)
                .ToArray();
        }

        private static ICell[] TryGetCells(TilemapCellManager manager)
        {
            if (manager == null)
                return new ICell[0];
            try
            {
                return manager.GetCells()?.ToArray() ?? new ICell[0];
            }
            catch (System.NullReferenceException)
            {
                return new ICell[0];
            }
        }

        private static Vector2Int ToUnityCoordinates(ICell cell)
        {
            return new Vector2Int(cell.GridCoordinates.x, cell.GridCoordinates.y);
        }

        private static float ReadPrivateFloat(BattleBoardCameraFitter fitter, string fieldName)
        {
            FieldInfo field = typeof(BattleBoardCameraFitter).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Expected private serialized field '{fieldName}'.");
            return (float)field.GetValue(fitter);
        }

        private static void AssertPaddedCornersAreVisible(
            Camera camera,
            Bounds bounds,
            float horizontalPadding,
            float verticalPadding,
            float aspect)
        {
            float minX = bounds.min.x - horizontalPadding;
            float maxX = bounds.max.x + horizontalPadding;
            float minY = bounds.min.y - verticalPadding;
            float maxY = bounds.max.y + verticalPadding;
            Vector3[] corners =
            {
                new Vector3(minX, minY, bounds.center.z),
                new Vector3(minX, maxY, bounds.center.z),
                new Vector3(maxX, minY, bounds.center.z),
                new Vector3(maxX, maxY, bounds.center.z),
            };

            foreach (Vector3 corner in corners)
            {
                Vector3 viewport = camera.WorldToViewportPoint(corner);
                Assert.That(viewport.x, Is.InRange(-0.001f, 1.001f), $"aspect={aspect:F6}, corner={corner}, viewport={viewport}");
                Assert.That(viewport.y, Is.InRange(-0.001f, 1.001f), $"aspect={aspect:F6}, corner={corner}, viewport={viewport}");
            }
        }

        private static string RuntimeDiagnostic(
            Scene probe,
            TilemapCellManager manager,
            ICell[] cells,
            TilemapUnit[] units)
        {
            string unitSummary = units == null
                ? "<null>"
                : string.Join(", ", units.Select(unit =>
                    $"{unit.name}:P{unit.PlayerNumber}:world={unit.transform.position}"));
            string occupancySummary = cells == null
                ? "<null>"
                : string.Join(", ", cells.Where(cell => cell.CurrentUnits.Count > 0 || cell.IsTaken).Select(cell =>
                    $"{ToUnityCoordinates(cell)}:taken={cell.IsTaken}:units=[{string.Join("|", cell.CurrentUnits.Select(unit => unit?.ToString() ?? "null"))}]"));
            return $"scene='{probe.path}', manager={(manager == null ? "null" : manager.name)}, cells={cells?.Length ?? -1}, " +
                $"occupancy=[{occupancySummary}], " +
                $"activeUnits={units?.Length ?? -1} [{unitSummary}], loaded=[{string.Join(", ", LoadedScenePaths())}].";
        }

        private static IEnumerable<string> LoadedScenePaths()
        {
            for (int index = 0; index < SceneManager.sceneCount; index++)
                yield return SceneManager.GetSceneAt(index).path;
        }


    }
}
#endif
