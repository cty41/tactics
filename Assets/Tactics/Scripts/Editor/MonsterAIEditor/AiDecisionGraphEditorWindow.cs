using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Tactics.Common.AI.MonsterAI;
using Tactics.Runtime.Utilities;

namespace Tactics.Editor.MonsterAIEditor
{
    /// <summary>
    /// 怪物 AI 决策图编辑器窗口。
    /// 选中节点时通过 Unity Inspector 显示对应属性。
    /// </summary>
    public class AiDecisionGraphEditorWindow : EditorWindow
    {
        private AiDecisionGraph _currentGraph;
        private AiDecisionGraphView _graphView;
        private ScriptableObject _currentWrapper;

        [MenuItem("Tactics/AI Decision Graph Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<AiDecisionGraphEditorWindow>();
            window.titleContent = new GUIContent("AI Decision Graph Editor");
            window.minSize = new Vector2(800, 600);
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;

            // 工具栏
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.height = 30;
            toolbar.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            root.Add(toolbar);

            AddToolbarButton(toolbar, "Load Graph", LoadGraph);
            AddToolbarButton(toolbar, "Save", () => { _graphView?.SaveGraph(_currentGraph); AssetDatabase.SaveAssets(); TLog.Info($"[AI Editor] Saved."); });
            AddToolbarButton(toolbar, "Validate", ValidateGraph);
            AddToolbarButton(toolbar, "Auto Layout", AutoLayoutGraph);
            AddToolbarButton(toolbar, "New Graph", CreateNewGraph);
            AddToolbarButton(toolbar, "Clear", ClearGraph);

            // 图视图
            _graphView = new AiDecisionGraphView();
            _graphView.style.flexGrow = 1;
            _graphView.OnNodeSelected += OnNodeSelected;
            root.Add(_graphView);
        }

        private void AddToolbarButton(VisualElement toolbar, string text, Action action)
        {
            var btn = new Button(action) { text = text };
            btn.style.marginLeft = 5;
            btn.style.height = 22;
            btn.style.marginTop = 4;
            toolbar.Add(btn);
        }

        private void OnDisable()
        {
            ClearWrapper();
        }

        private void LoadGraph()
        {
            string path = EditorUtility.OpenFilePanel("Load AI Decision Graph", "Assets", "asset");
            if (string.IsNullOrEmpty(path)) return;
            if (path.StartsWith(Application.dataPath))
                path = "Assets" + path.Substring(Application.dataPath.Length);

            var graph = AssetDatabase.LoadAssetAtPath<AiDecisionGraph>(path);
            if (graph != null)
            {
                _currentGraph = graph;
                _graphView.LoadGraph(graph);
                TLog.Info($"[AI Editor] Loaded: {graph.name}");
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "Failed to load graph.", "OK");
            }
        }

        private void ValidateGraph()
        {
            if (_currentGraph == null) { EditorUtility.DisplayDialog("Error", "No graph loaded.", "OK"); return; }
            if (_currentGraph.Validate(out var errors))
                EditorUtility.DisplayDialog("Validation", "Graph is valid!", "OK");
            else
                EditorUtility.DisplayDialog("Validation Errors", $"{errors.Count} error(s):\n\n{string.Join("\n", errors)}", "OK");
        }

        private void AutoLayoutGraph()
        {
            if (_currentGraph == null)
            {
                EditorUtility.DisplayDialog("Error", "No graph loaded.", "OK");
                return;
            }

            _graphView?.AutoLayoutGraph();
            AssetDatabase.SaveAssets();
            TLog.Info("[AI Editor] Auto layout applied.");
        }

        private void CreateNewGraph()
        {
            var graph = CreateInstance<AiDecisionGraph>();
            string path = EditorUtility.SaveFilePanelInProject("New Graph", "NewAiDecisionGraph", "asset", "Choose location");
            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.CreateAsset(graph, path);
                AssetDatabase.SaveAssets();
                _currentGraph = graph;
                _graphView?.LoadGraph(graph);
                TLog.Info($"[AI Editor] Created: {path}");
            }
        }

        private void ClearGraph()
        {
            _currentGraph?.Clear();
            _graphView?.LoadGraph(_currentGraph);
            EditorUtility.SetDirty(_currentGraph);
            ClearWrapper();
            TLog.Info("[AI Editor] Cleared.");
        }

        // ── 节点选中 → Inspector ──

        private void OnNodeSelected(string nodeId)
        {
            if (_currentGraph == null || string.IsNullOrEmpty(nodeId))
            {
                ClearWrapper();
                return;
            }

            var record = _currentGraph.FindNode(nodeId);
            if (record == null) { ClearWrapper(); return; }

            CreateWrapperForNode(record, nodeId);
        }

        private void CreateWrapperForNode(GraphNodeRecord record, string nodeId)
        {
            ClearWrapper();

            switch (record)
            {
                case IntentNodeRecord:
                    var iw = CreateInstance<AiIntentNodeWrapper>();
                    iw.Initialize(_currentGraph, nodeId);
                    iw.OnDataChanged += () => _graphView?.RefreshFromGraph(_currentGraph);
                    _currentWrapper = iw;
                    break;
                case RuleNodeRecord:
                    var rw = CreateInstance<AiRuleNodeWrapper>();
                    rw.Initialize(_currentGraph, nodeId);
                    rw.OnDataChanged += () => _graphView?.RefreshFromGraph(_currentGraph);
                    _currentWrapper = rw;
                    break;
                case ScoreNodeRecord:
                    var sw = CreateInstance<AiScoreNodeWrapper>();
                    sw.Initialize(_currentGraph, nodeId);
                    sw.OnDataChanged += () => _graphView?.RefreshFromGraph(_currentGraph);
                    _currentWrapper = sw;
                    break;
            }

            if (_currentWrapper != null)
                Selection.activeObject = _currentWrapper;
        }

        private void ClearWrapper()
        {
            if (_currentWrapper != null)
            {
                DestroyImmediate(_currentWrapper);
                _currentWrapper = null;
            }
            Selection.activeObject = null;
        }
    }
}
