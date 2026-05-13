using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Tactics.Runtime.Utilities;

namespace Tactics.Editor.RoguelikeEventEditor
{
    /// <summary>
    /// WYSIWYG Roguelike事件编辑器主窗口。
    /// 三列布局：左侧事件列表 | 中央节点画布 | 右侧属性面板 + 底部实时预览。
    /// </summary>
    public class RoguelikeEventEditorWindow : EditorWindow
    {
        // ── 面板引用 ──────────────────────────────
        private EventBlackboard _eventBlackboard;
        private EventGraphView _graphView;
        private EventInspectorPanel _inspectorPanel;
        private EventPreviewPanel _previewPanel;

        // ── 顶部工具栏 ────────────────────────────
        private VisualElement _toolbar;

        // ── UXML / USS 文件名 ─────────────────────
        private const string UxmlFile = "EditorResources/EditorWindow.uxml";
        private const string UssFile = "EditorResources/EditorWindow.uss";

        // ── 常量 ──────────────────────────────────
        private const float InspectorMinWidth = 220f;
        private const float InspectorMaxWidth = 380f;
        private const float BlackboardMinWidth = 180f;
        private const float BlackboardMaxWidth = 320f;
        private const float PreviewMinHeight = 120f;
        private const float PreviewMaxHeight = 300f;
        private const float ToolbarHeight = 28f;

        // ── MenuItem ──────────────────────────────
        [MenuItem("Tactics/Roguelike/Event Editor")]
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
            _eventBlackboard?.SaveSessionState();
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

            // 右列：属性面板
            var inspectorContainer = new VisualElement();
            inspectorContainer.name = "inspector-container";
            SetupSplitter(inspectorContainer, InspectorMinWidth, InspectorMaxWidth, "inspector");
            middleRow.Add(inspectorContainer);

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

            var inspector = rootVisualElement.Q<VisualElement>("inspector-container");
            inspector?.AddToClassList("panel-dark");

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

            var inspectorContainer = rootVisualElement.Q<VisualElement>("inspector-container");
            if (inspectorContainer != null)
            {
                _inspectorPanel = new EventInspectorPanel();
                inspectorContainer.Add(_inspectorPanel);
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
            _inspectorPanel?.ClearProperties();
        }

        private void OnEventAdded(SerializableEventData evt)
        {
            _graphView?.ClearCanvas();
            _previewPanel?.ClearPreview();
        }

        private void OnGraphNodeSelected(EventNodeElement node)
        {
            _inspectorPanel?.InspectNode(node, () =>
            {
                // 属性变更后刷新预览
                var data = _graphView?.BuildEventData();
                if (data != null)
                {
                    _previewPanel?.UpdatePreview(data);
                    _eventBlackboard?.UpdateEvent(data);
                }
            });
        }

        private void OnGraphChanged()
        {
            var data = _graphView?.BuildEventData();
            if (data != null)
            {
                _previewPanel?.UpdatePreview(data);
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
            }) { text = "新建", style = { height = 22, marginTop = 3, marginRight = 4, fontSize = 11 } };
            _toolbar.Add(newBtn);

            var exportBtn = new Button(() => ExportCurrentEvent())
            { text = "导出", style = { height = 22, marginTop = 3, marginRight = 4, fontSize = 11 } };
            _toolbar.Add(exportBtn);

            var importBtn = new Button(() => ImportEvents())
            { text = "导入", style = { height = 22, marginTop = 3, fontSize = 11 } };
            _toolbar.Add(importBtn);
        }

        // ── 导出 / 导入 ────────────────────────────
        private void ExportCurrentEvent()
        {
            var data = _graphView?.BuildEventData();
            if (data == null || string.IsNullOrEmpty(data.eventId))
            {
                EditorUtility.DisplayDialog("导出失败", "当前没有有效的事件数据", "确定");
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
            EditorUtility.DisplayDialog("导出成功", $"事件已导出到:\n{filePath}", "确定");
        }

        private void ImportEvents()
        {
            string dir = "Assets/Tactics/Resources/Events";
            if (!AssetDatabase.IsValidFolder(dir))
            {
                EditorUtility.DisplayDialog("导入失败", $"未找到事件目录:\n{dir}", "确定");
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
                    TLog.Warning($"[EventEditor] 导入失败: {path} — {ex.Message}");
                }
            }

            if (count > 0)
                EditorUtility.DisplayDialog("导入成功", $"已导入 {count} 个事件", "确定");
            else
                EditorUtility.DisplayDialog("导入完成", "未找到有效的事件JSON文件", "确定");
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
