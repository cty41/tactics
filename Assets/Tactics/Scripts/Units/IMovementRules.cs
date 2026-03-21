using Tactics.Tbsf.Common.Cells;
using Tactics.Tbsf.Common.Units;

namespace Tactics.Units
{
    public interface IMovementRules
    {
        bool IsCellMovableTo(IUnit unit, ICell cell);
        bool IsCellTraversable(IUnit unit, ICell source, ICell destination);
        float GetMovementCost(IUnit unit, ICell source, ICell destination);
    }
}