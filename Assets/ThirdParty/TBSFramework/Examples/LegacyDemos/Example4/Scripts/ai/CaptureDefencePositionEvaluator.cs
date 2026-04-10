using TurnBasedStrategyFramework.Common.AI.Evaluators;
using TurnBasedStrategyFramework.Common.Cells;
using TurnBasedStrategyFramework.Common.Controllers;
using TurnBasedStrategyFramework.Common.Units;
using TurnBasedStrategyFramework.Unity.Units;
using UnityEngine;

namespace TurnBasedStrategyFramework.Unity.Examples.Legacy.Example4.AI.Evaluators
{
    /// <summary>
    /// Evaluates positions near structures controlled by the same player and under potential enemy threat, 
    /// to prioritize defending those structures.
    /// </summary>
    public readonly struct CaptureDefencePositionEvaluator : IPositionEvaluator
    {
        public readonly float Weight { get; }

        private readonly ScriptableObject _structureUnitType;
        private readonly ScriptableObject _scoutUnitType;

        public CaptureDefencePositionEvaluator(float weight, ScriptableObject structureUnitType, ScriptableObject scoutUnitType) : this()
        {
            _structureUnitType = structureUnitType;
            _scoutUnitType = scoutUnitType;
            Weight = weight;
        }

        public float EvaluatePosition(ICell evaluatedCell, IUnit evaluatingUnit, IGridController gridController)
        {
            var neighbours = evaluatedCell.GetNeighbours(gridController.CellManager);
            int playerNumber = evaluatingUnit.PlayerNumber;

            foreach (var neighbour in neighbours)
            {
                bool hasEnemyScout = false;
                bool hasFriendlyStructure = false;
                bool hasNeutralStructure = false;

                foreach (var unit in neighbour.CurrentUnits)
                {
                    if (unit is not ITypedUnit typedUnit) continue;

                    if (typedUnit.UnitType.Equals(_scoutUnitType) && unit.PlayerNumber != playerNumber)
                    {
                        hasEnemyScout = true;
                    }

                    if (typedUnit.UnitType.Equals(_structureUnitType))
                    {
                        if (unit.PlayerNumber == playerNumber)
                        {
                            hasFriendlyStructure = true;
                        }
                        else if (unit.PlayerNumber == -1)
                        {
                            hasNeutralStructure = true;
                        }
                    }
                }

                if (hasEnemyScout)
                {
                    if (hasFriendlyStructure)
                    {
                        return 1f;
                    }
                    if (hasNeutralStructure)
                    {
                        return 0.5f;
                    }
                }
            }

            return 0f;
        }


        public void Initialize(IUnit evaluatingUnit, IGridController gridController)
        {
        }
    }
}