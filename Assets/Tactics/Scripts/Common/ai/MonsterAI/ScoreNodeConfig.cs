using UnityEngine;

namespace Tactics.Common.AI.MonsterAI
{
    /// <summary>
    /// 评分节点配置。
    /// 评分只产出归一化分值和权重，不直接执行。
    /// </summary>
    [System.Serializable]
    public class ScoreNodeConfig
    {
        [Tooltip("评分名称")]
        [SerializeField] private string _scoreName = "New Score";

        [Tooltip("评分类型")]
        [SerializeField] private ScoreType _scoreType;

        [Tooltip("是否启用此评分")]
        [SerializeField] private bool _enabled = true;

        [Tooltip("评分权重（用于加权求和）")]
        [SerializeField] private float _weight = 1f;

        [Tooltip("评分参数（根据评分类型不同而不同）")]
        [SerializeField] private float _parameter;

        [Tooltip("响应曲线（非线性映射）")]
        [SerializeField] private AnimationCurve _responseCurve = AnimationCurve.Linear(0, 0, 1, 1);

        public string ScoreName { get => _scoreName; set => _scoreName = value; }
        public ScoreType ScoreType { get => _scoreType; set => _scoreType = value; }
        public bool Enabled { get => _enabled; set => _enabled = value; }
        public float Weight { get => _weight; set => _weight = value; }
        public float Parameter { get => _parameter; set => _parameter = value; }
        public AnimationCurve ResponseCurve { get => _responseCurve; set => _responseCurve = value; }
    }

    /// <summary>
    /// 评分类型枚举。
    /// </summary>
    public enum ScoreType
    {
        /// <summary>距离目标越近分数越高</summary>
        DistanceToTarget,
        /// <summary>目标血量越低分数越高</summary>
        TargetHealth,
        /// <summary>自身血量越高分数越高</summary>
        SelfHealth,
        /// <summary>目标价值（基于单位类型）</summary>
        TargetValue,
        /// <summary>位置安全度（离敌人越远越安全）</summary>
        PositionSafety,
        /// <summary>技能效果匹配度</summary>
        AbilityEffectiveness,
        /// <summary>击杀可能性</summary>
        KillPotential,
        /// <summary>协同作战（与友军距离）</summary>
        AllyProximity,
        /// <summary>AOE 命中价值</summary>
        AOEValue,
        /// <summary>治疗紧急度</summary>
        HealUrgency,
        /// <summary>控制效果价值</summary>
        ControlValue,
        /// <summary>增益效果价值</summary>
        BuffUtility,
        /// <summary>减益效果价值</summary>
        DebuffUtility
    }
}
