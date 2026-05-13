using System;
using UnityEngine.UIElements;

namespace Tactics.Editor.RoguelikeEventEditor
{
    /// <summary>
    /// 右侧属性编辑面板。根据选中节点动态切换字段。
    /// </summary>
    public class EventInspectorPanel : VisualElement
    {
        private Label _placeholderLabel;
        private VisualElement _propContainer;
        private ScrollView _scrollView;
        private EventNodeElement _currentNode;
        private Action _onValueChanged;

        public EventInspectorPanel()
        {
            style.flexGrow = 1; style.minWidth = 200;
            style.paddingLeft = 8; style.paddingRight = 8; style.paddingTop = 4;
            style.backgroundColor = new UnityEngine.Color(0.17f, 0.17f, 0.17f);

            var header = new Label("Properties")
            {
                style = { unityFontStyleAndWeight = UnityEngine.FontStyle.Bold, fontSize = 12, paddingBottom = 4, borderBottomWidth = 1, borderBottomColor = new UnityEngine.Color(0.3f, 0.3f, 0.3f) }
            };
            Add(header);

            _placeholderLabel = new Label("Select a node")
            {
                style = { color = new UnityEngine.Color(0.5f, 0.5f, 0.5f), unityTextAlign = UnityEngine.TextAnchor.MiddleCenter, marginTop = 20 }
            };
            Add(_placeholderLabel);

            _scrollView = new ScrollView();
            _propContainer = new VisualElement { style = { display = DisplayStyle.None } };
            _scrollView.Add(_propContainer);
            Add(_scrollView);
        }

        public void InspectNode(EventNodeElement node, Action onChanged = null)
        {
            if (node == null)
            {
                ClearProperties();
                return;
            }

            _currentNode = node;
            _onValueChanged = onChanged;
            _placeholderLabel.style.display = DisplayStyle.None;
            _propContainer.style.display = DisplayStyle.Flex;
            _propContainer.Clear();

            AddField("Node Type", node.NodeType, v => { });
            AddField("Node ID", node.NodeId, v => node.NodeId = v);

            var data = node.Data;
            switch (node.NodeType)
            {
                case EventNodeTypes.Start:
                    AddField("Event ID", data.eventId, v => { data.eventId = v; node.UpdateLabels(); Changed(); });
                    AddField("Title", data.title, v => { data.title = v; node.UpdateLabels(); Changed(); });
                    AddField("Description", data.description, v => { data.description = v; Changed(); }, multiline: true);
                    AddDropdown("Region", data.region ?? EventRegions.DarkForest, EventRegions.All, EventRegions.DisplayNames,
                        v => { data.region = v; node.UpdateLabels(); Changed(); });
                    break;

                case EventNodeTypes.Option:
                    AddField("Option Text", data.text, v => { data.text = v; node.UpdateLabels(); Changed(); }, multiline: true);
                    AddDropdown("Attribute", data.attribute ?? EventAttributes.Strength, EventAttributes.All, EventAttributes.DisplayNames,
                        v => { data.attribute = v; node.UpdateLabels(); Changed(); });
                    AddIntField("Success Rate%", data.successRate ?? 40, 0, 100, v => { data.successRate = v; Changed(); });
                    break;

                case EventNodeTypes.Check:
                    AddIntField("Diff. Modifier", data.difficultyModifier ?? 0, -20, 20, v => { data.difficultyModifier = v; Changed(); });
                    break;

                case EventNodeTypes.Success:
                case EventNodeTypes.Failure:
                    AddDropdown("Result Type", data.resultType ?? EventResultTypes.Gold, EventResultTypes.All,
                        EventResultTypes.All,
                        v => { data.resultType = v; node.UpdateLabels(); Changed(); });
                    AddIntField("Amount", data.amount ?? 0, -100, 100, v => { data.amount = v; node.UpdateLabels(); Changed(); });
                    AddField("Result Text", data.resultText, v => { data.resultText = v; node.UpdateLabels(); Changed(); }, multiline: true);

                    if (data.resultType == EventResultTypes.Damage || data.resultType == EventResultTypes.DamageAll)
                        AddField("Target", data.target ?? "self", v => data.target = v);
                    if (data.resultType == EventResultTypes.Item)
                        AddField("Item ID", data.itemId, v => data.itemId = v);
                    if (data.resultType == EventResultTypes.Equip)
                        AddField("Equip ID", data.equipId, v => data.equipId = v);
                    if (data.resultType == EventResultTypes.Buff)
                        AddField("Buff ID", data.buffId, v => data.buffId = v);
                    if (data.resultType == EventResultTypes.Battle)
                        AddField("Enemy Group ID", data.enemyGroupId, v => data.enemyGroupId = v);
                    break;

                case EventNodeTypes.End:
                    AddField("Summary Text", data.summaryText, v => { data.summaryText = v; node.UpdateLabels(); Changed(); }, multiline: true);
                    break;
            }
        }

