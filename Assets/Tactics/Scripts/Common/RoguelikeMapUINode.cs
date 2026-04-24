using System;
using DG.Tweening;
using Tactics.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tactics.RoguelikeMap
{
    public enum NodeStates
    {
        Locked,
        Visited,
        Attainable
    }

    public sealed class RoguelikeMapUINode
    {
        public RoguelikeMapNode Node { get; }
        public RoguelikeNodeBlueprint Blueprint { get; }
        public VisualElement Root { get; }
        public Vector2 NodePosition { get; private set; }

        private readonly VisualElement _nodeIcon;
        private readonly VisualElement _visitedIndicator;
        private readonly RadialFillElement _swirlFill;

        private readonly float _initialScale;
        private const float HoverScaleFactor = 1.2f;
        private const float MaxClickDuration = 0.5f;
        private float _mouseDownTime;

        private readonly Color _visitedColor;
        private readonly Color _lockedColor;

        private float _currentScale = 1f;
        private Color _currentTintColor = Color.white;

        private Tween _hoverTween;
        private Tween _attainableTween;
        private Tween _swirlTween;

        public RoguelikeMapUINode(RoguelikeMapNode node, RoguelikeNodeBlueprint blueprint,
            Color visitedColor, Color lockedColor, VisualTreeAsset template)
        {
            Node = node;
            Blueprint = blueprint;
            _visitedColor = visitedColor;
            _lockedColor = lockedColor;

            if (template != null)
            {
                Root = template.Instantiate();
            }
            else
            {
                Root = new VisualElement();
                Root.AddToClassList("map-node");
            }

            _nodeIcon = Root.Q<VisualElement>("NodeIcon");
            _visitedIndicator = Root.Q<VisualElement>("VisitedIndicator");
            _swirlFill = new RadialFillElement();
            _swirlFill.name = "SwirlFill";
            _swirlFill.AddToClassList("swirl-fill");
            Root.Add(_swirlFill);

            if (_nodeIcon != null && blueprint?.sprite != null)
                _nodeIcon.style.backgroundImage = new StyleBackground(blueprint.sprite);

            if (node.nodeType == RoguelikeNodeType.Boss)
                _initialScale = 1.5f;
            else
                _initialScale = 1f;

            Root.style.transformOrigin = new TransformOrigin(50f, 50f, 0f);
            Root.style.scale = new Scale(new Vector2(_initialScale, _initialScale));
            _currentScale = _initialScale;

            if (_visitedIndicator != null)
                _visitedIndicator.style.display = DisplayStyle.None;
            if (_swirlFill != null)
                _swirlFill.style.display = DisplayStyle.None;

            SetState(NodeStates.Locked);

            Root.RegisterCallback<PointerEnterEvent>(OnPointerEnter);
            Root.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            Root.RegisterCallback<PointerDownEvent>(OnPointerDown);
            Root.RegisterCallback<PointerUpEvent>(OnPointerUp);
        }

        public void SetPosition(Vector2 pos)
        {
            NodePosition = pos;
            Root.style.position = UnityEngine.UIElements.Position.Absolute;
            Root.style.left = pos.x;
            Root.style.top = pos.y;
        }

        public void SetState(NodeStates state)
        {
            if (_visitedIndicator != null)
                _visitedIndicator.style.display = DisplayStyle.None;

            KillTweens();

            switch (state)
            {
                case NodeStates.Locked:
                    _currentTintColor = _lockedColor;
                    ApplyTint(_lockedColor);
                    break;
                case NodeStates.Visited:
                    _currentTintColor = _visitedColor;
                    ApplyTint(_visitedColor);
                    if (_visitedIndicator != null)
                        _visitedIndicator.style.display = DisplayStyle.Flex;
                    break;
                case NodeStates.Attainable:
                    _currentTintColor = _lockedColor;
                    ApplyTint(_lockedColor);
                    _attainableTween = DOTween.To(
                        () => _currentTintColor,
                        c => { _currentTintColor = c; ApplyTint(c); },
                        _visitedColor, 0.5f
                    ).SetLoops(-1, LoopType.Yoyo);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }

        private void ApplyTint(Color color)
        {
            if (_nodeIcon != null)
                _nodeIcon.style.unityBackgroundImageTintColor = color;
        }

        private void OnPointerEnter(PointerEnterEvent evt)
        {
            _hoverTween?.Kill();
            _hoverTween = DOTween.To(
                () => _currentScale,
                s => { _currentScale = s; Root.style.scale = new Scale(new Vector2(s, s)); },
                _initialScale * HoverScaleFactor, 0.3f
            );
        }

        private void OnPointerLeave(PointerLeaveEvent evt)
        {
            _hoverTween?.Kill();
            _hoverTween = DOTween.To(
                () => _currentScale,
                s => { _currentScale = s; Root.style.scale = new Scale(new Vector2(s, s)); },
                _initialScale, 0.3f
            );
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            _mouseDownTime = Time.time;
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (Time.time - _mouseDownTime < MaxClickDuration)
            {
                Tactics.UI.RoguelikeMapUIController.Instance?.SelectNode(this);
            }
        }

        public void ShowSwirlAnimation()
        {
            if (_swirlFill == null) return;

            _swirlFill.style.display = DisplayStyle.Flex;
            _swirlFill.FillAmount = 0f;
            _swirlFill.FillColor = _visitedColor;

            _swirlTween?.Kill();
            float fill = 0f;
            _swirlTween = DOTween.To(
                () => fill,
                f => { fill = f; _swirlFill.FillAmount = f; },
                1f, 0.3f
            );
        }

        private void KillTweens()
        {
            _attainableTween?.Kill();
            _attainableTween = null;
        }

        public void Dispose()
        {
            _hoverTween?.Kill();
            _attainableTween?.Kill();
            _swirlTween?.Kill();

            Root?.UnregisterCallback<PointerEnterEvent>(OnPointerEnter);
            Root?.UnregisterCallback<PointerLeaveEvent>(OnPointerLeave);
            Root?.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            Root?.UnregisterCallback<PointerUpEvent>(OnPointerUp);
        }
    }
}
