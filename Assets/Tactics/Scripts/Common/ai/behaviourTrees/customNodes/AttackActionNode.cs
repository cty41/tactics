using System;
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
            // Check if attack basic ability has been used this turn
            // Try to find the attack config name to check usage
            if (_unit.HasUsedBasicAbilityThisTurn("Melee Attack") || 
                _unit.HasUsedBasicAbilityThisTurn("Ranged Attack") ||
                _unit.HasUsedBasicAbilityThisTurn("Attack"))
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
            ExecuteAttackAbility(target, tcs);
            return tcs.Task;
        }

        /// <summary>
        /// Finds the best attack AbilityConfig for this unit and executes it against the target.
        /// </summary>
        private async void ExecuteAttackAbility(IUnit target, TaskCompletionSource<bool> tcs)
        {
            try
            {
                // Find the attack ability config from the unit's abilities
                var attackConfig = FindAttackConfig(_unit);
                
                if (attackConfig != null)
                {
                    // Use AbilityConfig-driven execution
                    var ability = new GenericAbilityImpl(_unit, attackConfig);
                    ability.Initialize(_gridController);
                    await ability.ExecuteEffectsAsync(new List<IUnit> { target }, _gridController);
                }
                else
                {
                    // Fallback to legacy CombatComponent damage calculation
                    var isRangedDamage = _unit.AttackRange > 1;
                    var damage = _unit.CalculateTotalDamage(target, target.CurrentCell, _unit.CurrentCell, isRangedDamage);
                    var command = new AttackCommand(target, damage);
                    _unit.AIExecuteAbility(command, _gridController, tcs);
                    return;
                }

                tcs.SetResult(true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AttackActionNode] Error executing attack: {ex.Message}");
                tcs.SetResult(false);
            }
        }

        /// <summary>
        /// Finds the best attack AbilityConfig for the unit.
        /// Prefers MeleeAttack over RangedAttack for consistency.
        /// </summary>
        private static AbilityConfig FindAttackConfig(IUnit unit)
        {
            if (unit is Unit unityUnit)
            {
                // Find ability configs that have DamageEffect
                foreach (var config in unityUnit.GetBaseAbilities())
                {
                    // Check if this ability was created from a config with DamageEffect
                    if (config is GenericAbilityImpl genericAbility)
                    {
                        // We need to access the config through reflection or by storing it
                        // For now, use a simpler approach: check the ability's Effects through the config
                    }
                }

                // Alternative: search through _abilityConfigs via a helper
                // Since we can't access private _abilityConfigs directly,
                // we'll use a naming convention approach
                return FindAttackConfigByName(unityUnit);
            }
            return null;
        }

        /// <summary>
        /// Finds attack AbilityConfig by searching for known attack config names.
        /// </summary>
        private static AbilityConfig FindAttackConfigByName(Unit unit)
        {
            // Use reflection to access _abilityConfigs
            var field = typeof(Unit).GetField("_abilityConfigs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                var abilityConfigs = field.GetValue(unit) as List<AbilityConfig>;
                if (abilityConfigs != null)
                {
                    // Priority: Melee > Ranged > Any with DamageEffect
                    AbilityConfig meleeConfig = null;
                    AbilityConfig rangedConfig = null;
                    AbilityConfig anyAttackConfig = null;

                    foreach (var config in abilityConfigs)
                    {
                        if (config == null) continue;

                        bool hasDamageEffect = config.Effects != null && config.Effects.Any(e => e is DamageEffect);
                        if (!hasDamageEffect) continue;

                        // Check if it's a melee attack (range 0-1)
                        bool isMelee = config.TargetingStrategy is SingleTargetEnemy singleTarget && singleTarget.MaxRange <= 1;
                        
                        if (isMelee)
                        {
                            meleeConfig = config;
                            break; // Melee is preferred
                        }
                        else
                        {
                            rangedConfig = config;
                        }

                        if (anyAttackConfig == null)
                        {
                            anyAttackConfig = config;
                        }
                    }

                    return meleeConfig ?? rangedConfig ?? anyAttackConfig;
                }
            }
            return null;
        }
    }
}