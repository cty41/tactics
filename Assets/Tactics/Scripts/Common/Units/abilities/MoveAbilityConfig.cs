using UnityEngine;
using Tactics.Common.Controllers;

namespace Tactics.Common.Units.Abilities
{
    /// <summary>
    /// Config for movement abilities.
    /// Uses MoveAbilityImpl for actual movement logic.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Abilities/Configs/Move Ability Config")]
    public class MoveAbilityConfig : AbilityConfig
    {
        [Header("Movement Settings")]
        [SerializeField] private bool _requiresPathfinding = true;

        public bool RequiresPathfinding => _requiresPathfinding;

        /// <summary>
        /// Creates a MoveAbilityImpl instance instead of GenericAbilityImpl.
        /// Movement requires complex pathfinding and grid state handling.
        /// </summary>
        public new IAbility CreateAbility(IUnit owner)
        {
            return new MoveAbilityImpl(owner);
        }
    }
}
