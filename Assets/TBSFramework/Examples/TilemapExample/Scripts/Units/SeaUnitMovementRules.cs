using System.Collections.Generic;
using TurnBasedStrategyFramework.Common.Cells;
using TurnBasedStrategyFramework.Common.Units;
using TurnBasedStrategyFramework.Unity.Cells;
using TurnBasedStrategyFramework.Unity.Units.Abilities;
using UnityEngine;

namespace TurnBasedStrategyFramework.Unity.Examples.TilemapExample.Units
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