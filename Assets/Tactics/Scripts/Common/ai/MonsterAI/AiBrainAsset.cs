using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tactics.Common.AI.MonsterAI
{
    /// <summary>
    /// 怪物原型级 AI 脚本资产。
    /// 持有决策图、默认参数、调试选项、版本信息。
    /// 单位通过 AiBrainAsset 驱动 AI 决策，不再支持旧 BehaviourTreeResource。
    /// </summary>
    [CreateAssetMenu(fileName = "NewAiBrain", menuName = "Tactics/AI/Ai Brain Asset")]
    public class AiBrainAsset : ScriptableObject
    {
        [Header("决策配置")]
        [Tooltip("AI 决策图资产，定义意图、规则、评分节点")]
        [SerializeField] private AiDecisionGraph _decisionGraph;

        [Header("评分风格")]
        [Tooltip("AI 行为风格配置（权重、曲线、扰动）")]
        [SerializeField] private AIProfile _profile;

        [Tooltip("旧资产只使用图节点权重；新资产可将图权重与 Profile 权重相乘")]
        [SerializeField] private AiScoreWeightMode _scoreWeightMode = AiScoreWeightMode.LegacyGraphOnly;

        [Header("默认参数")]
        [Tooltip("低血量阈值（百分比），低于此值触发撤退/保命意图")]
        [Range(0f, 1f)]
        [SerializeField] private float _lowHealthThreshold = 0.3f;

        [Tooltip("可击杀伤害阈值（百分比），目标血量低于此值视为可击杀")]
        [Range(0f, 1f)]
        [SerializeField] private float _killableDamageThreshold = 0.5f;

        [Tooltip("追击残血优先级加成")]
        [SerializeField] private float _lowHealthTargetBonus = 20f;

        [Tooltip("撤退意图基础分数")]
        [SerializeField] private float _retreatBaseScore = 50f;

        [Header("候选生成")]
        [Tooltip("每个目标最多保留的接敌候选数，避免可达格 × 目标导致候选爆炸")]
        [Min(1)]
        [SerializeField] private int _maxEngageCandidatesPerTarget = 3;

        [Header("固定 Pattern")]
        [Tooltip("按顺序执行的可学习技能循环。没有合法步骤时回退 Generic AI，且不推进游标")]
        [SerializeField] private List<AiPatternStep> _patternSteps = new List<AiPatternStep>();

        [Header("调试选项")]
        [Tooltip("是否启用详细日志")]
        [SerializeField] private bool _enableVerboseLogging = false;

        [Tooltip("是否逐条输出规则过滤日志。关闭时会按规则聚合输出，避免刷屏")]
        [SerializeField] private bool _enableDetailedRuleFilterLog = false;

        [Tooltip("是否在图上高亮命中节点")]
        [SerializeField] private bool _highlightDecisionNodes = true;

        [Header("版本信息")]
        [Tooltip("AI 配置版本号")]
        [SerializeField] private string _version = "1.0.0";

        // 公共属性
        public AiDecisionGraph DecisionGraph => _decisionGraph;
        public AIProfile Profile => _profile;
        public AiScoreWeightMode ScoreWeightMode => _scoreWeightMode;
        public float LowHealthThreshold => _lowHealthThreshold;
        public float KillableDamageThreshold => _killableDamageThreshold;
        public float LowHealthTargetBonus => _lowHealthTargetBonus;
        public float RetreatBaseScore => _retreatBaseScore;
        public int MaxEngageCandidatesPerTarget => _maxEngageCandidatesPerTarget;
        public IReadOnlyList<AiPatternStep> PatternSteps => _patternSteps;
        public bool EnableVerboseLogging => _enableVerboseLogging;
        public bool EnableDetailedRuleFilterLog => _enableDetailedRuleFilterLog;
        public bool HighlightDecisionNodes => _highlightDecisionNodes;
        public string Version => _version;

        /// <summary>
        /// 验证资产配置是否有效。
        /// </summary>
        public bool IsValid()
        {
            if (_decisionGraph == null)
            {
                Runtime.Utilities.TLog.Warning("[AiBrainAsset] Decision graph is null.");
                return false;
            }
            return true;
        }

        private void OnValidate()
        {
            _lowHealthThreshold = Mathf.Clamp01(_lowHealthThreshold);
            _killableDamageThreshold = Mathf.Clamp01(_killableDamageThreshold);
            _maxEngageCandidatesPerTarget = Mathf.Max(1, _maxEngageCandidatesPerTarget);
        }
    }

    public enum AiScoreWeightMode
    {
        LegacyGraphOnly,
        GraphTimesProfile
    }

    /// <summary>
    /// One learnable step in a monster's fixed high-threat loop.
    /// </summary>
    [Serializable]
    public sealed class AiPatternStep
    {
        [SerializeField] private string _abilityName;

        public string AbilityName => _abilityName;
    }
}
