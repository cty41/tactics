using System.Linq;
using Tactics.Common.Cells;
using Tactics.Common.Units;
using UnityEngine;

namespace Tactics.Units
{
    public class LandUnitMovementRules : MonoBehaviour, IMovementRules
    {
        public float GetMovementCost(IUnit unit, ICell source, ICell destination)
        {
            return destination.MovementCost;
        }

        public bool IsCellMovableTo(IUnit unit, ICell cell)
        {
            return !cell.IsTaken;
        }

        public bool IsCellTraversable(IUnit unit, ICell source, ICell destination)
        {
            return !destination.CurrentUnits.Any(u => u.PlayerNumber != unit.PlayerNumber);
        }
    }
}
