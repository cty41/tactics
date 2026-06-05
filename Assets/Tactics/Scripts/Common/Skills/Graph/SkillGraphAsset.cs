using System.Collections.Generic;
using Tactics.Common.Units.Buffs;
using UnityEngine;

namespace Tactics.Common.Skills.Graph
{
    // ═══════════════════════════════════════════════
    //  Enums
    // ═══════════════════════════════════════════════

    public enum SkillGraphNodeType
    {
        Start,
        SelectPrimaryTarget,
        SelectTargetPoint,
        CollectTargetsInArea,
        ForEachTarget,
        DashToTarget,
        ApplyDamage,
        ApplyKnockback,
        Branch,
        Finish,
        Fail,
        ProjectileLaunch,
        OnHit,
        ApplyBuff
    }

    public enum SkillGraphPortType
    {
        Default,
        OnHit,
        OnMiss,
        OnTrue,
        OnFalse,
        OnComplete
    }

    public enum SkillGraphDamageType
    {
        Physical,
        Magical
    }

    public enum SkillGraphAreaShape
    {
        Circle,
        Cross
    }

    // ═══════════════════════════════════════════════
    //  Node Records
    // ═══════════════════════════════════════════════

    /// <summary>
    /// 图节点记录基类。
    /// </summary>
    [System.Serializable]
    public class SkillGraphNodeRecord
    {
        [SerializeField] private string _nodeId;
        [SerializeField] private Vector2 _position;
        [SerializeField] private bool _enabled = true;

        public string NodeId { get => _nodeId; set => _nodeId = value; }
        public Vector2 Position { get => _position; set => _position = value; }
        public bool Enabled { get => _enabled; set => _enabled = value; }

        public virtual SkillGraphNodeType NodeType => SkillGraphNodeType.Start;

        public static SkillGraphNodeRecord Create(SkillGraphNodeType type)
        {
            return type switch
            {
                SkillGraphNodeType.Start => new StartNodeRecord(),
                SkillGraphNodeType.SelectPrimaryTarget => new SelectPrimaryTargetNodeRecord(),
                SkillGraphNodeType.SelectTargetPoint => new SelectTargetPointNodeRecord(),
                SkillGraphNodeType.CollectTargetsInArea => new CollectTargetsInAreaNodeRecord(),
                SkillGraphNodeType.ForEachTarget => new ForEachTargetNodeRecord(),
                SkillGraphNodeType.DashToTarget => new DashToTargetNodeRecord(),
                SkillGraphNodeType.ApplyDamage => new ApplyDamageNodeRecord(),
                SkillGraphNodeType.ApplyKnockback => new ApplyKnockbackNodeRecord(),
                SkillGraphNodeType.Branch => new BranchNodeRecord(),
                SkillGraphNodeType.Finish => new FinishNodeRecord(),
                SkillGraphNodeType.Fail => new FailNodeRecord(),
                SkillGraphNodeType.ProjectileLaunch => new ProjectileLaunchNodeRecord(),
                SkillGraphNodeType.OnHit => new OnHitNodeRecord(),
                SkillGraphNodeType.ApplyBuff => new ApplyBuffNodeRecord(),
                _ => null
            };
        }
    }

    [System.Serializable]
    public class StartNodeRecord : SkillGraphNodeRecord
    {
        public override SkillGraphNodeType NodeType => SkillGraphNodeType.Start;
    }

    [System.Serializable]
    public class SelectPrimaryTargetNodeRecord : SkillGraphNodeRecord
    {
        [SerializeField] private int _minRange;
        [SerializeField] private int _maxRange = 1;

        public int MinRange { get => _minRange; set => _minRange = value; }
        public int MaxRange { get => _maxRange; set => _maxRange = value; }
        public override SkillGraphNodeType NodeType => SkillGraphNodeType.SelectPrimaryTarget;
    }

    [System.Serializable]
    public class SelectTargetPointNodeRecord : SkillGraphNodeRecord
    {
        [SerializeField] private int _maxRange = 4;

        public int MaxRange { get => _maxRange; set => _maxRange = value; }
        public override SkillGraphNodeType NodeType => SkillGraphNodeType.SelectTargetPoint;
    }

    [System.Serializable]
    public class CollectTargetsInAreaNodeRecord : SkillGraphNodeRecord
    {
        [SerializeField] private int _radius = 1;
        [SerializeField] private SkillGraphAreaShape _shape = SkillGraphAreaShape.Circle;

        public int Radius { get => _radius; set => _radius = value; }
        public SkillGraphAreaShape Shape { get => _shape; set => _shape = value; }
        public override SkillGraphNodeType NodeType => SkillGraphNodeType.CollectTargetsInArea;
    }

