#if TOOLS
using Godot;
using Tactics.Application.Presentation;
using Tactics.Core.Presentation;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

[Tool]
public partial class TacticsGraphWorkbench : VBoxContainer
{
    private const string PresentationPath = "res://content/poison_spear/PoisonSpearPresentationLv1.tres";
    private const int PreviewWidth = 640;
    private const int PreviewHeight = 180;
    private EditorUndoRedoManager? _undoRedo;
    private Button? _toggleNodeButton;
    private Button? _autoLayoutButton;
    private Button? _togglePreviewButton;
    private Button? _savePresentationButton;
    private SplitContainer? _split;
    private GraphEdit? _graph;
    private Control? _previewPane;
    private SubViewportContainer? _previewContainer;
    private SubViewport? _preview;
    private PoisonSpearPreviewCanvas? _previewCanvas;
    private Resource? _presentation;
    private string[] _presentationNodeIds = Array.Empty<string>();
    private string[] _presentationNodeTypes = Array.Empty<string>();
    private string[] _presentationNodeKinds = Array.Empty<string>();
    private string[] _presentationNodeCues = Array.Empty<string>();
    private string[] _presentationNodeTitles = Array.Empty<string>();
    private string[] _presentationNodeChildren = Array.Empty<string>();
    private int[] _presentationNodeEnabled = Array.Empty<int>();
    private Vector2[] _presentationNodePositions = Array.Empty<Vector2>();
    private readonly Dictionary<string, GraphNode> _graphNodes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GraphElement.DraggedEventHandler> _dragHandlers = new(StringComparer.Ordinal);
    private bool _suppressGraphSignals;
    private bool _initialized;

    public void Configure(EditorUndoRedoManager undoRedo)
    {
        _undoRedo = undoRedo ?? throw new ArgumentNullException(nameof(undoRedo));
    }

    public override void _Ready() => CallDeferred(nameof(InitializeWorkbench));

    public void InitializeWorkbench()
    {
        if (_initialized || !IsInsideTree())
            return;

        _initialized = true;
        try
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            SizeFlagsVertical = SizeFlags.ExpandFill;
            CustomMinimumSize = new Vector2(960, 520);
            WorkbenchUi.StylePage(this);

            var toolbar = WorkbenchUi.Toolbar(this);
            toolbar.AddChild(new Label { Text = "POISON SPEAR GRAPH" });
            _toggleNodeButton = new Button { Text = "Toggle First Presentation Leaf (Undoable)" };
            _toggleNodeButton.Pressed += ToggleFirstLeaf;
            toolbar.AddChild(_toggleNodeButton);

            _autoLayoutButton = new Button { Text = "Auto Layout (Undoable)" };
            _autoLayoutButton.Pressed += ApplyAutoLayout;
            toolbar.AddChild(_autoLayoutButton);

            _togglePreviewButton = new Button { Text = "Hide Preview" };
            _togglePreviewButton.Pressed += TogglePreviewVisibility;
            toolbar.AddChild(_togglePreviewButton);

            _savePresentationButton = new Button { Text = "Save Poison Spear Presentation" };
            _savePresentationButton.Pressed += SavePresentation;
            toolbar.AddChild(_savePresentationButton);
            AddChild(toolbar);

            Resource? rawPresentation = ResourceLoader.Load(PresentationPath);
            _presentation = rawPresentation;
            if (_presentation is null)
                GD.PushError($"[Tactics Tooling] Presentation resource type is unavailable: {PresentationPath}");
            else
            {
                PoisonSpearPresentationEditorService.ValidateStoredRevision(_presentation);
                RefreshPresentationSnapshot();
                GD.Print($"[Tactics Tooling] Presentation loaded: {PresentationPath} (nodes={_presentationNodeIds.Length}).");
            }

            _split = new HSplitContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            _graph = new GraphEdit
            {
                CustomMinimumSize = new Vector2(560, 420),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                SizeFlagsStretchRatio = 64f,
                ShowArrangeButton = false
            };
            WorkbenchUi.StyleGraph(_graph);
            _split.AddChild(_graph);

            var previewPane = new VBoxContainer
            {
                CustomMinimumSize = new Vector2(360, 420),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                SizeFlagsStretchRatio = 36f
            };
            previewPane.AddChild(new Label { Text = "Poison Spear SubViewport Preview" });
            _previewPane = previewPane;
            AspectRatioContainer previewFrame = CreatePreviewFrame();
            _previewContainer = new SubViewportContainer
            {
                Stretch = true,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            _preview = new SubViewport
            {
                Size = new Vector2I(PreviewWidth, PreviewHeight),
                Size2DOverride = new Vector2I(PreviewWidth, PreviewHeight),
                Size2DOverrideStretch = true,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always
            };
            _previewCanvas = new PoisonSpearPreviewCanvas();
            if (_presentation is not null)
                RefreshPreviewPlan();
            _preview.AddChild(_previewCanvas);
            _previewContainer.AddChild(_preview);
            previewFrame.AddChild(_previewContainer);
            previewPane.AddChild(previewFrame);
            _split.AddChild(previewPane);
            AddChild(_split);
            SyncGraphNodes();
            GD.Print("[Tactics Tooling] GraphEdit and SubViewport workbench ready.");
        }
        catch (Exception exception)
        {
            GD.PushError($"[Tactics Tooling] Workbench failed to initialize: {exception}");
        }
    }

