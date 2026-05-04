using System;
using System.Collections.Generic;
using System.Linq;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Units;

namespace Tactics.Common.AI.Evaluators
{
    /// <summary>
    /// Evaluates a position based on the potential damage that can be dealt to enemy units from that position.
    /// </summary>
    public struct DamageDealtPositionEvaluator : IPositionEvaluator
    {
        public readonly float Weight { get; }
        private readonly float _decayRate;
        private readonly float _epsilon;

        private const int MaxScoreDistance = 3;

        private readonly Dictionary<ICell, float> _baseScores;
        private readonly Dictionary<ICell, float> _accumulatedScores;
        private float _maxAccumulatedScore;

        /// <summary>
        /// Initializes a new instance of the <see cref="DamageDealtPositionEvaluator"/> struct 
        /// with the specified weight and optional decay rate.
        /// </summary>
        /// <param name="weight">The weight of this evaluator in the scoring system.</param>
        /// <param name="decayRate">The rate at which damage contribution decays with distance.</param>
        public DamageDealtPositionEvaluator(float weight, float decayRate = 0.5f)
        {
            Weight = weight;
            _decayRate = decayRate;
            _epsilon = 1e-6f;

            _baseScores = new Dictionary<ICell, float>();
            _accumulatedScores = new Dictionary<ICell, float>();
            _maxAccumulatedScore = 0;
        }

        /// <summary>
        /// Precomputes all relevant scores for the evaluating unit on all possible cells.
        /// </summary>
        /// <param name="evaluatingUnit">The unit performing the evaluation.</param>
        /// <param name="gridController">The grid controller managing the game state.</param>
        public void Initialize(IReadOnlyList<ICell> possibleCells, IUnit evaluatingUnit, IGridController gridController)
        {
            _baseScores.Clear();
            _accumulatedScores.Clear();

            var enemies = gridController.UnitManager.GetEnemyUnits(evaluatingUnit.PlayerNumber).ToList();

            var enemyDamage = enemies.ToDictionary(
                u => u,
                u => evaluatingUnit.CalculateExpectedTotalDamage(u));

            foreach (var cell in possibleCells)
            {
                float baseScore = enemies
                    .Where(u => evaluatingUnit.IsUnitAttackable(u, u.CurrentCell, cell))
                    .Select(u => enemyDamage[u])
                    .DefaultIfEmpty(0f)
                    .Max();

                _baseScores[cell] = baseScore;
            }

            float maxBaseScore = _baseScores.Values.Max();
            foreach (var cell in _baseScores.Keys.ToList())
            {
                _baseScores[cell] /= (maxBaseScore + _epsilon);
            }

            foreach (var cell in possibleCells)
            {
                float localSum = _baseScores[cell];
                foreach (var otherCell in possibleCells)
                {
                    float distance = otherCell.GetDistance(cell);
                    if (distance > MaxScoreDistance) continue;
                    localSum += _baseScores[otherCell] * (float)Math.Pow((1 - _decayRate), distance);
                }
                _accumulatedScores[cell] = localSum;
            }

            _maxAccumulatedScore = _accumulatedScores.Values.Max();
        }

        /// <summary>
        /// Evaluates a position based on the potential damage that can be dealt to enemy units 
        /// from the specified cell, using precomputed distance-decayed scores.
        /// </summary>
        /// <param name="evaluatedCell">The cell to evaluate.</param>
        /// <param name="evaluatingUnit">The unit performing the evaluation.</param>
        /// <param name="gridController">The grid controller managing the game state.</param>
        /// <returns>A normalized score indicating how beneficial the cell is for dealing damage.</returns>
        public readonly float EvaluatePosition(ICell evaluatedCell, IUnit evaluatingUnit, IGridController gridController)
        {
            float finalScore = _accumulatedScores[evaluatedCell];
            return finalScore / (_maxAccumulatedScore + _epsilon);
        }
    }
}
