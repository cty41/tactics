using System;
using Tactics.Runtime.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tactics.Common.AI.Evaluators;
using Tactics.Common.Controllers;
using Tactics.Common.Units;
using Tactics.Common.Units.Abilities;
using UnityEngine;

namespace Tactics.Common.AI.BehaviourTrees
{
    /// <summary>
    /// Represents an attack action node in a behavior tree, responsible for executing an attack action by evaluating and selecting a target.
    /// Now uses AbilityConfig system for damage calculation, ensuring config-driven effects are applied.
    /// </summary>
    public class AttackActionNode : ITreeNode
    {
        /// <summary>
        /// The unit that will execute the attack.
        /// </summary>
        private readonly IUnit _unit;

        /// <summary>
        /// The grid controller responsible for managing the game state.
        /// </summary>
        private readonly IGridController _gridController;

        /// <summary>
        /// The evaluators used to determine the best target for the attack.
        /// </summary>
        private readonly IEnumerable<ITargetEvaluator> _targetEvaluators;

        /// <summary>
        /// Initializes a new instance of the <see cref="AttackActionNode"/> class with the specified unit, grid controller, and target evaluators.
        /// </summary>
        /// <param name="unit">The unit that will perform the attack.</param>
        /// <param name="gridController">The grid controller.</param>
        /// <param name="targetEvaluators">The collection of target evaluators used to assess the value of potential targets.</param>
        public AttackActionNode(IUnit unit, IGridController gridController, IEnumerable<ITargetEvaluator> targetEvaluators)
        {
            _unit = unit;
            _gridController = gridController;
            _targetEvaluators = targetEvaluators;
        }

        /// <summary>
        /// Executes the attack action by selecting the best target based on the provided evaluators and initiating the attack.
        /// </summary>
        /// <returns>A task representing the execution, with a boolean result indicating success.</returns>
        public Task<bool> Execute(bool debugMode)
        {
            var attackAbility = FindAttackAbility(_unit);
            if (attackAbility != null && !attackAbility.CanPerform(_gridController))
            {
                return Task.FromResult(false);
            }

            var enemyUnits = _gridController.UnitManager.GetEnemyUnits(_unit.PlayerNumber);
            var attackableUnits = enemyUnits.Where(u => _unit.IsUnitAttackable(u, u.CurrentCell, _unit.CurrentCell));

            if (!attackableUnits.Any())
            {
                return Task.FromResult(false);
            }

            foreach (var evaluator in _targetEvaluators)
            {
                evaluator.Initialize(_unit, _gridController);
            }

            IUnit target = null;
            float maxScore = float.MinValue;
            foreach (var unit in attackableUnits)
            {
                float currentScore = _targetEvaluators.Sum(e => e.EvaluateTarget(unit, _unit, _gridController));
                if (currentScore > maxScore)
                {
                    maxScore = currentScore;
                    target = unit;
                }
            }

            if (target == null)
            {
                return Task.FromResult(false);
            }

            var tcs = new TaskCompletionSource<bool>();
            ExecuteAttackAbility(target, attackAbility, tcs);
            return tcs.Task;
        }

        /// <summary>
        /// Executes the attack against the target using the provided ability.
        /// </summary>
        private async void ExecuteAttackAbility(IUnit target, GenericAbilityImpl attackAbility, TaskCompletionSource<bool> tcs)
        {
            try
            {
                if (attackAbility != null)
                {
                    await attackAbility.ExecuteEffectsAsync(new List<IUnit> { target }, _gridController);
                    tcs.SetResult(true);
                }
                else
                {
                    TLog.Error("[AttackActionNode] No attack ability found. Ensure unit has AbilityConfig with DamageEffect.");
                    tcs.SetResult(false);
                }
            }
            catch (Exception ex)
            {
                TLog.Error($"[AttackActionNode] Error executing attack: {ex.Message}");
                tcs.SetResult(false);
            }
        }

        /// <summary>
        /// Finds the best attack ability for the unit from its registered abilities.
        /// Prefers melee attacks (range <= 1) over ranged.
        /// </summary>
        private static GenericAbilityImpl FindAttackAbility(IUnit unit)
        {
            var attackAbilities = unit.GetBaseAbilities()
                .OfType<GenericAbilityImpl>()
                .Where(a => a.Config != null && a.Config.Effects != null && a.Config.Effects.Any(e => e is DamageEffect))
                .ToList();

            if (!attackAbilities.Any())
            {
                return null;
            }

            // Priority: Melee > Any
            return attackAbilities.FirstOrDefault(a =>
                a.Config.TargetingStrategy is SingleTargetEnemy single && single.MaxRange <= 1)
                ?? attackAbilities.First();
        }
    }
}