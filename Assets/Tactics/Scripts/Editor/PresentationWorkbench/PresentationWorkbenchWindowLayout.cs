#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Tactics.Common.Skills.Graph;
using Tactics.Editor.PresentationGraph;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tactics.EditorTools
{
    /// <summary>
    /// Owns the authoring shell around the shared presentation preview stage.
    /// </summary>
    public sealed partial class PresentationWorkbenchWindow
    {
        private const float GraphPaneWidth = 510f;
        private const float InspectorPaneWidth = 300f;
        private const float PaneResizeHandleWidth = 5f;

        private BattlePresentationGraph _sourceGraph;
        private BattlePresentationGraph _graphSandbox;
        private PresentationGraphView _workbenchGraphView;
        private ScrollView _workbenchInspector;
        private ObjectField _workbenchGraphField;
        private EnumField _previewScopeField;
        private IntegerField _previewPhaseField;
        private EnumField _previewCueField;
        private TextField _previewNodeField;
        private PresentationPreviewSurface _previewSurface;
        private PresentationPreviewRenderController _previewRenderController;
        private readonly Dictionary<UnityEngine.Object, UnityEngine.Object> _leafSandboxes = new();
        private readonly Dictionary<UnityEngine.Object, UnityEngine.Object> _leafSourcesBySandbox = new();
        private readonly Dictionary<UnityEngine.Object, UnityEditor.Editor> _leafSandboxEditors = new();
        private readonly HashSet<UnityEngine.Object> _dirtyLeafSandboxes = new();
        private readonly Dictionary<UnityEngine.Object, string> _pendingLeafPaths = new();
        private int _leafPreviewRefreshVersion;
        private bool _selectingTimelineNode;

        private void CreateGUI()
        {
            _previewRenderController?.Dispose();
            _previewRenderController = null;
            rootVisualElement.Clear();
            BuildWorkbenchToolbar();

            var workbenchContent = new VisualElement();
            workbenchContent.style.flexDirection = FlexDirection.Row;
            workbenchContent.style.flexGrow = 1f;
            workbenchContent.style.minHeight = 0f;

            _workbenchGraphView = new PresentationGraphView();
            _workbenchGraphView.NodeSelected += HandleWorkbenchNodeSelected;
            _workbenchGraphView.style.width = GraphPaneWidth;
            _workbenchGraphView.style.minWidth = 320f;
            _workbenchGraphView.style.flexShrink = 0f;
            workbenchContent.Add(_workbenchGraphView);
            workbenchContent.Add(CreatePaneResizeHandle(_workbenchGraphView, 320f, 800f, false));

            workbenchContent.Add(BuildRetainedPreviewWorkspace());

            _workbenchInspector = new ScrollView();
            _workbenchInspector.style.paddingLeft = 8f;
            _workbenchInspector.style.paddingRight = 8f;
            _workbenchInspector.style.width = InspectorPaneWidth;
            _workbenchInspector.style.minWidth = 260f;
            _workbenchInspector.style.flexShrink = 0f;
            workbenchContent.Add(CreatePaneResizeHandle(_workbenchInspector, 260f, 600f, true));
            workbenchContent.Add(_workbenchInspector);
            rootVisualElement.Add(workbenchContent);

            _previewRenderController = new PresentationPreviewRenderController(
                () => EditorApplication.timeSinceStartup,
                () => position.size,
                IsInteractivePreviewPlaying,
                RenderInteractivePreviewFrame,
                _previewSurface);

            SetWorkbenchGraph(_sourceGraph != null ? _sourceGraph : _presentationGraph);
            RequestInteractivePreviewFrame();
        }

        private VisualElement CreatePaneResizeHandle(
            VisualElement pane,
            float minimumWidth,
            float maximumWidth,
            bool invertDelta)
        {
            var handle = new VisualElement
            {
                tooltip = "Drag to resize this pane"
            };
            handle.style.width = PaneResizeHandleWidth;
            handle.style.flexShrink = 0f;
            handle.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 1f);

            int pointerId = -1;
            float pointerStart = 0f;
            float widthStart = 0f;
            bool resizeActive = false;
            handle.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0)
                    return;
                pointerId = evt.pointerId;
                pointerStart = evt.position.x;
                widthStart = pane.resolvedStyle.width;
                handle.CapturePointer(pointerId);
                resizeActive = true;
                _previewRenderController?.BeginResize();
                evt.StopPropagation();
            });
            handle.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (evt.pointerId != pointerId || !handle.HasPointerCapture(pointerId))
                    return;
                float delta = evt.position.x - pointerStart;
                pane.style.width = Mathf.Clamp(
                    widthStart + (invertDelta ? -delta : delta),
                    minimumWidth,
                    maximumWidth);
                evt.StopPropagation();
            });
            handle.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (evt.pointerId != pointerId)
                    return;
                if (handle.HasPointerCapture(pointerId))
                    handle.ReleasePointer(pointerId);
                pointerId = -1;
                if (resizeActive)
                {
                    resizeActive = false;
                    _previewRenderController?.EndResize();
                }
                evt.StopPropagation();
            });
            handle.RegisterCallback<PointerCaptureOutEvent>(_ =>
            {
                pointerId = -1;
                if (!resizeActive)
                    return;
                resizeActive = false;
                _previewRenderController?.EndResize();
            });
            return handle;
        }

        private void BuildWorkbenchToolbar()
        {
            var toolbar = new Toolbar();
            _workbenchGraphField = new ObjectField("Graph")
            {
                objectType = typeof(BattlePresentationGraph),
                allowSceneObjects = false
            };
            _workbenchGraphField.style.minWidth = 360f;
            _workbenchGraphField.RegisterValueChangedCallback(evt =>
                SetWorkbenchGraph(evt.newValue as BattlePresentationGraph));
            toolbar.Add(_workbenchGraphField);
            toolbar.Add(new ToolbarButton(() => ApplyWorkbench()) { text = "Apply All" });
            toolbar.Add(new ToolbarButton(RevertWorkbench) { text = "Revert All" });
            toolbar.Add(new ToolbarButton(ValidateWorkbench) { text = "Validate" });
            toolbar.Add(new ToolbarSpacer());
            _previewScopeField = new EnumField(_previewScope.Kind);
            _previewScopeField.RegisterValueChangedCallback(evt =>
            {
                _previewScope.Kind = (PresentationPreviewScopeKind)evt.newValue;
                RefreshPreviewScope();
            });
            toolbar.Add(_previewScopeField);
            _previewPhaseField = new IntegerField { value = _previewScope.PhaseIndex };
            _previewPhaseField.style.width = 48f;
            _previewPhaseField.RegisterValueChangedCallback(evt =>
            {
                _previewScope.PhaseIndex = Mathf.Max(0, evt.newValue);
                RefreshPreviewScope();
            });
            toolbar.Add(_previewPhaseField);
            _previewCueField = new EnumField(_previewScope.Cue);
            _previewCueField.RegisterValueChangedCallback(evt =>
            {
                _previewScope.Cue = (PresentationCueKind)evt.newValue;
                RefreshPreviewScope();
            });
            toolbar.Add(_previewCueField);
            _previewNodeField = new TextField { value = _previewScope.NodeId };
            _previewNodeField.style.width = 150f;
            _previewNodeField.RegisterValueChangedCallback(evt =>
            {
                _previewScope.NodeId = evt.newValue;
                RefreshPreviewScope();
            });
            toolbar.Add(_previewNodeField);
            rootVisualElement.Add(toolbar);
        }

        private void HandleWorkbenchNodeSelected(PresentationNodeRecord node)
        {
            DrawWorkbenchInspector(node);
            if (_selectingTimelineNode)
                return;
            if (node == null || node is PresentationEntryNodeRecord or PresentationFinishNodeRecord or PresentationJoinNodeRecord)
                return;
            _previewScope.Kind = node is PresentationForkNodeRecord
                ? PresentationPreviewScopeKind.ForkRegion
                : PresentationPreviewScopeKind.Leaf;
            _previewScope.NodeId = node.NodeId;
            _previewScopeField?.SetValueWithoutNotify(_previewScope.Kind);
            _previewNodeField?.SetValueWithoutNotify(node.NodeId);
            RefreshPreviewScope();
        }

        private void SelectWorkbenchTimelineNode(string nodeId)
        {
            _selectingTimelineNode = true;
            try
            {
                _workbenchGraphView?.SelectNode(nodeId);
            }
            finally
            {
                _selectingTimelineNode = false;
            }
        }

        private void RefreshPreviewScope()
        {
            if (_previewUtility == null || _actorInstance == null)
                return;
            RebuildSequence(false);
            RequestInteractivePreviewFrame();
        }

        private void SetWorkbenchGraph(BattlePresentationGraph graph)
        {
            if (graph == _graphSandbox)
                return;

            _workbenchGraphView?.SavePositions();
            ResetLeafSandboxes();
            if (_graphSandbox != null)
                DestroyImmediate(_graphSandbox);

            _sourceGraph = graph;
            _graphSandbox = graph != null ? Instantiate(graph) : null;
            if (_graphSandbox != null)
            {
                _graphSandbox.name = graph.name + " (Workbench Sandbox)";
                _graphSandbox.hideFlags = HideFlags.HideAndDontSave;
            }
            _presentationGraph = _graphSandbox;
            _workbenchGraphField?.SetValueWithoutNotify(graph);
            _workbenchGraphView?.Load(_graphSandbox);
            DrawWorkbenchInspector(null);
            UpdateUnsavedChangesState();

            if (_presentationGraph?.PreviewActorPrefab != null)
                _actorPrefab = _presentationGraph.PreviewActorPrefab;
            if (_presentationGraph?.PreviewTargetPrefab != null)
                _targetPrefab = _presentationGraph.PreviewTargetPrefab;
            if (_previewUtility != null)
                RebuildStage();
        }

        private bool ApplyWorkbench()
        {
            _workbenchGraphView?.SavePositions();
            if (_sourceGraph == null)
            {
                if (!_unitSandboxDirty && !_projectileSandboxDirty)
                    return true;
                Undo.IncrementCurrentGroup();
                int profileUndoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Apply Presentation Profile Sandboxes");
                bool unitWasDirty = _unitSandboxDirty;
                bool projectileWasDirty = _projectileSandboxDirty;
                try
                {
                    ApplyUnitSandbox();
                    ApplyProjectileSandbox();
                    Undo.CollapseUndoOperations(profileUndoGroup);
                    AssetDatabase.SaveAssets();
                }
                catch (Exception exception)
                {
                    Undo.RevertAllDownToGroup(profileUndoGroup);
                    _unitSandboxDirty = unitWasDirty;
                    _projectileSandboxDirty = projectileWasDirty;
                    EditorUtility.DisplayDialog(
                        "Presentation Workbench Apply Failed",
                        exception.Message,
                        "OK");
                    return false;
                }
                UpdateUnsavedChangesState();
                return true;
            }
            if (_sourceGraph != null && _graphSandbox != null)
            {
                bool valid = ValidateWorkbench(showDialog: false, out string message);
                if (!valid && !EditorUtility.DisplayDialog(
                        "Apply Invalid Presentation Draft",
                        message + "\n\nApply this invalid draft anyway?",
                        "Apply Draft",
                        "Cancel"))
                {
                    return false;
                }

                Undo.IncrementCurrentGroup();
                int undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Apply Presentation Workbench");
                var createdFolders = new List<string>();
                try
                {
                    ApplyLeafSandboxes(createdFolders);
                    Undo.RecordObject(_sourceGraph, "Apply Presentation Graph");
                    EditorUtility.CopySerialized(_graphSandbox, _sourceGraph);
                    EditorUtility.SetDirty(_sourceGraph);
                    if (_unitSandboxDirty)
                        ApplyUnitSandbox();
                    if (_projectileSandboxDirty)
                        ApplyProjectileSandbox();
                    Undo.CollapseUndoOperations(undoGroup);
                    AssetDatabase.SaveAssets();
                    SetWorkbenchGraph(_sourceGraph);
                }
                catch (Exception exception)
                {
                    Undo.RevertAllDownToGroup(undoGroup);
                    foreach (string folder in createdFolders
                                 .Distinct(StringComparer.OrdinalIgnoreCase)
                                 .OrderByDescending(value => value.Length))
                    {
                        if (AssetDatabase.IsValidFolder(folder) &&
                            AssetDatabase.FindAssets(string.Empty, new[] { folder }).Length == 0)
                        {
                            AssetDatabase.DeleteAsset(folder);
                        }
                    }
                    SetWorkbenchGraph(_sourceGraph);
                    EditorUtility.DisplayDialog(
                        "Presentation Workbench Apply Failed",
                        exception.Message,
                        "OK");
                    return false;
                }
            }
            UpdateUnsavedChangesState();
            return true;
        }

        private void RevertWorkbench()
        {
            SetWorkbenchGraph(_sourceGraph);
            SetUnitProfile(_unitProfile);
            SetProjectileProfile(_projectileProfile);
            RebuildSequence(false);
            UpdateUnsavedChangesState();
        }

        private void ValidateWorkbench()
        {
            ValidateWorkbench(showDialog: true, out _);
        }

        private bool ValidateWorkbench(bool showDialog, out string message)
        {
            bool graphValid = BattlePresentationGraphValidation.Validate(
                _graphSandbox,
                out List<PresentationGraphDiagnostic> graphErrors);
            bool scenarioValid = PresentationPreviewScenarioValidation.Validate(
                _graphSandbox,
                out List<string> scenarioErrors);
            message = graphValid && scenarioValid
                ? "Presentation graph and preview scenario are valid."
                : string.Join("\n", graphErrors.Select(error =>
                        $"{error.Code} [{error.NodeId ?? "graph"}]: {error.Message}")
                    .Concat(scenarioErrors)
                    .Take(20));
            if (showDialog)
                EditorUtility.DisplayDialog("Presentation Workbench", message, "OK");
            return graphValid && scenarioValid;
        }

        private void DrawWorkbenchInspector(PresentationNodeRecord node)
        {
            if (_workbenchInspector == null)
                return;
            _workbenchInspector.Clear();
            _workbenchInspector.Add(new Label(node == null ? "Graph & Preview Scenario" : node.NodeType.ToString())
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 8f, marginBottom = 8f }
            });
            if (_graphSandbox == null)
            {
                _workbenchInspector.Add(new HelpBox(
                    "Select a BattlePresentationGraph to begin.",
                    HelpBoxMessageType.Info));
                AddPreviewProfileSandboxInspectors();
                return;
            }

            var serializedGraph = new SerializedObject(_graphSandbox);
            if (node == null)
            {
                AddWorkbenchProperty(serializedGraph, "_displayName", "Display Name");
                AddWorkbenchProperty(serializedGraph, "_defaultPreviewEntry", "Legacy Preview Entry");
                AddWorkbenchProperty(serializedGraph, "_previewActorPrefab", "Representative Actor");
                AddWorkbenchProperty(serializedGraph, "_previewTargetPrefab", "Representative Target");
                AddWorkbenchProperty(serializedGraph, "_previewPhases", "Preview Scenario Phases");
                AddPreviewProfileSandboxInspectors();
                return;
            }

            SerializedProperty nodes = serializedGraph.FindProperty("_nodes");
            for (int i = 0; nodes != null && i < nodes.arraySize; i++)
            {
                SerializedProperty item = nodes.GetArrayElementAtIndex(i);
                SerializedProperty id = item.FindPropertyRelative("_nodeId");
                if (id == null || id.stringValue != node.NodeId)
                    continue;
                var property = new PropertyField(item, "Node Properties");
                property.Bind(serializedGraph);
                _workbenchInspector.Add(property);
                AddLeafSandboxInspector(node);
                break;
            }
        }

        private void AddPreviewProfileSandboxInspectors()
        {
            if (_unitSandboxEditor == null && _projectileSandboxEditor == null)
                return;

            var foldout = new Foldout
            {
                text = "Standalone Preview Profile Sandboxes",
                value = false
            };
            foldout.Add(new HelpBox(
                "These compatibility inspectors are low-frequency authoring controls. " +
                "Changes remain in hidden sandboxes until Apply All.",
                HelpBoxMessageType.Info));
            if (_unitSandboxEditor != null)
            {
                foldout.Add(new Label("Unit Profile")
                {
                    style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 6f }
                });
                foldout.Add(new IMGUIContainer(() =>
                {
                    if (_unitSandboxEditor == null)
                        return;
                    EditorGUI.BeginChangeCheck();
                    _unitSandboxEditor.OnInspectorGUI();
                    if (!EditorGUI.EndChangeCheck())
                        return;
                    _unitSandboxDirty = true;
                    UpdateUnsavedChangesState();
                    QueueLeafPreviewRefresh();
                }));
            }
            if (_projectileSandboxEditor != null)
            {
                foldout.Add(new Label("Projectile Profile")
                {
                    style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 6f }
                });
                foldout.Add(new IMGUIContainer(() =>
                {
                    if (_projectileSandboxEditor == null)
                        return;
                    EditorGUI.BeginChangeCheck();
                    _projectileSandboxEditor.OnInspectorGUI();
                    if (!EditorGUI.EndChangeCheck())
                        return;
                    _projectileSandboxDirty = true;
                    UpdateUnsavedChangesState();
                    QueueLeafPreviewRefresh();
                }));
            }
            _workbenchInspector.Add(foldout);
        }

        private void AddLeafSandboxInspector(PresentationNodeRecord node)
        {
            UnityEngine.Object source = GetLeafAsset(node);
            if (source == null)
                return;
            UnityEngine.Object sandbox = _pendingLeafPaths.ContainsKey(source)
                ? source
                : GetOrCreateLeafSandbox(source);
            SetLeafAsset(node, sandbox);
            int referenceCount = CountLeafReferences(source);
            _workbenchInspector.Add(new HelpBox(
                referenceCount > 1
                    ? $"Shared leaf asset: {referenceCount} presentation nodes reference {source.name}. " +
                      "Apply edits only after reviewing all consumers."
                    : $"Leaf asset: {AssetDatabase.GetAssetPath(source)}",
                referenceCount > 1 ? HelpBoxMessageType.Warning : HelpBoxMessageType.Info));
            _workbenchInspector.Add(new Button(() => DuplicateAndRebindLeaf(node, sandbox))
            {
                text = "Duplicate & Rebind Current Node"
            });
            var editorContainer = new IMGUIContainer(() =>
            {
                if (!_leafSandboxEditors.TryGetValue(sandbox, out UnityEditor.Editor editor) || editor == null)
                    return;
                EditorGUI.BeginChangeCheck();
                editor.OnInspectorGUI();
                if (!EditorGUI.EndChangeCheck())
                    return;
                _dirtyLeafSandboxes.Add(sandbox);
                UpdateUnsavedChangesState();
                QueueLeafPreviewRefresh();
            });
            _workbenchInspector.Add(editorContainer);
        }

        private UnityEngine.Object GetOrCreateLeafSandbox(UnityEngine.Object source)
        {
            if (_leafSandboxes.TryGetValue(source, out UnityEngine.Object existing) && existing != null)
                return existing;
            UnityEngine.Object sandbox = Instantiate(source);
            sandbox.name = source.name + " (Workbench Sandbox)";
            sandbox.hideFlags = HideFlags.HideAndDontSave;
            _leafSandboxes[source] = sandbox;
            _leafSourcesBySandbox[sandbox] = source;
            _leafSandboxEditors[sandbox] = UnityEditor.Editor.CreateEditor(sandbox);
            return sandbox;
        }

        private void ApplyLeafSandboxes(ICollection<string> createdFolders)
        {
            foreach ((UnityEngine.Object sandbox, string path) in _pendingLeafPaths.ToList())
            {
                EnsureWorkbenchAssetFolder(path, createdFolders);
                sandbox.hideFlags = HideFlags.None;
                AssetDatabase.CreateAsset(sandbox, path);
                Undo.RegisterCreatedObjectUndo(sandbox, "Create Presentation Leaf Asset");
                EditorUtility.SetDirty(sandbox);
                _pendingLeafPaths.Remove(sandbox);
                _dirtyLeafSandboxes.Remove(sandbox);
            }
            foreach (UnityEngine.Object sandbox in _dirtyLeafSandboxes.ToList())
            {
                if (sandbox == null || !_leafSourcesBySandbox.TryGetValue(sandbox, out UnityEngine.Object source) ||
                    source == null)
                    continue;
                Undo.RecordObject(source, "Apply Presentation Leaf Asset");
                EditorUtility.CopySerialized(sandbox, source);
                EditorUtility.SetDirty(source);
            }
            RestorePersistentLeafReferences();
            _dirtyLeafSandboxes.Clear();
        }

        private void DuplicateAndRebindLeaf(PresentationNodeRecord node, UnityEngine.Object current)
        {
            string sourcePath = _leafSourcesBySandbox.TryGetValue(current, out UnityEngine.Object source)
                ? AssetDatabase.GetAssetPath(source)
                : AssetDatabase.GetAssetPath(current);
            string folder = string.IsNullOrEmpty(sourcePath)
                ? "Assets/Tactics/Arts/PureRun"
                : System.IO.Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');
            string path = EditorUtility.SaveFilePanelInProject(
                "Duplicate Presentation Leaf Asset",
                current.name.Replace(" (Workbench Sandbox)", string.Empty) + "Copy",
                "asset",
                "The asset is created only when Apply All succeeds.",
                folder);
            if (string.IsNullOrEmpty(path))
                return;
            UnityEngine.Object duplicate = Instantiate(current);
            duplicate.name = System.IO.Path.GetFileNameWithoutExtension(path);
            duplicate.hideFlags = HideFlags.HideAndDontSave;
            _pendingLeafPaths[duplicate] = path;
            _dirtyLeafSandboxes.Add(duplicate);
            _leafSandboxEditors[duplicate] = UnityEditor.Editor.CreateEditor(duplicate);
            SetLeafAsset(node, duplicate);
            DrawWorkbenchInspector(node);
            UpdateUnsavedChangesState();
            QueueLeafPreviewRefresh();
        }

        private static void EnsureWorkbenchAssetFolder(string assetPath, ICollection<string> createdFolders)
        {
            string folder = System.IO.Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder))
                return;
            string current = "Assets";
            foreach (string part in folder.Split('/').Skip(1))
            {
                string next = current + "/" + part;
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, part);
                    createdFolders?.Add(next);
                }
                current = next;
            }
        }

        private void RestorePersistentLeafReferences()
        {
            if (_graphSandbox == null)
                return;
            foreach (PresentationNodeRecord node in _graphSandbox.Nodes)
            {
                UnityEngine.Object current = GetLeafAsset(node);
                if (current != null && _leafSourcesBySandbox.TryGetValue(current, out UnityEngine.Object source))
                    SetLeafAsset(node, source);
            }
        }

        private static UnityEngine.Object GetLeafAsset(PresentationNodeRecord node)
        {
            return node switch
            {
                PresentationProjectileNodeRecord projectile => projectile.Profile,
                PresentationPrefabFxNodeRecord prefabFx => prefabFx.Profile,
                PresentationProceduralVfxNodeRecord procedural => procedural.Recipe,
                _ => null
            };
        }

        private static void SetLeafAsset(PresentationNodeRecord node, UnityEngine.Object asset)
        {
            switch (node)
            {
                case PresentationProjectileNodeRecord projectile:
                    projectile.Profile = asset as ProjectileVisualProfile;
                    break;
                case PresentationPrefabFxNodeRecord prefabFx:
                    prefabFx.Profile = asset as VisualCueProfile;
                    break;
                case PresentationProceduralVfxNodeRecord procedural:
                    procedural.Recipe = asset as SkillVfxRecipe;
                    break;
            }
        }

        private static int CountLeafReferences(UnityEngine.Object asset)
        {
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:BattlePresentationGraph"))
            {
                BattlePresentationGraph graph = AssetDatabase.LoadAssetAtPath<BattlePresentationGraph>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (graph != null)
                    count += graph.Nodes.Count(node => GetLeafAsset(node) == asset);
            }
            return count;
        }

        private void QueueLeafPreviewRefresh()
        {
            int refreshVersion = ++_leafPreviewRefreshVersion;
            if (_previewSurface == null)
            {
                RefreshLeafPreview();
                return;
            }
            _previewSurface.schedule.Execute(() =>
            {
                if (refreshVersion != _leafPreviewRefreshVersion)
                    return;
                RefreshLeafPreview();
            }).StartingIn(100);
        }

        private void RefreshLeafPreview()
        {
            if (this == null || _previewUtility == null)
                return;
            RebuildSequence(false);
            RequestInteractivePreviewFrame();
        }

        private void AddWorkbenchProperty(SerializedObject serializedGraph, string name, string label)
        {
            SerializedProperty property = serializedGraph.FindProperty(name);
            if (property == null)
                return;
            var field = new PropertyField(property, label);
            field.Bind(serializedGraph);
            _workbenchInspector.Add(field);
        }

        private void DisposeWorkbenchSession()
        {
            _leafPreviewRefreshVersion++;
            _workbenchGraphView?.SavePositions();
            if (_graphSandbox != null)
                DestroyImmediate(_graphSandbox);
            _graphSandbox = null;
            _presentationGraph = null;
            ResetLeafSandboxes();
        }

        private void ResetLeafSandboxes()
        {
            foreach (UnityEditor.Editor editor in _leafSandboxEditors.Values)
                if (editor != null)
                    DestroyImmediate(editor);
            foreach (UnityEngine.Object sandbox in _leafSandboxes.Values)
                if (sandbox != null)
                    DestroyImmediate(sandbox);
            foreach (UnityEngine.Object sandbox in _pendingLeafPaths.Keys.ToList())
                if (sandbox != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(sandbox)))
                    DestroyImmediate(sandbox);
            _leafSandboxEditors.Clear();
            _leafSandboxes.Clear();
            _leafSourcesBySandbox.Clear();
            _dirtyLeafSandboxes.Clear();
            _pendingLeafPaths.Clear();
        }
    }
}
#endif
