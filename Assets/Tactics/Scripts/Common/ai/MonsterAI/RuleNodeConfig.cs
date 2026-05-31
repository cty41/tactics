using UnityEngine;

namespace Tactics.Common.AI.MonsterAI
{
    /// <summary>
    /// 规则节点配置。
    /// 规则只做硬门禁，不做加减分。
    /// </summary>
    [System.Serializable]
    public class RuleNodeConfig
    {
        [Tooltip("规则名称")]
        [SerializeField] private string _ruleName = "New Rule";

        [Tooltip("规则类型")]
        [SerializeField] private RuleType _ruleType;

        [Tooltip("是否启用此规则")]
        [SerializeField] private bool _enabled = true;

        [Tooltip("规则参数（根据规则类型不同而不同）")]
        [SerializeField] private float _parameter;

        // 公共属性（带 setter，支持从图记录构建）
        public string RuleName { get => _ruleName; set => _ruleName = value; }
        public RuleType RuleType { get => _ruleType; set => _ruleType = value; }
        public bool Enabled { get => _enabled; set => _enabled = value; }
        public float Parameter { get => _parameter; set => _parameter = value; }
    }

    /// <summary>
    /// 规则类型枚举。
    /// </summary>
    public enum RuleType
    {
        /// <summary>目标在攻击范围内</summary>
        TargetInRange,
        /// <summary>目标在移动+攻击范围内</summary>
        TargetInMoveAttackRange,
        /// <summary>自身血量高于阈值</summary>
        HealthAboveThreshold,
        /// <summary>自身血量低于阈值</summary>
        HealthBelowThreshold,
        /// <summary>有可用技能</summary>
        HasAvailableAbility,
        /// <summary>目标可击杀</summary>
        TargetKillable,
        /// <summary>移动目标位置安全（不在敌人攻击范围内）</summary>
        DestinationSafe,
        /// <summary>有友军在附近</summary>
        HasAllyNearby,
        /// <summary>技能候选带有指定 AI 标签（参数使用 AbilityAiTags 数值）</summary>
        HasAbilityTag,
        /// <summary>存在伤害技能候选</summary>
        HasDamageAbility,
        /// <summary>存在治疗技能候选</summary>
        HasHealAbility,
        /// <summary>存在控制技能候选</summary>
        HasControlAbility,
        /// <summary>存在 AOE 技能候选</summary>
        HasAOEAbility,
        /// <summary>目标需要治疗</summary>
        TargetNeedsHealing,
        /// <summary>有多目标命中机会</summary>
        MultiTargetOpportunity
    }
}
