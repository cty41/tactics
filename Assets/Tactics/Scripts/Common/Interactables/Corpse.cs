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
            if (sprite == null)
                return;

            var spriteRenderer = ResolveSpriteRenderer();
            spriteRenderer.sprite = sprite;
            if (material != null)
                spriteRenderer.sharedMaterial = material;
            spriteRenderer.color = color;
            spriteRenderer.flipX = false;

            var spriteTransform = spriteRenderer.transform;
            var boundsCenter = sprite.bounds.center;
            spriteTransform.localPosition = new Vector3(-boundsCenter.x, -boundsCenter.y, 0f);
            spriteTransform.localRotation = Quaternion.identity;
            spriteTransform.localScale = Vector3.one;

            if (tweenProfile != null)
            {
                _landingSequence?.Kill(false);
                _landingSequence = UnitTweenSequenceBuilder.BuildCorpseLanding(
                        spriteTransform,
                        tweenProfile,
                        spriteTransform.localPosition,
                        spriteTransform.localRotation,
                        spriteTransform.localScale)
                    .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
                    .OnComplete(() => _landingSequence = null);
            }
        }

        private void OnDestroy()
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
