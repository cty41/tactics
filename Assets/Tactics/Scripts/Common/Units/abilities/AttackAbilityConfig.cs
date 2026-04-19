using UnityEngine;
using Tactics.Common.Units.Abilities;

namespace Tactics.Common.Units.Abilities
{
    /// <summary>
    /// Backwards-compatible config for attack abilities.
    /// Uses GenericAbilityImpl for attack logic with Effects and TargetingStrategy.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Abilities/Configs/Attack Ability Config")]
    public class AttackAbilityConfig : AbilityConfig
    {
        // Note: AttackAbilityConfig uses the base AbilityConfig.CreateAbility() which returns GenericAbilityImpl.
        // Attack logic is handled through Effects (DamageEffect) and TargetingStrategy (SingleTargetEnemy).
    }
}