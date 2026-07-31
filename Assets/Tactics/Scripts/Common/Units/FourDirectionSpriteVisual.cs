using UnityEngine;

namespace Tactics.Common.Units
{
    /// <summary>
    /// Selects one of two authored isometric sprite views and mirrors it to cover all four
    /// cardinal facing directions without changing the owning unit transform.
    /// </summary>
    public sealed class FourDirectionSpriteVisual : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _targetRenderer;
        [SerializeField] private Sprite _downRightSprite;
        [SerializeField] private Sprite _upLeftSprite;

        public SpriteRenderer TargetRenderer => _targetRenderer != null
            ? _targetRenderer
            : FindTargetRenderer();
        public Sprite DownRightSprite => _downRightSprite;
        public Sprite UpLeftSprite => _upLeftSprite;

        /// <summary>
        /// Configures this visual for generated or test-only units. Prefabs normally use the
        /// serialized fields through the Unity inspector.
        /// </summary>
        public void Configure(SpriteRenderer targetRenderer, Sprite downRightSprite, Sprite upLeftSprite)
        {
            _targetRenderer = targetRenderer;
            _downRightSprite = downRightSprite;
            _upLeftSprite = upLeftSprite;
        }

        /// <summary>
        /// Applies the visual mapping for a cardinal direction.
        /// Returns false when the component has not been fully configured, so legacy unit
        /// visuals can retain their existing East/West behaviour.
        /// </summary>
        public bool TryApply(FacingDirection facing)
        {
            var targetRenderer = TargetRenderer;
            if (targetRenderer == null || _downRightSprite == null || _upLeftSprite == null)
                return false;

            switch (facing)
            {
                case FacingDirection.East:
                    // Unity's isometric +X axis points up-right on screen.
                    targetRenderer.sprite = _upLeftSprite;
                    targetRenderer.flipX = true;
                    return true;
                case FacingDirection.West:
                    targetRenderer.sprite = _downRightSprite;
                    targetRenderer.flipX = true;
                    return true;
                case FacingDirection.North:
                    targetRenderer.sprite = _upLeftSprite;
                    targetRenderer.flipX = false;
                    return true;
                case FacingDirection.South:
                    targetRenderer.sprite = _downRightSprite;
                    targetRenderer.flipX = false;
                    return true;
                default:
                    return false;
            }
        }

        private SpriteRenderer FindTargetRenderer()
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
