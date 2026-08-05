using DG.Tweening;
using Tactics.Common.Units.Tween;
using UnityEngine;

namespace Tactics.Common.Interactables
{
    /// <summary>
    /// 尸体：战场中由敌人死亡生成的可交互对象。
    /// 占格、可选中、可被死灵法术消耗。
    /// 作为 MonoBehaviour 挂在 GameObject 上，承载视觉表现。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Corpse : Interactable
    {
        [SerializeField] private SpriteRenderer _spriteRenderer;
        private Sequence _landingSequence;
        private StandardUnitTweenProfile _tweenProfile;

        public override bool OccupiesCell => true;
        public override bool Selectable => true;

        public override void Interact()
        {
            Consume();
        }

        /// <summary>
        /// 消耗尸体（例如被死灵法术用于召唤骷髅）。
        /// </summary>
        public void Consume()
        {
            Destroy();
        }

        /// <summary>
        /// Applies an authored corpse sprite while preserving the defeated unit's material
        /// treatment, such as Pure Run goat role tinting.
        /// </summary>
        internal void ApplyVisual(
            Sprite sprite,
            Material material,
            Color color,
            StandardUnitTweenProfile tweenProfile = null)
        {
            if (!PrepareVisual(sprite, material, color, tweenProfile, true))
                return;

            BeginLanding();
        }

        /// <summary>
        /// Applies the immediate corpse presentation and exposes its landing sequence to preview tools.
        /// </summary>
        /// <returns>The non-blocking landing sequence, or null when no sprite or profile is supplied.</returns>
        internal Sequence ApplyVisualForPreview(
            Sprite sprite,
            Material material,
            Color color,
            StandardUnitTweenProfile tweenProfile = null)
        {
            if (!PrepareVisual(sprite, material, color, tweenProfile, true))
                return null;

            return BeginLanding();
        }

        /// <summary>
        /// Inherits the defeated unit's renderer ordering before the corpse is revealed.
        /// </summary>
        /// <param name="sourceRenderer">The defeated unit's primary sprite renderer.</param>
        internal void InheritSortingFrom(SpriteRenderer sourceRenderer)
        {
            if (sourceRenderer == null)
                return;

            SpriteRenderer corpseRenderer = ResolveSpriteRenderer();
            corpseRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
            corpseRenderer.sortingOrder = sourceRenderer.sortingOrder;
        }

        /// <summary>
        /// Prepares an authored corpse while keeping gameplay interaction active independently of rendering.
        /// </summary>
        /// <returns>True when an authored sprite was prepared; otherwise false.</returns>
        internal bool PrepareVisual(
            Sprite sprite,
            Material material,
            Color color,
            StandardUnitTweenProfile tweenProfile,
            bool visible)
        {
            if (sprite == null)
                return false;

            KillLandingSequence();
            _tweenProfile = tweenProfile;

            var spriteRenderer = ResolveSpriteRenderer();
            spriteRenderer.sprite = sprite;
            if (material != null)
                spriteRenderer.sharedMaterial = material;
            spriteRenderer.color = color;
            spriteRenderer.flipX = false;
            spriteRenderer.enabled = visible;

            var spriteTransform = spriteRenderer.transform;
            spriteTransform.localPosition = Vector3.zero;
            spriteTransform.localRotation = Quaternion.identity;
            spriteTransform.localScale = Vector3.one;
            return true;
        }

        /// <summary>
        /// Reveals the prepared corpse and starts its non-blocking landing presentation.
        /// </summary>
        /// <returns>The landing sequence, or null when the prepared corpse has no tween profile.</returns>
        internal Sequence BeginLanding()
        {
            ResolveSpriteRenderer().enabled = true;
            return BuildLandingSequence();
        }

        /// <summary>
        /// Builds the prepared landing tail without changing renderer visibility.
        /// </summary>
        internal Sequence BuildLandingSequenceForPreview()
        {
            return BuildLandingSequence();
        }

        /// <summary>
        /// Reveals a prepared corpse at the runtime death handoff marker.
        /// </summary>
        internal void ShowPreparedVisual()
        {
            ResolveSpriteRenderer().enabled = true;
        }

        private void OnDestroy()
        {
            KillLandingSequence();
        }

        private Sequence BuildLandingSequence()
        {
            KillLandingSequence();
            if (_tweenProfile == null)
                return null;

            var spriteTransform = ResolveSpriteRenderer().transform;
            Sequence ownedSequence = UnitTweenSequenceBuilder.BuildCorpseLanding(
                    spriteTransform,
                    _tweenProfile,
                    spriteTransform.localPosition,
                    spriteTransform.localRotation,
                    spriteTransform.localScale)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            _landingSequence = ownedSequence;
            ownedSequence.OnComplete(() =>
            {
                if (_landingSequence == ownedSequence)
                    _landingSequence = null;
            });
            return ownedSequence;
        }

        private void KillLandingSequence()
        {
            if (_landingSequence != null && _landingSequence.IsActive())
                _landingSequence.Kill(false);
            _landingSequence = null;
        }

        private SpriteRenderer ResolveSpriteRenderer()
        {
            if (_spriteRenderer != null)
                return _spriteRenderer;

            foreach (var spriteRenderer in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (spriteRenderer.gameObject.name == "Sprite")
                {
                    _spriteRenderer = spriteRenderer;
                    return _spriteRenderer;
                }
            }

            var spriteObject = new GameObject("Sprite");
            spriteObject.transform.SetParent(transform, false);
            _spriteRenderer = spriteObject.AddComponent<SpriteRenderer>();
            return _spriteRenderer;
        }
    }
}