    public override void _ExitTree()
    {
        _initialized = false;
        _dragHandlers.Clear();
        _preview?.QueueFree();
        _preview = null;
        _previewCanvas = null;
        _previewContainer = null;
        _previewPane = null;
        _split = null;
        _graph = null;
        _presentation = null;
        _presentationNodeIds = Array.Empty<string>();
        _presentationNodeTypes = Array.Empty<string>();
        _presentationNodeKinds = Array.Empty<string>();
        _presentationNodeCues = Array.Empty<string>();
        _presentationNodeTitles = Array.Empty<string>();
        _presentationNodeChildren = Array.Empty<string>();
        _presentationNodeEnabled = Array.Empty<int>();
        _presentationNodePositions = Array.Empty<Vector2>();
        _graphNodes.Clear();
        _dragHandlers.Clear();
    }

    private void ToggleFirstLeaf()
    {
        if (_undoRedo is null || _presentation is null)
        {
            GD.PushError("[Tactics Tooling] Presentation or UndoRedo manager is not configured.");
            return;
        }

        string[] kinds = _presentation.Get("AuthoringNodeKinds").AsStringArray();
        int index = Array.FindIndex(kinds, kind => string.Equals(kind, "leaf", StringComparison.Ordinal));
        if (index < 0)
        {
            GD.PushError("[Tactics Tooling] Presentation has no editable leaf node.");
            return;
        }

        string nodeId = _presentationNodeIds[index];
        bool currentEnabled = _presentationNodeEnabled[index] != 0;
        _undoRedo.CreateAction("Toggle Poison Spear Presentation Node", UndoRedo.MergeMode.Disable, _presentation);
        _undoRedo.AddDoMethod(this, MethodName.ApplyNodeEnabledUndoable, nodeId, !currentEnabled);
        _undoRedo.AddUndoMethod(this, MethodName.ApplyNodeEnabledUndoable, nodeId, currentEnabled);
        _undoRedo.CommitAction();
    }

    public void ApplyNodeEnabledUndoable(string nodeId, bool enabled)
    {
        if (_presentation is null)
        {
            GD.PushError("[Tactics Tooling] Cannot apply ChangeSet without a presentation resource.");
            return;
        }

        PresentationGraphDocument current = PoisonSpearPresentationEditorService.Read(_presentation);
        var changeSet = new PresentationGraphChangeSet(
            $"editor.set-enabled.{nodeId}.{(enabled ? 1 : 0)}",
            current.Revision,
            new[] { new SetPresentationNodeEnabledOperation(nodeId, enabled) });
        PresentationGraphMutationResult result = PoisonSpearPresentationEditorService.Apply(_presentation, changeSet);
        if (!result.Succeeded)
        {
            GD.PushError($"[Tactics Tooling] ChangeSet rejected: {string.Join("; ", result.Diagnostics.Select(item => $"{item.Code}: {item.Message}"))}");
            return;
        }

        EditorInterface.Singleton.SetObjectEdited(_presentation, true);
        RefreshPresentationSnapshot();
        SyncGraphNodes();
        RefreshPreviewPlan();
        GD.Print($"[Tactics Tooling] ChangeSet applied: node={nodeId}, enabled={enabled}, revision={result.Document.Revision}.");
    }

