using System.Collections.Generic;
using Tactics.Common.Interactables;
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
        ApplyBuff,
        SelectSelf,
        SelectAlly,
        ApplyHeal,
        ApplyMana,
        DashToAlly,
        LaunchUnit,
        SelectMoveDestination,
        ExecuteMove,
        SelectCorpseTarget,
        SummonUnit,
        Teleport,
        MultiStab,
        ApplyShield,
        RemoveHarmfulBuffs,
        MageSkill,
        NecromancerSkill,
        AmazonSkill,
        PlayVisualCue,
        PlayPresentationCue
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

    public enum SkillGraphTargetFaction
    {
        All,
        Enemies,
        Allies
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
                SkillGraphNodeType.SelectSelf => new SelectSelfNodeRecord(),
                SkillGraphNodeType.SelectAlly => new SelectAllyNodeRecord(),
                SkillGraphNodeType.ApplyHeal => new ApplyHealNodeRecord(),
                SkillGraphNodeType.ApplyMana => new ApplyManaNodeRecord(),
                SkillGraphNodeType.RemoveHarmfulBuffs => new RemoveHarmfulBuffsNodeRecord(),
                SkillGraphNodeType.DashToAlly => new DashToAllyNodeRecord(),
                SkillGraphNodeType.LaunchUnit => new LaunchUnitNodeRecord(),
                SkillGraphNodeType.SelectMoveDestination => new SelectMoveDestinationNodeRecord(),
                SkillGraphNodeType.ExecuteMove => new ExecuteMoveNodeRecord(),
                SkillGraphNodeType.SelectCorpseTarget => new SelectCorpseTargetNodeRecord(),
                SkillGraphNodeType.SummonUnit => new SummonUnitNodeRecord(),
                SkillGraphNodeType.Teleport => new TeleportNodeRecord(),
                SkillGraphNodeType.MultiStab => new MultiStabNodeRecord(),
                SkillGraphNodeType.ApplyShield => new ApplyShieldNodeRecord(),
                SkillGraphNodeType.MageSkill => new MageSkillNodeRecord(),
                SkillGraphNodeType.NecromancerSkill => new NecromancerSkillNodeRecord(),
                SkillGraphNodeType.AmazonSkill => new AmazonSkillNodeRecord(),
                SkillGraphNodeType.PlayVisualCue => new PlayVisualCueNodeRecord(),
                SkillGraphNodeType.PlayPresentationCue => new PlayPresentationCueNodeRecord(),
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
        [SerializeField] private SkillGraphTargetFaction _targetFaction = SkillGraphTargetFaction.All;

        public int Radius { get => _radius; set => _radius = value; }
        public SkillGraphAreaShape Shape { get => _shape; set => _shape = value; }
        public SkillGraphTargetFaction TargetFaction { get => _targetFaction; set => _targetFaction = value; }
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
        [SerializeField] private ElementType _elementType = ElementType.None;
        [SerializeField] private bool _isRanged;
        [SerializeField] private bool _canCrit = true;
        [SerializeField] private float _accuracyPenalty;

        public float BaseDamage { get => _baseDamage; set => _baseDamage = value; }
        public SkillGraphDamageType DamageType { get => _damageType; set => _damageType = value; }
        public ElementType ElementType { get => _elementType; set => _elementType = value; }
        public bool IsRanged { get => _isRanged; set => _isRanged = value; }
        public bool CanCrit { get => _canCrit; set => _canCrit = value; }
        public float AccuracyPenalty { get => _accuracyPenalty; set => _accuracyPenalty = value; }
        public override SkillGraphNodeType NodeType => SkillGraphNodeType.ApplyDamage;
    }

    [System.Serializable]
    public class ApplyKnockbackNodeRecord : SkillGraphNodeRecord
    {
        [SerializeField] private int _distance = 1;
        [SerializeField] private float _height = 2f;
        [SerializeField] private float _duration = 0.2f;

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
        [SerializeField] private ProjectileVisualProfile _visualProfile;
        [SerializeField] private bool _dropOnHit;
        [SerializeField] private int _dropSearchRadius = 1;
        [SerializeField] private bool _requiresLineOfSight = true;

        public float TravelTime { get => _travelTime; set => _travelTime = value; }
        public float Speed { get => _speed; set => _speed = value; }
        public ProjectileVisualProfile VisualProfile { get => _visualProfile; set => _visualProfile = value; }
        public bool DropOnHit { get => _dropOnHit; set => _dropOnHit = value; }
        public int DropSearchRadius { get => _dropSearchRadius; set => _dropSearchRadius = value; }
        public bool RequiresLineOfSight { get => _requiresLineOfSight; set => _requiresLineOfSight = value; }
        public override SkillGraphNodeType NodeType => SkillGraphNodeType.ProjectileLaunch;
    }

    /// <summary>
    /// Plays an authored visual cue without changing battle state.
    /// </summary>
    [System.Serializable]
    public class PlayVisualCueNodeRecord : SkillGraphNodeRecord
    {
        [SerializeField] private VisualCueProfile _profile;

        public VisualCueProfile Profile { get => _profile; set => _profile = value; }
        public override SkillGraphNodeType NodeType => SkillGraphNodeType.PlayVisualCue;
    }

    /// <summary>
    /// Requests a visual-only semantic entry from the configured presentation graph.
    /// </summary>
    [System.Serializable]
    public class PlayPresentationCueNodeRecord : SkillGraphNodeRecord
    {
        [SerializeField] private PresentationCueKind _cue = PresentationCueKind.PrimaryTargetHit;

        public PresentationCueKind Cue { get => _cue; set => _cue = value; }
        public override SkillGraphNodeType NodeType => SkillGraphNodeType.PlayPresentationCue;
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
        [SerializeField] private bool _requiresSuccessfulHit;

        public BuffConfig BuffConfig { get => _buffConfig; set => _buffConfig = value; }
        public int Duration { get => _duration; set => _duration = value; }
        public bool RequiresSuccessfulHit { get => _requiresSuccessfulHit; set => _requiresSuccessfulHit = value; }
        public override SkillGraphNodeType NodeType => SkillGraphNodeType.ApplyBuff;
    }

    [System.Serializable]
    public class SelectSelfNodeRecord : SkillGraphNodeRecord
    {
        public override SkillGraphNodeType NodeType => SkillGraphNodeType.SelectSelf;
    }

    [System.Serializable]
    public class SelectAllyNodeRecord : SkillGraphNodeRecord
    {
        [SerializeField] private int _maxRange = 1;
        [SerializeField] private bool _includeSelf;

        public int MaxRange { get => _maxRange; set => _maxRange = value; }
        public bool IncludeSelf { get => _includeSelf; set => _includeSelf = value; }
        public override SkillGraphNodeType NodeType => SkillGraphNodeType.SelectAlly;
    }

    [System.Serializable]
    public class ApplyHealNodeRecord : SkillGraphNodeRecord
    {
        [SerializeField] private float _healAmount = 5f;

        public float HealAmount { get => _healAmount; set => _healAmount = value; }
        public override SkillGraphNodeType NodeType => SkillGraphNodeType.ApplyHeal;
    }

    [System.Serializable]
    public class ApplyManaNodeRecord : SkillGraphNodeRecord
    {
        [SerializeField] private float _manaAmount = 5f;

        public float ManaAmount { get => _manaAmount; set => _manaAmount = value; }
        public override SkillGraphNodeType NodeType => SkillGraphNodeType.ApplyMana;
    }

    [System.Serializable]
    public class RemoveHarmfulBuffsNodeRecord : SkillGraphNodeRecord
    {
        public override SkillGraphNodeType NodeType => SkillGraphNodeType.RemoveHarmfulBuffs;
    }

    [System.Serializable]
    public class DashToAllyNodeRecord : SkillGraphNodeRecord
    {
        [SerializeField] private int _maxRange = 4;

        public int MaxRange { get => _maxRange; set => _maxRange = value; }
        public override SkillGraphNodeType NodeType => SkillGraphNodeType.DashToAlly;
    }

    [System.Serializable]
    public class LaunchUnitNodeRecord : SkillGraphNodeRecord
    {
        [SerializeField] private int _launchDistance = 3;
        [SerializeField] private float _landingDamage = 2f;
        [SerializeField] private float _flightHeight = 3f;
        [SerializeField] private float _flightDuration = 0.8f;
        [SerializeField] private float _bounceHeight = 1.5f;
        [SerializeField] private float _bounceDuration = 0.3f;

        public int LaunchDistance { get => _launchDistance; set => _launchDistance = value; }
        public float LandingDamage { get => _landingDamage; set => _landingDamage = value; }
        public float FlightHeight { get => _flightHeight; set => _flightHeight = value; }
        public float FlightDuration { get => _flightDuration; set => _flightDuration = value; }
        public float BounceHeight { get => _bounceHeight; set => _bounceHeight = value; }
        public float BounceDuration { get => _bounceDuration; set => _bounceDuration = value; }
        public override SkillGraphNodeType NodeType => SkillGraphNodeType.LaunchUnit;
    }

    [System.Serializable]
    public class SelectMoveDestinationNodeRecord : SkillGraphNodeRecord
    {
        [SerializeField] private bool _respectMovementRules = true;

        public bool RespectMovementRules { get => _respectMovementRules; set => _respectMovementRules = value; }
        public override SkillGraphNodeType NodeType => SkillGraphNodeType.SelectMoveDestination;
    }

    [System.Serializable]
    public class ExecuteMoveNodeRecord : SkillGraphNodeRecord
    {
        [SerializeField] private bool _consumeMovementPoints = true;
        [SerializeField] private bool _markAsBasicAbilityUsed = true;

        public bool ConsumeMovementPoints { get => _consumeMovementPoints; set => _consumeMovementPoints = value; }
        public bool MarkAsBasicAbilityUsed { get => _markAsBasicAbilityUsed; set => _markAsBasicAbilityUsed = value; }
        public override SkillGraphNodeType NodeType => SkillGraphNodeType.ExecuteMove;
    }

    [System.Serializable]
    public class SelectCorpseTargetNodeRecord : SkillGraphNodeRecord
    {
        [SerializeField] private int _minRange;
        [SerializeField] private int _maxRange = 999;

        public int MinRange { get => _minRange; set => _minRange = value; }
        public int MaxRange { get => _maxRange; set => _maxRange = value; }
        public override SkillGraphNodeType NodeType => SkillGraphNodeType.SelectCorpseTarget;
    }

    [System.Serializable]
    public class SummonUnitNodeRecord : SkillGraphNodeRecord
    {
        [SerializeField] private string _unitPrefabPath;
        [SerializeField] private bool _requiresCorpse = true;
        [SerializeField] private string _summonName;
        [SerializeField] private string _summonCategory = "Default";
        [SerializeField] private int _maxActive = 1;
        [SerializeField] private bool _canReceiveHealing = true;

        public string UnitPrefabPath { get => _unitPrefabPath; set => _unitPrefabPath = value; }
        public bool RequiresCorpse { get => _requiresCorpse; set => _requiresCorpse = value; }
        public string SummonName { get => _summonName; set => _summonName = value; }
        public string SummonCategory { get => _summonCategory; set => _summonCategory = value; }
        public int MaxActive { get => Mathf.Max(1, _maxActive); set => _maxActive = Mathf.Max(1, value); }
        public bool CanReceiveHealing { get => _canReceiveHealing; set => _canReceiveHealing = value; }
        public override SkillGraphNodeType NodeType => SkillGraphNodeType.SummonUnit;
    }

    /// <summary>
    /// Moves the caster directly to an unoccupied destination without pathfinding
    /// or movement-point consumption.
    /// </summary>
    [System.Serializable]
    public class TeleportNodeRecord : SkillGraphNodeRecord
    {
        [SerializeField] private int _maxRange = 6;
        [SerializeField] private bool _requiresLineOfSight = true;

        public int MaxRange { get => _maxRange; set => _maxRange = value; }
        public bool RequiresLineOfSight { get => _requiresLineOfSight; set => _requiresLineOfSight = value; }
        public override SkillGraphNodeType NodeType => SkillGraphNodeType.Teleport;
    }

    /// <summary>
    /// Applies a fixed number of consecutive melee hits to the selected target.
    /// </summary>
    [System.Serializable]
    public class MultiStabNodeRecord : SkillGraphNodeRecord
    {
        [SerializeField] private int _segmentCount = 3;
        [SerializeField] private float _damagePerSegment = 4f;

        public int SegmentCount { get => _segmentCount; set => _segmentCount = value; }
        public float DamagePerSegment { get => _damagePerSegment; set => _damagePerSegment = value; }
        public override SkillGraphNodeType NodeType => SkillGraphNodeType.MultiStab;
    }

    [System.Serializable]
    public class ApplyShieldNodeRecord : SkillGraphNodeRecord
    {
        [SerializeField] private float _attributeMultiplier = 2f;
        public float AttributeMultiplier { get => _attributeMultiplier; set => _attributeMultiplier = value; }
        public override SkillGraphNodeType NodeType => SkillGraphNodeType.ApplyShield;
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
        [SerializeField] private SkillTargetingProtocol _targeting = new();
        [SerializeReference] private List<SkillGraphNodeRecord> _nodes = new();
        [SerializeField] private List<SkillGraphEdgeRecord> _edges = new();

        public string DisplayName { get => _displayName; set => _displayName = value; }
        public int Version { get => _version; set => _version = value; }
        public string[] Tags { get => _tags; set => _tags = value; }
        public SkillTargetingProtocol Targeting => _targeting ??= new SkillTargetingProtocol();
        public List<SkillGraphNodeRecord> Nodes => _nodes;
        public List<SkillGraphEdgeRecord> Edges => _edges;

        public SkillTargetMode ResolveTargetMode()
        {
            if (Targeting.Mode != SkillTargetMode.PrimaryUnit)
                return Targeting.Mode;
            if (_nodes.Exists(node => node is TeleportNodeRecord))
                return SkillTargetMode.PathlessMove;
            if (_nodes.Exists(node => node is MultiStabNodeRecord))
                return SkillTargetMode.OrderedMultiTarget;
            if (_nodes.Exists(node => node is SelectCorpseTargetNodeRecord))
                return SkillTargetMode.PhysicalObjectCell;
            if (_nodes.Exists(node => node is SelectTargetPointNodeRecord))
                return SkillTargetMode.AnyCellCenter;
            return SkillTargetMode.PrimaryUnit;
        }

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
