using System.Linq;
using Tactics.Cells;
using Tactics.Common.Cells;
using Tactics.Common.Units;
using UnityEngine;

namespace Tactics.Units
{
    public class LandUnitMovementRules : MonoBehaviour, IMovementRules
    {
        private ICellManager _cellManager;

        private void Awake()
        {
            _cellManager = FindFirstObjectByType<TilemapCellManager>();
        }

        public float GetMovementCost(IUnit unit, ICell source, ICell destination)
        {
            return destination.MovementCost;
        }

        public bool IsCellMovableTo(IUnit unit, ICell cell)
        {
            if (_cellManager != null && !_cellManager.IsCellWalkable(cell))
                return false;
            return !cell.IsTaken;
        }

        public bool IsCellTraversable(IUnit unit, ICell source, ICell destination)
        {
            if (_cellManager != null && !_cellManager.IsCellWalkable(destination))
                return false;
            return !destination.CurrentUnits.Any(u => u.PlayerNumber != unit.PlayerNumber);
        }
    }
}
