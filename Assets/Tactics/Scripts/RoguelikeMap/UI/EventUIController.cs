using System.Collections.Generic;
using Tactics.RoguelikeMap.Events;
using Tactics.Runtime.Utilities;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tactics.RoguelikeMap.UI
{
    /// <summary>
    /// 事件UI控制器
    /// 负责显示事件界面和处理玩家选择
    /// </summary>
    public class EventUIController : MonoBehaviour
    {
        public static EventUIController Instance { get; private set; }

        [Header("UI Settings")]
        [SerializeField] private VisualTreeAsset eventPanelTemplate;
        [SerializeField] private StyleSheet eventPanelStyle;

        private VisualElement _root;
        private VisualElement _eventPanel;
        private Label _titleLabel;
        private Label _descriptionLabel;
        private VisualElement _optionsContainer;
        private VisualElement _resultContainer;
        private Label _resultLabel;
        private Button _continueButton;

        private RoguelikeEvent _currentEvent;
        private System.Action<bool> _onComplete;

        private void Awake()
        {
            Instance = this;
        }

        /// <summary>
        /// 显示事件UI
        /// </summary>
        public void ShowEvent(RoguelikeEvent evt, System.Action<bool> onComplete)
        {
            if (evt == null)
            {
                TLog.Warning("[EventUIController] 事件为空");
                onComplete?.Invoke(false);
                return;
            }

            _currentEvent = evt;
            _onComplete = onComplete;

            // 创建UI
            CreateEventPanel();

            // 显示事件
            DisplayEvent(evt);

            TLog.Info($"[EventUIController] 显示事件: {evt.title}");
        }

        /// <summary>
        /// 创建事件面板
        /// </summary>
        private void CreateEventPanel()
        {
            // 清除现有面板
            if (_eventPanel != null)
            {
                _eventPanel.RemoveFromHierarchy();
            }

            // 创建根容器
            _root = new VisualElement();
            _root.style.position = Position.Absolute;
            _root.style.left = 0;
            _root.style.top = 0;
            _root.style.right = 0;
            _root.style.bottom = 0;
            _root.style.backgroundColor = new Color(0, 0, 0, 0.7f);

            // 创建事件面板
            _eventPanel = new VisualElement();
            _eventPanel.style.width = 600;
            _eventPanel.style.height = 500;
            _eventPanel.style.alignSelf = Align.Center;
            _eventPanel.style.justifyContent = Justify.Center;
            _eventPanel.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);
            _eventPanel.style.borderTopLeftRadius = 10;
            _eventPanel.style.borderTopRightRadius = 10;
            _eventPanel.style.borderBottomLeftRadius = 10;
            _eventPanel.style.borderBottomRightRadius = 10;
            _eventPanel.style.paddingTop = 20;
            _eventPanel.style.paddingBottom = 20;
            _eventPanel.style.paddingLeft = 20;
            _eventPanel.style.paddingRight = 20;

            // 标题
            _titleLabel = new Label();
            _titleLabel.style.fontSize = 24;
            _titleLabel.style.color = Color.white;
            _titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _titleLabel.style.marginBottom = 10;
            _eventPanel.Add(_titleLabel);

            // 描述
            _descriptionLabel = new Label();
            _descriptionLabel.style.fontSize = 14;
            _descriptionLabel.style.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            _descriptionLabel.style.whiteSpace = WhiteSpace.Normal;
            _descriptionLabel.style.marginBottom = 20;
            _eventPanel.Add(_descriptionLabel);

            // 选项容器
            _optionsContainer = new VisualElement();
            _optionsContainer.style.marginBottom = 20;
            _eventPanel.Add(_optionsContainer);

            // 结果容器（初始隐藏）
            _resultContainer = new VisualElement();
            _resultContainer.style.display = DisplayStyle.None;
            _eventPanel.Add(_resultContainer);

            _resultLabel = new Label();
            _resultLabel.style.fontSize = 16;
            _resultLabel.style.color = Color.white;
            _resultLabel.style.whiteSpace = WhiteSpace.Normal;
            _resultLabel.style.marginBottom = 20;
            _resultContainer.Add(_resultLabel);

            // 继续按钮
            _continueButton = new Button();
            _continueButton.text = "继续";
            _continueButton.style.fontSize = 18;
            _continueButton.style.height = 40;
            _continueButton.style.display = DisplayStyle.None;
            _continueButton.clicked += OnContinueClicked;
            _eventPanel.Add(_continueButton);

            _root.Add(_eventPanel);

            // 添加到UIManager
            // TODO: 需要集成到UIManager系统
        }

        /// <summary>
        /// 显示事件内容
        /// </summary>
        private void DisplayEvent(RoguelikeEvent evt)
        {
            _titleLabel.text = evt.title;
            _descriptionLabel.text = evt.description;

            // 清除现有选项
            _optionsContainer.Clear();

            // 创建选项按钮
            for (int i = 0; i < evt.options.Count; i++)
            {
                var option = evt.options[i];
                var button = CreateOptionButton(option, i);
                _optionsContainer.Add(button);
            }
        }

        /// <summary>
        /// 创建选项按钮
        /// </summary>
        private Button CreateOptionButton(EventOption option, int index)
        {
            var button = new Button();
            button.style.height = 60;
            button.style.marginBottom = 10;
            button.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 1f);
            button.style.borderTopLeftRadius = 5;
            button.style.borderTopRightRadius = 5;
            button.style.borderBottomLeftRadius = 5;
            button.style.borderBottomRightRadius = 5;

            // 选项文本
            var textLabel = new Label(option.text);
            textLabel.style.fontSize = 16;
            textLabel.style.color = Color.white;
            textLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            button.Add(textLabel);

            // 属性和成功率
            if (option.attribute != AttributeType.None)
            {
                var infoLabel = new Label($"{option.GetAttributeName()} | 成功率: {option.baseSuccessRate}%");
                infoLabel.style.fontSize = 12;
                infoLabel.style.color = AttributeCheckSystem.GetSuccessRateColor(option.baseSuccessRate);
                infoLabel.style.unityTextAlign = TextAnchor.MiddleRight;
                button.Add(infoLabel);
            }
            else
            {
                var infoLabel = new Label("自动成功");
                infoLabel.style.fontSize = 12;
                infoLabel.style.color = Color.green;
                infoLabel.style.unityTextAlign = TextAnchor.MiddleRight;
                button.Add(infoLabel);
            }

            button.clicked += () => OnOptionSelected(index);

            return button;
        }

        /// <summary>
        /// 选项被选中
        /// </summary>
        private void OnOptionSelected(int index)
        {
            if (_currentEvent == null || index >= _currentEvent.options.Count)
                return;

            var option = _currentEvent.options[index];

            // TODO: 获取角色属性值
            int attributeValue = 10; // 临时使用默认值

            // 执行判定
            bool success = option.Execute(attributeValue);

            // 显示结果
            ShowResult(option, success);
        }

        /// <summary>
        /// 显示结果
        /// </summary>
        private void ShowResult(EventOption option, bool success)
        {
            // 隐藏选项
            _optionsContainer.style.display = DisplayStyle.None;

            // 显示结果
            _resultContainer.style.display = DisplayStyle.Flex;

            var result = success ? option.success : option.failure;
            if (result != null)
            {
                _resultLabel.text = result.description;
                _resultLabel.style.color = success ? Color.green : Color.red;
            }

            // 显示继续按钮
            _continueButton.style.display = DisplayStyle.Flex;
        }

        /// <summary>
        /// 继续按钮被点击
        /// </summary>
        private void OnContinueClicked()
        {
            // 关闭事件面板
            if (_root != null)
            {
                _root.RemoveFromHierarchy();
            }

            // 回调
            _onComplete?.Invoke(true);
            _onComplete = null;
            _currentEvent = null;
        }

        /// <summary>
        /// 关闭事件UI
        /// </summary>
        public void Close()
        {
            if (_root != null)
            {
                _root.RemoveFromHierarchy();
            }

            _onComplete?.Invoke(false);
            _onComplete = null;
            _currentEvent = null;
        }
    }
}
