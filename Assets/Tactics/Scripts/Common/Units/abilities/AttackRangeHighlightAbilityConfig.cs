using UnityEngine;
using Tactics.Common.Units.Abilities;

namespace Tactics.Common.Units.Abilities
{
    /// <summary>
    /// Config for attack range highlight (passive ability).
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Abilities/Configs/Attack Range Highlight Config")]
    public class AttackRangeHighlightAbilityConfig : AbilityConfig
    {
        [Header("Highlight Settings")]
        [SerializeField] private int _displayRange = 1;

        public int DisplayRange => _displayRange;
    }
}
