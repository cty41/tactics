using System.Linq;
using Tactics.Tbsf.Common.Cells;
using Tactics.Tbsf.Common.Units;
using Tactics.Tbsf.Unity.Cells;
using UnityEngine;

namespace Tactics.Units
{
    public class LandUnitMovementRules : MonoBehaviour, IMovementRules
    {
        [SerializeField] private ScriptableObject _landCellType;

        public float GetMovementCost(IUnit unit, ICell source, ICell destination)
        {
            return destination.MovementCost;
        }

        public bool IsCellMovableTo(IUnit unit, ICell cell)
        {
            return (cell as ITypedCell).CellType.Equals(_landCellType) && !cell.IsTaken;
        }

        public bool IsCellTraversable(IUnit unit, ICell source, ICell destination)
        {
            return !destination.CurrentUnits.Any(u => u.PlayerNumber != unit.PlayerNumber) && (destination as ITypedCell).CellType.Equals(_landCellType);
        }
    }
}