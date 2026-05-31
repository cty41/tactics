using System.Collections.Generic;
using UnityEngine;

namespace Tactics.Common.AI.MonsterAI
{
    // ═══════════════════════════════════════════════
    //  图记录类型
    // ═══════════════════════════════════════════════

    public enum GraphNodeType
    {
        Intent,
        Rule,
        Score
    }

    /// <summary>
    /// 图节点记录基类。
    /// </summary>
    [System.Serializable]
    public class GraphNodeRecord
    {
        [SerializeField] private string _nodeId;
        [SerializeField] private Vector2 _position;
        [SerializeField] private bool _enabled = true;

        public string NodeId { get { return _nodeId; } set { _nodeId = value; } }
        public Vector2 Position { get { return _position; } set { _position = value; } }
        public bool Enabled { get { return _enabled; } set { _enabled = value; } }

        /// <summary>工厂：根据类型创建对应子类记录</summary>
        public static GraphNodeRecord Create(GraphNodeType type)
        {
            return type switch
            {
                GraphNodeType.Intent => new IntentNodeRecord(),
                GraphNodeType.Rule => new RuleNodeRecord(),
                GraphNodeType.Score => new ScoreNodeRecord(),
                _ => null
            };
        }
    }

    /// <summary>意图节点记录</summary>
    [System.Serializable]
    public class IntentNodeRecord : GraphNodeRecord
    {
        [SerializeField] private IntentType _intentType;
        [SerializeField] private float _basePriority = 10f;

        public IntentType IntentType { get { return _intentType; } set { _intentType = value; } }
        public float BasePriority { get { return _basePriority; } set { _basePriority = value; } }
    }

    /// <summary>规则节点记录</summary>
    [System.Serializable]
    public class RuleNodeRecord : GraphNodeRecord
    {
        [SerializeField] private string _ruleName = "New Rule";
        [SerializeField] private RuleType _ruleType;
        [SerializeField] private float _parameter;
        // 规则状态字段
        [SerializeField] private int _cooldownTurns = 0;
        [SerializeField] private int _remainingCooldown = 0;
        [SerializeField] private bool _isOneShot = false;
        [SerializeField] private bool _hasTriggered = false;

        public string RuleName { get { return _ruleName; } set { _ruleName = value; } }
        public RuleType RuleType { get { return _ruleType; } set { _ruleType = value; } }
        public float Parameter { get { return _parameter; } set { _parameter = value; } }
        public int CooldownTurns { get { return _cooldownTurns; } set { _cooldownTurns = value; } }
        public int RemainingCooldown { get { return _remainingCooldown; } set { _remainingCooldown = value; } }
        public bool IsOneShot { get { return _isOneShot; } set { _isOneShot = value; } }
        public bool HasTriggered { get { return _hasTriggered; } set { _hasTriggered = value; } }
    }

    /// <summary>评分节点记录</summary>
    [System.Serializable]
    public class ScoreNodeRecord : GraphNodeRecord
    {
        [SerializeField] private string _scoreName = "New Score";
        [SerializeField] private ScoreType _scoreType;
        [SerializeField] private float _weight = 1f;
        [SerializeField] private float _parameter;
        [SerializeField] private AnimationCurve _responseCurve = AnimationCurve.Linear(0, 0, 1, 1);

        public string ScoreName { get { return _scoreName; } set { _scoreName = value; } }
        public ScoreType ScoreType { get { return _scoreType; } set { _scoreType = value; } }
        public float Weight { get { return _weight; } set { _weight = value; } }
        public float Parameter { get { return _parameter; } set { _parameter = value; } }
        public AnimationCurve ResponseCurve { get { return _responseCurve; } set { _responseCurve = value; } }
    }

    /// <summary>图边记录</summary>
    [System.Serializable]
    public class GraphEdgeRecord
    {
        [SerializeField] private string _edgeId;
        [SerializeField] private string _sourceNodeId;
        [SerializeField] private string _targetNodeId;

        public string EdgeId { get { return _edgeId; } set { _edgeId = value; } }
        public string SourceNodeId { get { return _sourceNodeId; } set { _sourceNodeId = value; } }
        public string TargetNodeId { get { return _targetNodeId; } set { _targetNodeId = value; } }
    }

    // ═══════════════════════════════════════════════
    //  决策图资产
    // ═══════════════════════════════════════════════

    /// <summary>
    /// AI 决策图资产。
    /// 以节点+边的完整图模型存储意图/规则/评分。
    /// </summary>
    [CreateAssetMenu(fileName = "NewAiDecisionGraph", menuName = "Tactics/AI/Ai Decision Graph")]
    public class AiDecisionGraph : ScriptableObject
    {
        [SerializeReference] private List<GraphNodeRecord> _nodes = new();
        [SerializeField] private List<GraphEdgeRecord> _edges = new();

        public List<GraphNodeRecord> Nodes => _nodes;
        public List<GraphEdgeRecord> Edges => _edges;

