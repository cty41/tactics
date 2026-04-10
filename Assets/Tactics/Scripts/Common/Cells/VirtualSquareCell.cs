using System;
using System.Collections.Generic;
using Tactics.Tbsf.Common.Cells;
using Tactics.Tbsf.Common.Units;
using Tactics.Tbsf.Common.Utilities;
using UnityEngine;

namespace Tactics.Cells
{
    /// <summary>
    /// A pure c# class representing a square cell in the scene.
    /// </summary>
    public class VirtualSquareCell : ICell, ITypedCell
    {
        public event Action<ICell> CellClicked;
        public event Action<ICell> CellHighlighted;
        public event Action<ICell> CellDehighlighted;

        public ScriptableObject CellType { get; set; }
        public VirtualSquareCell(Vector2IntImpl coords, Vector3Impl worldPosition, float movementCost, bool isTaken, ScriptableObject cellType)
        {
            GridCoordinates = coords;
            WorldPosition = worldPosition;
            MovementCost = movementCost;
            IsTaken= isTaken;
            CellType = cellType;

            CurrentUnits = new List<IUnit>();
        }

        public Vector2IntImpl GridCoordinates { get; set; }
        public Vector3Impl WorldPosition { get; set; }

        public bool IsTaken { get; set; }
        public float MovementCost { get; set; }
        public IList<IUnit> CurrentUnits { get; }

        public int GetDistance(ICell other)
        {
            return SquareHelper.GetDistance(this, other);
        }

        public void OnMouseEnter()
        {
            CellHighlighted?.Invoke(this);
        }
        public void OnMouseExit()
        {
            CellDehighlighted?.Invoke(this);
        }
        public void OnMouseDown()
        {
            CellClicked?.Invoke(this);
        }

        public IEnumerable<ICell> GetNeighbours(ICellManager cellManager)
        {
            return SquareHelper.GetNeighbours(this, cellManager);
        }
        public bool Equals(ICell other)
        {
            return CellHelper.Equals(this, other);
        }

        public override bool Equals(object obj)
        {
            return CellHelper.Equals(this, obj);
        }

        public override int GetHashCode()
        {
            return CellHelper.GetHashCode(this);
        }

        public void InvokeCellHighlighted()
        {
            CellHighlighted?.Invoke(this);
        }

        public void InvokeCellDehighlighted()
        {
            CellDehighlighted?.Invoke(this);
        }

        public void InvokeCellClicked()
        {
            CellClicked?.Invoke(this);
        }
    }
}