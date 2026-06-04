using Sirenix.OdinInspector;
using Tactics.Common.Skills.Graph;
using UnityEngine;

namespace Tactics.Common.Units.Abilities
{
    /// <summary>
    /// SkillGraph 能力配置。
    /// 持有 SkillGraphAsset 引用，创建 SkillGraphAbilityImpl。
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Abilities/Skill Graph Ability Config")]
    public class SkillGraphAbilityConfig : AbilityConfig
    {
        [BoxGroup("Skill Graph")]
        [SerializeField] private SkillGraphAsset _skillGraph;

        [BoxGroup("Skill Graph")]
        [SerializeField] private int _targetRange = 1;

        public SkillGraphAsset SkillGraph => _skillGraph;
        public int TargetRange => _targetRange;

        public override IAbility CreateAbility(IUnit owner)
        {
            return new SkillGraphAbilityImpl(owner, this);
        }
    }
}
