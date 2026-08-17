#if TOOLS
using Godot;
using Tactics.Application.Authoring;
using Tactics.Core.Runs;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

[Tool]
public partial class PureRunMapWorkbench : VBoxContainer, IAuthoringWorkspaceParticipant
{
    private readonly TacticsAuthoringEditorService _authoring = new();
    public const string MapPath = "res://content/map/PureRunDefaultMap.tres";
    private const string MapScriptPath = "res://src/Tactics.Godot.Adapter/Runtime/PureRunMapResource.cs";
    private EditorUndoRedoManager? _undoRedo;
    private PureRunMapResource? _map;
    private AuthoringSession<MapAuthoringDocument>? _session;
    private GraphEdit? _graph;
    private OptionButton? _nodePicker;
    private OptionButton? _connectionTarget;
    private OptionButton? _kind;
    private LineEdit? _title;
    private LineEdit? _contentId;
    private SpinBox? _layer;
    private SpinBox? _lane;
    private TextEdit? _json;
    private Label? _status;
    private bool _initialized;
    private bool _suppressGraphSignals;
    private int _mapLoadAttempts;
    private readonly Dictionary<string, GraphNode> _graphNodes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GraphElement.DraggedEventHandler> _dragHandlers = new(StringComparer.Ordinal);

    public void Configure(EditorUndoRedoManager undoRedo) => _undoRedo = undoRedo;
    public string WorkspaceName => "Map";
    public IReadOnlyList<AuthoringDocumentChange> CaptureWorkspaceChanges() => _session is { IsDirty: true }
        ? [new AuthoringDocumentChange(AuthoringDocumentKind.Map, _session.Draft.ContentId, _session.ExpectedRevision, MapAuthoringJson.Serialize(_session.Draft))]
        : Array.Empty<AuthoringDocumentChange>();
    public void ValidateWorkspaceDraft() { if (_session is not null) ValidateDocument(_session.Draft); }
    public void RevertWorkspaceDraft() => RevertAll();
    public void ReloadWorkspaceDocuments() => LoadMap();
    public override void _Ready() => CallDeferred(nameof(InitializeWorkbench));

    public void InitializeWorkbench()
    {
        if (_initialized || !IsInsideTree()) return;
        _initialized = true;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;

        var toolbar = new HBoxContainer();
        _nodePicker = new OptionButton { CustomMinimumSize = new Vector2(170, 0) };
        _nodePicker.ItemSelected += SelectNode;
        toolbar.AddChild(_nodePicker);
        AddButton(toolbar, "Add", AddNode);
        AddButton(toolbar, "Delete", DeleteNode);
        _connectionTarget = new OptionButton { CustomMinimumSize = new Vector2(150, 0) };
        toolbar.AddChild(_connectionTarget);
        AddButton(toolbar, "Connect", AddConnection);
        AddButton(toolbar, "Disconnect", RemoveConnection);
        AddButton(toolbar, "Auto Layout", AutoLayout);
        AddButton(toolbar, "Validate", ValidateDraft);
        AddButton(toolbar, "Revert All", RevertAll);
        AddChild(toolbar);

        var inspector = new HBoxContainer();
        inspector.AddChild(new Label { Text = "Title" });
        _title = new LineEdit { CustomMinimumSize = new Vector2(150, 0) };
        inspector.AddChild(_title);
        inspector.AddChild(new Label { Text = "Kind" });
        _kind = new OptionButton { CustomMinimumSize = new Vector2(110, 0) };
        foreach (string name in Enum.GetNames<PureRunNodeKind>()) _kind.AddItem(name);
        inspector.AddChild(_kind);
        inspector.AddChild(new Label { Text = "Layer" });
        _layer = new SpinBox { MinValue = 0, MaxValue = 20, Step = 1, CustomMinimumSize = new Vector2(70, 0) };
        inspector.AddChild(_layer);
        inspector.AddChild(new Label { Text = "Lane" });
        _lane = new SpinBox { MinValue = -10, MaxValue = 10, Step = 0.25, CustomMinimumSize = new Vector2(75, 0) };
        inspector.AddChild(_lane);
        inspector.AddChild(new Label { Text = "ContentId" });
        _contentId = new LineEdit { CustomMinimumSize = new Vector2(240, 0) };
        inspector.AddChild(_contentId);
        AddButton(inspector, "Update Draft", UpdateSelectedNode);
        AddChild(inspector);

        _status = new Label { Text = "Loading authoritative map..." };
        AddChild(_status);
        var split = new VSplitContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        _graph = new GraphEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        split.AddChild(_graph);
        var jsonPanel = new VBoxContainer { CustomMinimumSize = new Vector2(0, 150) };
        var jsonToolbar = new HBoxContainer();
        AddButton(jsonToolbar, "Export Snapshot", ExportSnapshot);
        AddButton(jsonToolbar, "Import To Draft", ImportSnapshot);
        jsonPanel.AddChild(jsonToolbar);
        _json = new TextEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        jsonPanel.AddChild(_json);
        split.AddChild(jsonPanel);
        AddChild(split);
        LoadMap();
    }

