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

        public GameObject Prefab => _prefab;
        public VisualCueAnchor Anchor => _anchor;
        public VisualCueCompletionPolicy CompletionPolicy => _completionPolicy;
        public float Lifetime => _lifetime;
        public float Scale => _scale;
        public int SortingOrderOffset => _sortingOrderOffset;
    }

    /// <summary>
    /// Selects the combat anchor consumed by a visual cue.
    /// </summary>
    public enum VisualCueAnchor
    {
        Caster,
        PrimaryTarget,
        TargetPoint
    }

    /// <summary>
    /// Controls whether a visual cue delays the next skill graph node.
    /// </summary>
    public enum VisualCueCompletionPolicy
    {
        FireAndForget,
        AwaitCompletion
    }
}
