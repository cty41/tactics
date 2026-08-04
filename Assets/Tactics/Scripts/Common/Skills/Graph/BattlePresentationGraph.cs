using System;
using System.Collections.Generic;
using Tactics.Common.Units.Tween;
using UnityEngine;

namespace Tactics.Common.Skills.Graph
{
    /// <summary>
    /// Identifies a gameplay-semantic entry point into a presentation graph.
    /// Presentation cues never mutate battle state.
    /// </summary>
    public enum PresentationCueKind
    {
        Idle,
        Move,
        Action,
        CastCharge,
        Projectile,
        ProjectileImpact,
        PrimaryTargetHit,
        SecondaryTargetHit,
        DirectionalStrike,
        ConditionalDetonation,
        Hit,
        CorpseLanding
    }

    public enum PresentationMarkerKind
    {
        Release,
        Impact
    }

    internal enum PresentationPreviewAdvanceKind
    {
        Complete,
        Release,
        Impact,
        Blocking
    }

    /// <summary>
    /// Describes one editor-only representative preview phase without changing runtime cue flow.
    /// </summary>
    [Serializable]
    internal sealed class PresentationPreviewPhaseRecord
    {
        [SerializeField] private List<PresentationCueKind> _cues = new();
        [SerializeField] private PresentationCueKind _continuationCue = PresentationCueKind.Action;
        [SerializeField] private PresentationPreviewAdvanceKind _advanceKind =
            PresentationPreviewAdvanceKind.Complete;
        [SerializeField] private bool _playTargetHitReaction;

        internal List<PresentationCueKind> Cues => _cues;
        internal PresentationCueKind ContinuationCue
        {
            get => _continuationCue;
            set => _continuationCue = value;
        }
        internal PresentationPreviewAdvanceKind AdvanceKind
        {
            get => _advanceKind;
            set => _advanceKind = value;
        }
        internal bool PlayTargetHitReaction
        {
            get => _playTargetHitReaction;
            set => _playTargetHitReaction = value;
        }
    }

    public enum PresentationNodeType
    {
        Entry,
        Finish,
        UnitTween,
        Projectile,
        PrefabFx,
        ProceduralVfx,
        Delay,
        Marker,
        Fork,
        Join
    }

    [Serializable]
    public abstract class PresentationNodeRecord
    {
        [SerializeField] private string _nodeId;
        [SerializeField] private Vector2 _position;
        [SerializeField] private bool _enabled = true;

        public string NodeId { get => _nodeId; set => _nodeId = value; }
        public Vector2 Position { get => _position; set => _position = value; }
        public bool Enabled { get => _enabled; set => _enabled = value; }
        public abstract PresentationNodeType NodeType { get; }

        public static PresentationNodeRecord Create(PresentationNodeType type)
        {
            return type switch
            {
                PresentationNodeType.Entry => new PresentationEntryNodeRecord(),
                PresentationNodeType.Finish => new PresentationFinishNodeRecord(),
                PresentationNodeType.UnitTween => new PresentationUnitTweenNodeRecord(),
                PresentationNodeType.Projectile => new PresentationProjectileNodeRecord(),
                PresentationNodeType.PrefabFx => new PresentationPrefabFxNodeRecord(),
                PresentationNodeType.ProceduralVfx => new PresentationProceduralVfxNodeRecord(),
                PresentationNodeType.Delay => new PresentationDelayNodeRecord(),
                PresentationNodeType.Marker => new PresentationMarkerNodeRecord(),
                PresentationNodeType.Fork => new PresentationForkNodeRecord(),
                PresentationNodeType.Join => new PresentationJoinNodeRecord(),
                _ => null
            };
        }
    }

    [Serializable]
    public sealed class PresentationEntryNodeRecord : PresentationNodeRecord
    {
        [SerializeField] private PresentationCueKind _cue;
        public PresentationCueKind Cue { get => _cue; set => _cue = value; }
        public override PresentationNodeType NodeType => PresentationNodeType.Entry;
    }

