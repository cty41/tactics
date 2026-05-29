using System;
using UnityEngine;

namespace Tactics.Common.AI.MonsterAI
{
    /// <summary>
    /// AI 行为风格配置。
    /// 从 AiBrainAsset 中拆出，支持同一脑图 + 不同风格 = 不同行为。
    /// </summary>
    [CreateAssetMenu(fileName = "NewAIProfile", menuName = "Tactics/AI/AI Profile")]
    public class AIProfile : ScriptableObject
    {
        [Header("评分维度开关")]
        [SerializeField] private bool _enableDistanceScore = true;
        [SerializeField] private bool _enableTargetHealthScore = true;
        [SerializeField] private bool _enableSelfHealthScore = true;
        [SerializeField] private bool _enableTargetValueScore = false;
        [SerializeField] private bool _enablePositionSafetyScore = true;
        [SerializeField] private bool _enableKillPotentialScore = true;
        [SerializeField] private bool _enableAllyProximityScore = false;

        [Header("距离目标评分")]
        [SerializeField] private float _distanceWeight = 5f;
        [SerializeField] private AnimationCurve _distanceCurve = AnimationCurve.Linear(0, 1, 1, 0);

        [Header("目标血量评分")]
        [SerializeField] private float _targetHealthWeight = 3f;
        [SerializeField] private AnimationCurve _targetHealthCurve = AnimationCurve.Linear(0, 0, 1, 1);

        [Header("自身血量评分")]
        [SerializeField] private float _selfHealthWeight = 2f;
        [SerializeField] private AnimationCurve _selfHealthCurve = AnimationCurve.Linear(0, 0, 1, 1);

        [Header("目标价值评分")]
        [SerializeField] private float _targetValueWeight = 1f;
        [SerializeField] private AnimationCurve _targetValueCurve = AnimationCurve.Linear(0, 0, 1, 1);

        [Header("位置安全评分")]
        [SerializeField] private float _positionSafetyWeight = 4f;
        [SerializeField] private AnimationCurve _positionSafetyCurve = AnimationCurve.Linear(0, 0, 1, 1);

        [Header("击杀可能性评分")]
        [SerializeField] private float _killPotentialWeight = 8f;
        [SerializeField] private AnimationCurve _killPotentialCurve = AnimationCurve.Linear(0, 0, 1, 1);

        [Header("协同作战评分")]
        [SerializeField] private float _allyProximityWeight = 1f;
        [SerializeField] private AnimationCurve _allyProximityCurve = AnimationCurve.Linear(0, 0, 1, 1);

        [Header("随机扰动")]
        [Range(0f, 0.3f)]
        [SerializeField] private float _noiseFactor = 0.05f;

        [Header("行为风格")]
        [SerializeField] private string _styleLabel = "Default";

        // ── 公共属性 ──

        public bool EnableDistanceScore => _enableDistanceScore;
        public bool EnableTargetHealthScore => _enableTargetHealthScore;
        public bool EnableSelfHealthScore => _enableSelfHealthScore;
        public bool EnableTargetValueScore => _enableTargetValueScore;
        public bool EnablePositionSafetyScore => _enablePositionSafetyScore;
        public bool EnableKillPotentialScore => _enableKillPotentialScore;
        public bool EnableAllyProximityScore => _enableAllyProximityScore;

        public string StyleLabel => _styleLabel;

        public float NoiseFactor => _noiseFactor;

        /// <summary>获取指定评分维度的权重和曲线</summary>
        public (float weight, AnimationCurve curve) GetScoreConfig(ScoreType scoreType)
        {
            return scoreType switch
            {
                ScoreType.DistanceToTarget => (_distanceWeight, _distanceCurve),
                ScoreType.TargetHealth => (_targetHealthWeight, _targetHealthCurve),
                ScoreType.SelfHealth => (_selfHealthWeight, _selfHealthCurve),
                ScoreType.TargetValue => (_targetValueWeight, _targetValueCurve),
                ScoreType.PositionSafety => (_positionSafetyWeight, _positionSafetyCurve),
                ScoreType.KillPotential => (_killPotentialWeight, _killPotentialCurve),
                ScoreType.AllyProximity => (_allyProximityWeight, _allyProximityCurve),
                _ => (1f, AnimationCurve.Linear(0, 0, 1, 1))
            };
        }

        /// <summary>应用曲线映射：输入归一化值 [0,1]，输出曲线映射值</summary>
        public float ApplyCurve(float normalizedValue, AnimationCurve curve)
        {
            if (curve == null || curve.keys.Length == 0)
                return normalizedValue;
            return Mathf.Clamp01(curve.Evaluate(Mathf.Clamp01(normalizedValue)));
        }

        private void OnValidate()
        {
            _noiseFactor = Mathf.Clamp(_noiseFactor, 0f, 0.3f);
        }
    }
}
