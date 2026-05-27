using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Tactics.Runtime.Utilities;

namespace Tactics.Editor.RoguelikeEventEditor
{
    /// <summary>
    /// WYSIWYG Roguelike事件编辑器主窗口。
    /// 两列布局：左侧事件列表 | 中央节点图画布 + 底部实时预览。
    /// 属性编辑通过 Unity Inspector（CustomEditor + Selection.activeObject）完成。
    /// </summary>
    public class RoguelikeEventEditorWindow : EditorWindow
    {
        // ── 面板引用 ──────────────────────────────
        private EventBlackboard _eventBlackboard;
        private EventGraphView _graphView;
        private EventPreviewPanel _previewPanel;
        private EventNodeDataWrapper _selectionWrapper;
        private bool _isDirty = false;

        // ── 顶部工具栏 ────────────────────────────
        private VisualElement _toolbar;

        // ── UXML / USS 文件名 ─────────────────────
        private const string UxmlFile = "EditorResources/EditorWindow.uxml";
        private const string UssFile = "EditorResources/EditorWindow.uss";

        // ── 常量 ──────────────────────────────────
        private const float BlackboardMinWidth = 180f;
        private const float BlackboardMaxWidth = 320f;
        private const float PreviewMinHeight = 120f;
        private const float PreviewMaxHeight = 300f;
        private const float ToolbarHeight = 28f;

        // ── MenuItem ──────────────────────────────
        [MenuItem("Tactics/Event Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<RoguelikeEventEditorWindow>();
            window.titleContent = new GUIContent("Roguelike Event Editor");
            window.minSize = new Vector2(900, 600);
            window.Show();
        }

        // ── Lifecycle ─────────────────────────────
        private void CreateGUI()
        {
            // 尝试加载 UXML
            var visualTree = LoadUxml();
            if (visualTree != null)
            {
                visualTree.CloneTree(rootVisualElement);
            }
            else
            {
                BuildLayoutFromCode();
            }

            // 加载样式表
            var styleSheet = LoadUss();
            if (styleSheet != null)
            {
                rootVisualElement.styleSheets.Add(styleSheet);
            }

            ApplyBaseStyles();

            // 缓存面板引用
            CachePanelReferences();
        }

        private void OnEnable()
        {
            // 刷新资源
        }

        private void OnDisable()
        {
            if (_isDirty)
            {
                ExportCurrentEvent();
            }
            if (_selectionWrapper != null)
            {
                DestroyImmediate(_selectionWrapper);
                _selectionWrapper = null;
            }
            _eventBlackboard?.SaveSessionState();
        }

        private void MarkDirty()
        {
            _isDirty = true;
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

            // 左列：事件列表面板
            var blackboardContainer = new VisualElement();
            blackboardContainer.name = "blackboard-container";
            SetupSplitter(blackboardContainer, BlackboardMinWidth, BlackboardMaxWidth, "blackboard");
            middleRow.Add(blackboardContainer);

            // 中央：节点图画布
            var graphContainer = new VisualElement();
            graphContainer.name = "graph-container";
            graphContainer.style.flexGrow = 1;
            middleRow.Add(graphContainer);

            // 底部：预览面板
            var previewContainer = new VisualElement();
            previewContainer.name = "preview-container";
            SetupVerticalSplitter(previewContainer, PreviewMinHeight, PreviewMaxHeight, "preview");
            rootVisualElement.Add(previewContainer);
        }

        /// <summary>
        /// 通过两个面板之间的分隔条（divider）设置宽度可调
        /// </summary>
        private void SetupSplitter(VisualElement container, float min, float max, string trackKey)
        {
            container.style.minWidth = min;
            container.style.maxWidth = max;

            // 分隔条
            var divider = new VisualElement();
            divider.name = $"{trackKey}-divider";
            divider.style.width = 4f;
            divider.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);

            container.userData = new SplitterData { min = min, max = max, current = min, key = trackKey };
        }

        private void SetupVerticalSplitter(VisualElement container, float min, float max, string trackKey)
        {
            container.style.minHeight = min;
            container.style.maxHeight = max;
            container.style.height = 180f; // 默认
        }

        // ── Asset Loading ─────────────────────────
        private VisualTreeAsset LoadUxml()
        {
            return AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                GetAssetPath(UxmlFile));
        }

        private StyleSheet LoadUss()
        {
            return AssetDatabase.LoadAssetAtPath<StyleSheet>(
                GetAssetPath(UssFile));
        }

