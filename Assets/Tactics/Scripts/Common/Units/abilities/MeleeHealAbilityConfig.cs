using UnityEngine;
using Tactics.Common.Units.Abilities;

namespace Tactics.Common.Units.Abilities
{
    /// <summary>
    /// Config for melee heal abilities.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Abilities/Configs/Melee Heal Ability Config")]
    public class MeleeHealAbilityConfig : AbilityConfig
    {
        [Header("Heal Settings")]
        [SerializeField] private float _healAmount = 3f;
        [SerializeField] private int _healRange = 1;

        public float HealAmount => _healAmount;
        public int HealRange => _healRange;
    }
}
