using UnityEngine;
using Tactics.Common.Units.Abilities;

namespace Tactics.Common.Units.Abilities
{
    /// <summary>
    /// Config for fireball/AOE abilities.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Abilities/Configs/Fireball Ability Config")]
    public class FireballAbilityConfig : AbilityConfig
    {
        [Header("Fireball Settings")]
        [SerializeField] private int _maxRange = 4;
        [SerializeField] private AoeShape _aoeShape = AoeShape.Cross;

        public int MaxRange => _maxRange;
        public AoeShape AoeShape => _aoeShape;
    }
}
