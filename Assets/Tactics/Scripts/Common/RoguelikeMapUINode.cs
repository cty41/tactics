using System;
using System.Linq;
using DG.Tweening;
using Tactics.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tactics.RoguelikeMap
{
    // 使用 RoguelikeMap 命名空间中的 NodeState 枚举
    // NodeState: Unrevealed, Revealed, Reachable, Visited

    public sealed class RoguelikeMapUINode
    {
        public RoguelikeMapNode Node { get; }
        public RoguelikeNodeBlueprint Blueprint { get; }
        public VisualElement Root { get; }
        public Vector2 NodePosition { get; private set; }

        private readonly VisualElement _nodeIcon;
        private readonly VisualElement _visitedIndicator;
        private readonly RadialFillElement _swirlFill;
        private readonly Label _debugLabel;

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
                var container = template.Instantiate();
                Root = container.Q<VisualElement>("NodeRoot") ?? container;
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

            _debugLabel = new Label();
            _debugLabel.name = "DebugLabel";
            _debugLabel.style.position = UnityEngine.UIElements.Position.Absolute;
            _debugLabel.style.top = -20f;
            _debugLabel.style.left = 0f;
                        _debugLabel.style.fontSize = 16;
            _debugLabel.style.color = Color.white;
            _debugLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                        _debugLabel.style.width = 120f;
            _debugLabel.style.whiteSpace = WhiteSpace.NoWrap;
            _debugLabel.style.overflow = Overflow.Visible;
            _debugLabel.pickingMode = PickingMode.Ignore;
            Root.Add(_debugLabel);

            if (_nodeIcon != null)
            {
                _nodeIcon.style.width = 64f;
                _nodeIcon.style.height = 64f;
                if (blueprint?.sprite != null)
                {
                    _nodeIcon.style.backgroundImage = new StyleBackground(blueprint.sprite);
                }
                else
                {
                    _nodeIcon.style.backgroundColor = new StyleColor(_lockedColor);
                }
            }

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

            SetState(NodeState.Unrevealed);

            Root.RegisterCallback<PointerEnterEvent>(OnPointerEnter);
            Root.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            Root.RegisterCallback<ClickEvent>(OnClick);
        }

        public void SetPosition(Vector2 pos)
        {
            NodePosition = pos;
            Root.style.position = UnityEngine.UIElements.Position.Absolute;
            Root.style.left = pos.x;
            Root.style.top = pos.y;
            Root.style.width = 64f;
            Root.style.height = 64f;
        }

        private void UpdateDebugLabel()
        {
            if (_debugLabel == null) return;
            _debugLabel.text = $"#{Node.nodeId}\n{Node.nodeType}\n{Node.state}";
            _debugLabel.style.color = Node.state switch
            {
                NodeState.Reachable => new Color(0.3f, 1f, 0.3f),
                NodeState.Visited => new Color(0.5f, 0.5f, 0.5f),
                NodeState.Revealed => new Color(0.6f, 0.6f, 0.3f),
                _ => new Color(0.8f, 0.3f, 0.3f) // Unrevealed = red
            };
        }

        public void SetState(NodeState state)
        {
            if (_visitedIndicator != null)
                _visitedIndicator.style.display = DisplayStyle.None;

            // Reset border to default
            Root.style.borderTopWidth = 0f;
            Root.style.borderBottomWidth = 0f;
            Root.style.borderLeftWidth = 0f;
            Root.style.borderRightWidth = 0f;

            KillTweens();

            switch (state)
            {
                case NodeState.Unrevealed:
                    // 未揭示：灰色问号外观，不可点击
                    _currentTintColor = new Color(_lockedColor.r, _lockedColor.g, _lockedColor.b, 0.3f);
                    ApplyTint(_currentTintColor);
                    ApplyIconOpacity(0.3f);
                    Root.pickingMode = PickingMode.Ignore;
                    break;
                case NodeState.Revealed:
                    // 已揭示：真实图标，半透明，不可点击
                    _currentTintColor = new Color(_lockedColor.r, _lockedColor.g, _lockedColor.b, 0.5f);
                    ApplyTint(_currentTintColor);
                    ApplyIconOpacity(0.5f);
                    Root.pickingMode = PickingMode.Ignore;
                    break;
                case NodeState.Reachable:
                    // 可到达：真实图标，高亮边框闪烁，可点击
                    _currentTintColor = _visitedColor;
                    ApplyTint(_visitedColor);
                    ApplyIconOpacity(1f);
                    Root.pickingMode = PickingMode.Position;

                    // 高亮边框
                    ApplyBorderHighlight();

                    // 脉冲动画
                    _attainableTween = DOTween.To(
                        () => _currentTintColor,
                        c => { _currentTintColor = c; ApplyTint(c); },
                        _lockedColor, 0.6f
                    ).SetLoops(-1, LoopType.Yoyo);
                    break;
                case NodeState.Visited:
                    // 已访问：真实图标，0.4 透明度，不可点击
                    _currentTintColor = new Color(_visitedColor.r, _visitedColor.g, _visitedColor.b, 0.4f);
                    ApplyTint(_currentTintColor);
                    ApplyIconOpacity(0.4f);
                    if (_visitedIndicator != null)
                        _visitedIndicator.style.display = DisplayStyle.Flex;
                    Root.pickingMode = PickingMode.Ignore;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }

            UpdateDebugLabel();
        }

        private void ApplyTint(Color color)
        {
            if (_nodeIcon != null)
                _nodeIcon.style.unityBackgroundImageTintColor = color;
        }

        private void ApplyIconOpacity(float opacity)
        {
            if (_nodeIcon != null)
                _nodeIcon.style.opacity = opacity;
        }

        private void ApplyBorderHighlight()
        {
            Root.style.borderTopWidth = 2f;
            Root.style.borderBottomWidth = 2f;
            Root.style.borderLeftWidth = 2f;
            Root.style.borderRightWidth = 2f;
            Root.style.borderTopColor = _visitedColor;
            Root.style.borderBottomColor = _visitedColor;
            Root.style.borderLeftColor = _visitedColor;
            Root.style.borderRightColor = _visitedColor;
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

        private void OnClick(ClickEvent evt)
        {
            Tactics.UI.RoguelikeMapUIController.Instance?.SelectNode(this);
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
            Root?.UnregisterCallback<ClickEvent>(OnClick);
        }
    }
}