    public override void _ExitTree()
    {
        DisconnectGraphSignals();
        _initialized = false;
        _mapLoadAttempts = 0;
        if (_nodePicker is not null) _nodePicker.ItemSelected -= SelectNode;
        _session = null;
        _map = null;
    }

    public static void ValidateResource(PureRunMapResource resource) =>
        ValidateDocument(MapAuthoringEditorService.Read(resource));

    public static void ValidateDocument(MapAuthoringDocument document)
    {
        MapAuthoringValidator.ValidateOrThrow(document);
        _ = document.ToCoreDefinition();
    }

    public void LoadMap()
    {
        EditorResourceLoadResult<PureRunMapResource> result = ReloadSafeEditorResourceLoader.Load<PureRunMapResource>(
            MapPath, MapScriptPath, "ContentIdValue", "NodeIds", "NodeLayers", "NodeKinds", "NodeContentIds",
            "NodeTitles", "NodeLanes", "ConnectionFromNodeIds", "ConnectionToNodeIds");
        if (ReloadSafeEditorResourceLoader.RetryDeferred(this, MethodName.LoadMap, ref _mapLoadAttempts, result, "Pure Run map workbench"))
            return;
        _map = result.Resource!;
        MapAuthoringDocument document = MapAuthoringEditorService.Read(_map);
        ValidateDocument(document);
        _session = new AuthoringSession<MapAuthoringDocument>(AuthoringDocumentKind.Map, document);
        RefreshFromDraft();
        SetStatus($"Loaded {document.Nodes.Count} nodes / {document.Connections.Count} connections.");
    }

    private static void AddButton(Container parent, string text, Action action)
    {
        var button = new Button { Text = text };
        button.Pressed += action;
        parent.AddChild(button);
    }

    private void SelectNode(long index)
    {
        if (_session is null || index < 0 || index >= _session.Draft.Nodes.Count) return;
        MapAuthoringNode node = _session.Draft.Nodes[(int)index];
        _title!.Text = node.Title;
        _kind!.Select((int)node.Kind);
        _layer!.Value = node.Layer;
        _lane!.Value = node.Lane;
        _contentId!.Text = node.ContentId;
    }

    private void AddNode()
    {
        if (_session is null) return;
        int suffix = 1;
        string id;
        do id = $"node-{suffix++}"; while (_session.Draft.Nodes.Any(value => value.NodeId == id));
        int layer = _session.Draft.Nodes.Max(value => value.Layer);
        var node = new MapAuthoringNode(id, layer, PureRunNodeKind.Battle, "encounter.pure-run.n1", id, 0);
        ApplyDraftOperations("add-map-node", new AddMapNodeOperation(node));
        SelectNode(_session.Draft.Nodes.Count - 1);
    }

    private void DeleteNode()
    {
        if (_session is null || _nodePicker is null || _nodePicker.Selected < 0) return;
        ApplyDraftOperations("remove-map-node", new RemoveMapNodeOperation(_session.Draft.Nodes[_nodePicker.Selected].NodeId));
    }

    private void UpdateSelectedNode()
    {
        if (_session is null || _nodePicker is null || _nodePicker.Selected < 0) return;
        MapAuthoringNode before = _session.Draft.Nodes[_nodePicker.Selected];
        var after = before with
        {
            Title = _title!.Text.Trim(), Kind = (PureRunNodeKind)_kind!.Selected,
            Layer = (int)_layer!.Value, Lane = (float)_lane!.Value, ContentId = _contentId!.Text.Trim()
        };
        ApplyDraftOperations("update-map-node", new UpdateMapNodeOperation(after));
    }

