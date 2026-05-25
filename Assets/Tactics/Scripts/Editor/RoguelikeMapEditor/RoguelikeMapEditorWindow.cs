using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Tactics.RoguelikeMap;
using Tactics.Runtime.Utilities;
using RoguelikeMapData = Tactics.RoguelikeMap.RoguelikeMap;

namespace Tactics.Editor.RoguelikeMapEditor
{
    /// <summary>
    /// Roguelike 地图编辑器主窗口。
    /// 两列布局：左侧（配置面板）| 中央（MapGraphView 画布）。
    /// 属性编辑通过 Unity Inspector（CustomEditor + Selection.activeObject）完成。
    /// 工具栏支持 Generate / Save / Load / Export 操作。
    /// </summary>
    public class RoguelikeMapEditorWindow : EditorWindow
    {
        // ── 面板引用 ──────────────────────────────
        private VisualElement _leftPanel;
        private VisualElement _centerPanel;

        // ── 功能组件 ──────────────────────────────
        private MapGraphView _mapGraphView;

        // ── 数据 ──────────────────────────────────
        private RoguelikeMapConfig _currentConfig;
        private RoguelikeMapData _currentMap;
        private MapNodeDataWrapper _selectionWrapper;
        private bool _isDirty = false;

        // ── 顶部工具栏 ────────────────────────────
        private VisualElement _toolbar;

        // ── 常量 ──────────────────────────────────
        private const float ToolbarHeight = 28f;
        private const float LeftPanelMinWidth = 180f;
        private const float LeftPanelMaxWidth = 320f;
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
            if (_isDirty)
            {
                OnSaveClicked();
            }
            if (_selectionWrapper != null)
            {
                DestroyImmediate(_selectionWrapper);
                _selectionWrapper = null;
            }
            _currentMap = null;
        }

        private void MarkDirty()
        {
            _isDirty = true;
        }

        // ── Config Loading ────────────────────────
        private void LoadDefaultConfig()
        {
            // 尝试通过 AssetDatabase 查找项目中的第一个 RoguelikeMapConfig
            var guids = AssetDatabase.FindAssets("t:RoguelikeMapConfig");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _currentConfig = AssetDatabase.LoadAssetAtPath<RoguelikeMapConfig>(path);
                TLog.Info($"[RoguelikeMapEditor] Loaded default config: {_currentConfig.name} from {path}");
            }
            else
            {
                TLog.Warning("[RoguelikeMapEditor] No RoguelikeMapConfig found in project. Create one via Assets > Create.");
            }
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

            // 中部：两列布局
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

            // 创建功能组件
            BuildCenterPanel();
            BuildLeftPanel();
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

