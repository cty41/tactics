using System;
using Tactics.Common.Skills.Graph;
using UnityEditor;
using UnityEngine;

namespace Tactics.Editor.SkillGraphEditor
{
    public abstract class SkillGraphNodeWrapperBase : ScriptableObject
    {
        [HideInInspector] public SkillGraphAsset Graph;
        [HideInInspector] public string NodeId;
        [HideInInspector] public Action OnDataChanged;

        public void Initialize(SkillGraphAsset graph, string nodeId)
        {
            Graph = graph;
            NodeId = nodeId;
            SyncFromGraph();
        }

        protected abstract void SyncFromGraph();
        protected abstract void SyncToGraph();

        protected void Apply()
        {
            SyncToGraph();
            EditorUtility.SetDirty(Graph);
            OnDataChanged?.Invoke();
        }
    }

    public class SkillGraphSelectPrimaryTargetWrapper : SkillGraphNodeWrapperBase
    {
        [Header("Select Primary Target")]
        [SerializeField] private int _maxRange = 1;
        [SerializeField] private bool _enabled = true;

        public int MaxRange { get => _maxRange; set { _maxRange = value; Apply(); } }
        public bool Enabled { get => _enabled; set { _enabled = value; Apply(); } }

        protected override void SyncFromGraph()
        {
            var r = Graph?.FindNode(NodeId) as SelectPrimaryTargetNodeRecord;
            if (r == null) return;
            _maxRange = r.MaxRange;
            _enabled = r.Enabled;
        }

        protected override void SyncToGraph()
        {
            var r = Graph?.FindNode(NodeId) as SelectPrimaryTargetNodeRecord;
            if (r == null) return;
            r.MaxRange = _maxRange;
            r.Enabled = _enabled;
        }
    }

    public class SkillGraphSelectTargetPointWrapper : SkillGraphNodeWrapperBase
    {
        [Header("Select Target Point")]
        [SerializeField] private int _maxRange = 4;
        [SerializeField] private bool _enabled = true;

        public int MaxRange { get => _maxRange; set { _maxRange = value; Apply(); } }
        public bool Enabled { get => _enabled; set { _enabled = value; Apply(); } }

        protected override void SyncFromGraph()
        {
            var r = Graph?.FindNode(NodeId) as SelectTargetPointNodeRecord;
            if (r == null) return;
            _maxRange = r.MaxRange;
            _enabled = r.Enabled;
        }

        protected override void SyncToGraph()
        {
            var r = Graph?.FindNode(NodeId) as SelectTargetPointNodeRecord;
            if (r == null) return;
            r.MaxRange = _maxRange;
            r.Enabled = _enabled;
        }
    }

    public class SkillGraphCollectTargetsInAreaWrapper : SkillGraphNodeWrapperBase
    {
        [Header("Collect Targets In Area")]
        [SerializeField] private int _radius = 1;
        [SerializeField] private SkillGraphAreaShape _shape = SkillGraphAreaShape.Circle;
        [SerializeField] private bool _enabled = true;

        public int Radius { get => _radius; set { _radius = value; Apply(); } }
        public SkillGraphAreaShape Shape { get => _shape; set { _shape = value; Apply(); } }
        public bool Enabled { get => _enabled; set { _enabled = value; Apply(); } }

        protected override void SyncFromGraph()
        {
            var r = Graph?.FindNode(NodeId) as CollectTargetsInAreaNodeRecord;
            if (r == null) return;
            _radius = r.Radius;
            _shape = r.Shape;
            _enabled = r.Enabled;
        }

        protected override void SyncToGraph()
        {
            var r = Graph?.FindNode(NodeId) as CollectTargetsInAreaNodeRecord;
            if (r == null) return;
            r.Radius = _radius;
            r.Shape = _shape;
            r.Enabled = _enabled;
        }
    }

    public class SkillGraphDashToTargetWrapper : SkillGraphNodeWrapperBase
    {
        [Header("Dash To Target")]
        [SerializeField] private int _maxRange = 4;
        [SerializeField] private float _collisionDamage = 1f;
        [SerializeField] private bool _enabled = true;

