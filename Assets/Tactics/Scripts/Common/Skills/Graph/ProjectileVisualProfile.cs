using UnityEngine;

namespace Tactics.Common.Skills.Graph
{
    /// <summary>
    /// Defines the authored sprite treatment and trajectory language for a projectile node.
    /// </summary>
    [CreateAssetMenu(fileName = "ProjectileVisualProfile", menuName = "Tactics/Visuals/Projectile Visual Profile")]
    public sealed class ProjectileVisualProfile : ScriptableObject
    {
        [SerializeField] private Sprite _sprite;
        [SerializeField] private Material _material;
        [SerializeField] private Color _tint = Color.white;
        [SerializeField, Min(0.01f)] private float _scale = 1f;
        [SerializeField] private ProjectileTrajectoryStyle _trajectoryStyle = ProjectileTrajectoryStyle.MagicStraight;
        [SerializeField, Min(0f)] private float _arcHeight = 0.12f;
        [SerializeField] private bool _rotateAlongTangent;
        [SerializeField, Min(0f)] private float _pulseAmount = 0.08f;
        [SerializeField, Min(0f)] private float _pulseCycles = 2f;
        [SerializeField, Range(-50, 100)] private int _sortingOrderOffset = 20;

        public Sprite Sprite => _sprite;
        public Material Material => _material;
        public Color Tint => _tint;
        public float Scale => _scale;
        public ProjectileTrajectoryStyle TrajectoryStyle => _trajectoryStyle;
        public float ArcHeight => _arcHeight;
        public bool RotateAlongTangent => _rotateAlongTangent;
        public float PulseAmount => _pulseAmount;
        public float PulseCycles => _pulseCycles;
        public int SortingOrderOffset => _sortingOrderOffset;
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
