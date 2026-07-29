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
            // 完全脱离图标区域：底边贴节点顶，向上排；水平以节点中心居中
            _debugLabel.style.position = UnityEngine.UIElements.Position.Absolute;
            _debugLabel.style.bottom = 64f;
            _debugLabel.style.left = Length.Percent(50);
            _debugLabel.style.translate = new Translate(Length.Percent(-50), 0);
            _debugLabel.style.width = 160f;
            _debugLabel.style.fontSize = 13;
            _debugLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _debugLabel.style.color = Color.white;
            _debugLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _debugLabel.style.unityTextOutlineWidth = 2f;
            _debugLabel.style.unityTextOutlineColor = Color.black;
            // 半透明黑底 pill：任何地图背景下保证文字对比度
            _debugLabel.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);
            _debugLabel.style.borderTopLeftRadius = 4f;
            _debugLabel.style.borderTopRightRadius = 4f;
            _debugLabel.style.borderBottomLeftRadius = 4f;
            _debugLabel.style.borderBottomRightRadius = 4f;
            _debugLabel.style.paddingLeft = 6f;
            _debugLabel.style.paddingRight = 6f;
            _debugLabel.style.paddingTop = 2f;
            _debugLabel.style.paddingBottom = 2f;
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

            ApplyVisualState();

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
            string shortId = Node.nodeId != null && Node.nodeId.Length > 6
                ? Node.nodeId.Substring(0, 6)
                : Node.nodeId;
            _debugLabel.text = $"#{shortId} {Node.nodeType}\nV:{Node.Visibility} R:{Node.IsReachable}";
            _debugLabel.style.color = Node.IsReachable
                ? new Color(0.45f, 1f, 0.45f)
                : Node.VisitState == NodeVisitState.Visited
                    ? new Color(0.75f, 0.75f, 0.75f)
                    : Node.Visibility == NodeVisibility.Revealed
                        ? new Color(1f, 0.9f, 0.4f)
                        : new Color(1f, 0.45f, 0.45f);
        }

        [Obsolete("Use ApplyVisualState() which reads Node.Visibility, Node.VisitState, Node.IsReachable directly.")]
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
                    // tint alpha 恒 1，透明度只由 opacity 单点控制（避免双重衰减）
                    _currentTintColor = new Color(_lockedColor.r, _lockedColor.g, _lockedColor.b, 1f);
                    ApplyTint(_currentTintColor);
                    ApplyIconOpacity(0.55f);
                    Root.pickingMode = PickingMode.Ignore;
                    break;
                case NodeState.Revealed:
                    // 已揭示：真实图标，半透明，不可点击
                    _currentTintColor = new Color(_lockedColor.r, _lockedColor.g, _lockedColor.b, 1f);
                    ApplyTint(_currentTintColor);
                    ApplyIconOpacity(0.7f);
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
                    _currentTintColor = new Color(_visitedColor.r, _visitedColor.g, _visitedColor.b, 1f);
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

        /// <summary>
        /// 从 Node.Visibility、Node.VisitState、Node.IsReachable 读取状态并应用视觉效果。
        /// </summary>
        public void ApplyVisualState()
        {
            if (_visitedIndicator != null)
                _visitedIndicator.style.display = DisplayStyle.None;

            // Reset border to default
            Root.style.borderTopWidth = 0f;
            Root.style.borderBottomWidth = 0f;
            Root.style.borderLeftWidth = 0f;
            Root.style.borderRightWidth = 0f;

            KillTweens();

            bool reachable = Node.IsReachable;
            bool visited = Node.VisitState == NodeVisitState.Visited;

            // 消耗性节点已消耗时：半透明 + 禁用点击 + 显示访问标记
            bool isConsumed = Node.IsConsumed;
            bool isConsumableType = Node.nodeType == RoguelikeNodeType.Mystery
                || Node.nodeType == RoguelikeNodeType.Treasure
                || Node.nodeType == RoguelikeNodeType.RestSite;

            if (isConsumed && isConsumableType)
            {
                _currentTintColor = new Color(_lockedColor.r, _lockedColor.g, _lockedColor.b, 1f);
                ApplyTint(_currentTintColor);
                ApplyIconOpacity(0.4f);
                Root.pickingMode = PickingMode.Ignore;
                KillTweens();

                if (visited && _visitedIndicator != null)
                    _visitedIndicator.style.display = DisplayStyle.Flex;

                UpdateDebugLabel();
                return;
            }

            switch (Node.Visibility)
            {
                case NodeVisibility.Hidden:
                    // 不可见：灰色问号外观，不可点击
                    _currentTintColor = new Color(_lockedColor.r, _lockedColor.g, _lockedColor.b, 1f);
                    ApplyTint(_currentTintColor);
                    ApplyIconOpacity(0.55f);
                    Root.pickingMode = PickingMode.Ignore;
                    break;

                case NodeVisibility.Fogged:
                    // 迷雾：半透明真实图标，不可点击
                    _currentTintColor = new Color(_lockedColor.r, _lockedColor.g, _lockedColor.b, 1f);
                    ApplyTint(_currentTintColor);
                    ApplyIconOpacity(0.7f);
                    Root.pickingMode = PickingMode.Ignore;
                    break;

                case NodeVisibility.Revealed:
                    if (reachable)
                    {
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
                    }
                    else
                    {
                        // 已揭示但不可达：真实图标，正常显示，不可点击
                        _currentTintColor = _visitedColor;
                        ApplyTint(_visitedColor);
                        ApplyIconOpacity(1f);
                        Root.pickingMode = PickingMode.Ignore;
                    }
                    break;
            }

            // 访问标记叠加：已访问节点显示访问指示器
            if (visited && _visitedIndicator != null)
                _visitedIndicator.style.display = DisplayStyle.Flex;

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