    private void ApplyAutoLayout()
    {
        if (_undoRedo is null || _presentation is null)
        {
            GD.PushError("[Tactics Tooling] Presentation or UndoRedo manager is not configured.");
            return;
        }

        PresentationGraphDocument current = PoisonSpearPresentationEditorService.Read(_presentation);
        IReadOnlyDictionary<string, PresentationNodePosition> arranged =
            new PresentationGraphLayoutService().Arrange(current);
        string[] nodeIds = current.Nodes.Select(node => node.NodeId).ToArray();
        Vector2[] from = current.Nodes
            .Select(node => new Vector2(node.Position.X, node.Position.Y))
            .ToArray();
        Vector2[] to = current.Nodes
            .Select(node => arranged[node.NodeId])
            .Select(position => new Vector2(position.X, position.Y))
            .ToArray();
        if (from.SequenceEqual(to))
            return;

        _undoRedo.CreateAction("Auto Layout Poison Spear Presentation", UndoRedo.MergeMode.Disable, _presentation);
        _undoRedo.AddDoMethod(this, MethodName.ApplyNodePositionsUndoable, nodeIds, to);
        _undoRedo.AddUndoMethod(this, MethodName.ApplyNodePositionsUndoable, nodeIds, from);
        _undoRedo.CommitAction();
    }

    public void ApplyNodePositionUndoable(string nodeId, Vector2 position) =>
        ApplyNodePositionsUndoable(new[] { nodeId }, new[] { position });

    public void ApplyNodePositionsUndoable(string[] nodeIds, Vector2[] positions)
    {
        if (_presentation is null)
        {
            GD.PushError("[Tactics Tooling] Cannot move nodes without a presentation resource.");
            return;
        }
        if (nodeIds.Length != positions.Length || nodeIds.Length == 0)
        {
            GD.PushError("[Tactics Tooling] Node position mutation requires matching non-empty arrays.");
            return;
        }

        PresentationGraphDocument current = PoisonSpearPresentationEditorService.Read(_presentation);
        var changeSet = new PresentationGraphChangeSet(
            $"editor.set-position.{string.Join('.', nodeIds)}",
            current.Revision,
            nodeIds.Select((nodeId, index) =>
                (PresentationGraphOperation)new SetPresentationNodePositionOperation(
                    nodeId,
                    new PresentationNodePosition(positions[index].X, positions[index].Y))));
        PresentationGraphMutationResult result = PoisonSpearPresentationEditorService.Apply(_presentation, changeSet);
        if (!result.Succeeded)
        {
            GD.PushError($"[Tactics Tooling] Position ChangeSet rejected: {string.Join("; ", result.Diagnostics.Select(item => $"{item.Code}: {item.Message}"))}");
            SyncGraphNodes();
            return;
        }

        EditorInterface.Singleton.SetObjectEdited(_presentation, true);
        RefreshPresentationSnapshot();
        SyncGraphNodes();
        GD.Print($"[Tactics Tooling] Position ChangeSet applied: nodes={nodeIds.Length}, revision={result.Document.Revision}.");
    }

    private void OnGraphNodeDragged(string nodeId, Vector2 from, Vector2 to)
    {
        if (_suppressGraphSignals || _undoRedo is null || _presentation is null || from.IsEqualApprox(to))
            return;

        if (_graphNodes.TryGetValue(nodeId, out GraphNode? node))
        {
            _suppressGraphSignals = true;
            node.PositionOffset = from;
            _suppressGraphSignals = false;
        }

        _undoRedo.CreateAction("Move Poison Spear Presentation Node", UndoRedo.MergeMode.Disable, _presentation);
        _undoRedo.AddDoMethod(this, MethodName.ApplyNodePositionUndoable, nodeId, to);
        _undoRedo.AddUndoMethod(this, MethodName.ApplyNodePositionUndoable, nodeId, from);
        _undoRedo.CommitAction();
    }