    [Serializable]
    public sealed class PresentationFinishNodeRecord : PresentationNodeRecord
    {
        public override PresentationNodeType NodeType => PresentationNodeType.Finish;
    }

    [Serializable]
    public sealed class PresentationUnitTweenNodeRecord : PresentationNodeRecord
    {
        [SerializeField] private UnitVisualAction _action = UnitVisualAction.Cast;
        [SerializeField] private bool _emitReleaseMarker = true;
        public UnitVisualAction Action { get => _action; set => _action = value; }
        public bool EmitReleaseMarker { get => _emitReleaseMarker; set => _emitReleaseMarker = value; }
        public override PresentationNodeType NodeType => PresentationNodeType.UnitTween;
    }

    [Serializable]
    public sealed class PresentationProjectileNodeRecord : PresentationNodeRecord
    {
        [SerializeField] private ProjectileVisualProfile _profile;
        [SerializeField, Min(0f)] private float _speed = 10f;
        [SerializeField, Min(0f)] private float _fallbackTravelTime = 0.3f;
        [SerializeField] private bool _emitImpactMarker = true;
        public ProjectileVisualProfile Profile { get => _profile; set => _profile = value; }
        public float Speed { get => _speed; set => _speed = value; }
        public float FallbackTravelTime { get => _fallbackTravelTime; set => _fallbackTravelTime = value; }
        public bool EmitImpactMarker { get => _emitImpactMarker; set => _emitImpactMarker = value; }
        public override PresentationNodeType NodeType => PresentationNodeType.Projectile;
    }

    [Serializable]
    public sealed class PresentationPrefabFxNodeRecord : PresentationNodeRecord
    {
        [SerializeField] private VisualCueProfile _profile;
        public VisualCueProfile Profile { get => _profile; set => _profile = value; }
        public override PresentationNodeType NodeType => PresentationNodeType.PrefabFx;
    }

    [Serializable]
    public sealed class PresentationProceduralVfxNodeRecord : PresentationNodeRecord
    {
        [SerializeField] private SkillVfxRecipe _recipe;
        [SerializeField] private SkillVfxCueKind _cue = SkillVfxCueKind.PrimaryTargetHit;
        public SkillVfxRecipe Recipe { get => _recipe; set => _recipe = value; }
        public SkillVfxCueKind Cue { get => _cue; set => _cue = value; }
        public override PresentationNodeType NodeType => PresentationNodeType.ProceduralVfx;
    }

    [Serializable]
    public sealed class PresentationDelayNodeRecord : PresentationNodeRecord
    {
        [SerializeField, Min(0f)] private float _duration = 0.1f;
        public float Duration { get => _duration; set => _duration = Mathf.Max(0f, value); }
        public override PresentationNodeType NodeType => PresentationNodeType.Delay;
    }

    [Serializable]
    public sealed class PresentationMarkerNodeRecord : PresentationNodeRecord
    {
        [SerializeField] private PresentationMarkerKind _marker;
        public PresentationMarkerKind Marker { get => _marker; set => _marker = value; }
        public override PresentationNodeType NodeType => PresentationNodeType.Marker;
    }

    [Serializable]
    public sealed class PresentationForkNodeRecord : PresentationNodeRecord
    {
        [SerializeField] private string _joinNodeId;
        public string JoinNodeId { get => _joinNodeId; set => _joinNodeId = value; }
        public override PresentationNodeType NodeType => PresentationNodeType.Fork;
    }

    [Serializable]
    public sealed class PresentationJoinNodeRecord : PresentationNodeRecord
    {
        public override PresentationNodeType NodeType => PresentationNodeType.Join;
    }

    [Serializable]
    public sealed class PresentationEdgeRecord
    {
        [SerializeField] private string _edgeId;
        [SerializeField] private string _sourceNodeId;
        [SerializeField] private string _targetNodeId;
        public string EdgeId { get => _edgeId; set => _edgeId = value; }
        public string SourceNodeId { get => _sourceNodeId; set => _sourceNodeId = value; }
        public string TargetNodeId { get => _targetNodeId; set => _targetNodeId = value; }
    }

