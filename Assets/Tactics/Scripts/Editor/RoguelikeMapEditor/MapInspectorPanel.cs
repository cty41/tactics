using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Tactics.RoguelikeMap;
using UnityEngine;
using UnityEngine.UIElements;
using Tactics.Runtime.Utilities;

namespace Tactics.Editor.RoguelikeMapEditor
{
    /// <summary>
    /// 右侧属性编辑面板。显示并编辑选中地图节点的属性。
    /// </summary>
    public class MapInspectorPanel : VisualElement
    {
        private Label _placeholderLabel;
        private VisualElement _propContainer;
        private ScrollView _scrollView;
        private RoguelikeMapNode _currentNode;

        /// <summary>
        /// 当节点属性被编辑后触发。外部可订阅此事件刷新视图。
        /// </summary>
        public event Action OnNodeChanged;

        public MapInspectorPanel()
        {
            style.flexGrow = 1;
            style.minWidth = 200;
            style.paddingLeft = 8;
            style.paddingRight = 8;
            style.paddingTop = 4;
            style.backgroundColor = new Color(0.17f, 0.17f, 0.17f);

            var header = new Label("Properties")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 12,
                    paddingBottom = 4,
                    borderBottomWidth = 1,
                    borderBottomColor = new Color(0.3f, 0.3f, 0.3f)
                }
            };
            Add(header);

            _placeholderLabel = new Label("Select a node")
            {
                style =
                {
                    color = new Color(0.5f, 0.5f, 0.5f),
                    unityTextAlign = TextAnchor.MiddleCenter,
                    marginTop = 20
                }
            };
            Add(_placeholderLabel);

