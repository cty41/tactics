using System.Collections.Generic;
using Tactics.Tbsf.Common.Cells;
using Tactics.Tbsf.Common.Units;
using Tactics.Tbsf.Unity.Cells;
using Tactics.Tbsf.Unity.Units.Abilities;
using UnityEngine;

namespace Tactics.Units
{
    public class SeaUnitMovementRules : MonoBehaviour, IMovementRules
    {
        [SerializeField] private ScriptableObject _waterCellType;
        private MoveComponent _moveComponent;

        public bool IsCellMovableTo(IUnit unit, ICell cell)
        {
            return !cell.IsTaken && (cell as ITypedCell).CellType.Equals(_waterCellType);
        }

        public bool IsCellTraversable(IUnit unit, ICell source, ICell destination)
        {
            return (destination as ITypedCell).CellType.Equals(_waterCellType);
        }

        public Dictionary<ICell, Dictionary<ICell, float>> GetGraphEdges(IUnit unit, ICellManager cellManager)
        {
            _moveComponent = new UnityMoveComponent(unit);
            return _moveComponent.GetGraphEdges(cellManager);
        }

        public float GetMovementCost(IUnit unit, ICell source, ICell destination)
        {
            return destination.MovementCost;
        }
    }
}