        public int MaxRange { get => _maxRange; set { _maxRange = value; Apply(); } }
        public float CollisionDamage { get => _collisionDamage; set { _collisionDamage = value; Apply(); } }
        public bool Enabled { get => _enabled; set { _enabled = value; Apply(); } }

        protected override void SyncFromGraph()
        {
            var r = Graph?.FindNode(NodeId) as DashToTargetNodeRecord;
            if (r == null) return;
            _maxRange = r.MaxRange;
            _collisionDamage = r.CollisionDamage;
            _enabled = r.Enabled;
        }

        protected override void SyncToGraph()
        {
            var r = Graph?.FindNode(NodeId) as DashToTargetNodeRecord;
            if (r == null) return;
            r.MaxRange = _maxRange;
            r.CollisionDamage = _collisionDamage;
            r.Enabled = _enabled;
        }
    }

    public class SkillGraphApplyDamageWrapper : SkillGraphNodeWrapperBase
    {
        [Header("Apply Damage")]
        [SerializeField] private float _baseDamage = 10f;
        [SerializeField] private SkillGraphDamageType _damageType = SkillGraphDamageType.Physical;
        [SerializeField] private Tactics.Common.Units.Buffs.ElementType _elementType = Tactics.Common.Units.Buffs.ElementType.None;
        [SerializeField] private bool _isRanged;
        [SerializeField] private bool _canCrit = true;
        [SerializeField] private bool _enabled = true;

        public float BaseDamage { get => _baseDamage; set { _baseDamage = value; Apply(); } }
        public SkillGraphDamageType DamageType { get => _damageType; set { _damageType = value; Apply(); } }
        public Tactics.Common.Units.Buffs.ElementType ElementType { get => _elementType; set { _elementType = value; Apply(); } }
        public bool IsRanged { get => _isRanged; set { _isRanged = value; Apply(); } }
        public bool CanCrit { get => _canCrit; set { _canCrit = value; Apply(); } }
        public bool Enabled { get => _enabled; set { _enabled = value; Apply(); } }

        protected override void SyncFromGraph()
        {
            var r = Graph?.FindNode(NodeId) as ApplyDamageNodeRecord;
            if (r == null) return;
            _baseDamage = r.BaseDamage;
            _damageType = r.DamageType;
            _elementType = r.ElementType;
            _isRanged = r.IsRanged;
            _canCrit = r.CanCrit;
            _enabled = r.Enabled;
        }

        protected override void SyncToGraph()
        {
            var r = Graph?.FindNode(NodeId) as ApplyDamageNodeRecord;
            if (r == null) return;
            r.BaseDamage = _baseDamage;
            r.DamageType = _damageType;
            r.ElementType = _elementType;
            r.IsRanged = _isRanged;
            r.CanCrit = _canCrit;
            r.Enabled = _enabled;
        }
    }

    public class SkillGraphApplyKnockbackWrapper : SkillGraphNodeWrapperBase
    {
        [Header("Apply Knockback")]
        [SerializeField] private int _distance = 1;
        [SerializeField] private float _height = 2f;
        [SerializeField] private float _duration = 0.5f;
        [SerializeField] private bool _enabled = true;

        public int Distance { get => _distance; set { _distance = value; Apply(); } }
        public float Height { get => _height; set { _height = value; Apply(); } }
        public float Duration { get => _duration; set { _duration = value; Apply(); } }
        public bool Enabled { get => _enabled; set { _enabled = value; Apply(); } }

        protected override void SyncFromGraph()
        {
            var r = Graph?.FindNode(NodeId) as ApplyKnockbackNodeRecord;
            if (r == null) return;
            _distance = r.Distance;
            _height = r.Height;
            _duration = r.Duration;
            _enabled = r.Enabled;
        }

        protected override void SyncToGraph()
        {
            var r = Graph?.FindNode(NodeId) as ApplyKnockbackNodeRecord;
            if (r == null) return;
            r.Distance = _distance;
            r.Height = _height;
            r.Duration = _duration;
            r.Enabled = _enabled;
        }
    }
}
