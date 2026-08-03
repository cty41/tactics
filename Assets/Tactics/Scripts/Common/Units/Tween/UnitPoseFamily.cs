using UnityEngine;

namespace Tactics.Common.Units.Tween
{
    /// <summary>
    /// Determines when a single-frame action pose returns to the unit's idle sprite.
    /// </summary>
    public enum UnitPoseExitPolicy
    {
        RecoveryStart,
        Release
    }

    /// <summary>
    /// Identifies an authoritative equipment-dependent visual state.
    /// </summary>
    public enum UnitVisualState
    {
        Default,
        Unarmed
    }

    /// <summary>
    /// Defines reusable action-pose semantics independently from character artwork.
    /// </summary>
    /// <remarks>
    /// Abilities may explicitly reference a family when their physical action is known. When
    /// they do not, the unit action-pose profile selects a character-specific default family.
    /// </remarks>
    [CreateAssetMenu(fileName = "UnitPoseFamily", menuName = "Tactics/Units/Unit Pose Family")]
    public sealed class UnitPoseFamily : ScriptableObject
    {
        [SerializeField] private string _stableId;
        [SerializeField] private UnitPoseExitPolicy _exitPolicy = UnitPoseExitPolicy.RecoveryStart;

        /// <summary>
        /// Stable semantic identifier used by diagnostics and preview tools.
        /// </summary>
        public string StableId => string.IsNullOrWhiteSpace(_stableId) ? name : _stableId;

        /// <summary>
        /// Marker at which the pose returns to idle.
        /// </summary>
        public UnitPoseExitPolicy ExitPolicy => _exitPolicy;

        /// <summary>
        /// Configures a transient family used by tests or data-driven tooling.
        /// </summary>
        public void Configure(string stableId, UnitPoseExitPolicy exitPolicy)
        {
            _stableId = stableId;
            _exitPolicy = exitPolicy;
        }
    }
}
