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
            bool graphValid = BattlePresentationGraphValidation.Validate(
                _graph,
                out List<PresentationGraphDiagnostic> errors);
            bool previewValid = PresentationPreviewScenarioValidation.Validate(
                _graph,
                out List<string> previewErrors);
            if (graphValid && previewValid)
            {
                EditorUtility.DisplayDialog("Presentation Graph", "Graph is valid.", "OK");
                return;
            }
            IEnumerable<string> graphMessages = errors.Select(error =>
                $"{error.Code} [{error.NodeId ?? "graph"}]: {error.Message}");
            string message = string.Join("\n", graphMessages.Concat(previewErrors).Take(16));
            EditorUtility.DisplayDialog("Presentation Graph Errors", message, "OK");
        }

        private void DrawInspector(PresentationNodeRecord node)
        {
            if (_inspector == null)
                return;
            _inspector.Clear();
            _inspector.Add(new Label(node == null ? "Graph Settings" : node.NodeType.ToString())
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 8f, marginBottom = 8f }
            });
            if (_graph == null)
                return;
            if (node == null)
            {
                _inspector.Add(new Label("Full Skill Preview")
                {
                    style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 4f }
                });
                var serializedGraph = new SerializedObject(_graph);
                AddBoundProperty(serializedGraph, "_previewActorPrefab", "Representative Actor");
                AddBoundProperty(serializedGraph, "_previewTargetPrefab", "Representative Target");
                AddBoundProperty(serializedGraph, "_previewPhases", "Preview Scenario Phases");
                _inspector.Add(new Label(
                    $"Legacy fallback: {_graph.DefaultPreviewEntry} (used only when no scenario is configured)"));
                return;
            }

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

        private void AddBoundProperty(
            SerializedObject serializedObject,
            string propertyName,
            string label)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
                return;
            var field = new PropertyField(property, label);
            field.Bind(serializedObject);
            _inspector.Add(field);
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

    internal static class PresentationPreviewScenarioValidation
    {
        internal static bool Validate(BattlePresentationGraph graph, out List<string> errors)
        {
            errors = new List<string>();
            if (graph == null)
            {
                errors.Add("PreviewScenarioMissingGraph: Presentation graph is missing.");
                return false;
            }
            if (!graph.HasPreviewScenario)
            {
                errors.Add("PreviewScenarioMissing: Full skill preview scenario is not configured.");
                return false;
            }

            for (int phaseIndex = 0; phaseIndex < graph.PreviewPhases.Count; phaseIndex++)
            {
                PresentationPreviewPhaseRecord phase = graph.PreviewPhases[phaseIndex];
                if (phase == null || phase.Cues == null || phase.Cues.Count == 0)
                {
                    errors.Add($"PreviewPhaseEmpty [{phaseIndex}]: Phase requires at least one cue.");
                    continue;
                }

                var uniqueCues = new HashSet<PresentationCueKind>();
                foreach (PresentationCueKind cue in phase.Cues)
                {
                    if (!uniqueCues.Add(cue))
                        errors.Add($"PreviewPhaseDuplicateCue [{phaseIndex}]: {cue} appears more than once.");
                    PresentationEntryNodeRecord entry = graph.FindEntry(cue);
                    if (entry == null || !entry.Enabled)
                        errors.Add($"PreviewPhaseMissingEntry [{phaseIndex}]: Enabled {cue} entry is required.");
                }

                if (!phase.Cues.Contains(phase.ContinuationCue))
                {
                    errors.Add(
                        $"PreviewPhaseMissingDriver [{phaseIndex}]: Continuation cue must belong to the phase.");
                    continue;
                }
                if (phase.AdvanceKind != PresentationPreviewAdvanceKind.Complete &&
                    !HasAdvancePoint(graph, phase.ContinuationCue, phase.AdvanceKind))
                {
                    errors.Add(
                        $"PreviewPhaseMissingMarker [{phaseIndex}]: {phase.ContinuationCue} has no " +
                        $"{phase.AdvanceKind} continuation point.");
                }
            }
            return errors.Count == 0;
        }

        private static bool HasAdvancePoint(
            BattlePresentationGraph graph,
            PresentationCueKind cue,
            PresentationPreviewAdvanceKind advanceKind)
        {
            PresentationEntryNodeRecord entry = graph.FindEntry(cue);
            if (entry == null)
                return false;
            var pending = new Stack<string>();
            var visited = new HashSet<string>();
            pending.Push(entry.NodeId);
            while (pending.Count > 0)
            {
                string nodeId = pending.Pop();
                if (!visited.Add(nodeId))
                    continue;
                PresentationNodeRecord node = graph.FindNode(nodeId);
                if (node is PresentationUnitTweenNodeRecord tween &&
                    advanceKind == PresentationPreviewAdvanceKind.Release &&
                    tween.EmitReleaseMarker)
                {
                    return true;
                }
                if (node is PresentationProjectileNodeRecord projectile &&
                    advanceKind == PresentationPreviewAdvanceKind.Impact &&
                    projectile.EmitImpactMarker)
                {
                    return true;
                }
                if (node is PresentationMarkerNodeRecord marker &&
                    ((advanceKind == PresentationPreviewAdvanceKind.Release &&
                      marker.Marker == PresentationMarkerKind.Release) ||
                     (advanceKind == PresentationPreviewAdvanceKind.Impact &&
                      marker.Marker == PresentationMarkerKind.Impact)))
                {
                    return true;
                }
                if (node is PresentationProceduralVfxNodeRecord procedural &&
                    advanceKind == PresentationPreviewAdvanceKind.Blocking &&
                    procedural.Recipe != null &&
                    procedural.Recipe.GetLayers(procedural.Cue).Any(layer =>
                        layer != null && layer.BlockingMarker > 0f))
                {
                    return true;
                }

                foreach (PresentationEdgeRecord edge in graph.GetEdgesFrom(nodeId))
                    pending.Push(edge.TargetNodeId);
            }
            return false;
        }
    }
}
#endif
