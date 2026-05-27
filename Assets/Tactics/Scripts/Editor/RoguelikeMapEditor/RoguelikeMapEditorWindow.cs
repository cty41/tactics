using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Tactics.RoguelikeMap;
using Tactics.Runtime.Utilities;

namespace Tactics.Editor.RoguelikeMapEditor
{
    /// <summary>
    /// Roguelike 地图编辑器主窗口。
    /// 三列布局：左侧（配置面板）| 中央（MapGraphView 画布）| 右侧（MapInspectorPanel 属性面板）。
    /// 使用 MapEditorDocument 作为唯一数据源。
    /// </summary>
    public class RoguelikeMapEditorWindow : EditorWindow
    {
        // ── 面板引用 ──────────────────────────────
        private VisualElement _leftPanel;
        private VisualElement _centerPanel;
        private VisualElement _rightPanel;

        // ── 功能组件 ──────────────────────────────
        private MapGraphView _mapGraphView;
        private MapInspectorPanel _inspectorPanel;

        // ── 数据 ──────────────────────────────────
        private RoguelikeMapConfig _currentConfig;
        private MapEditorDocument _document;

        // ── 顶部工具栏 ────────────────────────────
        private VisualElement _toolbar;

        // ── 常量 ──────────────────────────────────
        private const float ToolbarHeight = 28f;
        private const float LeftPanelMinWidth = 180f;
        private const float LeftPanelMaxWidth = 320f;
        private const float RightPanelMinWidth = 220f;
        private const float RightPanelMaxWidth = 360f;
        private const string DefaultSaveDir = "Assets/Tactics/Arts/MapData";

        // ── MenuItem ──────────────────────────────
        [MenuItem("Tactics/RoguelikeMap Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<RoguelikeMapEditorWindow>();
            window.titleContent = new GUIContent("RoguelikeMap Editor");
            window.minSize = new Vector2(900, 600);
            window.Show();
        }

        // ── Lifecycle ─────────────────────────────
        private void CreateGUI()
        {
            BuildLayoutFromCode();
            ApplyBaseStyles();
            BuildToolbarButtons();
            WireUpCallbacks();
        }

        private void OnEnable()
        {
            LoadDefaultConfig();
        }

        private void OnDisable()
        {
            if (_document != null && _document.IsDirty)
            {
                // 不再静默保存，改为提示用户确认
                if (EditorUtility.DisplayDialog(
                    "Unsaved Changes",
                    "There are unsaved changes. Save before closing?",
                    "Save", "Discard"))
                {
                    OnSaveClicked();
                }
            }
            _document = null;
        }

        // ── Config Loading ────────────────────────
        private void LoadDefaultConfig()
        {
            var guids = AssetDatabase.FindAssets("t:RoguelikeMapConfig");
            if (guids.Length == 0)
            {
                TLog.Warning("[RoguelikeMapEditor] No RoguelikeMapConfig found in project. Create one via Assets > Create.");
                _currentConfig = null;
                UpdateConfigLabel();
                return;
            }

            // 如果有多个配置，允许用户选择
            if (guids.Length > 1)
            {
                var configNames = new string[guids.Length];
                for (int i = 0; i < guids.Length; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    configNames[i] = System.IO.Path.GetFileNameWithoutExtension(path);
                }

                // 构建选项列表供用户选择
                int selectedIndex = EditorUtility.DisplayDialogComplex(
                    "Select Config",
                    $"Found {guids.Length} RoguelikeMapConfig assets. Select one:",
                    configNames[0],
                    configNames.Length > 1 ? configNames[1] : "Cancel",
                    configNames.Length > 2 ? configNames[2] : "Cancel");

                // DisplayDialogComplex 返回 0/1/2，映射到前三个选项
                // 如果选项不足，回退到第一个
                if (selectedIndex >= 0 && selectedIndex < guids.Length)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[selectedIndex]);
                    _currentConfig = AssetDatabase.LoadAssetAtPath<RoguelikeMapConfig>(path);
                }
                else
                {
                    // 用户点击了 Cancel 或超出范围，加载第一个
                    var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    _currentConfig = AssetDatabase.LoadAssetAtPath<RoguelikeMapConfig>(path);
                }
            }
            else
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _currentConfig = AssetDatabase.LoadAssetAtPath<RoguelikeMapConfig>(path);
            }

