using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Tactics.Common.Skills.Graph;
using Tactics.Runtime.Utilities;

namespace Tactics.Editor.SkillGraphEditor
{
    /// <summary>
    /// 技能图编辑器窗口。
    /// </summary>
    public class SkillGraphEditorWindow : EditorWindow
    {
        private SkillGraphAsset _currentGraph;
        private SkillGraphView _graphView;
        private ScriptableObject _currentWrapper;

        [MenuItem("Tactics/Skill Graph Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<SkillGraphEditorWindow>();
            window.titleContent = new GUIContent("Skill Graph Editor");
            window.minSize = new Vector2(900, 600);
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;

            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.height = 30;
            toolbar.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            root.Add(toolbar);

            AddToolbarButton(toolbar, "Load Graph", LoadGraph);
            AddToolbarButton(toolbar, "Save", SaveGraph);
            AddToolbarButton(toolbar, "Validate", ValidateGraph);
            AddToolbarButton(toolbar, "Build Ability Config", BuildAbilityConfig);
            AddToolbarButton(toolbar, "Auto Layout", AutoLayoutGraph);
            AddToolbarButton(toolbar, "New Graph", CreateNewGraph);
            AddToolbarButton(toolbar, "Clear", ClearGraph);

            _graphView = new SkillGraphView();
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
            string path = EditorUtility.OpenFilePanel("Load Skill Graph", "Assets", "asset");
            if (string.IsNullOrEmpty(path)) return;
            if (path.StartsWith(Application.dataPath))
                path = "Assets" + path.Substring(Application.dataPath.Length);

            var graph = AssetDatabase.LoadAssetAtPath<SkillGraphAsset>(path);
            if (graph != null)
            {
                _currentGraph = graph;
                _graphView.LoadGraph(graph);
                TLog.Info($"[SkillGraph Editor] Loaded: {graph.name}");
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "Failed to load skill graph.", "OK");
            }
        }

        private void SaveGraph()
        {
            _graphView?.SaveGraph(_currentGraph);
            AssetDatabase.SaveAssets();
            TLog.Info("[SkillGraph Editor] Saved.");
        }

        private void ValidateGraph()
        {
            if (_currentGraph == null)
            {
                EditorUtility.DisplayDialog("Error", "No graph loaded.", "OK");
                return;
            }

            if (SkillGraphValidation.Validate(_currentGraph, out var errors, out var warnings))
            {
                string msg = "Graph is valid!";
                if (warnings.Count > 0)
                    msg += $"\n\n{warnings.Count} warning(s):\n{FormatDiagnostics(warnings)}";
                EditorUtility.DisplayDialog("Validation", msg, "OK");
            }
            else
            {
                string msg = $"{errors.Count} error(s):\n\n{FormatDiagnostics(errors)}";
                if (warnings.Count > 0)
                    msg += $"\n\n{warnings.Count} warning(s):\n{FormatDiagnostics(warnings)}";
                EditorUtility.DisplayDialog("Validation Errors", msg, "OK");
            }
        }

        private string FormatDiagnostics(List<SkillGraphDiagnostic> diagnostics)
        {
            var lines = new string[diagnostics.Count];
            for (int i = 0; i < diagnostics.Count; i++)
                lines[i] = diagnostics[i].ToString();
            return string.Join("\n", lines);
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
            TLog.Info("[SkillGraph Editor] Auto layout applied.");
        }

        private void BuildAbilityConfig()
        {
            if (_currentGraph == null)
            {
                EditorUtility.DisplayDialog("Error", "No graph loaded.", "OK");
                return;
            }

            _graphView?.SaveGraph(_currentGraph);
            AssetDatabase.SaveAssets();

            if (!SkillGraphValidation.Validate(_currentGraph, out var errors, out var warnings))
            {
                string msg = $"Graph has {errors.Count} error(s), cannot build ability config until fixed.\n\n{FormatDiagnostics(errors)}";
                if (warnings.Count > 0)
                    msg += $"\n\nWarnings:\n{FormatDiagnostics(warnings)}";
                EditorUtility.DisplayDialog("Build Blocked", msg, "OK");
                return;
            }

            var config = SkillGraphAbilityConfigGenerator.CreateOrSync(_currentGraph);
            if (config == null)
            {
                EditorUtility.DisplayDialog("Build Failed", "Failed to create or sync SkillGraphAbilityConfig.", "OK");
                return;
            }

            string configPath = AssetDatabase.GetAssetPath(config);
            string msg2 = $"Ability config ready:\n{configPath}";
            if (warnings.Count > 0)
                msg2 += $"\n\nWarnings:\n{FormatDiagnostics(warnings)}";

            if (EditorUtility.DisplayDialog("Build Complete", msg2, "Select Config", "Close"))
                Selection.activeObject = config;

            TLog.Info($"[SkillGraph Editor] Ability config built: {configPath}");
        }

        private void CreateNewGraph()
        {
            var graph = CreateInstance<SkillGraphAsset>();
            string path = EditorUtility.SaveFilePanelInProject("New Skill Graph", "NewSkillGraph", "asset", "Choose location");
            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.CreateAsset(graph, path);
                AssetDatabase.SaveAssets();
                _currentGraph = graph;
                _graphView?.LoadGraph(graph);
                TLog.Info($"[SkillGraph Editor] Created: {path}");
            }
        }

        private void ClearGraph()
        {
            _currentGraph?.Clear();
            _graphView?.LoadGraph(_currentGraph);
            EditorUtility.SetDirty(_currentGraph);
            ClearWrapper();
            TLog.Info("[SkillGraph Editor] Cleared.");
        }

        // ── Node Selection → Inspector ──

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

        private void CreateWrapperForNode(SkillGraphNodeRecord record, string nodeId)
        {
            ClearWrapper();

            ScriptableObject wrapper = record switch
            {
                SelectPrimaryTargetNodeRecord => CreateNodeWrapper<SkillGraphSelectPrimaryTargetWrapper>(_currentGraph, nodeId),
                SelectTargetPointNodeRecord => CreateNodeWrapper<SkillGraphSelectTargetPointWrapper>(_currentGraph, nodeId),
                CollectTargetsInAreaNodeRecord => CreateNodeWrapper<SkillGraphCollectTargetsInAreaWrapper>(_currentGraph, nodeId),
                DashToTargetNodeRecord => CreateNodeWrapper<SkillGraphDashToTargetWrapper>(_currentGraph, nodeId),
                ApplyDamageNodeRecord => CreateNodeWrapper<SkillGraphApplyDamageWrapper>(_currentGraph, nodeId),
                ApplyKnockbackNodeRecord => CreateNodeWrapper<SkillGraphApplyKnockbackWrapper>(_currentGraph, nodeId),
                _ => null
            };

            if (wrapper != null)
            {
                _currentWrapper = wrapper;
                Selection.activeObject = _currentWrapper;
            }
        }

        private T CreateNodeWrapper<T>(SkillGraphAsset graph, string nodeId) where T : ScriptableObject
        {
            var wrapper = CreateInstance<T>();
            var initMethod = typeof(T).GetMethod("Initialize");
            if (initMethod != null)
            {
                initMethod.Invoke(wrapper, new object[] { graph, nodeId });
                var onChanged = typeof(T).GetField("OnDataChanged");
                if (onChanged != null)
                    onChanged.SetValue(wrapper, (Action)(() => _graphView?.RefreshFromGraph(_currentGraph)));
            }
            return wrapper;
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