        // ── 兼容旧 IntentNodeConfig 接口 ──
        public List<IntentNodeConfig> IntentNodes
        {
            get
            {
                var result = new List<IntentNodeConfig>();
                foreach (var node in _nodes)
                {
                    if (node is IntentNodeRecord intent)
                    {
                        result.Add(new IntentNodeConfig
                        {
                            IntentType = intent.IntentType,
                            BasePriority = intent.BasePriority,
                            Enabled = intent.Enabled,
                            Rules = GetRuleConfigsForIntent(intent.NodeId),
                            Scores = GetScoreConfigsForIntent(intent.NodeId)
                        });
                    }
                }
                return result;
            }
        }

        public List<RuleNodeConfig> GlobalRules => new();

        /// <summary>获取指定意图节点的规则配置列表</summary>
        private List<RuleNodeConfig> GetRuleConfigsForIntent(string intentNodeId)
        {
            var rules = new List<RuleNodeConfig>();
            foreach (var edge in _edges)
            {
                if (edge.SourceNodeId != intentNodeId) continue;
                var targetNode = FindNode(edge.TargetNodeId);
                if (targetNode is RuleNodeRecord rule)
                {
                    rules.Add(new RuleNodeConfig
                    {
                        RuleName = rule.RuleName,
                        RuleType = rule.RuleType,
                        Enabled = rule.Enabled,
                        Parameter = rule.Parameter
                    });
                }
            }
            return rules;
        }

        /// <summary>获取指定意图节点的评分配置列表</summary>
        private List<ScoreNodeConfig> GetScoreConfigsForIntent(string intentNodeId)
        {
            var scores = new List<ScoreNodeConfig>();
            foreach (var edge in _edges)
            {
                if (edge.SourceNodeId != intentNodeId) continue;
                var targetNode = FindNode(edge.TargetNodeId);
                if (targetNode is ScoreNodeRecord score)
                {
                    scores.Add(new ScoreNodeConfig
                    {
                        ScoreName = score.ScoreName,
                        ScoreType = score.ScoreType,
                        Enabled = score.Enabled,
                        Weight = score.Weight,
                        Parameter = score.Parameter,
                        ResponseCurve = score.ResponseCurve
                    });
                }
            }
            return scores;
        }

        /// <summary>按 NodeId 查找节点</summary>
        public GraphNodeRecord FindNode(string nodeId)
        {
            foreach (var node in _nodes)
            {
                if (node.NodeId == nodeId) return node;
            }
            return null;
        }

        /// <summary>生成唯一 NodeId</summary>
        public string GenerateNodeId()
        {
            int max = 0;
            foreach (var node in _nodes)
            {
                if (int.TryParse(node.NodeId, out int id) && id > max)
                    max = id;
            }
            return (max + 1).ToString();
        }

        /// <summary>添加节点并返回索引</summary>
        public GraphNodeRecord AddNode(GraphNodeType type, Vector2 position)
        {
            var record = GraphNodeRecord.Create(type);
            record.NodeId = GenerateNodeId();
            record.Position = position;
            _nodes.Add(record);
            return record;
        }

        /// <summary>移除节点及关联边</summary>
        public void RemoveNode(string nodeId)
        {
            _nodes.RemoveAll(n => n.NodeId == nodeId);
            _edges.RemoveAll(e => e.SourceNodeId == nodeId || e.TargetNodeId == nodeId);
        }

        /// <summary>添加边</summary>
        public GraphEdgeRecord AddEdge(string sourceNodeId, string targetNodeId)
        {
            var edge = new GraphEdgeRecord
            {
                EdgeId = System.Guid.NewGuid().ToString(),
                SourceNodeId = sourceNodeId,
                TargetNodeId = targetNodeId
            };
            _edges.Add(edge);
            return edge;
        }

        /// <summary>移除边</summary>
        public void RemoveEdge(string edgeId)
        {
            _edges.RemoveAll(e => e.EdgeId == edgeId);
        }

        /// <summary>清除所有数据</summary>
        public void Clear()
        {
            _nodes.Clear();
            _edges.Clear();
        }

        /// <summary>验证图结构</summary>
        public bool Validate(out List<string> errors)
        {
            errors = new List<string>();

            if (_nodes == null || _nodes.Count == 0)
            {
                errors.Add("Decision graph has no nodes.");
                return false;
            }

            var intentIds = new HashSet<string>();
            var intentTypes = new HashSet<IntentType>();

            foreach (var node in _nodes)
            {
                if (node is IntentNodeRecord intent)
                {
                    if (!intentIds.Add(intent.NodeId))
                        errors.Add($"Duplicate intent node ID: {intent.NodeId}");
                    if (!intentTypes.Add(intent.IntentType))
                        errors.Add($"Duplicate intent type: {intent.IntentType}");
                }
            }

            // 检查边合法性
            foreach (var edge in _edges)
            {
                var source = FindNode(edge.SourceNodeId);
                var target = FindNode(edge.TargetNodeId);
                if (source == null) errors.Add($"Edge {edge.EdgeId}: source node {edge.SourceNodeId} not found.");
                if (target == null) errors.Add($"Edge {edge.EdgeId}: target node {edge.TargetNodeId} not found.");
            }

            return errors.Count == 0;
        }
    }

    // ═══════════════════════════════════════════════
    //  枚举定义
    // ═══════════════════════════════════════════════

    public enum IntentType
    {
        Engage,
        BasicAttack,
        AbilityUse,
        Retreat,
        FinishOff,
        HoldPosition
    }
}