        public void ClearProperties()
        {
            _currentNode = null;
            _placeholderLabel.style.display = DisplayStyle.Flex;
            _propContainer.style.display = DisplayStyle.None;
            _propContainer.Clear();
        }

        // ── 辅助 ──────────────────────────────────
        private void AddField(string label, string value, Action<string> onChange, bool multiline = false)
        {
            var row = new VisualElement { style = { marginBottom = 8 } };
            var lbl = new Label(label) { style = { fontSize = 10, color = new UnityEngine.Color(0.6f, 0.6f, 0.6f), paddingBottom = 2 } };
            row.Add(lbl);

            TextField field;
            if (multiline)
            {
                field = new TextField { value = value ?? "", multiline = true };
                field.style.height = 60;
            }
            else
            {
                field = new TextField { value = value ?? "" };
                field.style.height = 22;
            }
            field.style.fontSize = 11;
            field.RegisterValueChangedCallback(evt => onChange(evt.newValue));
            row.Add(field);
            _propContainer.Add(row);
        }

        private void AddIntField(string label, int value, int min, int max, Action<int> onChange)
        {
            var row = new VisualElement { style = { marginBottom = 8 } };
            var lbl = new Label(label) { style = { fontSize = 10, color = new UnityEngine.Color(0.6f, 0.6f, 0.6f), paddingBottom = 2 } };
            row.Add(lbl);

            var field = new IntegerField { value = value };
            field.style.fontSize = 11; field.style.height = 22;
            field.RegisterValueChangedCallback(evt => onChange(Math.Clamp(evt.newValue, min, max)));
            row.Add(field);
            _propContainer.Add(row);
        }

        private void AddDropdown(string label, string currentValue, string[] options, string[] displayNames, Action<string> onChange)
        {
            var row = new VisualElement { style = { marginBottom = 8 } };
            var lbl = new Label(label) { style = { fontSize = 10, color = new UnityEngine.Color(0.6f, 0.6f, 0.6f), paddingBottom = 2 } };
            row.Add(lbl);

            int defaultIdx = System.Array.IndexOf(options, currentValue);
            if (defaultIdx < 0) defaultIdx = 0;

            var dropdown = new PopupField<string>(
                new System.Collections.Generic.List<string>(options),
                defaultIdx,
                v => FormatDropdownValue(v, options, displayNames),
                v => FormatDropdownValue(v, options, displayNames));

            dropdown.style.fontSize = 11;
            dropdown.style.height = 22;

            dropdown.RegisterValueChangedCallback(evt => onChange(evt.newValue));
            row.Add(dropdown);
            _propContainer.Add(row);
        }

        private void Changed()
        {
            _onValueChanged?.Invoke();
        }

        private static string FormatDropdownValue(string value, string[] options, string[] displayNames)
        {
            if (displayNames == null) return value;
            int i = System.Array.IndexOf(options, value);
            return i >= 0 && i < displayNames.Length ? displayNames[i] : value;
        }
    }
}
