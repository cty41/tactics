#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Tactics.Cells;
using Tactics.Common.Battle;
using Tactics.Common.Cells;
using Tactics.Common.Interactables;
using Tactics.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Tactics.Tests.PlayMode
{
    public sealed class Test1BattleSmokeTests
    {
        private const string ScenePath = "Assets/Tactics/Scenes/Test1.unity";
        private const string HomeScenePath = "Assets/Tactics/Scenes/Home.unity";
        private const float InitializationTimeoutSeconds = 5f;

        private bool _originalIgnoreFailingMessages;
        private string _activeScenePathBeforeTest;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _originalIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            _activeScenePathBeforeTest = SceneManager.GetActiveScene().path;
            LogAssert.ignoreFailingMessages = false;
            yield return LoadSceneSingle(ScenePath);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                string restorePath = AssetDatabase.LoadAssetAtPath<SceneAsset>(_activeScenePathBeforeTest) != null
                    ? _activeScenePathBeforeTest
                    : HomeScenePath;
                yield return LoadSceneSingle(restorePath);
                yield return null;
                Assert.That(SceneManager.GetSceneByPath(ScenePath).isLoaded, Is.False);
                Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(restorePath));
            }
            finally
            {
                LogAssert.ignoreFailingMessages = _originalIgnoreFailingMessages;
            }
        }

        [UnityTest]
        public IEnumerator RuntimeSmoke_LoadsFixedBoardAndFormalTest1Spawns()
        {
            Scene scene = RequireActiveScene();
            TilemapCellManager manager = null;
            ICell[] cells = new ICell[0];
            TilemapUnit[] units = new TilemapUnit[0];
            Corpse[] corpses = new Corpse[0];
            float deadline = Time.realtimeSinceStartup + InitializationTimeoutSeconds;

            while (Time.realtimeSinceStartup < deadline)
            {
                manager = ComponentsInScene<TilemapCellManager>(scene, true).SingleOrDefault();
                cells = TryGetCells(manager);
                units = ComponentsInScene<TilemapUnit>(scene, false);
                corpses = ComponentsInScene<Corpse>(scene, false).Where(corpse => !corpse.IsDestroyed).ToArray();
                if (cells.Length == BattleBoardSpec.CellCount && units.Length == 2 && corpses.Length == 1)
                    break;
                yield return null;
            }

            Assert.That(manager, Is.Not.Null, RuntimeDiagnostic(scene, cells, units, corpses));
            Assert.That(cells, Has.Length.EqualTo(BattleBoardSpec.CellCount), RuntimeDiagnostic(scene, cells, units, corpses));
            Assert.That(cells.Select(ToCoordinates).Distinct().Count(), Is.EqualTo(BattleBoardSpec.CellCount));
            Assert.That(cells.All(cell => BattleBoardSpec.Contains(ToCoordinates(cell))), Is.True);

            Assert.That(units, Has.Length.EqualTo(2), RuntimeDiagnostic(scene, cells, units, corpses));
            Assert.That(corpses, Has.Length.EqualTo(1), RuntimeDiagnostic(scene, cells, units, corpses));

            // CurrentCell can lazily write occupancy. Prove initialization from cell-owned state first,
            // then use CurrentCell only as a cross-check below.
            ICell[] unitCells = cells.Where(cell => cell.CurrentUnits.Count > 0).ToArray();
            ICell[] corpseCells = cells.Where(cell => cell.CurrentInteractables.Any(item => item is Corpse)).ToArray();
            object[] cellOwnedUnits = unitCells.SelectMany(cell => cell.CurrentUnits).Cast<object>().ToArray();
            Corpse[] cellOwnedCorpses = corpseCells
                .SelectMany(cell => cell.CurrentInteractables)
                .OfType<Corpse>()
                .Where(corpse => !corpse.IsDestroyed)
                .ToArray();

            Assert.That(unitCells, Has.Length.EqualTo(2), RuntimeDiagnostic(scene, cells, units, corpses));
            Assert.That(corpseCells, Has.Length.EqualTo(1), RuntimeDiagnostic(scene, cells, units, corpses));
            CollectionAssert.AreEquivalent(units.Cast<object>().ToArray(), cellOwnedUnits,
                "Cell-owned unit occupancy must match the two active scene units before CurrentCell is read.");
            CollectionAssert.AreEquivalent(corpses, cellOwnedCorpses,
                "Cell-owned interactable occupancy must match the active corpse before CurrentCell is read.");
            Assert.That(unitCells.Concat(corpseCells).Distinct().Count(), Is.EqualTo(3));
            Assert.That(unitCells.Concat(corpseCells).All(cell => cell.IsTaken), Is.True);
            Assert.That(cells.Count(cell => cell.IsTaken), Is.EqualTo(3),
                "Only the formal party, enemy, and corpse cells may be taken after initialization.");

            TilemapUnit party = units.Single(unit => unit.PlayerNumber == 1);
            TilemapUnit enemy = units.Single(unit => unit.PlayerNumber == 2);
            Assert.That(ToCoordinates(party.CurrentCell), Is.EqualTo(new Vector2Int(1, 4)));
            Assert.That(ToCoordinates(enemy.CurrentCell), Is.EqualTo(new Vector2Int(6, 4)));

            Corpse corpse = corpses[0];
            Assert.That(ToCoordinates(corpse.CurrentCell), Is.EqualTo(new Vector2Int(4, 4)));
            Assert.That(corpse.CurrentCell.CurrentInteractables, Does.Contain(corpse));
            Assert.That(corpse.CurrentCell.IsTaken, Is.True);

            Vector2Int[] occupied =
            {
                ToCoordinates(party.CurrentCell),
                ToCoordinates(enemy.CurrentCell),
                ToCoordinates(corpse.CurrentCell),
            };
            Assert.That(occupied.Distinct().Count(), Is.EqualTo(occupied.Length),
                "Formal party, enemy, and corpse cells must not overlap.");
            foreach (TilemapUnit unit in units)
            {
                Assert.That(unit.CurrentCell.CurrentUnits, Does.Contain(unit));
                Assert.That(unit.CurrentCell.IsTaken, Is.True);
            }
        }

        private static IEnumerator LoadSceneSingle(string scenePath)
        {
            EditorSceneManager.LoadSceneInPlayMode(scenePath, new LoadSceneParameters(LoadSceneMode.Single));
            float deadline = Time.realtimeSinceStartup + InitializationTimeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                Scene active = SceneManager.GetActiveScene();
                if (active.IsValid() && active.isLoaded && active.path == scenePath)
                    yield break;
                yield return null;
            }

            Assert.Fail($"Scene '{scenePath}' did not become active. Active='{SceneManager.GetActiveScene().path}'.");
        }

        private static Scene RequireActiveScene()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            Assert.That(scene.IsValid() && scene.isLoaded, Is.True);
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(scene));
            return scene;
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

        private static Vector2Int ToCoordinates(ICell cell)
        {
            Assert.That(cell, Is.Not.Null);
            return new Vector2Int(cell.GridCoordinates.x, cell.GridCoordinates.y);
        }

        private static string RuntimeDiagnostic(Scene scene, ICell[] cells, TilemapUnit[] units, Corpse[] corpses)
        {
            return $"scene='{scene.path}', cells={cells?.Length ?? -1}, units={units?.Length ?? -1}, " +
                   $"corpses={corpses?.Length ?? -1}, loaded=[{string.Join(",", LoadedScenePaths())}]";
        }

        private static IEnumerable<string> LoadedScenePaths()
        {
            for (int index = 0; index < SceneManager.sceneCount; index++)
                yield return SceneManager.GetSceneAt(index).path;
        }
    }
}
#endif
