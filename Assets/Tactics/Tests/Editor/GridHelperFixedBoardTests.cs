using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Tactics.Common.Battle;
using Tactics.Common.Cells;
using Tactics.Common.Interactables;
using Tactics.Common.Units;
using Tactics.Common.Utilities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tactics.Tests.Editor
{
    public class GridHelperFixedBoardTests
    {
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly Type GridHelperType = Type.GetType("TbsFramework.EditorUtils.GridHelper, Tactics.Editor");

        [Test]
        public void GenerationDimensions_AreForcedToFixedBoardContract()
        {
            Assert.That(GridHelperType, Is.Not.Null);
            var window = ScriptableObject.CreateInstance(GridHelperType) as EditorWindow;
            try
            {
                SetField(window, "mapWidth", 7);
                SetField(window, "mapHeight", 12);

                MethodInfo apply = GridHelperType.GetMethod("ApplyFixedBoardDimensions", InstanceFlags);
                Assert.That(apply, Is.Not.Null, "Grid Helper must expose its fixed-board generation policy.");
                apply.Invoke(window, null);

                Assert.That(GetField<int>(window, "mapWidth"), Is.EqualTo(BattleBoardSpec.Width));
                Assert.That(GetField<int>(window, "mapHeight"), Is.EqualTo(BattleBoardSpec.Height));
            }
            finally
            {
                if (window != null)
                    UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [TestCase(-1, 0)]
        [TestCase(0, -1)]
        [TestCase(10, 0)]
        [TestCase(0, 10)]
        public void PaintingPolicy_RejectsCoordinatesOutsideFixedBoard(int x, int y)
        {
            var cellObject = new GameObject("OutOfBoundsCell");
            try
            {
                var cell = cellObject.AddComponent<Square>();
                cell.GridCoordinates = new Vector2IntImpl(x, y);

                bool valid = ValidatePaintTarget(cell, false, out string message);

                Assert.That(valid, Is.False);
                Assert.That(message, Does.Contain("0-9"));
                Assert.That(cell.IsTaken, Is.False, "Validation must not mutate occupancy.");
                Assert.That(cell.CurrentUnits, Is.Empty, "Validation must not mutate unit membership.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cellObject);
            }
        }

        [Test]
        public void UnitPaintingPolicy_RejectsOccupiedCellWithoutMutation()
        {
            var cellObject = new GameObject("OccupiedCell");
            try
            {
                var cell = cellObject.AddComponent<Square>();
                cell.GridCoordinates = new Vector2IntImpl(4, 4);
                cell.IsTaken = true;

                bool valid = ValidatePaintTarget(cell, true, out string message);

                Assert.That(valid, Is.False);
                Assert.That(message, Does.Contain("occupied").IgnoreCase);
                Assert.That(cell.IsTaken, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cellObject);
            }
        }

        [Test]
        public void PaintingPolicy_AllowsVacantCellAtMaximumBoundary()
        {
            var cellObject = new GameObject("BoundaryCell");
            try
            {
                var cell = cellObject.AddComponent<Square>();
                cell.GridCoordinates = new Vector2IntImpl(9, 9);

                bool valid = ValidatePaintTarget(cell, true, out string message);

                Assert.That(valid, Is.True, message);
                Assert.That(message, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cellObject);
            }
        }

        [Test]
        public void TileBrushAtBoardEdge_SkipsOutOfBoundsNeighborsWithFeedback()
        {
            var objects = new List<GameObject>();
            try
            {
                Cell center = CreateCell("Center", 0, 0, objects);
                Cell inBounds = CreateCell("InBounds", 1, 0, objects);
                Cell outOfBounds = CreateCell("OutOfBounds", -1, 0, objects);
                Cell unrelated = CreateCell("Unrelated", 9, 9, objects);

                MethodInfo filter = GridHelperType.GetMethod("GetValidTilePaintTargets", StaticFlags);
                Assert.That(filter, Is.Not.Null,
                    "Tile painting must filter every radius candidate before any replacement occurs.");
                object[] arguments = { new[] { center, inBounds, outOfBounds, unrelated }, center, 2, null };

                var targets = ((IEnumerable<Cell>)filter.Invoke(null, arguments)).ToList();
                string message = arguments[3] as string;

                Assert.That(targets, Is.EquivalentTo(new[] { center, inBounds }));
                Assert.That(targets, Has.No.Member(outOfBounds));
                Assert.That(message, Does.Contain("outside").IgnoreCase);
                Assert.That(outOfBounds.IsTaken, Is.False, "Filtering must not mutate the skipped cell.");
                Assert.That(outOfBounds.CurrentUnits, Is.Empty, "Filtering must not mutate unit membership.");
            }
            finally
            {
                foreach (GameObject cellObject in objects)
                    UnityEngine.Object.DestroyImmediate(cellObject);
            }
        }

        [Test]
        public void TileReplacement_TransfersUnitOccupancyBeforeOldCellIsDestroyed()
        {
            var objects = new List<GameObject>();
            try
            {
                Cell oldCell = CreateCell("OldCell", 4, 4, objects);
                Cell newCell = CreateCell("NewCell", 4, 4, objects);
                var unitObject = new GameObject("Unit");
                objects.Add(unitObject);
                var unit = unitObject.AddComponent<Unit>();
                unit.CurrentCell = oldCell;
                oldCell.CurrentUnits.Add(unit);
                oldCell.IsTaken = true;

                MethodInfo transfer = GridHelperType.GetMethod("TransferCellOccupancy", StaticFlags);
                Assert.That(transfer, Is.Not.Null,
                    "Tile replacement must transfer occupancy before destroying the old cell.");
                transfer.Invoke(null, new object[] { oldCell, newCell });

                Assert.That(unit.CurrentCell, Is.SameAs(newCell));
                Assert.That(newCell.CurrentUnits, Is.EqualTo(new IUnit[] { unit }));
                Assert.That(newCell.IsTaken, Is.True);
                Assert.That(oldCell.CurrentUnits, Is.Empty);
                Assert.That(oldCell.IsTaken, Is.False);
            }
            finally
            {
                foreach (GameObject gameObject in objects)
                    UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void UndoRedoCellLookup_RejectsSameCoordinateFromDifferentScene()
        {
            var objects = new List<GameObject>();
            Scene sourceScene = SceneManager.GetActiveScene();
            Scene decoyScene = EditorSceneManager.NewPreviewScene();
            try
            {
                Cell decoyCell = CreateCell("DecoyCell", 4, 4, objects);
                SceneManager.MoveGameObjectToScene(decoyCell.gameObject, decoyScene);
                Cell sourceCell = CreateCell("SourceCell", 4, 4, objects);

                MethodInfo findCell = GridHelperType.GetMethod("FindCellInSceneAtPosition", StaticFlags);
                Assert.That(findCell, Is.Not.Null,
                    "Undo/Redo repair needs a scene-aware coordinate lookup.");

                var candidates = new[] { decoyCell, sourceCell };
                var found = (Cell)findCell.Invoke(null, new object[]
                {
                    candidates,
                    sourceScene,
                    sourceCell.transform.position
                });

                Assert.That(found, Is.SameAs(sourceCell));
            }
            finally
            {
                foreach (GameObject gameObject in objects)
                {
                    if (gameObject != null)
                        UnityEngine.Object.DestroyImmediate(gameObject);
                }

                if (decoyScene.IsValid() && decoyScene.isLoaded)
                    EditorSceneManager.ClosePreviewScene(decoyScene);
            }
        }

        [Test]
        public void TileReplacementUndoRedo_RestoresUnitAndInteractableOccupancy()
        {
            var objects = new List<GameObject>();
            Scene sourceScene = SceneManager.GetActiveScene();
            Scene decoyScene = EditorSceneManager.NewPreviewScene();
            try
            {
                Cell decoyCell = CreateCell("DecoyCell", 4, 4, objects);
                SceneManager.MoveGameObjectToScene(decoyCell.gameObject, decoyScene);
                Assert.That(decoyCell.gameObject.scene, Is.EqualTo(decoyScene));
                SceneManager.SetActiveScene(sourceScene);

                Cell oldCell = CreateCell("OldCell", 4, 4, objects);
                Cell newCell = CreateCell("NewCell", 4, 4, objects);
                var corpseObject = new GameObject("Corpse");
                objects.Add(corpseObject);
                var corpse = corpseObject.AddComponent<Corpse>();
                oldCell.AddInteractable(corpse);
                var unitObject = new GameObject("Unit");
                objects.Add(unitObject);
                var unit = unitObject.AddComponent<Unit>();
                unit.CurrentCell = oldCell;
                oldCell.CurrentUnits.Add(unit);
                oldCell.IsTaken = true;

                Undo.IncrementCurrentGroup();
                int undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Tile replacement interactable occupancy");

                MethodInfo transfer = GridHelperType.GetMethod("TransferCellOccupancy", StaticFlags);
                Assert.That(transfer, Is.Not.Null);
                Undo.RegisterCreatedObjectUndo(newCell.gameObject, "Tile replacement interactable occupancy");
                transfer.Invoke(null, new object[] { oldCell, newCell });
                Undo.DestroyObjectImmediate(oldCell.gameObject);
                Undo.CollapseUndoOperations(undoGroup);
                Undo.FlushUndoRecordObjects();

                Assert.That(corpse.CurrentCell, Is.SameAs(newCell));
                Assert.That(newCell.CurrentInteractables, Has.Member(corpse));
                Assert.That(unit.CurrentCell, Is.SameAs(newCell));
                Assert.That(newCell.CurrentUnits, Has.Member(unit));
                Assert.That(oldCell == null, Is.True);

                Undo.PerformUndo();

                Assert.That(newCell == null, Is.True);
                Cell restoredOldCell = Resources.FindObjectsOfTypeAll<Cell>()
                    .Single(cell => cell != null && cell.name == "OldCell");
                Assert.That(corpse.CurrentCell, Is.SameAs(restoredOldCell),
                    "Undo must restore the interactable's cell reference, not a destroyed replacement cell.");
                Assert.That(restoredOldCell.CurrentInteractables, Has.Member(corpse),
                    "Undo must restore cell-owned interactable occupancy.");
                Assert.That(unit.CurrentCell, Is.SameAs(restoredOldCell));
                Assert.That(restoredOldCell.CurrentUnits, Has.Member(unit),
                    "Undo must restore cell-owned unit occupancy.");
                Assert.That(restoredOldCell.IsTaken, Is.True);
                Assert.That(restoredOldCell.gameObject.scene, Is.EqualTo(sourceScene));
                Assert.That(decoyCell.CurrentUnits, Is.Empty);
                Assert.That(decoyCell.CurrentInteractables, Is.Empty);

                Undo.PerformRedo();

                Cell restoredNewCell = Resources.FindObjectsOfTypeAll<Cell>()
                    .Single(cell => cell != null && cell.name == "NewCell");
                Assert.That(restoredOldCell == null, Is.True);
                Assert.That(corpse.CurrentCell, Is.SameAs(restoredNewCell));
                Assert.That(restoredNewCell.CurrentInteractables, Has.Member(corpse));
                Assert.That(unit.CurrentCell, Is.SameAs(restoredNewCell));
                Assert.That(restoredNewCell.CurrentUnits, Has.Member(unit));
                Assert.That(restoredNewCell.IsTaken, Is.True);
                Assert.That(restoredNewCell.gameObject.scene, Is.EqualTo(sourceScene));
                Assert.That(decoyCell.CurrentUnits, Is.Empty);
                Assert.That(decoyCell.CurrentInteractables, Is.Empty);
            }
            finally
            {
                Undo.ClearAll();
                foreach (GameObject gameObject in objects)
                {
                    if (gameObject != null)
                        UnityEngine.Object.DestroyImmediate(gameObject);
                }

                if (decoyScene.IsValid() && decoyScene.isLoaded)
                    EditorSceneManager.ClosePreviewScene(decoyScene);
            }
        }

        private static bool ValidatePaintTarget(Cell cell, bool requireVacant, out string message)
        {
            Assert.That(GridHelperType, Is.Not.Null);
            MethodInfo validate = GridHelperType.GetMethod("TryValidatePaintTarget", StaticFlags);
            Assert.That(validate, Is.Not.Null, "Grid Helper must reject invalid coordinates before painting.");
            object[] arguments = { cell, requireVacant, null };
            bool valid = (bool)validate.Invoke(null, arguments);
            message = arguments[2] as string;
            return valid;
        }

        private static Cell CreateCell(string name, int x, int y, ICollection<GameObject> objects)
        {
            var cellObject = new GameObject(name);
            objects.Add(cellObject);
            var cell = cellObject.AddComponent<Square>();
            cell.GridCoordinates = new Vector2IntImpl(x, y);
            return cell;
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = GridHelperType.GetField(name, InstanceFlags);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }

        private static T GetField<T>(object target, string name)
        {
            FieldInfo field = GridHelperType.GetField(name, InstanceFlags);
            Assert.That(field, Is.Not.Null, name);
            return (T)field.GetValue(target);
        }
    }
}
