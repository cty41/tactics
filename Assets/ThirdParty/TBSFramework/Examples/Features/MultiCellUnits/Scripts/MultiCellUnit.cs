using System.Collections.Generic;
using System.Linq;
using TurnBasedStrategyFramework.Common.Cells;
using TurnBasedStrategyFramework.Common.Controllers;
using TurnBasedStrategyFramework.Common.Units;
using TurnBasedStrategyFramework.Common.Utilities;
using TurnBasedStrategyFramework.Unity.Cells;
using TurnBasedStrategyFramework.Unity.Units;
using UnityEngine;

namespace TurnBasedStrategyFramework.Unity.Examples.Features.MultiCellUnits
{
    /// <summary>
    /// Represents a unit that occupies multiple cells on either a square or hexagonal grid.
    /// </summary>
    public class MultiCellUnit : Unit
    {
        [SerializeField] private int _unitSize = 1;
        [SerializeField] private bool _isHexMap;

        private ICellManager _cellManager;
        private HexGridType _gridType;

        private Vector2IntImpl[] _squareOffsets;
        private Vector3IntImpl[] _hexCubeOffsets;

        public override void Initialize(IGridController gridController)
        {
            base.Initialize(gridController);
            _cellManager = gridController.CellManager;

            if (_isHexMap && CurrentCell is Hexagon hex)
            {
                _gridType = hex.GridType;
                _hexCubeOffsets = GetHexShapeOffsets(_unitSize).ToArray();
            }
            else
            {
                _squareOffsets = GetSquareShapeOffsets(_unitSize).ToArray();
            }

            UnitMoved += OnUnitMoved;
            RefreshOccupiedCells(CurrentCell, true);
        }

        public override bool IsCellTraversable(ICell source, ICell destination)
        {
            return CanOccupy(destination);
        }

        public override bool IsCellMovableTo(ICell cell)
        {
            return CanOccupy(cell);
        }

        /// <summary>
        /// Verifies if every cell covered by the unit's shape at the anchor position is available.
        /// </summary>
        /// <param name="anchor">The center/pivot cell of the unit.</param>
        /// <returns>True if all cells are within grid bounds and not taken by others.</returns>
        private bool CanOccupy(ICell anchor)
        {
            foreach (var cell in GetCellsForPos(anchor))
            {
                if (cell == null || (cell.IsTaken && !cell.CurrentUnits.Contains(this)))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Updates grid cell states when the unit moves.
        /// </summary>
        private void OnUnitMoved(UnitMovedEventArgs eventArgs)
        {
            RefreshOccupiedCells(eventArgs.SourceCell, false);
            RefreshOccupiedCells(eventArgs.TargetCell, true);
        }

        /// <summary>
        /// Marks/Unmarks cells as taken and updates their unit references.
        /// </summary>
        /// <param name="anchor">The pivot cell position.</param>
        /// <param name="isTaken">If true, marks as taken; if false, clears them.</param>
        private void RefreshOccupiedCells(ICell anchor, bool isTaken)
        {
            foreach (var cell in GetCellsForPos(anchor))
            {
                if (cell == null)
                {
                    continue;
                }

                if (isTaken)
                {
                    if (!cell.CurrentUnits.Contains(this))
                    {
                        cell.CurrentUnits.Add(this);
                    }
                    cell.IsTaken = true;
                }
                else
                {
                    cell.CurrentUnits.Remove(this);
                    cell.IsTaken = false;
                }
            }
        }

        public override bool IsUnitAttackable(IUnit otherUnit, ICell otherUnitCell, ICell attackSourceCell)
        {
            var myCells = GetCellsForPos(attackSourceCell);
            var multiCellEnemy = otherUnit as MultiCellUnit;

            var enemyCells = multiCellEnemy.GetCellsForPos(otherUnitCell);

            foreach (var myCell in myCells)
            {
                if (myCell == null) continue;
                foreach (var enemyCell in enemyCells)
                {
                    if (enemyCell == null) continue;

                    if (myCell.GetDistance(enemyCell) <= AttackRange)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Populates the provided buffer with all cells occupied by the unit based on its pivot position.
        /// Handles coordinate conversion for hexagonal grids.
        /// </summary>
        /// <param name="anchorCell">The pivot cell.</param>
        /// <param name="cellBuffer">The list to be cleared and filled with cells.</param>
        private List<ICell> GetCellsForPos(ICell anchorCell)
        {
            var cellBuffer = new List<ICell>();
            var anchorCoords = anchorCell.GridCoordinates;

            if (_isHexMap)
            {
                Vector3IntImpl anchorCube = HexagonHelper.OffsetToCubeCoordinates(anchorCoords, _gridType);
                foreach (var offset in _hexCubeOffsets)
                {
                    var targetCube = new Vector3IntImpl(anchorCube.x + offset.x, anchorCube.y + offset.y, anchorCube.z + offset.z);
                    var targetOffset = HexagonHelper.CubeToOffsetCoordinates(targetCube, _gridType);
                    cellBuffer.Add(_cellManager.GetCellAt(targetOffset));
                }
            }
            else
            {
                foreach (var offset in _squareOffsets)
                {
                    cellBuffer.Add(_cellManager.GetCellAt(anchorCoords.Add(offset)));
                }
            }
            return cellBuffer;
        }

        /// <summary>
        /// Calculates relative cube coordinate offsets for a hexagonal shape of a given radius.
        /// </summary>
        private static IEnumerable<Vector3IntImpl> GetHexShapeOffsets(int radius)
        {
            for (int q = -radius; q <= radius; q++)
            {
                int r1 = Mathf.Max(-radius, -q - radius);
                int r2 = Mathf.Min(radius, -q + radius);
                for (int r = r1; r <= r2; r++)
                {
                    yield return new Vector3IntImpl(q, r, -q - r);
                }
            }
        }

        /// <summary>
        /// Calculates relative grid coordinate offsets for a square shape of a given size.
        /// </summary>
        private static IEnumerable<Vector2IntImpl> GetSquareShapeOffsets(int size)
        {
            int start = -(size / 2);
            int end = start + size;

            for (int x = start; x < end; x++)
            {
                for (int y = start; y < end; y++)
                {
                    yield return new Vector2IntImpl(x, y);
                }
            }
        }
    }
}