            UpdateConfigLabel();
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
        }

        // ── Wire Up Callbacks ─────────────────────
        private void WireUpCallbacks()
        {
            if (_mapGraphView == null) return;

            _mapGraphView.OnNodeSelected += node =>
            {
                if (node == null)
                {
                    if (_selectionWrapper != null)
                    {
                        DestroyImmediate(_selectionWrapper);
                        _selectionWrapper = null;
                    }
                    Selection.activeObject = null;
                    return;
                }

                var mapNode = _currentMap?.GetNode(node.NodeId);
                TLog.Info($"[RoguelikeMapEditor] OnNodeSelected: nodeId={node.NodeId}, mapNode={mapNode != null}");
                if (mapNode == null) return;

                if (_selectionWrapper == null)
                    _selectionWrapper = ScriptableObject.CreateInstance<MapNodeDataWrapper>();

                _selectionWrapper.Initialize(mapNode);
                _selectionWrapper.OnDataChanged = MarkDirty;
                Selection.activeObject = _selectionWrapper;
            };

            _mapGraphView.OnNodeAdded += (type, x, y, nodeId) =>
            {
                TLog.Info($"[RoguelikeMapEditor] OnNodeAdded handler called: nodeId={nodeId}, _currentMap={_currentMap != null}");
                if (_currentMap == null)
                {
                    _currentMap = new RoguelikeMapData("UntitledMap", "", new System.Collections.Generic.List<RoguelikeMapNode>(), new System.Collections.Generic.HashSet<string>(), 10f, 5f);
                    TLog.Info("[RoguelikeMapEditor] OnNodeAdded: auto-created new empty map");
                }
                var mapNode = new RoguelikeMapNode(nodeId, type, "", new Vector2(x, y));
                if (type == RoguelikeNodeType.Treasure)
                {
                    mapNode.treasureConfig = new TreasureNodeConfig();
                }
                else if (type == RoguelikeNodeType.Store)
                {
                    mapNode.storeConfig = new StoreNodeConfig();
                }

                TLog.Info($"[RoguelikeMapEditor] OnNodeAdded: nodeId={nodeId}, creating RoguelikeMapNode");
                _currentMap.nodes.Add(mapNode);
                MarkDirty();
                TLog.Info($"[RoguelikeMapEditor] OnNodeAdded: added to map, total nodes={_currentMap.nodes.Count}");
            };
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

            var clearBtn = new Button(() => _mapGraphView?.ClearCanvas())
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
                style = { height = 22, marginTop = 3, fontSize = 11 }
            };
            _toolbar.Add(exportBtn);
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

            // 清除现有节点
            _mapGraphView.ClearCanvas();

            // 调用生成器
            var map = RoguelikeMapGenerator.GetMap(_currentConfig);
            if (map == null)
            {
                TLog.Error("[RoguelikeMapEditor] Map generation failed.");
                EditorUtility.DisplayDialog("Generate", "Map generation failed. Check console for details.", "OK");
                return;
            }

            // 设置 MaxReachableDistance 并加载节点
            _mapGraphView.MaxReachableDistance = _currentConfig.maxReachableDistance;
            // Calculate canvas bounds from config
            float genCellW = _currentConfig.maxReachableDistance * _currentConfig.gridColumns * 0.8f / _currentConfig.gridColumns;
            float genCellH = _currentConfig.maxReachableDistance * _currentConfig.gridRows * 0.6f / _currentConfig.gridRows;
            float genCanvasW = genCellW * _currentConfig.gridColumns + 1f;
            float genCanvasH = genCellH * _currentConfig.gridRows + 1f;
            _mapGraphView.SetCanvasBounds(genCanvasW, genCanvasH);
            _mapGraphView.LoadNodes(map.nodes);
            _currentMap = map;

            TLog.Info($"[RoguelikeMapEditor] Map generated: {map.nodes.Count} nodes, boss={map.bossNodeName}");
        }

        // ═══════════════════════════════════════════
        //  Save — 保存到默认路径
        // ═══════════════════════════════════════════
        private void OnSaveClicked()
        {
            if (_mapGraphView == null || _mapGraphView.GetNodes().Count == 0)
            {
                TLog.Warning("[RoguelikeMapEditor] No nodes to save.");
                EditorUtility.DisplayDialog("Save", "No nodes on the canvas to save.", "OK");
                return;
            }

            // 确定保存路径
            string mapName = _currentConfig != null ? _currentConfig.name : "UntitledMap";
            string dirPath = DefaultSaveDir;
            string filePath = $"{dirPath}/{mapName}.json";

            // 确保目录存在
            if (!Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
                AssetDatabase.Refresh();
            }

            // 序列化并保存
            try
            {
                string json = BuildJsonFromGraph();
                File.WriteAllText(filePath, json);
                AssetDatabase.Refresh();
                TLog.Info($"[RoguelikeMapEditor] Map saved to: {filePath}");
                EditorUtility.DisplayDialog("Save", $"Map saved to:\n{filePath}", "OK");
                _isDirty = false;
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
                var map = MapDataSerializer.ToRuntimeMap(data);

                if (map == null)
                {
                    TLog.Error("[RoguelikeMapEditor] Deserialization returned null map.");
                    EditorUtility.DisplayDialog("Load", "Failed to deserialize map data.", "OK");
                    return;
                }

                // 加载到画布
                _mapGraphView.ClearCanvas();
                _mapGraphView.MaxReachableDistance = data.maxReachableDistance > 0
                    ? data.maxReachableDistance
                    : (_currentConfig != null ? _currentConfig.maxReachableDistance : 200f);
                if (_currentConfig != null)
                {
                    float loadCellW = _currentConfig.maxReachableDistance * _currentConfig.gridColumns * 0.8f / _currentConfig.gridColumns;
                    float loadCellH = _currentConfig.maxReachableDistance * _currentConfig.gridRows * 0.6f / _currentConfig.gridRows;
                    float loadCanvasW = loadCellW * _currentConfig.gridColumns + 1f;
                    float loadCanvasH = loadCellH * _currentConfig.gridRows + 1f;
                    _mapGraphView.SetCanvasBounds(loadCanvasW, loadCanvasH);
                }
                _mapGraphView.LoadNodes(map.nodes);
                _currentMap = map;

                TLog.Info($"[RoguelikeMapEditor] Map loaded from: {path} ({map.nodes.Count} nodes)");
            }
            catch (Exception ex)
            {
                TLog.Error($"[RoguelikeMapEditor] Load failed: {ex.Message}");
                EditorUtility.DisplayDialog("Load", $"Load failed: {ex.Message}", "OK");
            }
        }

        // ═══════════════════════════════════════════
        //  Export — 导出 JSON（使用 MapDataSerializer）
        // ═══════════════════════════════════════════
        private void OnExportClicked()
        {
            if (_mapGraphView == null || _mapGraphView.GetNodes().Count == 0)
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
                string json = BuildJsonFromGraph();
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
        //  Helpers
        // ═══════════════════════════════════════════

        /// <summary>
        /// 从当前画布构建 JSON 字符串。
        /// 使用 MapReachabilityUtility 计算连接关系，再通过 MapDataSerializer 序列化。
        /// </summary>
        private string BuildJsonFromGraph()
        {
            // 从画布获取节点位置信息
            var graphNodes = _mapGraphView.BuildMapNodeList(_currentMap?.nodes);

            // 使用 MapReachabilityUtility 重建连接关系
            float maxDist = _currentConfig != null
                ? _currentConfig.maxReachableDistance
                : _mapGraphView.MaxReachableDistance;
            var connections = MapReachabilityUtility.GetAllConnections(graphNodes, maxDist);

            foreach (var (from, to) in connections)
            {
                from.AddOutgoing(to.nodeId);
                to.AddIncoming(from.nodeId);
            }

            // 找到 Boss 节点
            var bossNode = graphNodes.Find(n => n.nodeType == RoguelikeNodeType.Boss);
            string bossNodeName = bossNode?.nodeId ?? string.Empty;

            // 构建 RoguelikeMap
            var map = new RoguelikeMapData(
                _currentConfig != null ? _currentConfig.name : "UntitledMap",
                bossNodeName,
                graphNodes,
                new HashSet<string>(),
                maxDist,
                _currentConfig != null ? _currentConfig.visionRange : 0f);

            // 转换为 SerializableMapData 并序列化
            int reachDist = _currentConfig != null ? (int)_currentConfig.maxReachableDistance : (int)maxDist;
            int vision = _currentConfig != null ? (int)_currentConfig.visionRange : 0;
            var data = MapDataSerializer.FromRuntimeMap(map, reachDist, vision);
            return MapDataSerializer.Serialize(data);
        }
    }
}
