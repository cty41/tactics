using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Tactics.RoguelikeMap;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Tactics.Runtime.Utilities;

namespace Tactics.Editor.RoguelikeMapEditor
{
    /// <summary>
    /// 右侧属性编辑面板。显示并编辑选中地图节点的属性。
    /// 绑定到 MapEditorDocument 后，所有编辑操作直接修改文档模型。
    /// </summary>
    public class MapInspectorPanel : VisualElement
    {
        private Label _placeholderLabel;
        private VisualElement _propContainer;
        private ScrollView _scrollView;
        private EditableMapNodeData _currentEditableNode;
        private RoguelikeMapNode _currentNode; // 向后兼容
        private MapEditorDocument _document;

        /// <summary>
        /// 当节点属性被编辑后触发。外部可订阅此事件刷新视图。
        /// </summary>
        public event Action OnNodeChanged;

        /// <summary>
        /// 当前正在编辑的可编辑节点（文档模型路径）。只读。
        /// </summary>
        public EditableMapNodeData CurrentEditableNode => _currentEditableNode;

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
        /// 绑定文档模型。
        /// </summary>
        public void SetDocument(MapEditorDocument doc)
        {
            _document = doc;
        }

        /// <summary>
        /// 获取当前绑定的文档模型。
        /// </summary>
        public MapEditorDocument GetDocument() => _document;

        /// <summary>
        /// 显示指定可编辑节点的属性。传入 null 则清空面板。
        /// 这是文档模型架构下的主要入口。
        /// </summary>
        public void InspectEditableNode(EditableMapNodeData node)
        {
            if (node == null)
            {
                ClearProperties();
                return;
            }

            _currentEditableNode = node;
            _currentNode = null; // 清除旧引用
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
                    var newType = Enum.Parse<RoguelikeNodeType>(v, true);
                    if (newType == node.nodeType) return;
                    node.nodeType = newType;
                    TLog.Info($"[MapInspectorPanel] 节点 '{node.nodeId}' 类型更新为: {newType}");
                    OnNodeChanged?.Invoke();
                    // 重新构建属性面板（Treasure/Store 区域依赖节点类型）
                    InspectEditableNode(node);
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

            // ── 宝藏节点配置（仅 Treasure 类型）──
            if (node.nodeType == RoguelikeNodeType.Treasure)
            {
                var treasureHeader = new Label("宝藏奖励配置")
                {
                    style =
                    {
                        unityFontStyleAndWeight = FontStyle.Bold,
                        fontSize = 12,
                        marginTop = 8,
                        paddingBottom = 4,
                        borderBottomWidth = 1,
                        borderBottomColor = new Color(0.3f, 0.3f, 0.3f)
                    }
                };
                _propContainer.Add(treasureHeader);

                var goldRow = new VisualElement
                {
                    style = { flexDirection = FlexDirection.Row, marginBottom = 8 }
                };
                goldRow.Add(CreateIntField("金币下限", node.goldMin ?? 2,
                    v => { node.goldMin = v; OnNodeChanged?.Invoke(); }));
                goldRow.Add(CreateIntField("金币上限", node.goldMax ?? 5,
                    v => { node.goldMax = v; OnNodeChanged?.Invoke(); }));
                _propContainer.Add(goldRow);

                // Equipment 列表
                BuildEquipmentSection(node);

                // Buff 列表
                BuildBuffSection(node);
            }

            // ── 商店节点配置（仅 Store 类型）──
            if (node.nodeType == RoguelikeNodeType.Store)
            {
                var storeHeader = new Label("商店配置")
                {
                    style =
                    {
                        unityFontStyleAndWeight = FontStyle.Bold,
                        fontSize = 12,
                        marginTop = 8,
                        paddingBottom = 4,
                        borderBottomWidth = 1,
                        borderBottomColor = new Color(0.3f, 0.3f, 0.3f)
                    }
                };
                _propContainer.Add(storeHeader);

                if (node.storeGoods.Count == 0)
                {
                    node.storeGoods.Add(new StoreGoodEntry());
                }

                for (int i = 0; i < node.storeGoods.Count; i++)
                {
                    int currentIndex = i;
                    var good = node.storeGoods[i];

                    var goodRow = new VisualElement
                    {
                        style = { flexDirection = FlexDirection.Row, marginBottom = 6, alignItems = Align.FlexEnd }
                    };
                    goodRow.Add(CreateStoreGoodKindField("类型", good.ResolvedKind,
                        v => { good.SetContent(v, good.ResolvedContentId); OnNodeChanged?.Invoke(); }));
                    goodRow.Add(CreateStringField("内容ID", good.ResolvedContentId ?? string.Empty,
                        v => { good.SetContent(good.ResolvedKind, v); OnNodeChanged?.Invoke(); }));
                    goodRow.Add(CreateIntField("价格", good.price,
                        v => { good.price = v; OnNodeChanged?.Invoke(); }));

                    var removeBtn = new Button(() =>
                    {
                        node.storeGoods.RemoveAt(currentIndex);
                        InspectEditableNode(node);
                        OnNodeChanged?.Invoke();
                    })
                    {
                        text = "-",
                        style = { width = 24, height = 22, marginBottom = 2 }
                    };
                    goodRow.Add(removeBtn);
                    _propContainer.Add(goodRow);
                }

                var addGoodBtn = new Button(() =>
                {
                    node.storeGoods.Add(new StoreGoodEntry());
                    InspectEditableNode(node);
                    OnNodeChanged?.Invoke();
                })
                {
                    text = "+ Good",
                    style = { height = 22, marginBottom = 8 }
                };
                _propContainer.Add(addGoodBtn);
            }

            // ── 连接 ──
            BuildConnectionsSectionFromEditable(node);
        }

