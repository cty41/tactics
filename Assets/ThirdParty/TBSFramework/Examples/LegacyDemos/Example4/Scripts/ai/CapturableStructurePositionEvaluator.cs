using System.Linq;
using TurnBasedStrategyFramework.Common.AI.Evaluators;
using TurnBasedStrategyFramework.Common.Cells;
using TurnBasedStrategyFramework.Common.Controllers;
using TurnBasedStrategyFramework.Common.Units;
using TurnBasedStrategyFramework.Unity.Units;
using UnityEngine;

namespace TurnBasedStrategyFramework.Unity.Examples.Legacy.Example4.AI.Evaluators
{
    /// <summary>
    /// Evaluates the score of a position based on the presence of capturable structures.
    /// </summary>
    public readonly struct CapturableStructurePositionEvaluator : IPositionEvaluator
    {
        public readonly float Weight { get; }
        private readonly ScriptableObject _structureUnitType;

        public CapturableStructurePositionEvaluator(float weight, ScriptableObject structureUnitType) : this()
        {
            Weight = weight;
            _structureUnitType = structureUnitType;
        }

        public float EvaluatePosition(ICell evaluatedCell, IUnit evaluatingUnit, IGridController gridController)
        {
            var structureUnitType = _structureUnitType;
            return evaluatedCell.CurrentUnits.Any(u => (u as ITypedUnit).UnitType.Equals(structureUnitType) && u.PlayerNumber != evaluatingUnit.PlayerNumber) ? 1 : 0;
        }

        public void Initialize(IUnit evaluatingUnit, IGridController gridController)
        {
        }
    }
}