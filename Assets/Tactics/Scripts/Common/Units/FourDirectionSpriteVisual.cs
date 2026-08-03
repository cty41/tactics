using Tactics.Common.Units.Tween;
using UnityEngine;

namespace Tactics.Common.Units
{
    /// <summary>
    /// Selects one of two authored isometric sprite views and mirrors it to cover all four
    /// cardinal facing directions without changing the owning unit transform.
    /// </summary>
    /// <remarks>
    /// Only the configured primary SpriteRenderer is changed. Material, tint, sorting, child
    /// renderers and the owning transform remain untouched by directional or pose resolution.
    /// </remarks>
    public sealed class FourDirectionSpriteVisual : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _targetRenderer;
        [SerializeField] private Sprite _downRightSprite;
        [SerializeField] private Sprite _upLeftSprite;
        [SerializeField] private Sprite _deathSprite;
        [SerializeField] private UnitActionPoseProfile _actionPoseProfile;

        private UnitPoseFamily _activePoseFamily;
        private UnitVisualState _visualState;
        private FacingDirection _lastFacing = FacingDirection.South;
        private UnitPoseResolution _lastResolution = UnitPoseResolution.None;

        public SpriteRenderer TargetRenderer => _targetRenderer != null
            ? _targetRenderer
            : ResolveTargetRenderer();
        public Sprite DownRightSprite => _downRightSprite;
        public Sprite UpLeftSprite => _upLeftSprite;
        public Sprite DeathSprite => _deathSprite;
        public UnitActionPoseProfile ActionPoseProfile => _actionPoseProfile;
        public UnitPoseFamily ActivePoseFamily => _activePoseFamily;
        public UnitVisualState VisualState => _visualState;
        public FacingDirection LastFacing => _lastFacing;
        public UnitPoseResolution LastResolution => _lastResolution;

        /// <summary>
        /// Configures this visual for generated or test-only units. Prefabs normally use the
        /// serialized fields through the Unity inspector.
        /// </summary>
        public void Configure(
            SpriteRenderer targetRenderer,
            Sprite downRightSprite,
            Sprite upLeftSprite,
            UnitActionPoseProfile actionPoseProfile = null)
        {
            _targetRenderer = targetRenderer;
            _downRightSprite = downRightSprite;
            _upLeftSprite = upLeftSprite;
            _actionPoseProfile = actionPoseProfile;
        }

        /// <summary>
        /// Assigns a pose profile for generated, preview or test-only units.
        /// </summary>
        public void ConfigureActionPoseProfile(UnitActionPoseProfile actionPoseProfile)
        {
            _actionPoseProfile = actionPoseProfile;
            TryApply(_lastFacing);
        }

        /// <summary>
        /// Activates one semantic pose family and resolves it for the supplied direction.
        /// </summary>
        public bool SetPose(UnitPoseFamily family, FacingDirection facing)
        {
            _activePoseFamily = family;
            return TryApply(facing);
        }

        /// <summary>
        /// Clears the current action pose and restores the current visual state's idle pair.
        /// </summary>
        public bool ClearPose(FacingDirection facing)
        {
            _activePoseFamily = null;
            return TryApply(facing);
        }

        /// <summary>
        /// Changes the equipment-dependent visual state and immediately re-resolves the sprite.
        /// </summary>
        public bool SetVisualState(UnitVisualState state, FacingDirection facing)
        {
            _visualState = state;
            return TryApply(facing);
        }

        /// <summary>
        /// Applies the visual mapping for a cardinal direction.
        /// Returns false when the component has not been fully configured, so legacy unit
        /// visuals can retain their existing East/West behaviour.
        /// </summary>
        public bool TryApply(FacingDirection facing)
        {
            var targetRenderer = TargetRenderer;
            _lastFacing = facing;
            if (targetRenderer == null || !TryResolveSprites(out var downRight, out var upLeft))
                return false;

            switch (facing)
            {
                case FacingDirection.East:
                    // Unity's isometric +X axis points up-right on screen.
                    targetRenderer.sprite = upLeft;
                    targetRenderer.flipX = true;
                    return true;
                case FacingDirection.West:
                    targetRenderer.sprite = downRight;
                    targetRenderer.flipX = true;
                    return true;
                case FacingDirection.North:
                    targetRenderer.sprite = upLeft;
                    targetRenderer.flipX = false;
                    return true;
                case FacingDirection.South:
                    targetRenderer.sprite = downRight;
                    targetRenderer.flipX = false;
                    return true;
                default:
                    return false;
            }
        }

        private bool TryResolveSprites(out Sprite downRight, out Sprite upLeft)
        {
            if (_actionPoseProfile != null && _activePoseFamily != null &&
                _actionPoseProfile.TryResolvePose(
                    _activePoseFamily,
                    _visualState,
                    out downRight,
                    out upLeft,
                    out _lastResolution))
            {
                return true;
            }

            if (_actionPoseProfile != null &&
                _actionPoseProfile.TryResolveIdle(_visualState, out downRight, out upLeft))
            {
                _lastResolution = UnitPoseResolution.StateIdle;
                return true;
            }

            downRight = _downRightSprite;
            upLeft = _upLeftSprite;
            _lastResolution = UnitPoseResolution.BaseIdle;
            return downRight != null && upLeft != null;
        }

        private SpriteRenderer ResolveTargetRenderer()
        {
            foreach (var spriteRenderer in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (spriteRenderer.gameObject.name == "Sprite")
                    return spriteRenderer;
            }

            return null;
        }
    }
}