        /// <summary>
        /// 构建 Equipment 编辑区域（文档模型版本）。
        /// </summary>
        private void BuildEquipmentSection(EditableMapNodeData node)
        {
            var equipHeader = new Label("Equipment Entries")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 11,
                    marginTop = 8,
                    paddingBottom = 4
                }
            };
            _propContainer.Add(equipHeader);

            for (int i = 0; i < node.equipmentEntries.Count; i++)
            {
                int currentIndex = i;
                var entry = node.equipmentEntries[i];

                var row = new VisualElement
                {
                    style = { flexDirection = FlexDirection.Row, marginBottom = 4, alignItems = Align.FlexEnd }
                };
                row.Add(CreateStringField("ID", entry.equipmentId ?? string.Empty,
                    v => { entry.equipmentId = v; OnNodeChanged?.Invoke(); }));
                row.Add(CreateFloatField("W", entry.weight,
                    v => { entry.weight = v; OnNodeChanged?.Invoke(); }));

                var removeBtn = new Button(() =>
                {
                    node.equipmentEntries.RemoveAt(currentIndex);
                    InspectEditableNode(node);
                    OnNodeChanged?.Invoke();
                })
                {
                    text = "-",
                    style = { width = 24, height = 22, marginBottom = 2 }
                };
                row.Add(removeBtn);
                _propContainer.Add(row);
            }

