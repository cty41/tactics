using UnityEngine;
using Tactics.Common.Units.Abilities;

namespace Tactics.Common.Units.Abilities
{
    /// <summary>
    /// Config for melee attack abilities (range 1).
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Abilities/Configs/Melee Attack Ability Config")]
    public class MeleeAttackAbilityConfig : AbilityConfig
    {
        [Header("Melee Attack Settings")]
        [SerializeField] private int _attackRange = 1;

        public int AttackRange => _attackRange;
    }
}
