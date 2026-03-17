using System.Linq;
using System.Threading.Tasks;
using TurnBasedStrategyFramework.Common.AI.BehaviourTrees;
using TurnBasedStrategyFramework.Common.Controllers;
using TurnBasedStrategyFramework.Common.Units;
using TurnBasedStrategyFramework.Unity.Examples.Legacy.Example4.Units;
using TurnBasedStrategyFramework.Unity.Examples.Legacy.Example4.Units.Abilities;
using TurnBasedStrategyFramework.Unity.Units;
using UnityEngine;

namespace TurnBasedStrategyFramework.Unity.Examples.Legacy.Example4.AI.BehaviourTrees
{
    /// <summary>
    /// AI behavior tree node that handles capturing enemy structures by a unit.
    /// </summary>
    public readonly struct CaptureActionNode : ITreeNode
    {
        private readonly IUnit _unit;
        private readonly ScriptableObject _structureUnitType;
        private readonly IGridController _gridController;

        public CaptureActionNode(IUnit unit, ScriptableObject structureUnitType, IGridController gridController)
        {
            _unit = unit;
            _structureUnitType = structureUnitType;
            _gridController = gridController;
        }

        public Task<bool> Execute(bool debugMode)
        {
            if (_unit.ActionPoints <= 0)
            {
                return Task.FromResult(false);
            }

            var structureUnitType = _structureUnitType;
            var unit = _unit;

            var _structure = unit.CurrentCell.CurrentUnits
                .FirstOrDefault(u => (u as ITypedUnit).UnitType.Equals(structureUnitType) && u.PlayerNumber != unit.PlayerNumber) as Unit;

            if (_structure != null && _structure.TryGetComponent<ICapturable>(out var capturable))
            {
                var tcs = new TaskCompletionSource<bool>();
                int loyaltyDelta = -Mathf.CeilToInt(unit.Health * 10f / unit.MaxHealth);

                unit.AIExecuteAbility(
                    new CaptureCommand(capturable, loyaltyDelta, (unit as IColoredUnit).Color, _structureUnitType, _unit.CurrentCell.GridCoordinates.x, _unit.CurrentCell.GridCoordinates.y),
                    _gridController,
                    tcs
                );

                return tcs.Task;
            }

            return Task.FromResult(false);
        }

    }
}