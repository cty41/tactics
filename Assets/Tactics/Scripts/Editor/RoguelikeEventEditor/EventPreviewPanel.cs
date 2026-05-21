using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Tactics.Editor.RoguelikeEventEditor
{
    /// <summary>
    /// 底部实时预览面板。模拟游戏运行时的事件 UI 渲染。
    /// </summary>
    public class EventPreviewPanel : VisualElement
    {
        private Label _titleLabel;
        private Label _descLabel;
        private VisualElement _optionsRow;
        private Label _resultLabel;
        private Label _placeholderLabel;

        public EventPreviewPanel()
        {
            style.flexGrow = 1; style.minHeight = 120;
            style.paddingLeft = 12; style.paddingRight = 12;
            style.paddingTop = 8; style.paddingBottom = 8;
            style.backgroundColor = new UnityEngine.Color(0.10f, 0.10f, 0.12f);
            style.borderTopWidth = 2;
            style.borderTopColor = new UnityEngine.Color(0.25f, 0.25f, 0.25f);

            // 标题
            var headerRow = new VisualElement { style = { flexDirection = FlexDirection.Row, paddingBottom = 4 } };
            var icon = new Label("▶") { style = { color = new UnityEngine.Color(0.8f, 0.6f, 0.2f), fontSize = 14 } };
            var headerText = new Label("Live Preview") { style = { unityFontStyleAndWeight = UnityEngine.FontStyle.Bold, marginLeft = 4, fontSize = 11, color = new UnityEngine.Color(0.6f, 0.6f, 0.6f) } };
            headerRow.Add(icon); headerRow.Add(headerText);
            Add(headerRow);

            _titleLabel = new Label("No event selected") { style = { fontSize = 15, unityFontStyleAndWeight = UnityEngine.FontStyle.Bold, color = new UnityEngine.Color(0.9f, 0.9f, 0.9f), paddingBottom = 4 } };
            Add(_titleLabel);

            _descLabel = new Label("Select an event from the list...") { style = { fontSize = 12, color = new UnityEngine.Color(0.7f, 0.7f, 0.7f), paddingBottom = 8, whiteSpace = WhiteSpace.Normal } };
            Add(_descLabel);

            _optionsRow = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, paddingTop = 4 } };
            Add(_optionsRow);

            _resultLabel = new Label("") { style = { fontSize = 12, paddingTop = 8, color = new UnityEngine.Color(0.9f, 0.8f, 0.4f), display = DisplayStyle.None } };
            Add(_resultLabel);

            _placeholderLabel = new Label("") { style = { color = new UnityEngine.Color(0.4f, 0.4f, 0.4f), unityTextAlign = UnityEngine.TextAnchor.MiddleCenter, marginTop = 12 } };
            Add(_placeholderLabel);
        }

        public void UpdatePreview(SerializableEventData evt)
        {
            _placeholderLabel.style.display = DisplayStyle.None;
            _resultLabel.style.display = DisplayStyle.None;

            if (evt == null)
            {
_titleLabel.text = "No event selected";
                _descLabel.text = "Select an event from the list...";
                _optionsRow.Clear();
                return;
            }

            _titleLabel.text = evt.title ?? evt.eventId ?? "Unnamed Event";
            _descLabel.text = evt.description ?? "";

            _optionsRow.Clear();
            var options = evt.nodes.FindAll(n => n.type == EventNodeTypes.Option);
            foreach (var opt in options)
            {
                var btn = BuildOptionButton(opt);
                var capturedEvt = evt;
                btn.RegisterCallback<ClickEvent>(_ => ShowOptionResult(opt, capturedEvt));
                _optionsRow.Add(btn);
            }

            if (options.Count == 0)
            {
                _placeholderLabel.text = "(No options - add an Option node)";
                _placeholderLabel.style.display = DisplayStyle.Flex;
            }
        }

        private VisualElement BuildOptionButton(EventNodeData opt)
        {
            var container = new VisualElement
            {
                style =
                {
                    marginRight = 8, marginBottom = 6,
                    paddingTop = 6, paddingBottom = 6, paddingLeft = 12, paddingRight = 12,
                    backgroundColor = new UnityEngine.Color(0.18f, 0.18f, 0.22f),
                    borderTopLeftRadius = 6, borderTopRightRadius = 6,
                    borderBottomLeftRadius = 6, borderBottomRightRadius = 6,
                    borderTopWidth = 2,
                    borderTopColor = GetAttributeColor(opt.data?.attribute),
                    flexDirection = FlexDirection.Column,
                }
            };

            var textLabel = new Label(opt.data?.text ?? "选项")
            {
                style = { fontSize = 12, color = new UnityEngine.Color(0.9f, 0.9f, 0.9f), unityFontStyleAndWeight = UnityEngine.FontStyle.Bold }
            };
            container.Add(textLabel);

            if (!string.IsNullOrEmpty(opt.data?.attribute))
            {
                var attrLabel = new Label($"{GetAttributeDisplayName(opt.data.attribute)} {opt.data.successRate ?? 40}%")
                {
                    style = { fontSize = 10, color = GetAttributeColor(opt.data.attribute), paddingTop = 2 }
                };
                container.Add(attrLabel);
            }
            else
            {
                var autoLabel = new Label("Auto Success")
                {
                    style = { fontSize = 10, color = new UnityEngine.Color(0.3f, 0.7f, 0.3f), paddingTop = 2 }
                };
                container.Add(autoLabel);
            }

            return container;
        }

        private void ShowOptionResult(EventNodeData opt, SerializableEventData evt)
        {
            _resultLabel.style.display = DisplayStyle.Flex;
            var sb = new System.Text.StringBuilder();
            sb.Append($"→ Option \"{opt.data?.text ?? "?"}\" triggered...\n");

            // 显示所有 Result 节点的目标信息
            foreach (var node in evt.nodes)
            {
                if (node.type == EventNodeTypes.Success || node.type == EventNodeTypes.Failure)
                {
                    var targetText = GetTargetDisplayText(node.data.target);
                    sb.AppendLine($"  [{node.type}] {node.data.resultType}{targetText}");
                }
            }

            _resultLabel.text = sb.ToString().TrimEnd();
        }

        public void ClearPreview()
        {
            _titleLabel.text = "No event selected";
            _descLabel.text = "";
            _optionsRow.Clear();
            _resultLabel.style.display = DisplayStyle.None;
            _placeholderLabel.style.display = DisplayStyle.Flex;
            _placeholderLabel.text = "Select an event to begin editing";
        }

        private static UnityEngine.Color GetAttributeColor(string attr) => attr switch
        {
            EventAttributes.Strength => new UnityEngine.Color(0.9f, 0.4f, 0.3f),
            EventAttributes.Dexterity => new UnityEngine.Color(0.3f, 0.8f, 0.4f),
            EventAttributes.Constitution => new UnityEngine.Color(0.9f, 0.7f, 0.3f),
            EventAttributes.Intelligence => new UnityEngine.Color(0.3f, 0.5f, 0.9f),
            EventAttributes.Charisma => new UnityEngine.Color(0.8f, 0.4f, 0.9f),
            _ => new UnityEngine.Color(0.6f, 0.6f, 0.6f)
        };

        private static string GetTargetDisplayText(string target) => target switch
        {
            EventTargetTypes.Self => "(仅影响自己)",
            EventTargetTypes.RandomAlly => "(随机影响一名队友)",
            EventTargetTypes.All => "(影响全队)",
            _ => ""
        };

        private static string GetAttributeDisplayName(string attr) => attr switch
        {
            EventAttributes.Strength => "力量",
            EventAttributes.Dexterity => "敏捷",
            EventAttributes.Constitution => "体质",
            EventAttributes.Intelligence => "智力",
            EventAttributes.Charisma => "魅力",
            _ => attr
        };
    }
}
