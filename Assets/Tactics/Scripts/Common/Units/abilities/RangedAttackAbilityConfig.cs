using UnityEngine;
using Tactics.Common.Units.Abilities;

namespace Tactics.Common.Units.Abilities
{
    /// <summary>
    /// Config for ranged attack abilities with minimum range.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Abilities/Configs/Ranged Attack Ability Config")]
    public class RangedAttackAbilityConfig : AbilityConfig
    {
        [Header("Ranged Attack Settings")]
        [SerializeField] private int _maxRange = 5;
        [SerializeField] private int _minRange = 2;

        public int MaxRange => _maxRange;
        public int MinRange => _minRange;
    }
}