    public void SyncGraphNodes()
    {
        if (_graph is null)
            return;

        if (_presentation is not null)
        {
            var expectedIds = _presentationNodeIds.ToHashSet(StringComparer.Ordinal);
            foreach (string staleId in _graphNodes.Keys.Where(id => !expectedIds.Contains(id)).ToArray())
                RemoveGraphNode(staleId);

            Vector2 scrollOffset = _graph.ScrollOffset;
            _suppressGraphSignals = true;
            for (int index = 0; index < _presentationNodeIds.Length; index++)
            {
                string nodeId = _presentationNodeIds[index];
                GraphNode node = _graphNodes.TryGetValue(nodeId, out GraphNode? existing)
                    ? existing
                    : AddGraphNode(nodeId);
                UpdateGraphNode(node, index);
            }
            _suppressGraphSignals = false;

            _graph.ClearConnections();
            for (int index = 0; index < _presentationNodeIds.Length && index < _presentationNodeChildren.Length; index++)
            {
                string parentId = _presentationNodeIds[index];
                foreach (string childId in _presentationNodeChildren[index].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (_graphNodes.ContainsKey(childId))
                        _graph.ConnectNode(GraphNodeName(parentId), 0, GraphNodeName(childId), 0, true);
                }
            }
            _graph.ScrollOffset = scrollOffset;
        }
    }

    private GraphNode AddGraphNode(string nodeId)
    {
        if (_graph is null)
            throw new InvalidOperationException("GraphEdit is not initialized.");

        var node = new GraphNode
        {
            Name = GraphNodeName(nodeId)
        };
        node.AddChild(new Label());
        GraphElement.DraggedEventHandler handler = (from, to) => OnGraphNodeDragged(nodeId, from, to);
        node.Dragged += handler;
        _dragHandlers[nodeId] = handler;
        _graph.AddChild(node);
        _graphNodes[nodeId] = node;
        return node;
    }

    private void UpdateGraphNode(GraphNode node, int index)
    {
        bool enabled = _presentationNodeEnabled[index] != 0;
        node.Title = _presentationNodeTitles[index];
        node.TooltipText =
            $"Stable ID: {_presentationNodeIds[index]}\n" +
            $"Type: {_presentationNodeTypes[index]}\n" +
            $"Kind: {_presentationNodeKinds[index]}\n" +
            $"Cue: {(string.IsNullOrWhiteSpace(_presentationNodeCues[index]) ? "(none)" : _presentationNodeCues[index])}\n" +
            $"State: {(enabled ? "enabled" : "disabled")}";
        node.PositionOffset = _presentationNodePositions[index];
        if (node.GetChildCount() > 0 && node.GetChild(0) is Label label)
            label.Text = $"{FriendlyType(_presentationNodeTypes[index])} · {(enabled ? "enabled" : "disabled")}";
        Color color = NodeColor(_presentationNodeTypes[index], enabled);
        node.SetSlot(0, true, 0, color, true, 0, color, null, null, true);
        WorkbenchUi.StyleGraphNode(node, color, enabled);
    }

    private void RemoveGraphNode(string nodeId)
    {
        if (!_graphNodes.Remove(nodeId, out GraphNode? node))
            return;
        if (_dragHandlers.Remove(nodeId, out GraphElement.DraggedEventHandler? handler))
            node.Dragged -= handler;
        node.Free();
    }

    private void DisconnectGraphNodeSignals()
    {
        foreach ((string nodeId, GraphNode node) in _graphNodes)
        {
            if (_dragHandlers.TryGetValue(nodeId, out GraphElement.DraggedEventHandler? handler))
                node.Dragged -= handler;
        }
    }

    private static string GraphNodeName(string nodeId) => nodeId.Replace('.', '_');

    private static Color NodeColor(string nodeType, bool enabled)
    {
        Color color;
        if (nodeType.StartsWith("sequence", StringComparison.Ordinal) ||
            nodeType.StartsWith("PresentationEntryNodeRecord", StringComparison.Ordinal))
            color = new Color(0.35f, 0.55f, 0.95f);
        else if (nodeType.StartsWith("PresentationUnitTweenNodeRecord", StringComparison.Ordinal))
            color = new Color(0.45f, 0.7f, 1f);
        else if (nodeType.StartsWith("PresentationProjectileNodeRecord", StringComparison.Ordinal))
            color = new Color(0.25f, 0.9f, 0.7f);
        else if (nodeType.StartsWith("PresentationFinishNodeRecord", StringComparison.Ordinal))
            color = new Color(0.95f, 0.55f, 0.2f);
        else
            color = Colors.Gray;
        return enabled ? color : color.Darkened(0.55f);
    }

    private static string FriendlyType(string nodeType) => nodeType switch
    {
        "PresentationEntryNodeRecord" => "Entry",
        "PresentationUnitTweenNodeRecord" => "Ranged Tween",
        "PresentationProjectileNodeRecord" => "Projectile",
        "PresentationFinishNodeRecord" => "Finish",
        _ => nodeType
            .Replace("Presentation", string.Empty, StringComparison.Ordinal)
            .Replace("NodeRecord", string.Empty, StringComparison.Ordinal)
    };

