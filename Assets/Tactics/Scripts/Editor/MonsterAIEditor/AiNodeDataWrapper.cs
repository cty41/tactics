using System;
using Tactics.Common.AI.MonsterAI;
using UnityEditor;
using UnityEngine;

namespace Tactics.Editor.MonsterAIEditor
{
    /// <summary>意图节点 wrapper。用于 Unity Inspector 显示意图属性。</summary>
    public class AiIntentNodeWrapper : ScriptableObject
    {
        [HideInInspector] public AiDecisionGraph Graph;
        [HideInInspector] public string NodeId;
        [HideInInspector] public Action OnDataChanged;

        [Header("Intent Config")]
        [SerializeField] private IntentType _intentType;
        [SerializeField] private float _basePriority = 10f;
        [SerializeField] private bool _enabled = true;

        public IntentType IntentType
        {
            get => _intentType;
            set { _intentType = value; Apply(); }
        }
        public float BasePriority
        {
            get => _basePriority;
            set { _basePriority = value; Apply(); }
        }
        public bool Enabled
        {
            get => _enabled;
            set { _enabled = value; Apply(); }
        }

        public void Initialize(AiDecisionGraph graph, string nodeId)
        {
            Graph = graph;
            NodeId = nodeId;
            SyncFromGraph();
        }

        public void SyncFromGraph()
        {
            var record = Graph?.FindNode(NodeId) as IntentNodeRecord;
            if (record == null) return;
            _intentType = record.IntentType;
            _basePriority = record.BasePriority;
            _enabled = record.Enabled;
        }

        public void SyncToGraph()
        {
            var record = Graph?.FindNode(NodeId) as IntentNodeRecord;
            if (record == null) return;
            record.IntentType = _intentType;
            record.BasePriority = _basePriority;
            record.Enabled = _enabled;
        }

        private void Apply()
        {
            SyncToGraph();
            EditorUtility.SetDirty(Graph);
            OnDataChanged?.Invoke();
        }
    }

    /// <summary>规则节点 wrapper。用于 Unity Inspector 显示规则属性。</summary>
    public class AiRuleNodeWrapper : ScriptableObject
    {
        [HideInInspector] public AiDecisionGraph Graph;
        [HideInInspector] public string NodeId;
        [HideInInspector] public Action OnDataChanged;

        [Header("Rule Config")]
        [SerializeField] private string _ruleName = "New Rule";
        [SerializeField] private RuleType _ruleType;
        [SerializeField] private float _parameter;
        [SerializeField] private bool _enabled = true;

        public string RuleName
        {
            get => _ruleName;
            set { _ruleName = value; Apply(); }
        }
        public RuleType RuleType
        {
            get => _ruleType;
            set { _ruleType = value; Apply(); }
        }
        public float Parameter
        {
            get => _parameter;
            set { _parameter = value; Apply(); }
        }
        public bool Enabled
        {
            get => _enabled;
            set { _enabled = value; Apply(); }
        }

        public void Initialize(AiDecisionGraph graph, string nodeId)
        {
            Graph = graph;
            NodeId = nodeId;
            SyncFromGraph();
        }

        public void SyncFromGraph()
        {
            var record = Graph?.FindNode(NodeId) as RuleNodeRecord;
            if (record == null) return;
            _ruleName = record.RuleName;
            _ruleType = record.RuleType;
            _parameter = record.Parameter;
            _enabled = record.Enabled;
        }

        public void SyncToGraph()
        {
            var record = Graph?.FindNode(NodeId) as RuleNodeRecord;
            if (record == null) return;
            record.RuleName = _ruleName;
            record.RuleType = _ruleType;
            record.Parameter = _parameter;
            record.Enabled = _enabled;
        }

        private void Apply()
        {
            SyncToGraph();
            EditorUtility.SetDirty(Graph);
            OnDataChanged?.Invoke();
        }
    }

    /// <summary>评分节点 wrapper。用于 Unity Inspector 显示评分属性。</summary>
    public class AiScoreNodeWrapper : ScriptableObject
    {
        [HideInInspector] public AiDecisionGraph Graph;
        [HideInInspector] public string NodeId;
        [HideInInspector] public Action OnDataChanged;

        [Header("Score Config")]
        [SerializeField] private string _scoreName = "New Score";
        [SerializeField] private ScoreType _scoreType;
        [SerializeField] private float _weight = 1f;
        [SerializeField] private float _parameter;
        [SerializeField] private bool _enabled = true;

        public string ScoreName
        {
            get => _scoreName;
            set { _scoreName = value; Apply(); }
        }
        public ScoreType ScoreType
        {
            get => _scoreType;
            set { _scoreType = value; Apply(); }
        }
        public float Weight
        {
            get => _weight;
            set { _weight = value; Apply(); }
        }
        public float Parameter
        {
            get => _parameter;
            set { _parameter = value; Apply(); }
        }
        public bool Enabled
        {
            get => _enabled;
            set { _enabled = value; Apply(); }
        }

        public void Initialize(AiDecisionGraph graph, string nodeId)
        {
            Graph = graph;
            NodeId = nodeId;
            SyncFromGraph();
        }

        public void SyncFromGraph()
        {
            var record = Graph?.FindNode(NodeId) as ScoreNodeRecord;
            if (record == null) return;
            _scoreName = record.ScoreName;
            _scoreType = record.ScoreType;
            _weight = record.Weight;
            _parameter = record.Parameter;
            _enabled = record.Enabled;
        }

        public void SyncToGraph()
        {
            var record = Graph?.FindNode(NodeId) as ScoreNodeRecord;
            if (record == null) return;
            record.ScoreName = _scoreName;
            record.ScoreType = _scoreType;
            record.Weight = _weight;
            record.Parameter = _parameter;
            record.Enabled = _enabled;
        }

        private void Apply()
        {
            SyncToGraph();
            EditorUtility.SetDirty(Graph);
            OnDataChanged?.Invoke();
        }
    }
}