        private static string GetAssetPath(string relativePath)
        {
            return $"Assets/Tactics/Scripts/Editor/RoguelikeEventEditor/{relativePath}";
        }

        // ── Base Styles ───────────────────────────
        private void ApplyBaseStyles()
        {
            rootVisualElement.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);

            // 给主要区域加上基础样式类
            var blackboard = rootVisualElement.Q<VisualElement>("blackboard-container");
            blackboard?.AddToClassList("panel-dark");

            var preview = rootVisualElement.Q<VisualElement>("preview-container");
            preview?.AddToClassList("panel-dark");
        }

        // ── Panel Reference Caching ───────────────
        private void CachePanelReferences()
        {
            var blackboardContainer = rootVisualElement.Q<VisualElement>("blackboard-container");
            if (blackboardContainer != null)
            {
                _eventBlackboard = new EventBlackboard();
                blackboardContainer.Add(_eventBlackboard);
                _eventBlackboard.OnEventSelected += OnEventSelected;
                _eventBlackboard.OnEventAdded += OnEventAdded;
            }

            var graphContainer = rootVisualElement.Q<VisualElement>("graph-container");
            if (graphContainer != null)
            {
                _graphView = new EventGraphView();
                graphContainer.Add(_graphView);
                _graphView.OnNodeSelected += OnGraphNodeSelected;
                _graphView.OnGraphChanged += OnGraphChanged;
            }

            var previewContainer = rootVisualElement.Q<VisualElement>("preview-container");
            if (previewContainer != null)
            {
                _previewPanel = new EventPreviewPanel();
                previewContainer.Add(_previewPanel);
            }

            // 工具栏按钮
            BuildToolbarButtons();
        }

        // ── Panel Wiring ──────────────────────────
        private void OnEventSelected(SerializableEventData evt)
        {
            _graphView?.LoadEvent(evt);
            _previewPanel?.UpdatePreview(evt);
        }

        private void OnEventAdded(SerializableEventData evt)
        {
            _graphView?.ClearCanvas();
            _previewPanel?.ClearPreview();
        }

        private void OnGraphNodeSelected(EventNodeElement node)
        {
            // Selection/wrapper logic
            if (node == null)
            {
                if (_selectionWrapper != null)
                {
                    DestroyImmediate(_selectionWrapper);
                    _selectionWrapper = null;
                }
                Selection.activeObject = null;
            }
            else
            {
                if (_selectionWrapper == null)
                    _selectionWrapper = ScriptableObject.CreateInstance<EventNodeDataWrapper>();
                _selectionWrapper.Initialize(node.Data, node.NodeType, node.NodeId);
                _selectionWrapper.OnDataChanged = MarkDirty;
                Selection.activeObject = _selectionWrapper;
            }
        }

        private void OnGraphChanged()
        {
            var data = _graphView?.BuildEventData();
            if (data != null)
            {
                _previewPanel?.UpdatePreview(data);
                // Only update blackboard if an event is selected
                if (!string.IsNullOrEmpty(_eventBlackboard?.SelectedEventId))
                    _eventBlackboard?.UpdateEvent(data);
            }
        }

        // ── 工具栏 ────────────────────────────────
        private void BuildToolbarButtons()
        {
            if (_toolbar == null) return;

            var newBtn = new Button(() =>
            {
                _eventBlackboard?.CreateNewEvent();
            }) { text = "New", style = { height = 22, marginTop = 3, marginRight = 4, fontSize = 11 } };
            _toolbar.Add(newBtn);

            var exportBtn = new Button(() => ExportCurrentEvent())
            { text = "Export", style = { height = 22, marginTop = 3, marginRight = 4, fontSize = 11 } };
            _toolbar.Add(exportBtn);

            var importBtn = new Button(() => ImportEvents())
            { text = "Import", style = { height = 22, marginTop = 3, fontSize = 11 } };
            _toolbar.Add(importBtn);
        }

        // ── 导出 / 导入 ────────────────────────────
        private void ExportCurrentEvent()
        {
            var data = _graphView?.BuildEventData();
            if (data == null || string.IsNullOrEmpty(data.eventId))
            {
                EditorUtility.DisplayDialog("Export Failed", "No valid event data to export", "OK");
                return;
            }

            string json = EventGraphSerializer.Serialize(data);
            string dir = $"Assets/Tactics/Resources/Events/{data.region}";
            if (!AssetDatabase.IsValidFolder(dir))
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(Application.dataPath, "Tactics/Resources/Events", data.region));
                AssetDatabase.Refresh();
            }

            string filePath = System.IO.Path.Combine(dir, $"{data.eventId}.json");
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(Application.dataPath, "Tactics/Resources/Events", data.region, $"{data.eventId}.json"),
                json);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Export Success", $"Event exported to:\n{filePath}", "OK");
        }

        private void ImportEvents()
        {
            string dir = "Assets/Tactics/Resources/Events";
            if (!AssetDatabase.IsValidFolder(dir))
            {
                EditorUtility.DisplayDialog("Import Failed", $"Event directory not found:\n{dir}", "OK");
                return;
            }

            var guids = AssetDatabase.FindAssets("t:TextAsset", new[] { dir });
            int count = 0;
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".json")) continue;
                try
                {
                    string json = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, path.Substring("Assets/".Length)));
                    var data = EventGraphSerializer.Deserialize(json);
                    _eventBlackboard?.AddEvent(data);
                    count++;
                }
                catch (System.Exception ex)
                {
                    TLog.Warning($"[EventEditor] Import failed: {path} — {ex.Message}");
                }
            }

            if (count > 0)
                EditorUtility.DisplayDialog("Import Success", $"Imported {count} event(s)", "OK");
            else
                EditorUtility.DisplayDialog("Import Complete", "No valid event JSON files found", "OK");
        }

        // ── 外部调用接口 ──────────────────────────
        /// <summary>
        /// 打开指定事件。若事件未加载则先导入。
        /// </summary>
        public void OpenEvent(string eventId)
        {
            if (string.IsNullOrEmpty(eventId)) return;

            // 确保面板已初始化
            if (_eventBlackboard == null)
            {
                TLog.Warning("[EventEditor] Blackboard not initialized, retrying after layout");
                // 如果面板还没初始化，延迟执行
                rootVisualElement.schedule.Execute(() => OpenEvent(eventId)).StartingIn(100);
                return;
            }

            // 如果事件未在列表中，尝试从文件导入
            var existing = _eventBlackboard.GetEvent(eventId);
            if (existing == null)
            {
                ImportEventById(eventId);
            }

            // 选中事件
            _eventBlackboard.SelectEvent(eventId);
            TLog.Info($"[EventEditor] Opened event '{eventId}'");
        }

        /// <summary>
        /// 创建新事件并选中。
        /// </summary>
        public void CreateNewEvent(string eventId)
        {
            if (string.IsNullOrEmpty(eventId)) return;

            // 确保面板已初始化
            if (_eventBlackboard == null)
            {
                TLog.Warning("[EventEditor] Blackboard not initialized, retrying after layout");
                rootVisualElement.schedule.Execute(() => CreateNewEvent(eventId)).StartingIn(100);
                return;
            }

            var evt = new SerializableEventData
            {
                eventId = eventId,
                title = "新事件",
                region = EventRegions.DarkForest,
                nodes = new List<EventNodeData>
                {
                    new() { nodeId = "start_1", type = EventNodeTypes.Start, data = new() { eventId = eventId, title = "New Event", region = EventRegions.DarkForest } },
                    new() { nodeId = "end_1", type = EventNodeTypes.End, data = new() { summaryText = "Event ends" } }
                },
                connections = new List<EventConnectionData> { new() { from = "start_1", to = "end_1", port = "out" } }
            };

            _eventBlackboard.AddEvent(evt);
            _eventBlackboard.SelectEvent(eventId);
            TLog.Info($"[EventEditor] Created new event '{eventId}'");
        }

        /// <summary>
        /// 按 eventId 从文件导入单个事件。
        /// </summary>
        private void ImportEventById(string eventId)
        {
            string baseDir = "Assets/Tactics/Resources/Events";
            if (!AssetDatabase.IsValidFolder(baseDir)) return;

            var guids = AssetDatabase.FindAssets($"{eventId}", new[] { baseDir });
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith($"/{eventId}.json")) continue;
                try
                {
                    string json = System.IO.File.ReadAllText(
                        System.IO.Path.Combine(Application.dataPath, path.Substring("Assets/".Length)));
                    var data = EventGraphSerializer.Deserialize(json);
                    _eventBlackboard?.AddEvent(data);
                    TLog.Info($"[EventEditor] Imported event '{eventId}' from {path}");
                }
                catch (System.Exception ex)
                {
                    TLog.Warning($"[EventEditor] Import failed: {path} — {ex.Message}");
                }
                break;
            }
        }

        // ── Helper Types ──────────────────────────
        private struct SplitterData
        {
            public float min;
            public float max;
            public float current;
            public string key;
        }
    }
}
