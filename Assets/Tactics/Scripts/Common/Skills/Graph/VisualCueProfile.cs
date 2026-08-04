using UnityEngine;

namespace Tactics.Common.Skills.Graph
{
    /// <summary>
    /// Describes a transient skill VFX independent from battle resolution.
    /// </summary>
    [CreateAssetMenu(fileName = "VisualCueProfile", menuName = "Tactics/Visuals/Visual Cue Profile")]
    public sealed class VisualCueProfile : ScriptableObject
    {
        [SerializeField] private GameObject _prefab;
        [SerializeField] private VisualCueAnchor _anchor = VisualCueAnchor.TargetPoint;
        [SerializeField] private VisualCueCompletionPolicy _completionPolicy = VisualCueCompletionPolicy.FireAndForget;
        [SerializeField, Min(0.05f)] private float _lifetime = 0.6f;
        [SerializeField, Min(0.01f)] private float _scale = 1f;
        [SerializeField, Range(-50, 100)] private int _sortingOrderOffset = 20;
        [SerializeField] private VisualCueOrientationMode _orientationMode;
        [SerializeField] private bool _stretchXToSourceTarget;
        [SerializeField, Min(0.01f)] private float _referenceDistance = 1f;

        public GameObject Prefab => _prefab;
        public VisualCueAnchor Anchor => _anchor;
        public VisualCueCompletionPolicy CompletionPolicy => _completionPolicy;
        public float Lifetime => _lifetime;
        public float Scale => _scale;
        public int SortingOrderOffset => _sortingOrderOffset;
        internal VisualCueOrientationMode OrientationMode => _orientationMode;
        internal bool StretchXToSourceTarget => _stretchXToSourceTarget;
        internal float ReferenceDistance => Mathf.Max(0.01f, _referenceDistance);
    }

    /// <summary>
    /// Selects the combat anchor consumed by a visual cue.
    /// </summary>
    public enum VisualCueAnchor
    {
        Caster,
        PrimaryTarget,
        TargetPoint,
        PrimaryTargetGround
    }

    /// <summary>
    /// Controls whether a visual cue delays the next skill graph node.
    /// </summary>
    public enum VisualCueCompletionPolicy
    {
        FireAndForget,
        AwaitCompletion
    }

    /// <summary>
    /// Selects the internal orientation rule used by project-owned adapted VFX.
    /// </summary>
    internal enum VisualCueOrientationMode
    {
        World,
        SourceToTarget
    }

    /// <summary>
    /// Resolves the shared runtime and editor-preview transform for a visual cue.
    /// </summary>
    internal static class VisualCueTransformUtility
    {
        internal static Quaternion ResolveRotation(
            VisualCueProfile profile,
            Vector3 sourceWorldPosition,
            Vector3 targetWorldPosition)
        {
            if (profile == null || profile.OrientationMode != VisualCueOrientationMode.SourceToTarget)
                return Quaternion.identity;

            Vector3 direction = targetWorldPosition - sourceWorldPosition;
            if (direction.sqrMagnitude <= 0.000001f)
                return Quaternion.identity;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            return Quaternion.Euler(0f, 0f, angle);
        }

        internal static Vector3 ResolveScale(
            VisualCueProfile profile,
            Vector3 sourceWorldPosition,
            Vector3 targetWorldPosition)
        {
            if (profile == null)
                return Vector3.one;

            float scale = Mathf.Max(0.01f, profile.Scale);
            if (!profile.StretchXToSourceTarget)
                return Vector3.one * scale;

            float distance = Vector3.Distance(sourceWorldPosition, targetWorldPosition);
            return new Vector3(
                scale * distance / profile.ReferenceDistance,
                scale,
                scale);
        }
    }
}