    /// <summary>
    /// Stores visual-only choreography organized by gameplay-semantic entry points.
    /// </summary>
    [CreateAssetMenu(fileName = "NewBattlePresentationGraph", menuName = "Tactics/Visuals/Battle Presentation Graph")]
    public sealed class BattlePresentationGraph : ScriptableObject
    {
        [SerializeField] private string _displayName;
        [SerializeField] private int _version = 1;
        [SerializeField] private PresentationCueKind _defaultPreviewEntry = PresentationCueKind.Action;
        [SerializeField] private GameObject _previewActorPrefab;
        [SerializeField] private GameObject _previewTargetPrefab;
        [SerializeField] private List<PresentationPreviewPhaseRecord> _previewPhases = new();
        [SerializeReference] private List<PresentationNodeRecord> _nodes = new();
        [SerializeField] private List<PresentationEdgeRecord> _edges = new();

        public string DisplayName { get => _displayName; set => _displayName = value; }
        public int Version { get => _version; set => _version = Mathf.Max(1, value); }
        public PresentationCueKind DefaultPreviewEntry
        {
            get => _defaultPreviewEntry;
            set => _defaultPreviewEntry = value;
        }
        public List<PresentationNodeRecord> Nodes => _nodes;
        public List<PresentationEdgeRecord> Edges => _edges;

        internal GameObject PreviewActorPrefab
        {
            get => _previewActorPrefab;
            set => _previewActorPrefab = value;
        }
        internal GameObject PreviewTargetPrefab
        {
            get => _previewTargetPrefab;
            set => _previewTargetPrefab = value;
        }
        internal List<PresentationPreviewPhaseRecord> PreviewPhases => _previewPhases;
        internal bool HasPreviewScenario => _previewPhases != null && _previewPhases.Count > 0;

        public PresentationEntryNodeRecord FindEntry(PresentationCueKind cue)
        {
            return _nodes.Find(node => node is PresentationEntryNodeRecord entry && entry.Cue == cue)
                as PresentationEntryNodeRecord;
        }

        public PresentationNodeRecord FindNode(string nodeId)
        {
            return _nodes.Find(node => node != null && node.NodeId == nodeId);
        }

        public List<PresentationEdgeRecord> GetEdgesFrom(string nodeId)
        {
            return _edges.FindAll(edge => edge != null && edge.SourceNodeId == nodeId);
        }

        public PresentationNodeRecord AddNode(PresentationNodeType type, Vector2 position)
        {
            PresentationNodeRecord node = PresentationNodeRecord.Create(type);
            if (node == null)
                return null;
            node.NodeId = Guid.NewGuid().ToString("N");
            node.Position = position;
            _nodes.Add(node);
            return node;
        }

        public PresentationEdgeRecord AddEdge(string sourceNodeId, string targetNodeId)
        {
            if (string.IsNullOrEmpty(sourceNodeId) || string.IsNullOrEmpty(targetNodeId) ||
                sourceNodeId == targetNodeId)
            {
                return null;
            }
            PresentationEdgeRecord existing = _edges.Find(edge => edge.SourceNodeId == sourceNodeId &&
                edge.TargetNodeId == targetNodeId);
            if (existing != null)
                return existing;
            var edge = new PresentationEdgeRecord
            {
                EdgeId = Guid.NewGuid().ToString("N"),
                SourceNodeId = sourceNodeId,
                TargetNodeId = targetNodeId
            };
            _edges.Add(edge);
            return edge;
        }

        public bool RemoveNode(string nodeId)
        {
            int removed = _nodes.RemoveAll(node => node != null && node.NodeId == nodeId);
            _edges.RemoveAll(edge => edge.SourceNodeId == nodeId || edge.TargetNodeId == nodeId);
            return removed > 0;
        }

        public bool RemoveEdge(string edgeId)
        {
            return _edges.RemoveAll(edge => edge != null && edge.EdgeId == edgeId) > 0;
        }
    }
}
