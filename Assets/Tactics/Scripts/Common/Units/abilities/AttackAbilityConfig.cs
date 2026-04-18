using UnityEngine;
using Tactics.Common.Controllers;

namespace Tactics.Common.Units.Abilities
{
    /// <summary>
    /// Config for basic attack abilities.
    /// Uses GenericAbilityImpl for attack logic with Effects and TargetingStrategy.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Abilities/Configs/Attack Ability Config")]
    public class AttackAbilityConfig : AbilityConfig
    {
        [Header("Attack Settings")]
        [SerializeField] private int _attackRange = 1;
        [SerializeField] private bool _isRanged;

        public int AttackRange => _attackRange;
        public bool IsRanged => _isRanged;

        // Note: AttackAbilityConfig uses the base AbilityConfig.CreateAbility() which returns GenericAbilityImpl.
        // Attack logic is handled through Effects (DamageEffect) and TargetingStrategy (SingleTargetEnemy).
    }
}