    [System.Serializable]
    public class ForEachTargetNodeRecord : SkillGraphNodeRecord
    {
        public override SkillGraphNodeType NodeType => SkillGraphNodeType.ForEachTarget;
    }

    [System.Serializable]
    public class DashToTargetNodeRecord : SkillGraphNodeRecord
    {
        [SerializeField] private int _maxRange = 4;
        [SerializeField] private float _collisionDamage = 1f;

        public int MaxRange { get => _maxRange; set => _maxRange = value; }
        public float CollisionDamage { get => _collisionDamage; set => _collisionDamage = value; }
        public override SkillGraphNodeType NodeType => SkillGraphNodeType.DashToTarget;
    }

    [System.Serializable]
    public class ApplyDamageNodeRecord : SkillGraphNodeRecord
    {
        [SerializeField] private float _baseDamage = 10f;
        [SerializeField] private SkillGraphDamageType _damageType = SkillGraphDamageType.Physical;
        [SerializeField] private bool _isRanged;
        [SerializeField] private bool _canCrit = true;

        public float BaseDamage { get => _baseDamage; set => _baseDamage = value; }
        public SkillGraphDamageType DamageType { get => _damageType; set => _damageType = value; }
        public bool IsRanged { get => _isRanged; set => _isRanged = value; }
        public bool CanCrit { get => _canCrit; set => _canCrit = value; }
        public override SkillGraphNodeType NodeType => SkillGraphNodeType.ApplyDamage;
    }

    [System.Serializable]
    public class ApplyKnockbackNodeRecord : SkillGraphNodeRecord
    {
        [SerializeField] private int _distance = 1;
        [SerializeField] private float _height = 2f;
        [SerializeField] private float _duration = 0.5f;

        public int Distance { get => _distance; set => _distance = value; }
        public float Height { get => _height; set => _height = value; }
        public float Duration { get => _duration; set => _duration = value; }
        public override SkillGraphNodeType NodeType => SkillGraphNodeType.ApplyKnockback;
    }

    [System.Serializable]
    public class BranchNodeRecord : SkillGraphNodeRecord
    {
        [SerializeField] private SkillGraphPortType _truePort = SkillGraphPortType.OnTrue;
        [SerializeField] private SkillGraphPortType _falsePort = SkillGraphPortType.OnFalse;

        public SkillGraphPortType TruePort { get => _truePort; set => _truePort = value; }
        public SkillGraphPortType FalsePort { get => _falsePort; set => _falsePort = value; }
        public override SkillGraphNodeType NodeType => SkillGraphNodeType.Branch;
    }

    [System.Serializable]
    public class FinishNodeRecord : SkillGraphNodeRecord
    {
        public override SkillGraphNodeType NodeType => SkillGraphNodeType.Finish;
    }

    [System.Serializable]
    public class FailNodeRecord : SkillGraphNodeRecord
    {
        public override SkillGraphNodeType NodeType => SkillGraphNodeType.Fail;
    }

    [System.Serializable]
    public class ProjectileLaunchNodeRecord : SkillGraphNodeRecord
    {
        [SerializeField] private float _travelTime = 0.3f;
        [SerializeField] private float _speed = 10f;

        public float TravelTime { get => _travelTime; set => _travelTime = value; }
        public float Speed { get => _speed; set => _speed = value; }
        public override SkillGraphNodeType NodeType => SkillGraphNodeType.ProjectileLaunch;
    }

    [System.Serializable]
    public class OnHitNodeRecord : SkillGraphNodeRecord
    {
        public override SkillGraphNodeType NodeType => SkillGraphNodeType.OnHit;
    }

    [System.Serializable]
    public class ApplyBuffNodeRecord : SkillGraphNodeRecord
    {
        [SerializeField] private BuffConfig _buffConfig;
        [SerializeField] private int _duration;

        public BuffConfig BuffConfig { get => _buffConfig; set => _buffConfig = value; }
        public int Duration { get => _duration; set => _duration = value; }
        public override SkillGraphNodeType NodeType => SkillGraphNodeType.ApplyBuff;
    }

    // ═══════════════════════════════════════════════
    //  Edge Record
    // ═══════════════════════════════════════════════

    [System.Serializable]
    public class SkillGraphEdgeRecord
    {
        [SerializeField] private string _edgeId;
        [SerializeField] private string _sourceNodeId;
        [SerializeField] private string _targetNodeId;
        [SerializeField] private SkillGraphPortType _portType = SkillGraphPortType.Default;

        public string EdgeId { get => _edgeId; set => _edgeId = value; }
        public string SourceNodeId { get => _sourceNodeId; set => _sourceNodeId = value; }
        public string TargetNodeId { get => _targetNodeId; set => _targetNodeId = value; }
        public SkillGraphPortType PortType { get => _portType; set => _portType = value; }
    }