    internal static AspectRatioContainer CreatePreviewFrame() => new()
    {
        Ratio = PreviewWidth / (float)PreviewHeight,
        StretchMode = AspectRatioContainer.StretchModeEnum.Fit,
        AlignmentHorizontal = AspectRatioContainer.AlignmentMode.Center,
        AlignmentVertical = AspectRatioContainer.AlignmentMode.Center,
        ClipContents = true,
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
        SizeFlagsVertical = SizeFlags.ExpandFill
    };

    private void TogglePreviewVisibility()
    {
        if (_previewPane is null || _togglePreviewButton is null)
            return;
        _previewPane.Visible = !_previewPane.Visible;
        _togglePreviewButton.Text = _previewPane.Visible ? "Hide Preview" : "Show Preview";
    }

    private void SavePresentation()
    {
        if (_presentation is null)
        {
            GD.PushError($"[Tactics Tooling] Cannot save missing presentation: {PresentationPath}");
            return;
        }

        try
        {
            PoisonSpearPresentationEditorService.SaveWithRollback(_presentation, PresentationPath);
            EditorInterface.Singleton.SetObjectEdited(_presentation, false);
            GD.Print($"[Tactics Tooling] Saved Poison Spear presentation: {PresentationPath}");
        }
        catch (Exception exception)
        {
            Resource? restored = ResourceLoader.Load<Resource>(
                PresentationPath,
                string.Empty,
                ResourceLoader.CacheMode.Ignore);
            if (restored is not null)
            {
                _presentation = restored;
                RefreshPresentationSnapshot();
                SyncGraphNodes();
                RefreshPreviewPlan();
            }
            GD.PushError($"[Tactics Tooling] Failed to save presentation and restored prior bytes: {exception}");
        }
    }

    private void RefreshPresentationSnapshot()
    {
        if (_presentation is null)
            return;
        _presentationNodeIds = _presentation.Get("AuthoringNodeIds").AsStringArray();
        _presentationNodeTypes = _presentation.Get("AuthoringNodeTypes").AsStringArray();
        _presentationNodeKinds = _presentation.Get("AuthoringNodeKinds").AsStringArray();
        _presentationNodeCues = _presentation.Get("AuthoringNodeCues").AsStringArray();
        _presentationNodeEnabled = _presentation.Get("AuthoringNodeEnabled").AsInt32Array();
        _presentationNodePositions = _presentation.Get("AuthoringNodePositions").AsVector2Array();
        string[] edgeSources = _presentation.Get("EdgeSources").AsStringArray();
        string[] edgeTargets = _presentation.Get("EdgeTargets").AsStringArray();
        var children = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        for (int index = 0; index < edgeSources.Length && index < edgeTargets.Length; index++)
        {
            if (!children.TryGetValue(edgeSources[index], out List<string>? targets))
            {
                targets = new List<string>();
                children[edgeSources[index]] = targets;
            }
            targets.Add(edgeTargets[index]);
        }
        _presentationNodeChildren = _presentationNodeIds
            .Select(id => children.TryGetValue(id, out List<string>? targets)
                ? string.Join(',', targets)
                : string.Empty)
            .ToArray();
        IReadOnlyDictionary<string, string> titles = new PresentationGraphTitleService()
            .CreateTitles(PoisonSpearPresentationEditorService.Read(_presentation));
        _presentationNodeTitles = _presentationNodeIds.Select(nodeId => titles[nodeId]).ToArray();
        if (_toggleNodeButton is not null)
        {
            _toggleNodeButton.TooltipText =
                $"Current normalized revision: {_presentation.Get("Revision").AsString()}";
        }
    }

    private void RefreshPreviewPlan()
    {
        if (_presentation is null || _previewCanvas is null)
            return;
        PresentationExecutionPlan previewPlan = PoisonSpearPresentationResource.BuildExecutionPlan(
            _presentation.Get("SchemaVersion").AsInt32(),
            _presentation.Get("PlanRootNodeId").AsString(),
            _presentation.Get("NodeIds").AsStringArray(),
            _presentation.Get("NodeTypes").AsStringArray(),
            _presentation.Get("NodeChildren").AsStringArray());
        _previewCanvas.Configure(previewPlan);
    }

}
#endif