    private void AddConnection() => ChangeConnection(true);
    private void RemoveConnection() => ChangeConnection(false);

    private void ChangeConnection(bool add)
    {
        if (_session is null || _nodePicker is null || _connectionTarget is null ||
            _nodePicker.Selected < 0 || _connectionTarget.Selected < 0) return;
        var edge = new MapAuthoringConnection(
            _session.Draft.Nodes[_nodePicker.Selected].NodeId,
            _session.Draft.Nodes[_connectionTarget.Selected].NodeId);
        ApplyDraftOperations(add ? "add-map-connection" : "remove-map-connection",
            add ? new AddMapConnectionOperation(edge) : new RemoveMapConnectionOperation(edge));
    }

    private void AutoLayout()
    {
        if (_session is null) return;
        AuthoringOperation[] operations = _session.Draft.Nodes.Select(node =>
        {
            MapAuthoringNode[] peers = _session.Draft.Nodes.Where(value => value.Layer == node.Layer).ToArray();
            int peerIndex = Array.FindIndex(peers, value => value.NodeId == node.NodeId);
            return (AuthoringOperation)new UpdateMapNodeOperation(node with { Lane = peerIndex - (peers.Length - 1) / 2f });
        }).ToArray();
        ApplyDraftOperations("auto-layout-map", operations);
    }

    private void OnGraphNodeDragged(string nodeId, Vector2 from, Vector2 to)
    {
        if (_suppressGraphSignals || _session is null || from.IsEqualApprox(to)) return;
        MapAuthoringNode node = _session.Draft.Nodes.Single(value => value.NodeId == nodeId);
        float lane = (to.Y - 280f) / 105f;
        int layer = Math.Max(0, (int)MathF.Round((to.X - 90f) / 210f));
        ApplyDraftOperations("drag-map-node", new UpdateMapNodeOperation(node with { Layer = layer, Lane = lane }));
    }

    private void ApplyDraftOperations(string changeId, params AuthoringOperation[] operations)
    {
        if (_session is null) return;
        var changeSet = new AuthoringChangeSet(changeId, AuthoringDocumentKind.Map, _session.Draft.ContentId,
            AuthoringRevision.Compute(_session.Draft), operations);
        MapAuthoringMutationResult result = new MapAuthoringMutationService().Apply(_session.Draft, changeSet);
        if (!result.Succeeded) { SetStatus(result.Diagnostics[0].Message, true); return; }
        _session.ReplaceDraft(result.Document);
        RefreshFromDraft();
        SetStatus(result.Changed ? "Draft changed; Apply All to persist." : "Draft unchanged.");
    }

    private void ValidateDraft()
    {
        try { ValidateDocument(_session?.Draft ?? throw new InvalidOperationException("Map is not loaded.")); SetStatus("Draft validation passed."); }
        catch (Exception exception) { SetStatus($"Validation failed: {exception.Message}", true); }
    }

    private void ApplyAll()
    {
        if (_session is null || _map is null || _undoRedo is null) return;
        try
        {
            MapAuthoringDocument current = MapAuthoringEditorService.Read(_map);
            if (_session.HasExternalConflict(current))
                throw new InvalidOperationException("Map changed outside this Workbench session; reload before applying.");
            ValidateDocument(_session.Draft);
            string before = MapAuthoringJson.Serialize(current, false);
            string after = MapAuthoringJson.Serialize(_session.Draft, false);
            if (string.Equals(before, after, StringComparison.Ordinal)) { SetStatus("Nothing to apply."); return; }
            _undoRedo.CreateAction("Apply Pure Run map authoring session", UndoRedo.MergeMode.Disable, _map);
            _undoRedo.AddDoMethod(this, MethodName.ApplySerializedMap, after);
            _undoRedo.AddUndoMethod(this, MethodName.ApplySerializedMap, before);
            _undoRedo.CommitAction();
        }
        catch (Exception exception) { SetStatus($"Apply failed: {exception.Message}", true); }
    }

