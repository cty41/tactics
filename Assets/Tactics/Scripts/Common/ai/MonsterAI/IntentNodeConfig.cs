using System.Collections.Generic;
using UnityEngine;

namespace Tactics.Common.AI.MonsterAI
{
    /// <summary>
    /// 意图节点配置。
    /// 定义一个意图类型及其关联的规则和评分节点。
    /// </summary>
    [System.Serializable]
    public class IntentNodeConfig
    {
        [Tooltip("意图类型")]
        [SerializeField] private IntentType _intentType;

        [Tooltip("意图基础优先级（越高越容易被选中）")]
        [SerializeField] private float _basePriority = 10f;

        [Tooltip("是否启用此意图")]
        [SerializeField] private bool _enabled = true;

        [Tooltip("意图专属规则节点列表")]
        [SerializeField] private List<RuleNodeConfig> _rules = new();

        [Tooltip("意图专属评分节点列表")]
        [SerializeField] private List<ScoreNodeConfig> _scores = new();

        // 公共属性（带 setter，支持从图记录构建）
        public IntentType IntentType { get => _intentType; set => _intentType = value; }
        public float BasePriority { get => _basePriority; set => _basePriority = value; }
        public bool Enabled { get => _enabled; set => _enabled = value; }
        public List<RuleNodeConfig> Rules { get => _rules; set => _rules = value; }
        public List<ScoreNodeConfig> Scores { get => _scores; set => _scores = value; }

        /// <summary>
        /// 验证意图节点配置是否合法。
        /// </summary>
        public bool Validate(out List<string> errors)
        {
            errors = new List<string>();

            if (_rules == null)
                _rules = new List<RuleNodeConfig>();
            if (_scores == null)
                _scores = new List<ScoreNodeConfig>();

            // 验证规则节点
            foreach (var rule in _rules)
            {
                if (rule == null)
                {
                    errors.Add($"Intent {_intentType}: Found null rule node.");
                }
            }

            // 验证评分节点
            foreach (var score in _scores)
            {
                if (score == null)
                {
                    errors.Add($"Intent {_intentType}: Found null score node.");
                }
            }

            return errors.Count == 0;
        }
    }
}
