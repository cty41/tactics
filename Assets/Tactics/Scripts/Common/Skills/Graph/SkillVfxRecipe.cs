using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Tactics.Common.Skills.Graph
{
    /// <summary>
    /// Identifies gameplay-semantic visual moments without coupling graph executors to renderers.
    /// </summary>
    public enum SkillVfxCueKind
    {
        ProjectileImpact,
        DirectionalStrike,
        PrimaryTargetHit,
        SecondaryTargetHit,
        ConditionalDetonation,
        CastCharge
    }

    /// <summary>
    /// The finite primitive vocabulary supported by the Pure Run skill VFX runtime.
    /// </summary>
    public enum SkillVfxPrimitiveKind
    {
        RadialCore,
        RadialRing,
        TaperedLine,
        CrossFlash,
        ParticleBurst,
        ProjectileGhostTrail
    }

    public enum SkillVfxBlendMode
    {
        Transparent,
        Additive
    }

    public enum SkillVfxShapeMode
    {
        Solid,
        SoftDisc,
        Ring
    }

    /// <summary>
    /// Immutable world-space snapshot captured before gameplay mutations can destroy a target.
    /// </summary>
    public sealed class SkillVfxCueContext
    {
        private static readonly IReadOnlyList<Vector3> EmptyPositions = Array.Empty<Vector3>();

        public int Level { get; }
        public Vector3 SourceWorldPosition { get; }
        public Vector3 TargetWorldPosition { get; }
        public Vector3 Direction { get; }
        public IReadOnlyList<Vector3> PathWorldPositions { get; }
        public IReadOnlyList<Vector3> HitWorldPositions { get; }
        public Vector3 PrimaryHitWorldPosition { get; }
        public float StrengthMultiplier { get; }

        public SkillVfxCueContext(
            int level,
            Vector3 sourceWorldPosition,
            Vector3 targetWorldPosition,
            Vector3 direction,
            IReadOnlyList<Vector3> pathWorldPositions = null,
            IReadOnlyList<Vector3> hitWorldPositions = null,
            Vector3? primaryHitWorldPosition = null,
            float strengthMultiplier = 1f)
        {
            Level = Mathf.Max(1, level);
            SourceWorldPosition = sourceWorldPosition;
            TargetWorldPosition = targetWorldPosition;
            Direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.right;
            PathWorldPositions = CopyPositions(pathWorldPositions);
            HitWorldPositions = CopyPositions(hitWorldPositions);
            PrimaryHitWorldPosition = primaryHitWorldPosition ?? targetWorldPosition;
            StrengthMultiplier = Mathf.Max(0f, strengthMultiplier);
        }

        private static IReadOnlyList<Vector3> CopyPositions(IReadOnlyList<Vector3> source)
        {
            if (source == null || source.Count == 0)
                return EmptyPositions;

            var copy = new Vector3[source.Count];
            for (int index = 0; index < source.Count; index++)
                copy[index] = source[index];
            return copy;
        }
    }

    /// <summary>
    /// Optional runtime endpoint for SkillGraph visual cues. A missing sink is a valid no-op.
    /// </summary>
    public interface ISkillVfxSink
    {
        Task PlayAsync(
            SkillVfxCueKind cue,
            SkillVfxCueContext context,
            CancellationToken cancellationToken);
    }

    [Serializable]
    public sealed class SkillVfxPrimitiveLayer
    {
        [SerializeField] private SkillVfxPrimitiveKind _primitiveKind;
        [SerializeField] private SkillVfxBlendMode _blendMode = SkillVfxBlendMode.Additive;
        [SerializeField] private SkillVfxShapeMode _shapeMode = SkillVfxShapeMode.Solid;
        [SerializeField] private Color _color = Color.white;
        [SerializeField] private Color _secondaryColor = Color.white;
        [SerializeField, Min(0f)] private float _startSize = 0.05f;
        [SerializeField] private bool _useMiddleKey;
        [SerializeField, Min(0f)] private float _middleSize = 0.1f;
        [SerializeField, Min(0f)] private float _middleTime = 0.04f;
        [SerializeField, Min(0f)] private float _peakSize = 0.15f;
        [SerializeField, Min(0f)] private float _endSize = 0.2f;
        [SerializeField, Min(0f)] private float _peakTime = 0.05f;
        [SerializeField, Min(0.01f)] private float _duration = 0.15f;
        [SerializeField, Min(0f)] private float _blockingMarker;
        [SerializeField, Range(0f, 1f)] private float _startAlpha;
        [SerializeField, Range(0f, 1f)] private float _middleAlpha = 1f;
        [SerializeField, Range(0f, 1f)] private float _peakAlpha = 1f;
        [SerializeField, Range(0f, 1f)] private float _endAlpha;
        [SerializeField, Range(0f, 1f)] private float _radialInner = 0.5f;
        [SerializeField, Range(0.01f, 1f)] private float _radialOuter = 1f;
        [SerializeField, Range(0.001f, 0.5f)] private float _softness = 0.12f;
        [SerializeField, Min(0f)] private float _emission = 1f;
        [SerializeField] private float _angle = 35f;
        [SerializeField, Min(0.001f)] private float _rootWidth = 0.045f;
        [SerializeField, Min(0.001f)] private float _tipWidth = 0.01f;
        [SerializeField, Min(0)] private int _particleCount;
        [SerializeField, Min(0f)] private float _particleSize = 0.03f;
        [SerializeField, Min(0f)] private float _particleSpeed = 0.2f;
        [SerializeField, Min(0.01f)] private float _particleLifetimeMin = 0.12f;
        [SerializeField, Min(0.01f)] private float _particleLifetimeMax = 0.18f;
        [SerializeField, Min(0f)] private float _particleDrag;
        [SerializeField] private uint _randomSeed = 1;
        [SerializeField, Range(1, 32)] private int _maximumInstances = 16;
        [SerializeField, Range(-50, 100)] private int _sortingOrderOffset = 30;

        public SkillVfxPrimitiveKind PrimitiveKind => _primitiveKind;
        public SkillVfxBlendMode BlendMode => _blendMode;
        public SkillVfxShapeMode ShapeMode => _shapeMode;
        public Color Color => _color;
        public Color SecondaryColor => _secondaryColor;
        public float StartSize => _startSize;
        public bool UseMiddleKey => _useMiddleKey;
        public float MiddleSize => _middleSize;
        public float MiddleTime => Mathf.Min(_middleTime, PeakTime);
        public float PeakSize => _peakSize;
        public float EndSize => _endSize;
        public float PeakTime => Mathf.Min(_peakTime, Duration);
        public float Duration => Mathf.Max(0.01f, _duration);
        public float BlockingMarker => IsAlwaysNonBlocking ? 0f : Mathf.Clamp(_blockingMarker, 0f, Duration);
        public float StartAlpha => _startAlpha;
        public float MiddleAlpha => _middleAlpha;
        public float PeakAlpha => _peakAlpha;
        public float EndAlpha => _endAlpha;
        public float RadialInner => Mathf.Min(_radialInner, RadialOuter);
        public float RadialOuter => Mathf.Max(0.01f, _radialOuter);
        public float Softness => _softness;
        public float Emission => _emission;
        public float Angle => _angle;
        public float RootWidth => Mathf.Max(0.001f, _rootWidth);
        public float TipWidth => Mathf.Min(RootWidth, Mathf.Max(0.001f, _tipWidth));
        public int ParticleCount => Mathf.Max(0, _particleCount);
        public float ParticleSize => _particleSize;
        public float ParticleSpeed => _particleSpeed;
        public float ParticleLifetimeMin => Mathf.Min(_particleLifetimeMin, ParticleLifetimeMax);
        public float ParticleLifetimeMax => Mathf.Max(0.01f, _particleLifetimeMax);
        public float ParticleDrag => _particleDrag;
        public uint RandomSeed => _randomSeed == 0 ? 1u : _randomSeed;
        public int MaximumInstances => Mathf.Clamp(_maximumInstances, 1, 32);
        public int SortingOrderOffset => _sortingOrderOffset;
        public bool IsAlwaysNonBlocking =>
            _primitiveKind is SkillVfxPrimitiveKind.ParticleBurst or SkillVfxPrimitiveKind.ProjectileGhostTrail;
    }

    [Serializable]
    public sealed class SkillVfxCueBinding
    {
        [SerializeField] private SkillVfxCueKind _cue;
        [SerializeField] private List<SkillVfxPrimitiveLayer> _layers = new();

        public SkillVfxCueKind Cue => _cue;
        public IReadOnlyList<SkillVfxPrimitiveLayer> Layers => _layers;
    }

    /// <summary>
    /// Authored mapping from semantic skill cues to a finite, deterministic primitive recipe.
    /// </summary>
    [CreateAssetMenu(fileName = "SkillVfxRecipe", menuName = "Tactics/Visuals/Skill VFX Recipe")]
    public sealed class SkillVfxRecipe : ScriptableObject
    {
        [SerializeField] private Material _transparentMaterial;
        [SerializeField] private Material _additiveMaterial;
        [SerializeField] private List<SkillVfxCueBinding> _bindings = new();

        public Material TransparentMaterial => _transparentMaterial;
        public Material AdditiveMaterial => _additiveMaterial;

        public IReadOnlyList<SkillVfxPrimitiveLayer> GetLayers(SkillVfxCueKind cue)
        {
            return _bindings.FirstOrDefault(binding => binding != null && binding.Cue == cue)?.Layers
                ?? Array.Empty<SkillVfxPrimitiveLayer>();
        }

        public Material ResolveMaterial(SkillVfxBlendMode blendMode)
        {
            return blendMode == SkillVfxBlendMode.Additive ? _additiveMaterial : _transparentMaterial;
        }
    }
}