            var addBtn = new Button(() =>
            {
                node.equipmentEntries.Add(new EquipmentEntry());
                InspectEditableNode(node);
                OnNodeChanged?.Invoke();
            })
            {
                text = "+ Equipment",
                style = { height = 22, marginBottom = 8 }
            };
            _propContainer.Add(addBtn);
        }

        /// <summary>
        /// 构建 Buff 编辑区域（文档模型版本）。
        /// </summary>
        private void BuildBuffSection(EditableMapNodeData node)
        {
            var buffHeader = new Label("Buff Entries")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 11,
                    marginTop = 8,
                    paddingBottom = 4
                }
            };
            _propContainer.Add(buffHeader);

            for (int i = 0; i < node.buffEntries.Count; i++)
            {
                int currentIndex = i;
                var entry = node.buffEntries[i];

                var row = new VisualElement
                {
                    style = { flexDirection = FlexDirection.Row, marginBottom = 4, alignItems = Align.FlexEnd }
                };

                // ObjectField for BuffConfig ScriptableObject
                var objField = new ObjectField
                {
                    objectType = typeof(Tactics.Common.Units.Buffs.BuffConfig),
                    value = entry.buffConfig,
                    style = { flexGrow = 1, marginRight = 4 }
                };
                objField.RegisterValueChangedCallback(evt =>
                {
                    entry.buffConfig = evt.newValue as Tactics.Common.Units.Buffs.BuffConfig;
                    OnNodeChanged?.Invoke();
                });
                row.Add(objField);

                row.Add(CreateFloatField("W", entry.weight,
                    v => { entry.weight = v; OnNodeChanged?.Invoke(); }));

                var removeBtn = new Button(() =>
                {
                    node.buffEntries.RemoveAt(currentIndex);
                    InspectEditableNode(node);
                    OnNodeChanged?.Invoke();
                })
                {
                    text = "-",
                    style = { width = 24, height = 22, marginBottom = 2 }
                };
                row.Add(removeBtn);
                _propContainer.Add(row);
            }

            var addBtn = new Button(() =>
            {
                node.buffEntries.Add(new BuffConfigEntry());
                InspectEditableNode(node);
                OnNodeChanged?.Invoke();
            })
            {
                text = "+ Buff",
                style = { height = 22, marginBottom = 8 }
            };
            _propContainer.Add(addBtn);
        }

        /// <summary>
        /// 构建连接编辑区域（文档模型版本）。
        /// </summary>
        private void BuildConnectionsSectionFromEditable(EditableMapNodeData node)
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
            RefreshConnectionsFromEditable(connectionsContainer, node);

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
                RefreshConnectionsFromEditable(connectionsContainer, node);
                OnNodeChanged?.Invoke();
            })
            {
                text = "+",
                style = { width = 24, height = 22, fontSize = 14, marginLeft = 2 }
            };
            addRow.Add(addBtn);

            _propContainer.Add(addRow);
        }

        private void RefreshConnectionsFromEditable(VisualElement container, EditableMapNodeData node)
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
                    RefreshConnectionsFromEditable(container, node);
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

        /// <summary>
        /// 显示指定节点的属性。传入 null 则清空面板。
        /// 如果文档模型可用，优先使用 InspectEditableNode。
        /// </summary>
        public void InspectNode(RoguelikeMapNode node)
        {
            if (node == null)
            {
                ClearProperties();
                return;
            }

            // 如果文档模型可用，从文档中查找对应的可编辑节点
            if (_document != null)
            {
                var editableNode = _document.GetNode(node.nodeId);
                if (editableNode != null)
                {
                    InspectEditableNode(editableNode);
                    return;
                }
            }

            // Fallback: 使用旧的 RoguelikeMapNode 直接编辑
            _currentNode = node;
            _currentEditableNode = null;
            _placeholderLabel.style.display = DisplayStyle.None;
            _propContainer.style.display = DisplayStyle.Flex;
            _propContainer.Clear();

            // ── 基本信息 ──
            AddLabel("Node ID", node.nodeId);

            // nodeType 在 RoguelikeMapNode 中是 readonly，fallback 模式下只读显示
            AddLabel("Node Type", FormatEnumName(node.nodeType.ToString()));

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

            // ── 宝藏节点配置（仅 Treasure 类型）──
            if (node.nodeType == RoguelikeNodeType.Treasure)
            {
                var config = node.treasureConfig;
                if (config == null)
                {
                    config = new TreasureNodeConfig();
                    node.treasureConfig = config;
                }

                var treasureHeader = new Label("宝藏奖励配置")
                {
                    style =
                    {
                        unityFontStyleAndWeight = FontStyle.Bold,
                        fontSize = 12,
                        marginTop = 8,
                        paddingBottom = 4,
                        borderBottomWidth = 1,
                        borderBottomColor = new Color(0.3f, 0.3f, 0.3f)
                    }
                };
                _propContainer.Add(treasureHeader);

                var goldRow = new VisualElement
                {
                    style = { flexDirection = FlexDirection.Row, marginBottom = 8 }
                };
                goldRow.Add(CreateIntField("金币下限", config.goldMin,
                    v => { config.goldMin = v; OnNodeChanged?.Invoke(); }));
                goldRow.Add(CreateIntField("金币上限", config.goldMax,
                    v => { config.goldMax = v; OnNodeChanged?.Invoke(); }));
                _propContainer.Add(goldRow);

                // Equipment 列表（fallback 路径）
                BuildEquipmentSectionFallback(config);

                // Buff 列表（fallback 路径）
                BuildBuffSectionFallback(config);
            }

            // ── 商店节点配置（仅 Store 类型，占位）──
            if (node.nodeType == RoguelikeNodeType.Store)
            {
                var config = node.storeConfig;
                if (config == null)
                {
                    config = new StoreNodeConfig();
                    node.storeConfig = config;
                }

                var storeHeader = new Label("商店配置")
                {
                    style =
                    {
                        unityFontStyleAndWeight = FontStyle.Bold,
                        fontSize = 12,
                        marginTop = 8,
                        paddingBottom = 4,
                        borderBottomWidth = 1,
                        borderBottomColor = new Color(0.3f, 0.3f, 0.3f)
                    }
                };
                _propContainer.Add(storeHeader);

                if (config.goods.Count == 0)
                {
                    config.goods.Add(new StoreGoodEntry());
                }

                for (int i = 0; i < config.goods.Count; i++)
                {
                    int currentIndex = i;
                    var good = config.goods[i];

                    var goodRow = new VisualElement
                    {
                        style = { flexDirection = FlexDirection.Row, marginBottom = 6, alignItems = Align.FlexEnd }
                    };
                    goodRow.Add(CreateStoreGoodKindField("类型", good.ResolvedKind,
                        v => { good.SetContent(v, good.ResolvedContentId); OnNodeChanged?.Invoke(); }));
                    goodRow.Add(CreateStringField("内容ID", good.ResolvedContentId ?? string.Empty,
                        v => { good.SetContent(good.ResolvedKind, v); OnNodeChanged?.Invoke(); }));
                    goodRow.Add(CreateIntField("价格", good.price,
                        v => { good.price = v; OnNodeChanged?.Invoke(); }));

                    var removeBtn = new Button(() =>
                    {
                        config.goods.RemoveAt(currentIndex);
                        InspectNode(node);
                        OnNodeChanged?.Invoke();
                    })
                    {
                        text = "-",
                        style = { width = 24, height = 22, marginBottom = 2 }
                    };
                    goodRow.Add(removeBtn);
                    _propContainer.Add(goodRow);
                }

                var addGoodBtn = new Button(() =>
                {
                    config.goods.Add(new StoreGoodEntry());
                    InspectNode(node);
                    OnNodeChanged?.Invoke();
                })
                {
                    text = "+ Good",
                    style = { height = 22, marginBottom = 8 }
                };
                _propContainer.Add(addGoodBtn);
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
            _currentEditableNode = null;
            _placeholderLabel.style.display = DisplayStyle.Flex;
            _propContainer.style.display = DisplayStyle.None;
            _propContainer.Clear();
        }

        // ── 连接管理 ──────────────────────────────────

        /// <summary>
        /// 构建 Equipment 编辑区域（Runtime fallback 路径）。
        /// </summary>
        private void BuildEquipmentSectionFallback(TreasureNodeConfig config)
        {
            var equipHeader = new Label("Equipment Entries")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 11,
                    marginTop = 8,
                    paddingBottom = 4
                }
            };
            _propContainer.Add(equipHeader);

            for (int i = 0; i < config.equipmentEntries.Count; i++)
            {
                int currentIndex = i;
                var entry = config.equipmentEntries[i];

                var row = new VisualElement
                {
                    style = { flexDirection = FlexDirection.Row, marginBottom = 4, alignItems = Align.FlexEnd }
                };
                row.Add(CreateStringField("ID", entry.equipmentId ?? string.Empty,
                    v => { entry.equipmentId = v; OnNodeChanged?.Invoke(); }));
                row.Add(CreateFloatField("W", entry.weight,
                    v => { entry.weight = v; OnNodeChanged?.Invoke(); }));

                var removeBtn = new Button(() =>
                {
                    config.equipmentEntries.RemoveAt(currentIndex);
                    InspectNode(_currentNode);
                    OnNodeChanged?.Invoke();
                })
                {
                    text = "-",
                    style = { width = 24, height = 22, marginBottom = 2 }
                };
                row.Add(removeBtn);
                _propContainer.Add(row);
            }

            var addBtn = new Button(() =>
            {
                config.equipmentEntries.Add(new EquipmentEntry());
                InspectNode(_currentNode);
                OnNodeChanged?.Invoke();
            })
            {
                text = "+ Equipment",
                style = { height = 22, marginBottom = 8 }
            };
            _propContainer.Add(addBtn);
        }

        /// <summary>
        /// 构建 Buff 编辑区域（Runtime fallback 路径）。
        /// </summary>
        private void BuildBuffSectionFallback(TreasureNodeConfig config)
        {
            var buffHeader = new Label("Buff Entries")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 11,
                    marginTop = 8,
                    paddingBottom = 4
                }
            };
            _propContainer.Add(buffHeader);

            for (int i = 0; i < config.buffEntries.Count; i++)
            {
                int currentIndex = i;
                var entry = config.buffEntries[i];

                var row = new VisualElement
                {
                    style = { flexDirection = FlexDirection.Row, marginBottom = 4, alignItems = Align.FlexEnd }
                };

                var objField = new ObjectField
                {
                    objectType = typeof(Tactics.Common.Units.Buffs.BuffConfig),
                    value = entry.buffConfig,
                    style = { flexGrow = 1, marginRight = 4 }
                };
                objField.RegisterValueChangedCallback(evt =>
                {
                    entry.buffConfig = evt.newValue as Tactics.Common.Units.Buffs.BuffConfig;
                    OnNodeChanged?.Invoke();
                });
                row.Add(objField);

                row.Add(CreateFloatField("W", entry.weight,
                    v => { entry.weight = v; OnNodeChanged?.Invoke(); }));

                var removeBtn = new Button(() =>
                {
                    config.buffEntries.RemoveAt(currentIndex);
                    InspectNode(_currentNode);
                    OnNodeChanged?.Invoke();
                })
                {
                    text = "-",
                    style = { width = 24, height = 22, marginBottom = 2 }
                };
                row.Add(removeBtn);
                _propContainer.Add(row);
            }

            var addBtn = new Button(() =>
            {
                config.buffEntries.Add(new BuffConfigEntry());
                InspectNode(_currentNode);
                OnNodeChanged?.Invoke();
            })
            {
                text = "+ Buff",
                style = { height = 22, marginBottom = 8 }
            };
            _propContainer.Add(addBtn);
        }

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

        private VisualElement CreateIntField(string label, int value, Action<int> onChange)
        {
            var container = new VisualElement { style = { marginRight = 8 } };
            container.Add(new Label(label)
            {
                style = { fontSize = 10, color = new Color(0.6f, 0.6f, 0.6f), paddingBottom = 2 }
            });

            var field = new IntegerField { value = value };
            field.style.fontSize = 11;
            field.style.height = 22;
            field.style.minWidth = 60;
            field.RegisterValueChangedCallback(evt => onChange(evt.newValue));
            container.Add(field);
            return container;
        }

        private VisualElement CreateStringField(string label, string value, Action<string> onChange)
        {
            var container = new VisualElement { style = { marginRight = 8, minWidth = 120, flexGrow = 1 } };
            container.Add(new Label(label)
            {
                style = { fontSize = 10, color = new Color(0.6f, 0.6f, 0.6f), paddingBottom = 2 }
            });

            var field = new TextField { value = value };
            field.style.fontSize = 11;
            field.style.height = 22;
            field.RegisterValueChangedCallback(evt => onChange(evt.newValue));
            container.Add(field);
            return container;
        }

        private VisualElement CreateStoreGoodKindField(
            string label,
            StoreGoodKind value,
            Action<StoreGoodKind> onChange)
        {
            var container = new VisualElement { style = { marginRight = 8, minWidth = 100 } };
            container.Add(new Label(label)
            {
                style = { fontSize = 10, color = new Color(0.6f, 0.6f, 0.6f), paddingBottom = 2 }
            });

            var field = new EnumField(value);
            field.style.fontSize = 11;
            field.style.height = 22;
            field.RegisterValueChangedCallback(evt => onChange((StoreGoodKind)evt.newValue));
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