    public void ApplySerializedMap(string json)
    {
        if (_map is null) return; StoredAuthoringDocument current = _authoring.Get("map", _map.ContentIdValue);
        StoredAuthoringDocument applied = _authoring.ApplySingle("map", current.Document.ContentId, current.Revision, json);
        _map = (PureRunMapResource)applied.Resource; MapAuthoringDocument document = (MapAuthoringDocument)applied.Document;
        _session = new AuthoringSession<MapAuthoringDocument>(AuthoringDocumentKind.Map, document);
        RefreshFromDraft();
        SetStatus("Applied, saved and reload-validated map.");
    }

    private void RevertAll()
    {
        _session?.Revert();
        RefreshFromDraft();
        SetStatus("Draft reverted to the loaded revision.");
    }

    private void ExportSnapshot()
    {
        if (_session is null) return;
        _json!.Text = MapAuthoringJson.Serialize(_session.Draft);
        DisplayServer.ClipboardSet(_json.Text);
        SetStatus("Canonical authoring snapshot copied to the clipboard.");
    }

    private void ImportSnapshot()
    {
        if (_session is null || _json is null) return;
        try
        {
            MapAuthoringDocument document = MapAuthoringJson.Deserialize(_json.Text);
            if (!string.Equals(document.ContentId, _session.Draft.ContentId, StringComparison.Ordinal))
                throw new InvalidOperationException("Imported snapshot ContentId differs from the active map.");
            ValidateDocument(document);
            _session.ReplaceDraft(document);
            RefreshFromDraft();
            SetStatus("Imported snapshot into the draft; formal Resource unchanged.");
        }
        catch (Exception exception) { SetStatus($"Import failed: {exception.Message}", true); }
    }

    private void RefreshFromDraft()
    {
        if (_session is null || _nodePicker is null || _connectionTarget is null) return;
        int selected = Math.Clamp(_nodePicker.Selected, 0, Math.Max(0, _session.Draft.Nodes.Count - 1));
        _nodePicker.Clear();
        _connectionTarget.Clear();
        foreach (MapAuthoringNode node in _session.Draft.Nodes)
        {
            _nodePicker.AddItem(node.NodeId);
            _connectionTarget.AddItem(node.NodeId);
        }
        if (_session.Draft.Nodes.Count > 0)
        {
            _nodePicker.Select(selected);
            _connectionTarget.Select(Math.Min(selected + 1, _session.Draft.Nodes.Count - 1));
            SelectNode(selected);
        }
        RebuildGraph();
    }

    private void RebuildGraph()
    {
        if (_graph is null || _session is null) return;
        DisconnectGraphSignals();
        foreach (GraphNode child in _graph.GetChildren().OfType<GraphNode>())
        {
            _graph.RemoveChild(child);
            child.QueueFree();
        }
        _graphNodes.Clear();
        _suppressGraphSignals = true;
        foreach (MapAuthoringNode value in _session.Draft.Nodes)
        {
            var node = new GraphNode
            {
                Name = value.NodeId, Title = $"{value.Title} [{value.Kind}]",
                PositionOffset = new Vector2(90 + value.Layer * 210, 280 + value.Lane * 105), Resizable = false
            };
            node.AddChild(new Label { Text = value.ContentId, CustomMinimumSize = new Vector2(175, 32) });
            node.SetSlot(0, true, 0, Colors.White, true, 0, Colors.White);
            GraphElement.DraggedEventHandler handler = (from, to) => OnGraphNodeDragged(value.NodeId, from, to);
            node.Dragged += handler;
            _dragHandlers[value.NodeId] = handler;
            _graph.AddChild(node);
            _graphNodes[value.NodeId] = node;
        }
        foreach (MapAuthoringConnection edge in _session.Draft.Connections)
            _graph.ConnectNode(_graphNodes[edge.FromNodeId].Name, 0, _graphNodes[edge.ToNodeId].Name, 0);
        _suppressGraphSignals = false;
    }

    private void DisconnectGraphSignals()
    {
        foreach ((string id, GraphElement.DraggedEventHandler handler) in _dragHandlers)
            if (_graphNodes.TryGetValue(id, out GraphNode? node)) node.Dragged -= handler;
        _dragHandlers.Clear();
    }

    private void SetStatus(string text, bool error = false)
    {
        if (_status is null) return;
        _status.Text = text;
        _status.Modulate = error ? Colors.IndianRed : Colors.LightGreen;
    }
}
#endif
