using Tactics.Common.Cells;
using Tactics.Common.Units;
using UnityEngine;

namespace Tactics.Units
{
    public class AirUnitMovementRules : MonoBehaviour, IMovementRules
    {
        public float GetMovementCost(IUnit unit, ICell source, ICell destination)
        {
            return 1.0f;
        }

        public bool IsCellMovableTo(IUnit unit, ICell cell)
        {
            return !cell.IsTaken;
        }

        public bool IsCellTraversable(IUnit unit, ICell source, ICell destination)
        {
            return true;
        }
    }
}