            if (_currentConfig != null)
            {
                TLog.Info($"[RoguelikeMapEditor] Loaded config: {_currentConfig.name}");
            }
            UpdateConfigLabel();
        }

        // ── Layout ────────────────────────────────
        private void BuildLayoutFromCode()
        {
            rootVisualElement.Clear();

            // 顶部工具栏
            _toolbar = new VisualElement();
            _toolbar.name = "toolbar";
            _toolbar.style.height = ToolbarHeight;
            _toolbar.style.flexShrink = 0;
            _toolbar.style.flexDirection = FlexDirection.Row;
            _toolbar.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f);
            _toolbar.style.paddingLeft = 8;
            _toolbar.style.paddingRight = 8;
            rootVisualElement.Add(_toolbar);

            // 中部：三列布局
            var middleRow = new VisualElement();
            middleRow.style.flexGrow = 1;
            middleRow.style.flexDirection = FlexDirection.Row;
            rootVisualElement.Add(middleRow);

            // 左列：配置信息面板
            _leftPanel = new VisualElement();
            _leftPanel.name = "left-panel";
            _leftPanel.style.flexGrow = 0.2f;
            _leftPanel.style.minWidth = LeftPanelMinWidth;
            _leftPanel.style.maxWidth = LeftPanelMaxWidth;
            _leftPanel.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            middleRow.Add(_leftPanel);

            // 中央：MapGraphView 画布
            _centerPanel = new VisualElement();
            _centerPanel.name = "center-panel";
            _centerPanel.style.flexGrow = 1;
            _centerPanel.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);
            middleRow.Add(_centerPanel);

            // 右列：属性编辑面板
            _rightPanel = new VisualElement();
            _rightPanel.name = "right-panel";
            _rightPanel.style.flexGrow = 0.25f;
            _rightPanel.style.minWidth = RightPanelMinWidth;
            _rightPanel.style.maxWidth = RightPanelMaxWidth;
            _rightPanel.style.backgroundColor = new Color(0.17f, 0.17f, 0.17f);
            middleRow.Add(_rightPanel);

            // 创建功能组件
            BuildCenterPanel();
            BuildLeftPanel();
            BuildRightPanel();
        }

        private void BuildCenterPanel()
        {
            _mapGraphView = new MapGraphView();
            _centerPanel.Add(_mapGraphView);
        }

        private void BuildLeftPanel()
        {
            var header = new Label("Config")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 12,
                    paddingBottom = 4,
                    paddingLeft = 8,
                    borderBottomWidth = 1,
                    borderBottomColor = new Color(0.3f, 0.3f, 0.3f)
                }
            };
            _leftPanel.Add(header);

            var configLabel = new Label("No config loaded")
            {
                name = "config-label",
                style =
                {
                    color = new Color(0.7f, 0.7f, 0.7f),
                    fontSize = 11,
                    paddingLeft = 8,
                    paddingTop = 4
                }
            };
            _leftPanel.Add(configLabel);

            // Reload Config 按钮
            var reloadBtn = new Button(() =>
            {
                LoadDefaultConfig();
                UpdateLeftPanelInfo();
            })
            {
                text = "Reload Config",
                style = { height = 22, marginTop = 6, marginLeft = 8, marginRight = 8, fontSize = 11 }
            };
            _leftPanel.Add(reloadBtn);

            // 分隔线
            _leftPanel.Add(new VisualElement
            {
                style =
                {
                    height = 1,
                    marginTop = 8,
                    marginBottom = 4,
                    marginLeft = 8,
                    marginRight = 8,
                    backgroundColor = new Color(0.3f, 0.3f, 0.3f)
                }
            });

            // Document Info 标题
            var docHeader = new Label("Document")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 12,
                    paddingBottom = 4,
                    paddingLeft = 8,
                    borderBottomWidth = 1,
                    borderBottomColor = new Color(0.3f, 0.3f, 0.3f)
                }
            };
            _leftPanel.Add(docHeader);

            // 文档信息
            var docInfoLabel = new Label("No document loaded")
            {
                name = "doc-info-label",
                style =
                {
                    color = new Color(0.7f, 0.7f, 0.7f),
                    fontSize = 11,
                    paddingLeft = 8,
                    paddingTop = 4
                }
            };
            _leftPanel.Add(docInfoLabel);

            // 分隔线
            _leftPanel.Add(new VisualElement
            {
                style =
                {
                    height = 1,
                    marginTop = 8,
                    marginBottom = 4,
                    marginLeft = 8,
                    marginRight = 8,
                    backgroundColor = new Color(0.3f, 0.3f, 0.3f)
                }
            });

            // Validation 标题
            var validHeader = new Label("Validation")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 12,
                    paddingBottom = 4,
                    paddingLeft = 8,
                    borderBottomWidth = 1,
                    borderBottomColor = new Color(0.3f, 0.3f, 0.3f)
                }
            };
            _leftPanel.Add(validHeader);

            // 校验状态
            var validationLabel = new Label("")
            {
                name = "validation-label",
                style =
                {
                    color = new Color(0.7f, 0.7f, 0.7f),
                    fontSize = 11,
                    paddingLeft = 8,
                    paddingTop = 4
                }
            };
            _leftPanel.Add(validationLabel);

            UpdateConfigLabel();
        }

        private void BuildRightPanel()
        {
            _inspectorPanel = new MapInspectorPanel();
            _rightPanel.Add(_inspectorPanel);
        }

        private void UpdateConfigLabel()
        {
            var label = _leftPanel?.Q<Label>("config-label");
            if (label == null) return;

            if (_currentConfig == null)
            {
                label.text = "No config loaded";
            }
            else
            {
                label.text = $"Config: {_currentConfig.name}\n" +
                             $"Nodes: {_currentConfig.nodeCount}\n" +
                             $"Reach: {_currentConfig.maxReachableDistance}\n" +
                             $"MinDist: {_currentConfig.minDistanceBetweenNodes}";
            }
        }

        // ── Base Styles ───────────────────────────
        private void ApplyBaseStyles()
        {
            rootVisualElement.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);
            _leftPanel?.AddToClassList("panel-dark");
            _centerPanel?.AddToClassList("panel-dark");
            _rightPanel?.AddToClassList("panel-dark");
        }

        // ── Wire Up Callbacks ─────────────────────
        private void WireUpCallbacks()
        {
            if (_mapGraphView == null) return;

            _mapGraphView.OnNodeSelected += node =>
            {
                if (node == null)
                {
                    _inspectorPanel?.InspectEditableNode(null);
                    return;
                }

                // 从文档模型获取可编辑节点
                if (_document != null)
                {
                    var editableNode = _document.GetNode(node.NodeId);
                    if (editableNode != null)
                    {
                        _inspectorPanel?.InspectEditableNode(editableNode);
                    }
                }
            };

            _mapGraphView.OnNodeAdded += (type, x, y, nodeId) =>
            {
                TLog.Info($"[RoguelikeMapEditor] OnNodeAdded handler called: nodeId={nodeId}, _document={_document != null}");
                // 节点已通过 MapGraphView.AddNode 添加到文档模型
                // 此回调仅用于日志和额外处理
            };

            _mapGraphView.OnNodeDoubleClicked += node =>
            {
                if (node == null) return;

                var doc = _mapGraphView.GetDocument();
                if (doc == null) return;

                var editableNode = doc.GetNode(node.NodeId);
                if (editableNode == null) return;

                // 只处理 Mystery 节点
                if (editableNode.nodeType == RoguelikeNodeType.Mystery)
                {
                    OpenOrCreateEventForMysteryNode(editableNode);
                }
            };

            // Inspector 面板变更 → 标记文档脏 + 刷新画布连线 + 同步视觉
            if (_inspectorPanel != null)
            {
                _inspectorPanel.OnNodeChanged += () =>
                {
                    _document?.MarkDirty();

                    // 同步节点类型视觉样式和位置到画布
                    var editedNode = _inspectorPanel.CurrentEditableNode;
                    if (editedNode != null && _mapGraphView != null)
                    {
                        _mapGraphView.UpdateNodeVisual(editedNode.nodeId, editedNode.nodeType);
                        _mapGraphView.UpdateNodePosition(editedNode.nodeId, editedNode.position);
                    }

                    _mapGraphView?.RebuildAllConnections();
                    UpdateLeftPanelInfo();
                };
            }
        }

        // ── Mystery Node → Event Editor 联动 ──────
        /// <summary>
        /// 双击 Mystery 节点时：有 eventId 则定位，无则创建，资源缺失则提示补建。
        /// </summary>
        private void OpenOrCreateEventForMysteryNode(EditableMapNodeData node)
        {
            string eventId = node.eventId;

            if (string.IsNullOrEmpty(eventId))
            {
                // 创建新事件
                if (EditorUtility.DisplayDialog(
                    "Create Event",
                    $"No event assigned to Mystery node '{node.nodeId}'.\nCreate a new event?",
                    "Create", "Cancel"))
                {
                    eventId = $"event_{node.nodeId}";
                    node.eventId = eventId;
                    _mapGraphView.GetDocument().MarkDirty();

                    // 打开 Event Editor 并创建事件
                    var eventEditor = EditorWindow.GetWindow<Tactics.Editor.RoguelikeEventEditor.RoguelikeEventEditorWindow>();
                    eventEditor.CreateNewEvent(eventId);
                }
            }
            else
            {
                // 检查事件是否存在
                if (EventExists(eventId))
                {
                    // 打开 Event Editor 并定位事件
                    var eventEditor = EditorWindow.GetWindow<Tactics.Editor.RoguelikeEventEditor.RoguelikeEventEditorWindow>();
                    eventEditor.OpenEvent(eventId);
                }
                else
                {
                    // 事件不存在，提示补建
                    if (EditorUtility.DisplayDialog(
                        "Event Not Found",
                        $"Event '{eventId}' not found.\nRecreate it?",
                        "Recreate", "Cancel"))
                    {
                        var eventEditor = EditorWindow.GetWindow<Tactics.Editor.RoguelikeEventEditor.RoguelikeEventEditorWindow>();
                        eventEditor.CreateNewEvent(eventId);
                    }
                }
            }
        }

        /// <summary>
        /// 检查事件 JSON 文件是否存在（扫描所有 region 子目录）。
        /// </summary>
        private bool EventExists(string eventId)
        {
            string baseDir = "Assets/Tactics/Resources/Events";
            if (!AssetDatabase.IsValidFolder(baseDir)) return false;

            // 搜索所有子目录中的 {eventId}.json
            var guids = AssetDatabase.FindAssets($"{eventId}", new[] { baseDir });
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith($"/{eventId}.json"))
                    return true;
            }
            return false;
        }

        // ── 工具栏 ────────────────────────────────
        private void BuildToolbarButtons()
        {
            if (_toolbar == null) return;

            var generateBtn = new Button(() => OnGenerateClicked())
            {
                text = "Generate",
                style = { height = 22, marginTop = 3, marginRight = 4, fontSize = 11 }
            };
            _toolbar.Add(generateBtn);

            var clearBtn = new Button(() => OnClearClicked())
            {
                text = "Clear",
                style = { height = 22, marginTop = 3, marginRight = 4, fontSize = 11 }
            };
            _toolbar.Add(clearBtn);

            var saveBtn = new Button(() => OnSaveClicked())
            {
                text = "Save",
                style = { height = 22, marginTop = 3, marginRight = 4, fontSize = 11 }
            };
            _toolbar.Add(saveBtn);

            var loadBtn = new Button(() => OnLoadClicked())
            {
                text = "Load",
                style = { height = 22, marginTop = 3, marginRight = 4, fontSize = 11 }
            };
            _toolbar.Add(loadBtn);

            var exportBtn = new Button(() => OnExportClicked())
            {
                text = "Export",
                style = { height = 22, marginTop = 3, marginRight = 12, fontSize = 11 }
            };
            _toolbar.Add(exportBtn);

            // Validate 按钮
            var validateBtn = new Button(() => OnValidateClicked())
            {
                text = "Validate",
                tooltip = "Validate map structure (duplicate nodes, dangling connections, missing Boss/Start, invalid eventId)",
                style = { height = 22, marginTop = 3, marginRight = 12, fontSize = 11 }
            };
            _toolbar.Add(validateBtn);

            // 分隔符
            var separator = new VisualElement
            {
                style =
                {
                    width = 1,
                    height = 22,
                    marginTop = 3,
                    marginRight = 8,
                    backgroundColor = new Color(0.4f, 0.4f, 0.4f)
                }
            };
            _toolbar.Add(separator);

            // Rebuild Connections（按距离重建所有连接）
            var rebuildBtn = new Button(() => OnRebuildConnectionsClicked())
            {
                text = "Rebuild Connections",
                tooltip = "按距离重建所有连接（覆盖现有连接）",
                style = { height = 22, marginTop = 3, marginRight = 4, fontSize = 11 }
            };
            _toolbar.Add(rebuildBtn);

            // Show Distance Hints 切换按钮
            var hintsToggle = new Button(() => OnToggleDistanceHints())
            {
                text = "Distance Hints: OFF",
                name = "distance-hints-toggle",
                tooltip = "显示/隐藏距离建议连接（虚线）",
                style = { height = 22, marginTop = 3, marginRight = 4, fontSize = 11 }
            };
            _toolbar.Add(hintsToggle);
        }

        private void OnRebuildConnectionsClicked()
        {
            if (_mapGraphView == null)
            {
                TLog.Warning("[RoguelikeMapEditor] MapGraphView is null.");
                return;
            }
            if (_document == null)
            {
                TLog.Warning("[RoguelikeMapEditor] No document loaded.");
                EditorUtility.DisplayDialog("Rebuild Connections", "No map loaded. Generate or load a map first.", "OK");
                return;
            }

            _mapGraphView.RebuildConnectionsByDistance();
            TLog.Info("[RoguelikeMapEditor] Connections rebuilt by distance.");
        }

        private void OnToggleDistanceHints()
        {
            if (_mapGraphView == null) return;

            bool newState = !_mapGraphView.ShowDistanceHints;
            _mapGraphView.SetShowDistanceHints(newState);

            var toggleBtn = _toolbar?.Q<Button>("distance-hints-toggle");
            if (toggleBtn != null)
            {
                toggleBtn.text = newState ? "Distance Hints: ON" : "Distance Hints: OFF";
            }
        }

        // ═══════════════════════════════════════════
        //  Generate — 随机生成地图
        // ═══════════════════════════════════════════
        private void OnGenerateClicked()
        {
            if (_currentConfig == null)
            {
                TLog.Warning("[RoguelikeMapEditor] No config loaded. Cannot generate.");
                EditorUtility.DisplayDialog("Generate", "No RoguelikeMapConfig loaded. Create or assign one first.", "OK");
                return;
            }

            if (_mapGraphView == null)
            {
                TLog.Error("[RoguelikeMapEditor] MapGraphView is null.");
                return;
            }

            TLog.Info($"[RoguelikeMapEditor] Generating map with config: {_currentConfig.name}");

            // 清除现有节点和选择状态
            _mapGraphView.ClearCanvas();
            ClearSelection();

            // 调用生成器
            var map = RoguelikeMapGenerator.GetMap(_currentConfig);
            if (map == null)
            {
                TLog.Error("[RoguelikeMapEditor] Map generation failed.");
                EditorUtility.DisplayDialog("Generate", "Map generation failed. Check console for details.", "OK");
                return;
            }

            // 从生成的运行时地图创建文档模型
            int reachDist = (int)_currentConfig.maxReachableDistance;
            int vision = (int)_currentConfig.visionRange;
            _document = MapEditorDocument.FromRuntimeMap(map, reachDist, vision);

            // 绑定文档模型到组件
            _mapGraphView.SetDocument(_document);
            _inspectorPanel?.SetDocument(_document);

            // 设置画布边界
            float genCellW = _currentConfig.maxReachableDistance * _currentConfig.gridColumns * 0.8f / _currentConfig.gridColumns;
            float genCellH = _currentConfig.maxReachableDistance * _currentConfig.gridRows * 0.6f / _currentConfig.gridRows;
            float genCanvasW = genCellW * _currentConfig.gridColumns + 1f;
            float genCanvasH = genCellH * _currentConfig.gridRows + 1f;
            _mapGraphView.SetCanvasBounds(genCanvasW, genCanvasH);

            TLog.Info($"[RoguelikeMapEditor] Map generated: {_document.nodes.Count} nodes, boss={_document.GetBossNodeId()}");
            UpdateLeftPanelInfo();
        }

        // ═══════════════════════════════════════════
        //  Save — 保存到项目默认目录（覆盖同名文件）
        // ═══════════════════════════════════════════
        private void OnSaveClicked()
        {
            if (_document == null || _document.nodes.Count == 0)
            {
                TLog.Warning("[RoguelikeMapEditor] No nodes to save.");
                EditorUtility.DisplayDialog("Save", "No nodes on the canvas to save.", "OK");
                return;
            }

            // 确定保存路径（项目默认目录）
            string mapName = _currentConfig != null ? _currentConfig.name : "UntitledMap";
            string dirPath = DefaultSaveDir;
            string filePath = $"{dirPath}/{mapName}.json";

            // 如果文件已存在，提示用户确认覆盖
            if (File.Exists(filePath))
            {
                if (!EditorUtility.DisplayDialog(
                    "Confirm Overwrite",
                    $"File already exists:\n{filePath}\n\nOverwrite?",
                    "Overwrite", "Cancel"))
                {
                    return;
                }
            }

            // 确保目录存在
            if (!Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
                AssetDatabase.Refresh();
            }

            // 序列化并保存
            try
            {
                var data = _document.ToSerializable();
                string json = MapDataSerializer.Serialize(data);
                File.WriteAllText(filePath, json);
                AssetDatabase.Refresh();
                _document.ClearDirty();
                UpdateLeftPanelInfo();
                TLog.Info($"[RoguelikeMapEditor] Map saved to: {filePath}");
                EditorUtility.DisplayDialog("Save", $"Map saved to:\n{filePath}", "OK");
            }
            catch (Exception ex)
            {
                TLog.Error($"[RoguelikeMapEditor] Save failed: {ex.Message}");
                EditorUtility.DisplayDialog("Save", $"Save failed: {ex.Message}", "OK");
            }
        }

        // ═══════════════════════════════════════════
        //  Load — 从 JSON 文件加载
        // ═══════════════════════════════════════════
        private void OnLoadClicked()
        {
            string path = EditorUtility.OpenFilePanel("Load Map JSON", DefaultSaveDir, "json");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                string json = File.ReadAllText(path);
                var data = MapDataSerializer.Deserialize(json);

                // 从 SerializableMapData 创建文档模型
                _document = MapEditorDocument.FromSerializable(data);

                // 绑定文档模型到组件
                _mapGraphView.ClearCanvas();
                ClearSelection();
                _mapGraphView.SetDocument(_document);
                _inspectorPanel?.SetDocument(_document);

                // 设置画布边界
                if (_currentConfig != null)
                {
                    float loadCellW = _currentConfig.maxReachableDistance * _currentConfig.gridColumns * 0.8f / _currentConfig.gridColumns;
                    float loadCellH = _currentConfig.maxReachableDistance * _currentConfig.gridRows * 0.6f / _currentConfig.gridRows;
                    float loadCanvasW = loadCellW * _currentConfig.gridColumns + 1f;
                    float loadCanvasH = loadCellH * _currentConfig.gridRows + 1f;
                    _mapGraphView.SetCanvasBounds(loadCanvasW, loadCanvasH);
                }

                TLog.Info($"[RoguelikeMapEditor] Map loaded from: {path} ({_document.nodes.Count} nodes)");
                UpdateLeftPanelInfo();
            }
            catch (Exception ex)
            {
                TLog.Error($"[RoguelikeMapEditor] Load failed: {ex.Message}");
                EditorUtility.DisplayDialog("Load", $"Load failed: {ex.Message}", "OK");
            }
        }

        // ═══════════════════════════════════════════
        //  Export — 导出到用户指定路径（可选择任意位置）
        // ═══════════════════════════════════════════
        private void OnExportClicked()
        {
            if (_document == null || _document.nodes.Count == 0)
            {
                TLog.Warning("[RoguelikeMapEditor] No nodes to export.");
                EditorUtility.DisplayDialog("Export", "No nodes on the canvas to export.", "OK");
                return;
            }

            string mapName = _currentConfig != null ? _currentConfig.name : "UntitledMap";
            string path = EditorUtility.SaveFilePanel("Export Map JSON", DefaultSaveDir, mapName, "json");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                var data = _document.ToSerializable();
                string json = MapDataSerializer.Serialize(data);
                File.WriteAllText(path, json);

                // 如果导出路径在 Assets 内，刷新资源数据库
                if (path.StartsWith(Application.dataPath))
                {
                    AssetDatabase.Refresh();
                }

                TLog.Info($"[RoguelikeMapEditor] Map exported to: {path}");
                EditorUtility.DisplayDialog("Export", $"Map exported to:\n{path}", "OK");
            }
            catch (Exception ex)
            {
                TLog.Error($"[RoguelikeMapEditor] Export failed: {ex.Message}");
                EditorUtility.DisplayDialog("Export", $"Export failed: {ex.Message}", "OK");
            }
        }

        // ═══════════════════════════════════════════
        //  Clear — 清空画布和文档状态
        // ═══════════════════════════════════════════
        private void OnClearClicked()
        {
            // 如果有未保存的更改，提示用户确认
            if (_document != null && _document.IsDirty)
            {
                if (!EditorUtility.DisplayDialog(
                    "Unsaved Changes",
                    "There are unsaved changes. Clear anyway?",
                    "Clear", "Cancel"))
                {
                    return;
                }
            }

            // 清理文档
            _document = null;

            // 清理画布
            _mapGraphView?.ClearCanvas();

            // 清理选择
            ClearSelection();

            // 更新 UI
            UpdateLeftPanelInfo();

            TLog.Info("[RoguelikeMapEditor] Cleared");
        }

        // ═══════════════════════════════════════════
        //  Validate — 校验地图结构
        // ═══════════════════════════════════════════
        private void OnValidateClicked()
        {
            if (_document == null)
            {
                EditorUtility.DisplayDialog("Validation", "No document loaded.", "OK");
                return;
            }

            if (_document.nodes.Count == 0)
            {
                EditorUtility.DisplayDialog("Validation", "No nodes in document.", "OK");
                return;
            }

            var errors = new List<string>();

            // 1. 检查重复 nodeId
            var nodeIds = new HashSet<string>();
            foreach (var node in _document.nodes)
            {
                if (!nodeIds.Add(node.nodeId))
                {
                    errors.Add($"Duplicate nodeId: {node.nodeId}");
                }
            }

            // 2. 检查缺少 Boss/Start
            bool hasStart = _document.nodes.Any(n => n.nodeType == RoguelikeNodeType.Start);
            bool hasBoss = _document.nodes.Any(n => n.nodeType == RoguelikeNodeType.Boss);

            if (!hasStart) errors.Add("Missing Start node");
            if (!hasBoss) errors.Add("Missing Boss node");

            // 3. 检查悬空连接（outgoing 指向不存在的节点）
            foreach (var node in _document.nodes)
            {
                foreach (var outgoingId in node.outgoing)
                {
                    if (_document.GetNode(outgoingId) == null)
                    {
                        errors.Add($"Dangling connection: {node.nodeId} -> {outgoingId}");
                    }
                }
            }

            // 4. 检查非法 eventId（Mystery 节点必须有 eventId）
            foreach (var node in _document.nodes)
            {
                if (node.nodeType == RoguelikeNodeType.Mystery && string.IsNullOrEmpty(node.eventId))
                {
                    errors.Add($"Mystery node '{node.nodeId}' has no eventId");
                }
            }

            // 显示结果
            if (errors.Count == 0)
            {
                EditorUtility.DisplayDialog("Validation", "All checks passed!", "OK");
            }
            else
            {
                string message = $"Found {errors.Count} issue(s):\n\n" + string.Join("\n", errors);
                EditorUtility.DisplayDialog("Validation Errors", message, "OK");
            }

            // 更新左侧面板的校验状态
            UpdateLeftPanelInfo();
        }

        // ── 选择管理 ────────────────────────────────

        /// <summary>
        /// 清除属性面板选择状态和画布高亮。
        /// </summary>
        private void ClearSelection()
        {
            _inspectorPanel?.InspectEditableNode(null);
            _mapGraphView?.ClearSelection();
        }

        // ── 左侧面板信息更新 ────────────────────────

        /// <summary>
        /// 更新左侧面板的文档信息和校验状态。
        /// </summary>
        private void UpdateLeftPanelInfo()
        {
            if (_leftPanel == null) return;

            var docInfoLabel = _leftPanel.Q<Label>("doc-info-label");
            if (docInfoLabel != null)
            {
                if (_document == null)
                {
                    docInfoLabel.text = "No document loaded";
                }
                else
                {
                    string dirtyMark = _document.IsDirty ? " *" : "";
                    docInfoLabel.text = $"Nodes: {_document.nodes.Count}{dirtyMark}\n" +
                                        $"Reach: {_document.maxReachableDistance}\n" +
                                        $"Vision: {_document.visionRange}";
                }
            }

            var validationLabel = _leftPanel.Q<Label>("validation-label");
            if (validationLabel != null)
            {
                if (_document == null || _document.nodes.Count == 0)
                {
                    validationLabel.text = "";
                    return;
                }

                var issues = new List<string>();

                // 检查是否有 Start 节点
                bool hasStart = _document.nodes.Any(n => n.nodeType == RoguelikeNodeType.Start);
                if (!hasStart) issues.Add("Missing Start node");

                // 检查是否有 Boss 节点
                bool hasBoss = _document.nodes.Any(n => n.nodeType == RoguelikeNodeType.Boss);
                if (!hasBoss) issues.Add("Missing Boss node");

                // 检查 Mystery 节点是否都有 eventId
                var mysteryNoEvent = _document.nodes
                    .Where(n => n.nodeType == RoguelikeNodeType.Mystery && string.IsNullOrEmpty(n.eventId))
                    .Select(n => n.nodeId)
                    .ToList();
                if (mysteryNoEvent.Count > 0)
                    issues.Add($"{mysteryNoEvent.Count} Mystery node(s) without eventId");

                // 检查孤立节点（无 outgoing 且无 incoming）
                var allOutgoingTargets = _document.nodes
                    .SelectMany(n => n.outgoing)
                    .ToHashSet();
                var nodesWithIncoming = _document.nodes
                    .Where(n => allOutgoingTargets.Contains(n.nodeId))
                    .Select(n => n.nodeId)
                    .ToHashSet();
                var orphans = _document.nodes
                    .Where(n => n.outgoing.Count == 0 && !nodesWithIncoming.Contains(n.nodeId)
                                && n.nodeType != RoguelikeNodeType.Start
                                && n.nodeType != RoguelikeNodeType.Boss)
                    .Select(n => n.nodeId)
                    .ToList();
                if (orphans.Count > 0)
                    issues.Add($"{orphans.Count} orphan node(s)");

                if (issues.Count == 0)
                {
                    validationLabel.text = "✓ Valid";
                    validationLabel.style.color = new Color(0.3f, 0.8f, 0.3f);
                }
                else
                {
                    validationLabel.text = string.Join("\n", issues);
                    validationLabel.style.color = new Color(0.9f, 0.6f, 0.2f);
                }
            }

            UpdateConfigLabel();
        }

    }
}