            _scrollView = new ScrollView();
            _propContainer = new VisualElement { style = { display = DisplayStyle.None } };
            _scrollView.Add(_propContainer);
            Add(_scrollView);
        }

        /// <summary>
        /// 显示指定节点的属性。传入 null 则清空面板。
        /// </summary>
        public void InspectNode(RoguelikeMapNode node)
        {
            if (node == null)
            {
                ClearProperties();
                return;
            }

            _currentNode = node;
            _placeholderLabel.style.display = DisplayStyle.None;
            _propContainer.style.display = DisplayStyle.Flex;
            _propContainer.Clear();

            // ── 基本信息 ──
            AddLabel("Node ID", node.nodeId);

            var nodeTypeNames = Enum.GetNames(typeof(RoguelikeNodeType));
            AddDropdown("Node Type", node.nodeType.ToString(), nodeTypeNames,
                FormatEnumName,
                v =>
                {
                    // nodeType 为 readonly 字段，此处回调供外部感知意图
                    OnNodeChanged?.Invoke();
                });

            // ── 位置 ──
            var posRow = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, marginBottom = 8 }
            };
            posRow.Add(CreateFloatField("X", node.position.x,
                v => { node.position = new Vector2(v, node.position.y); OnNodeChanged?.Invoke(); }));
            posRow.Add(CreateFloatField("Y", node.position.y,
                v => { node.position = new Vector2(node.position.x, v); OnNodeChanged?.Invoke(); }));
            _propContainer.Add(posRow);

            // ── Event ID（仅 Mystery 类型节点）──
            if (node.nodeType == RoguelikeNodeType.Mystery)
            {
                AddStringField("Event ID", node.eventId ?? "", v =>
                {
                    node.eventId = v;
                    TLog.Info($"[MapInspectorPanel] 节点 '{node.nodeId}' eventId 更新为: '{v}'");
                    OnNodeChanged?.Invoke();
                });
            }

            // ── 连接 ──
            BuildConnectionsSection(node);
        }

        /// <summary>
        /// 清空面板，恢复占位提示。
        /// </summary>
        public void ClearProperties()
        {
            _currentNode = null;
            _placeholderLabel.style.display = DisplayStyle.Flex;
            _propContainer.style.display = DisplayStyle.None;
            _propContainer.Clear();
        }

        // ── 连接管理 ──────────────────────────────────

        private void BuildConnectionsSection(RoguelikeMapNode node)
        {
            var sectionHeader = new Label("Outgoing Connections")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 12,
                    marginTop = 12,
                    paddingBottom = 4,
                    borderBottomWidth = 1,
                    borderBottomColor = new Color(0.3f, 0.3f, 0.3f)
                }
            };
            _propContainer.Add(sectionHeader);

            var connectionsContainer = new VisualElement();
            _propContainer.Add(connectionsContainer);
            RefreshConnections(connectionsContainer, node);

            // 添加连接：输入框 + 按钮
            var addRow = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, marginTop = 4 }
            };

            var idField = new TextField { style = { flexGrow = 1, fontSize = 11, height = 22 } };
            addRow.Add(idField);

            var addBtn = new Button(() =>
            {
                var targetId = idField.value?.Trim();
                if (string.IsNullOrEmpty(targetId)) return;
                node.AddOutgoing(targetId);
                idField.value = "";
                RefreshConnections(connectionsContainer, node);
                OnNodeChanged?.Invoke();
            })
            {
                text = "+",
                style = { width = 24, height = 22, fontSize = 14, marginLeft = 2 }
            };
            addRow.Add(addBtn);

            _propContainer.Add(addRow);
        }

        private void RefreshConnections(VisualElement container, RoguelikeMapNode node)
        {
            container.Clear();

            if (node.outgoing.Count == 0)
            {
                container.Add(new Label("(none)")
                {
                    style = { fontSize = 10, color = new Color(0.5f, 0.5f, 0.5f), marginBottom = 4 }
                });
                return;
            }

            for (int i = 0; i < node.outgoing.Count; i++)
            {
                var targetId = node.outgoing[i];
                var row = new VisualElement
                {
                    style = { flexDirection = FlexDirection.Row, marginBottom = 2 }
                };

                row.Add(new Label(targetId)
                {
                    style = { flexGrow = 1, fontSize = 11 }
                });

                var removeBtn = new Button(() =>
                {
                    node.RemoveOutgoing(targetId);
                    RefreshConnections(container, node);
                    OnNodeChanged?.Invoke();
                })
                {
                    text = "×",
                    style = { width = 24, height = 18, fontSize = 12, marginLeft = 2 }
                };
                row.Add(removeBtn);

                container.Add(row);
            }
        }

        // ── 辅助方法 ──────────────────────────────────

        private void AddLabel(string label, string value)
        {
            var row = new VisualElement { style = { marginBottom = 8 } };
            var lbl = new Label(label)
            {
                style = { fontSize = 10, color = new Color(0.6f, 0.6f, 0.6f), paddingBottom = 2 }
            };
            row.Add(lbl);

            var valLabel = new Label(value ?? "")
            {
                style = { fontSize = 11 }
            };
            row.Add(valLabel);
            _propContainer.Add(row);
        }

        private void AddDropdown(string label, string currentValue, string[] options,
            Func<string, string> formatFunc, Action<string> onChange)
        {
            var row = new VisualElement { style = { marginBottom = 8 } };
            var lbl = new Label(label)
            {
                style = { fontSize = 10, color = new Color(0.6f, 0.6f, 0.6f), paddingBottom = 2 }
            };
            row.Add(lbl);

            var optionsList = new List<string>(options);
            int defaultIdx = Array.IndexOf(options, currentValue);
            if (defaultIdx < 0) defaultIdx = 0;

            var dropdown = new PopupField<string>(optionsList, defaultIdx, formatFunc, formatFunc);
            dropdown.style.fontSize = 11;
            dropdown.style.height = 22;
            dropdown.RegisterValueChangedCallback(evt => onChange(evt.newValue));
            row.Add(dropdown);
            _propContainer.Add(row);
        }

        private VisualElement CreateFloatField(string label, float value, Action<float> onChange)
        {
            var container = new VisualElement { style = { marginRight = 8 } };
            container.Add(new Label(label)
            {
                style = { fontSize = 10, color = new Color(0.6f, 0.6f, 0.6f), paddingBottom = 2 }
            });

            var field = new FloatField { value = value };
            field.style.fontSize = 11;
            field.style.height = 22;
            field.style.minWidth = 60;
            field.RegisterValueChangedCallback(evt =>
            {
                var clamped = Mathf.Clamp(evt.newValue, -9999f, 9999f);
                onChange(clamped);
            });
            container.Add(field);
            return container;
        }

        private static string FormatEnumName(string name)
        {
            return Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");
        }

        private void AddStringField(string label, string value, Action<string> onChange)
        {
            var row = new VisualElement { style = { marginBottom = 8 } };
            var lbl = new Label(label)
            {
                style = { fontSize = 10, color = new Color(0.6f, 0.6f, 0.6f), paddingBottom = 2 }
            };
            row.Add(lbl);

            var field = new TextField { value = value };
            field.style.fontSize = 11;
            field.style.height = 22;
            field.RegisterValueChangedCallback(evt => onChange(evt.newValue));
            row.Add(field);
            _propContainer.Add(row);
        }
    }
}
