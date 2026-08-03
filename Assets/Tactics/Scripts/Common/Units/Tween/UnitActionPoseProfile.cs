using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tactics.Common.Units.Tween
{
    /// <summary>
    /// Describes how a sprite pair was resolved for the current action and visual state.
    /// </summary>
    public enum UnitPoseResolution
    {
        None,
        ExactPoseState,
        DefaultPoseState,
        StateIdle,
        BaseIdle
    }

    /// <summary>
    /// Maps reusable pose families and equipment states to one character's directional sprites.
    /// </summary>
    /// <remarks>
    /// Resolution never substitutes an unrelated family. An explicit missing future family
    /// falls back to idle instead of borrowing the character's default ranged or cast pose.
    /// </remarks>
    [CreateAssetMenu(fileName = "UnitActionPoseProfile", menuName = "Tactics/Units/Unit Action Pose Profile")]
    public sealed class UnitActionPoseProfile : ScriptableObject
    {
        [Serializable]
        private sealed class IdleEntry
        {
            public UnitVisualState State;
            public Sprite DownRight;
            public Sprite UpLeft;
        }

        [Serializable]
        private sealed class PoseEntry
        {
            public UnitPoseFamily Family;
            public UnitVisualState State;
            public Sprite DownRight;
            public Sprite UpLeft;
        }

        [Header("Default Families")]
        [SerializeField] private UnitPoseFamily _meleeAttackFamily;
        [SerializeField] private UnitPoseFamily _rangedAttackFamily;
        [SerializeField] private UnitPoseFamily _castFamily;
        [SerializeField] private UnitPoseFamily _hitFamily;

        [Header("Sprites")]
        [SerializeField] private List<IdleEntry> _idleEntries = new();
        [SerializeField] private List<PoseEntry> _poseEntries = new();

        /// <summary>
        /// Default family used for hit reactions.
        /// </summary>
        public UnitPoseFamily HitFamily => _hitFamily;

        /// <summary>
        /// Resolves an explicit family or the character default for a Tween action.
        /// </summary>
        public UnitPoseFamily ResolveFamily(UnitVisualAction action, UnitPoseFamily explicitFamily = null)
        {
            if (explicitFamily != null)
                return explicitFamily;

            return action switch
            {
                UnitVisualAction.Melee => _meleeAttackFamily,
                UnitVisualAction.Ranged => _rangedAttackFamily,
                UnitVisualAction.Cast => _castFamily,
                _ => null
            };
        }

        /// <summary>
        /// Resolves a pose within the requested family without substituting another family.
        /// </summary>
        public bool TryResolvePose(
            UnitPoseFamily family,
            UnitVisualState state,
            out Sprite downRight,
            out Sprite upLeft,
            out UnitPoseResolution resolution)
        {
            if (TryFindPose(family, state, out downRight, out upLeft))
            {
                resolution = UnitPoseResolution.ExactPoseState;
                return true;
            }

            if (state != UnitVisualState.Default &&
                TryFindPose(family, UnitVisualState.Default, out downRight, out upLeft))
            {
                resolution = UnitPoseResolution.DefaultPoseState;
                return true;
            }

            downRight = null;
            upLeft = null;
            resolution = UnitPoseResolution.None;
            return false;
        }

        /// <summary>
        /// Resolves an equipment-state idle pair stored by this profile.
        /// </summary>
        public bool TryResolveIdle(UnitVisualState state, out Sprite downRight, out Sprite upLeft)
        {
            foreach (var entry in _idleEntries)
            {
                if (entry != null && entry.State == state && HasCompletePair(entry.DownRight, entry.UpLeft))
                {
                    downRight = entry.DownRight;
                    upLeft = entry.UpLeft;
                    return true;
                }
            }

            downRight = null;
            upLeft = null;
            return false;
        }

        /// <summary>
        /// Configures default families for transient profiles used by tests or editor tooling.
        /// </summary>
        public void ConfigureDefaultFamilies(
            UnitPoseFamily meleeAttackFamily,
            UnitPoseFamily rangedAttackFamily,
            UnitPoseFamily castFamily,
            UnitPoseFamily hitFamily)
        {
            _meleeAttackFamily = meleeAttackFamily;
            _rangedAttackFamily = rangedAttackFamily;
            _castFamily = castFamily;
            _hitFamily = hitFamily;
        }

        /// <summary>
        /// Adds or replaces an equipment-state idle pair.
        /// </summary>
        public void SetIdleSprites(UnitVisualState state, Sprite downRight, Sprite upLeft)
        {
            var entry = _idleEntries.Find(candidate => candidate != null && candidate.State == state);
            if (entry == null)
            {
                entry = new IdleEntry { State = state };
                _idleEntries.Add(entry);
            }

            entry.DownRight = downRight;
            entry.UpLeft = upLeft;
        }

        /// <summary>
        /// Adds or replaces a pose pair for one family and equipment state.
        /// </summary>
        public void SetPoseSprites(
            UnitPoseFamily family,
            UnitVisualState state,
            Sprite downRight,
            Sprite upLeft)
        {
            var entry = _poseEntries.Find(candidate =>
                candidate != null && candidate.Family == family && candidate.State == state);
            if (entry == null)
            {
                entry = new PoseEntry { Family = family, State = state };
                _poseEntries.Add(entry);
            }

            entry.DownRight = downRight;
            entry.UpLeft = upLeft;
        }

        private bool TryFindPose(
            UnitPoseFamily family,
            UnitVisualState state,
            out Sprite downRight,
            out Sprite upLeft)
        {
            if (family != null)
            {
                foreach (var entry in _poseEntries)
                {
                    if (entry != null && entry.Family == family && entry.State == state &&
                        HasCompletePair(entry.DownRight, entry.UpLeft))
                    {
                        downRight = entry.DownRight;
                        upLeft = entry.UpLeft;
                        return true;
                    }
                }
            }

            downRight = null;
            upLeft = null;
            return false;
        }

        private static bool HasCompletePair(Sprite downRight, Sprite upLeft) =>
            downRight != null && upLeft != null;
    }
}
