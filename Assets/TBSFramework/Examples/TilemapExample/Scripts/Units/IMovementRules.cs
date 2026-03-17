using TurnBasedStrategyFramework.Common.Cells;
using TurnBasedStrategyFramework.Common.Units;

namespace TurnBasedStrategyFramework.Unity.Examples.TilemapExample.Units
{
    public interface IMovementRules
    {
        bool IsCellMovableTo(IUnit unit, ICell cell);
        bool IsCellTraversable(IUnit unit, ICell source, ICell destination);
        float GetMovementCost(IUnit unit, ICell source, ICell destination);
    }
}