    // ═══════════════════════════════════════════════
    //  Asset
    // ═══════════════════════════════════════════════

    /// <summary>
    /// 技能图资产。
    /// 以节点+边存储技能执行逻辑流程。
    /// </summary>
    [CreateAssetMenu(fileName = "NewSkillGraph", menuName = "Tactics/Skill Graph")]
    public class SkillGraphAsset : ScriptableObject
    {
        [SerializeField] private string _displayName;
        [SerializeField] private int _version = 1;
        [SerializeField] private string[] _tags;
        [SerializeReference] private List<SkillGraphNodeRecord> _nodes = new();
        [SerializeField] private List<SkillGraphEdgeRecord> _edges = new();

        public string DisplayName { get => _displayName; set => _displayName = value; }
        public int Version { get => _version; set => _version = value; }
        public string[] Tags { get => _tags; set => _tags = value; }
        public List<SkillGraphNodeRecord> Nodes => _nodes;
        public List<SkillGraphEdgeRecord> Edges => _edges;

        // ── 查询 ──

        public SkillGraphNodeRecord FindNode(string nodeId)
        {
            for (int i = 0; i < _nodes.Count; i++)
            {
                if (_nodes[i].NodeId == nodeId)
                    return _nodes[i];
            }
            return null;
        }

        public SkillGraphNodeRecord FindEntryNode()
        {
            for (int i = 0; i < _nodes.Count; i++)
            {
                if (_nodes[i] is StartNodeRecord)
                    return _nodes[i];
            }
            return null;
        }

        public List<SkillGraphNodeRecord> GetChildren(string nodeId)
        {
            var result = new List<SkillGraphNodeRecord>();
            for (int i = 0; i < _edges.Count; i++)
            {
                if (_edges[i].SourceNodeId == nodeId)
                {
                    var child = FindNode(_edges[i].TargetNodeId);
                    if (child != null)
                        result.Add(child);
                }
            }
            return result;
        }

        public List<SkillGraphEdgeRecord> GetEdgesFrom(string nodeId)
        {
            var result = new List<SkillGraphEdgeRecord>();
            for (int i = 0; i < _edges.Count; i++)
            {
                if (_edges[i].SourceNodeId == nodeId)
                    result.Add(_edges[i]);
            }
            return result;
        }

        public List<SkillGraphEdgeRecord> GetEdgesTo(string nodeId)
        {
            var result = new List<SkillGraphEdgeRecord>();
            for (int i = 0; i < _edges.Count; i++)
            {
                if (_edges[i].TargetNodeId == nodeId)
                    result.Add(_edges[i]);
            }
            return result;
        }

        public bool HasIncomingEdge(string nodeId)
        {
            for (int i = 0; i < _edges.Count; i++)
            {
                if (_edges[i].TargetNodeId == nodeId)
                    return true;
            }
            return false;
        }

        // ── 操作 ──

        public SkillGraphNodeRecord AddNode(SkillGraphNodeType type, Vector2 position)
        {
            var record = SkillGraphNodeRecord.Create(type);
            if (record == null) return null;
            record.NodeId = GenerateNodeId();
            record.Position = position;
            _nodes.Add(record);
            return record;
        }

        public bool RemoveNode(string nodeId)
        {
            int removed = _nodes.RemoveAll(n => n.NodeId == nodeId);
            _edges.RemoveAll(e => e.SourceNodeId == nodeId || e.TargetNodeId == nodeId);
            return removed > 0;
        }

        public SkillGraphEdgeRecord AddEdge(string sourceNodeId, string targetNodeId, SkillGraphPortType portType = SkillGraphPortType.Default)
        {
            if (sourceNodeId == targetNodeId) return null;

            // 防重复：同源同目标同端口只允许一条边
            for (int i = 0; i < _edges.Count; i++)
            {
                if (_edges[i].SourceNodeId == sourceNodeId &&
                    _edges[i].TargetNodeId == targetNodeId &&
                    _edges[i].PortType == portType)
                    return _edges[i];
            }

            var edge = new SkillGraphEdgeRecord
            {
                EdgeId = System.Guid.NewGuid().ToString(),
                SourceNodeId = sourceNodeId,
                TargetNodeId = targetNodeId,
                PortType = portType
            };
            _edges.Add(edge);
            return edge;
        }

        public bool RemoveEdge(string edgeId)
        {
            return _edges.RemoveAll(e => e.EdgeId == edgeId) > 0;
        }

        public void Clear()
        {
            _nodes.Clear();
            _edges.Clear();
        }

        private string GenerateNodeId()
        {
            int max = 0;
            for (int i = 0; i < _nodes.Count; i++)
            {
                if (int.TryParse(_nodes[i].NodeId, out int id) && id > max)
                    max = id;
            }
            return (max + 1).ToString();
        }
    }
}
