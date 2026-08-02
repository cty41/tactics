using UnityEngine;

namespace Tactics.Common.Skills.Graph
{
    /// <summary>
    /// Defines the authored sprite treatment and trajectory language for a projectile node.
    /// </summary>
    [CreateAssetMenu(fileName = "ProjectileVisualProfile", menuName = "Tactics/Visuals/Projectile Visual Profile")]
    public sealed class ProjectileVisualProfile : ScriptableObject
    {
        [SerializeField] private ProjectileVisualKind _visualKind = ProjectileVisualKind.Sprite;
        [SerializeField] private Sprite _sprite;
        [SerializeField] private Material _material;
        [SerializeField] private GameObject _flightPrefab;
        [SerializeField] private GameObject _impactPrefab;
        [SerializeField, Min(0.05f)] private float _impactLifetime = 0.45f;
        [SerializeField, Min(0.01f)] private float _impactScale = 1f;
        [SerializeField] private Color _tint = Color.white;
        [SerializeField, Min(0.01f)] private float _scale = 1f;
        [SerializeField] private ProjectileTrajectoryStyle _trajectoryStyle = ProjectileTrajectoryStyle.MagicStraight;
        [SerializeField, Min(0f)] private float _arcHeight = 0.12f;
        [SerializeField] private bool _rotateAlongTangent;
        [SerializeField, Min(0f)] private float _pulseAmount = 0.08f;
        [SerializeField, Min(0f)] private float _pulseCycles = 2f;
        [SerializeField, Range(-50, 100)] private int _sortingOrderOffset = 20;
        [SerializeField] private ProjectileParticleTrailSettings _particleTrail = new();
        [SerializeField] private ProjectileGhostTrailSettings _ghostTrail = new();

        public ProjectileVisualKind VisualKind => _visualKind;
        public Sprite Sprite => _sprite;
        public Material Material => _material;
        public GameObject FlightPrefab => _flightPrefab;
        public GameObject ImpactPrefab => _impactPrefab;
        public float ImpactLifetime => _impactLifetime;
        public float ImpactScale => _impactScale;
        public Color Tint => _tint;
        public float Scale => _scale;
        public ProjectileTrajectoryStyle TrajectoryStyle => _trajectoryStyle;
        public float ArcHeight => _arcHeight;
        public bool RotateAlongTangent => _rotateAlongTangent;
        public float PulseAmount => _pulseAmount;
        public float PulseCycles => _pulseCycles;
        public int SortingOrderOffset => _sortingOrderOffset;
        public ProjectileParticleTrailSettings ParticleTrail => _particleTrail;
        public ProjectileGhostTrailSettings GhostTrail => _ghostTrail;
    }

    public enum ProjectileVisualKind
    {
        Sprite,
        SoftDisc
    }

    [System.Serializable]
    public sealed class ProjectileParticleTrailSettings
    {
        [SerializeField] private bool _enabled;
        [SerializeField, Min(0.01f)] private float _emissionInterval = 0.05f;
        [SerializeField, Min(1)] private int _maximumParticles = 3;
        [SerializeField, Min(0.01f)] private float _lifetimeMin = 0.12f;
        [SerializeField, Min(0.01f)] private float _lifetimeMax = 0.18f;
        [SerializeField, Min(0.001f)] private float _sizeMin = 0.025f;
        [SerializeField, Min(0.001f)] private float _sizeMax = 0.045f;
        [SerializeField] private Color _color = Color.white;
        [SerializeField] private uint _randomSeed = 1;

        public bool Enabled => _enabled;
        public float EmissionInterval => Mathf.Max(0.01f, _emissionInterval);
        public int MaximumParticles => Mathf.Max(1, _maximumParticles);
        public float LifetimeMin => Mathf.Min(_lifetimeMin, LifetimeMax);
        public float LifetimeMax => Mathf.Max(0.01f, _lifetimeMax);
        public float SizeMin => Mathf.Min(_sizeMin, SizeMax);
        public float SizeMax => Mathf.Max(0.001f, _sizeMax);
        public Color Color => _color;
        public uint RandomSeed => _randomSeed == 0 ? 1u : _randomSeed;
    }

    [System.Serializable]
    public sealed class ProjectileGhostTrailSettings
    {
        [SerializeField] private bool _enabled;
        [SerializeField, Min(0.01f)] private float _sampleInterval = 0.055f;
        [SerializeField, Min(0.01f)] private float _lifetime = 0.12f;
        [SerializeField, Range(0f, 1f)] private float _alpha = 0.28f;
        [SerializeField, Min(0.01f)] private float _scale = 0.92f;
        [SerializeField, Range(1, 8)] private int _maximumAlive = 2;

        public bool Enabled => _enabled;
        public float SampleInterval => Mathf.Max(0.01f, _sampleInterval);
        public float Lifetime => Mathf.Max(0.01f, _lifetime);
        public float Alpha => Mathf.Clamp01(_alpha);
        public float Scale => Mathf.Max(0.01f, _scale);
        public int MaximumAlive => Mathf.Clamp(_maximumAlive, 1, 8);
    }

    /// <summary>
    /// Identifies the small set of reusable trajectory families used by Pure Run.
    /// </summary>
    public enum ProjectileTrajectoryStyle
    {
        PhysicalArc,
        MagicStraight,
        SpearArc
    }
}
