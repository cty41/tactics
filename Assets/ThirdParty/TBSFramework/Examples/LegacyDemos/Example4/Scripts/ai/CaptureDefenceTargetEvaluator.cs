using System.Linq;
using TurnBasedStrategyFramework.Common.AI.Evaluators;
using TurnBasedStrategyFramework.Common.Controllers;
using TurnBasedStrategyFramework.Common.Units;
using TurnBasedStrategyFramework.Unity.Units;
using UnityEngine;

namespace TurnBasedStrategyFramework.Unity.Examples.Legacy.Example4.AI.Evaluators
{
    /// <summary>
    /// Evaluates enemy units that are capturing structures, to prioritize attacking those units.
    /// </summary>
    public readonly struct CaptureDefenceTargetEvaluator : ITargetEvaluator
    {
        public readonly float Weight { get; }
        public readonly ScriptableObject _structureUnitType;

        public CaptureDefenceTargetEvaluator(float weight, ScriptableObject structureUnitType)
        {
            Weight = weight;
            _structureUnitType = structureUnitType;
        }

        public float EvaluateTarget(IUnit evaluatedTarget, IUnit evaluatingUnit, IGridController gridController)
        {
            var structureUnitType = _structureUnitType;
            return evaluatedTarget.CurrentCell.CurrentUnits.Any(u => (u as ITypedUnit).UnitType.Equals(structureUnitType) && u.PlayerNumber == evaluatingUnit.PlayerNumber) ? 1 : 0;
        }

        public void Initialize(IUnit evaluatingUnit, IGridController gridController)
        {
        }
    }
}