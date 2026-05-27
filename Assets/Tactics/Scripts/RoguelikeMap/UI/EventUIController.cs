using System.Collections.Generic;
using Tactics.RoguelikeMap.Events;
using Tactics.Runtime.Utilities;
using Tactics.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tactics.RoguelikeMap.UI
{
    /// <summary>
    /// 事件UI控制器
    /// 使用 UXML 模板构建 FTL 风格事件界面，支持 BG3 风格判定信息显示
    /// </summary>
    public class EventUIController : MonoBehaviour
    {
        public static EventUIController Instance { get; private set; }

        private VisualElement _overlay;
        private Label _titleLabel;
        private Label _descriptionLabel;
        private VisualElement _optionsContainer;
        private VisualElement _resultPanel;
        private Label _resultLabel;
        private Button _continueButton;

        private RoguelikeEvent _currentEvent;
        private System.Action<bool> _onComplete;
        private EventEffectContext _effectContext;

        // 判定上下文：角色名 → 属性值字典
        private string _adjudicatorName;
        private Dictionary<AttributeType, int> _attributeValues;

        private void Awake()
        {
            Instance = this;
        }

        /// <summary>
        /// 设置判定上下文（在 ShowEvent 前调用）
        /// </summary>
        /// <param name="characterName">判定角色名</param>
        /// <param name="attributeValues">角色属性值字典</param>
        public void SetAdjudicatorContext(string characterName, Dictionary<AttributeType, int> attributeValues)
        {
            _adjudicatorName = characterName;
            _attributeValues = attributeValues;
        }

        /// <summary>
        /// 显示事件UI
        /// </summary>
        private bool _lastExecutionSucceeded;

        public void ShowEvent(RoguelikeEvent evt, System.Action<bool> onComplete)
        {
            ShowEvent(evt, null, onComplete);
        }

        public async void ShowEvent(RoguelikeEvent evt, EventEffectContext effectContext, System.Action<bool> onComplete)
        {
            if (evt == null)
            {
                TLog.Warning("[EventUIController] 事件为空");
                onComplete?.Invoke(false);
                return;
            }

            _currentEvent = evt;
            _onComplete = onComplete;
            _effectContext = effectContext;
            _lastExecutionSucceeded = false;

            // 通过 UIManager 显示面板
            await UIManager.Instance.ShowAsync(UIManager.UIId.EventPanel);
            var root = UIManager.Instance.GetRootElement(UIManager.UIId.EventPanel);
            if (root == null)
            {
                TLog.Error("[EventUIController] 无法获取 EventPanel 根元素");
                onComplete?.Invoke(false);
                _onComplete = null;
                _currentEvent = null;
                return;
            }

            // 缓存元素引用
            _overlay = root.Q<VisualElement>("EventOverlay");
            _titleLabel = root.Q<Label>("EventTitle");
            _descriptionLabel = root.Q<Label>("EventDescription");
            _optionsContainer = root.Q<VisualElement>("OptionsContainer");
            _resultPanel = root.Q<VisualElement>("ResultPanel");
            _resultLabel = root.Q<Label>("ResultText");
            _continueButton = root.Q<Button>("ContinueButton");

            // 注册继续按钮事件
            _continueButton?.RegisterCallback<ClickEvent>(OnContinueClicked);

            // 显示事件内容
            DisplayEvent(evt);

            TLog.Info($"[EventUIController] 显示事件: {evt.title}");
        }

        /// <summary>
        /// 显示事件内容
        /// </summary>
        private void DisplayEvent(RoguelikeEvent evt)
        {
            if (_titleLabel != null)
                _titleLabel.text = evt.title;

            if (_descriptionLabel != null)
                _descriptionLabel.text = evt.description;

            // 确保选项可见，结果隐藏
            if (_optionsContainer != null)
                _optionsContainer.style.display = DisplayStyle.Flex;

            if (_resultPanel != null)
                _resultPanel.style.display = DisplayStyle.None;

            if (_continueButton != null)
                _continueButton.style.display = DisplayStyle.None;

            // 清除现有选项
            _optionsContainer?.Clear();

            // 创建选项
            for (int i = 0; i < evt.options.Count; i++)
            {
                var option = evt.options[i];
                var optionElement = CreateOptionElement(option, i);
                _optionsContainer?.Add(optionElement);
            }
        }

        /// <summary>
        /// 创建选项元素（BG3 风格：选项文本 + 成功率 + 判定信息）
        /// </summary>
        private VisualElement CreateOptionElement(EventOption option, int index)
        {
            // 选项容器
            var container = new VisualElement();
            container.AddToClassList("option-container");

            // 文本行（选项文本 + 成功率）
            var textRow = new VisualElement();
            textRow.AddToClassList("option-text-row");

            var textLabel = new Label(option.text);
            textLabel.AddToClassList("option-text");
            textRow.Add(textLabel);

            // 成功率标签
            var rateLabel = new Label();
            rateLabel.AddToClassList("option-success-rate");

            if (option.attribute == AttributeType.None)
            {
                // 自动成功
                rateLabel.text = "100%";
                rateLabel.AddToClassList("auto-success");
            }
            else
            {
                int attrValue = ResolveAttributeValue(option.attribute);
                int successRate = option.CalculateSuccessRate(attrValue);
                rateLabel.text = $"{successRate}%";

                // 根据成功率添加颜色类
                string colorClass = GetSuccessRateClass(successRate);
                rateLabel.AddToClassList(colorClass);
            }

            textRow.Add(rateLabel);
            container.Add(textRow);

            // BG3 风格判定信息
            var adjudicatorLabel = new Label();
            adjudicatorLabel.AddToClassList("adjudicator-info");

            if (option.attribute == AttributeType.None)
            {
                adjudicatorLabel.text = "(自动成功)";
            }
            else
            {
                int attrValue = ResolveAttributeValue(option.attribute);
                string charName = ResolveAdjudicatorName(option.attribute);
                string attrName = option.GetAttributeName();
                adjudicatorLabel.text = $"(由 {charName} 进行判定，{attrName} {attrValue})";
            }

            container.Add(adjudicatorLabel);

            // 点击事件
            int capturedIndex = index;
            container.RegisterCallback<ClickEvent>(evt => OnOptionSelected(capturedIndex));

            return container;
        }

        /// <summary>
        /// 获取属性值（从上下文或默认值）
        /// </summary>
        private int GetAttributeValue(AttributeType attribute)
        {
            if (_attributeValues != null && _attributeValues.TryGetValue(attribute, out int value))
                return value;

            return 10; // 默认值
        }

        /// <summary>
        /// 获取成功率对应的 USS 类名
        /// </summary>
        private static string GetSuccessRateClass(int successRate)
        {
            if (successRate >= 60)
                return "success-rate-high";
            if (successRate >= 40)
                return "success-rate-mid";
            return "success-rate-low";
        }

        /// <summary>
        /// 选项被选中
        /// </summary>
        private void OnOptionSelected(int index)
        {
            if (_currentEvent == null || index >= _currentEvent.options.Count)
                return;

            var option = _currentEvent.options[index];

            // 获取属性值并执行判定
            bool success = _effectContext != null
                ? option.Execute(_effectContext)
                : option.Execute(GetAttributeValue(option.attribute));

            // 显示结果
            ShowResult(option, success);
        }

        /// <summary>
        /// 显示结果
        /// </summary>
        private void ShowResult(EventOption option, bool success)
        {
            _lastExecutionSucceeded = success;
            // 隐藏选项
            if (_optionsContainer != null)
                _optionsContainer.style.display = DisplayStyle.None;

            // 显示结果面板
            if (_resultPanel != null)
                _resultPanel.style.display = DisplayStyle.Flex;

            var result = success ? option.success : option.failure;
            if (result != null && _resultLabel != null)
            {
                _resultLabel.text = result.description;

                // 清除旧样式类，添加结果样式
                _resultLabel.RemoveFromClassList("result-success");
                _resultLabel.RemoveFromClassList("result-failure");
                _resultLabel.AddToClassList(success ? "result-success" : "result-failure");
            }

            // 显示继续按钮
            if (_continueButton != null)
                _continueButton.style.display = DisplayStyle.Flex;
        }

        /// <summary>
        /// 继续按钮被点击
        /// </summary>
        private void OnContinueClicked(ClickEvent evt)
        {
            ClosePanel();
            _onComplete?.Invoke(_lastExecutionSucceeded);
            _onComplete = null;
            _currentEvent = null;
            _effectContext = null;
        }

        /// <summary>
        /// 关闭事件UI
        /// </summary>
        public void Close()
        {
            ClosePanel();
            _onComplete?.Invoke(false);
            _onComplete = null;
            _currentEvent = null;
            _effectContext = null;
        }

        /// <summary>
        /// 清除现有面板
        /// </summary>
        private void ClearExisting()
        {
            if (_continueButton != null)
                _continueButton.UnregisterCallback<ClickEvent>(OnContinueClicked);

            _overlay = null;
            _titleLabel = null;
            _descriptionLabel = null;
            _optionsContainer = null;
            _resultPanel = null;
            _resultLabel = null;
            _continueButton = null;
        }

        /// <summary>
        /// 关闭面板并清理
        /// </summary>
        private void ClosePanel()
        {
            ClearExisting();
            UIManager.Instance.Hide(UIManager.UIId.EventPanel);
        }

        private int ResolveAttributeValue(AttributeType attribute)
        {
            if (_effectContext != null)
            {
                var character = _effectContext.PickTarget(EventTargetType.All, attribute);
                return EventEffectContext.GetCharacterAttribute(character, attribute);
            }

            return GetAttributeValue(attribute);
        }

        private string ResolveAdjudicatorName(AttributeType attribute)
        {
            if (_effectContext != null)
            {
                var character = _effectContext.PickTarget(EventTargetType.All, attribute);
                return character?.DisplayName ?? "角色";
            }

            return string.IsNullOrEmpty(_adjudicatorName) ? "角色" : _adjudicatorName;
        }
    }
}
