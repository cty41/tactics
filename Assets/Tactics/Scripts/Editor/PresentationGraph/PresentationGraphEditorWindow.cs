#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Units.Tween;
using Tactics.EditorTools;
using Tactics.Runtime.Utilities;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tactics.Editor.PresentationGraph
{
    /// <summary>
    /// Authors visual-only choreography while keeping SkillGraph gameplay logic separate.
    /// </summary>
    public sealed class PresentationGraphEditorWindow : EditorWindow
    {
        private BattlePresentationGraph _graph;
        private PresentationGraphView _graphView;
        private ScrollView _inspector;
        private ObjectField _graphField;

        [MenuItem("Tactics/Pure Run/Presentation Graph Editor")]
        private static void Open()
        {
            var window = GetWindow<PresentationGraphEditorWindow>();
            window.titleContent = new GUIContent("Presentation Graph");
            window.minSize = new Vector2(1000f, 640f);
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += HandleUndoRedo;
        }

        private void CreateGUI()
        {
            rootVisualElement.Clear();
            BuildToolbar();
            var split = new TwoPaneSplitView(1, 310f, TwoPaneSplitViewOrientation.Horizontal);
            _graphView = new PresentationGraphView();
            _graphView.NodeSelected += DrawInspector;
            split.Add(_graphView);
            _inspector = new ScrollView();
            _inspector.style.paddingLeft = 8f;
            _inspector.style.paddingRight = 8f;
            split.Add(_inspector);
            rootVisualElement.Add(split);
            SetGraph(_graph);
        }

        private void BuildToolbar()
        {
            var toolbar = new Toolbar();
            _graphField = new ObjectField("Graph")
            {
                objectType = typeof(BattlePresentationGraph),
                allowSceneObjects = false
            };
            _graphField.style.minWidth = 360f;
            _graphField.RegisterValueChangedCallback(evt => SetGraph(evt.newValue as BattlePresentationGraph));
            toolbar.Add(_graphField);
            toolbar.Add(new ToolbarButton(CreateGraph) { text = "New" });
            toolbar.Add(new ToolbarButton(SaveGraph) { text = "Save" });
            toolbar.Add(new ToolbarButton(ValidateGraph) { text = "Validate" });
            toolbar.Add(new ToolbarSpacer());
            toolbar.Add(new ToolbarButton(() => PureRunTweenPreviewWindow.OpenPresentationGraph(_graph))
            {
                text = "Preview"
            });
            rootVisualElement.Add(toolbar);
        }

        private void SetGraph(BattlePresentationGraph graph)
        {
            _graphView?.SavePositions();
            _graph = graph;
            if (_graphField != null)
                _graphField.SetValueWithoutNotify(graph);
            _graphView?.Load(graph);
            DrawInspector(null);
        }

        private void CreateGraph()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "New Presentation Graph",
                "NewBattlePresentationGraph",
                "asset",
                "Choose a location for the presentation graph.");
            if (string.IsNullOrEmpty(path))
                return;
            var graph = CreateInstance<BattlePresentationGraph>();
            graph.DisplayName = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(graph, path);
            AssetDatabase.SaveAssets();
            SetGraph(graph);
        }

        private void SaveGraph()
        {
            _graphView?.SavePositions();
            AssetDatabase.SaveAssets();
        }

        private void ValidateGraph()
        {
            if (BattlePresentationGraphValidation.Validate(_graph, out List<PresentationGraphDiagnostic> errors))
            {
                EditorUtility.DisplayDialog("Presentation Graph", "Graph is valid.", "OK");
                return;
            }
            string message = string.Join("\n", errors.Take(16).Select(error =>
                $"{error.Code} [{error.NodeId ?? "graph"}]: {error.Message}"));
            EditorUtility.DisplayDialog("Presentation Graph Errors", message, "OK");
        }

        private void DrawInspector(PresentationNodeRecord node)
        {
            if (_inspector == null)
                return;
            _inspector.Clear();
            _inspector.Add(new Label(node == null ? "Select a node" : node.NodeType.ToString())
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 8f, marginBottom = 8f }
            });
            if (node == null || _graph == null)
                return;

            var enabled = new Toggle("Enabled") { value = node.Enabled };
            enabled.RegisterValueChangedCallback(evt => Change(node, () => node.Enabled = evt.newValue));
            _inspector.Add(enabled);

            switch (node)
            {
                case PresentationEntryNodeRecord entry:
                    AddEnumField("Cue", entry.Cue, value => entry.Cue = (PresentationCueKind)value, node);
                    break;
                case PresentationUnitTweenNodeRecord tween:
                    AddEnumField("Action", tween.Action, value => tween.Action = (UnitVisualAction)value, node);
                    AddToggle("Emit Release", tween.EmitReleaseMarker, value => tween.EmitReleaseMarker = value, node);
                    break;
                case PresentationProjectileNodeRecord projectile:
                    AddObjectField("Profile", projectile.Profile, typeof(ProjectileVisualProfile),
                        value => projectile.Profile = value as ProjectileVisualProfile, node);
                    AddFloatField("Speed", projectile.Speed, value => projectile.Speed = Mathf.Max(0f, value), node);
                    AddFloatField("Fallback Time", projectile.FallbackTravelTime,
                        value => projectile.FallbackTravelTime = Mathf.Max(0f, value), node);
                    AddToggle("Emit Impact", projectile.EmitImpactMarker,
                        value => projectile.EmitImpactMarker = value, node);
                    break;
                case PresentationPrefabFxNodeRecord prefabFx:
                    AddObjectField("Profile", prefabFx.Profile, typeof(VisualCueProfile),
                        value => prefabFx.Profile = value as VisualCueProfile, node);
                    break;
                case PresentationProceduralVfxNodeRecord procedural:
                    AddObjectField("Recipe", procedural.Recipe, typeof(SkillVfxRecipe),
                        value => procedural.Recipe = value as SkillVfxRecipe, node);
                    AddEnumField("Cue", procedural.Cue,
                        value => procedural.Cue = (SkillVfxCueKind)value, node);
                    break;
                case PresentationDelayNodeRecord delay:
                    AddFloatField("Duration", delay.Duration,
                        value => delay.Duration = Mathf.Max(0f, value), node);
                    break;
                case PresentationMarkerNodeRecord marker:
                    AddEnumField("Marker", marker.Marker,
                        value => marker.Marker = (PresentationMarkerKind)value, node);
                    break;
                case PresentationForkNodeRecord fork:
                    var choices = _graph.Nodes.OfType<PresentationJoinNodeRecord>()
                        .Select(join => join.NodeId).ToList();
                    if (choices.Count == 0)
                    {
                        _inspector.Add(new HelpBox("Add a Join node first.", HelpBoxMessageType.Warning));
                    }
                    else
                    {
                        int index = Mathf.Max(0, choices.IndexOf(fork.JoinNodeId));
                        var popup = new PopupField<string>("Join", choices, index);
                        popup.RegisterValueChangedCallback(evt =>
                            Change(node, () => fork.JoinNodeId = evt.newValue));
                        _inspector.Add(popup);
                    }
                    break;
            }
        }

        private void AddEnumField(string label, Enum value, Action<Enum> setter, PresentationNodeRecord node)
        {
            var field = new EnumField(label, value);
            field.RegisterValueChangedCallback(evt => Change(node, () => setter(evt.newValue)));
            _inspector.Add(field);
        }

        private void AddObjectField(
            string label,
            UnityEngine.Object value,
            Type type,
            Action<UnityEngine.Object> setter,
            PresentationNodeRecord node)
        {
            var field = new ObjectField(label)
            {
                objectType = type,
                allowSceneObjects = false,
                value = value
            };
            field.RegisterValueChangedCallback(evt => Change(node, () => setter(evt.newValue)));
            _inspector.Add(field);
        }

        private void AddFloatField(
            string label,
            float value,
            Action<float> setter,
            PresentationNodeRecord node)
        {
            var field = new FloatField(label) { value = value };
            field.RegisterValueChangedCallback(evt => Change(node, () => setter(evt.newValue)));
            _inspector.Add(field);
        }

        private void AddToggle(
            string label,
            bool value,
            Action<bool> setter,
            PresentationNodeRecord node)
        {
            var field = new Toggle(label) { value = value };
            field.RegisterValueChangedCallback(evt => Change(node, () => setter(evt.newValue)));
            _inspector.Add(field);
        }

        private void Change(PresentationNodeRecord node, Action mutation)
        {
            Undo.RecordObject(_graph, $"Edit {node.NodeType}");
            mutation();
            EditorUtility.SetDirty(_graph);
            _graphView.Load(_graph);
            DrawInspector(node);
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= HandleUndoRedo;
            _graphView?.SavePositions();
        }

        private void HandleUndoRedo()
        {
            _graphView?.Load(_graph);
            DrawInspector(null);
        }
    }
}
#endif
