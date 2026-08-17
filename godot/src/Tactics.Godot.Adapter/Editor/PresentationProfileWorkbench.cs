#if TOOLS
using Godot;
using Tactics.Application.Authoring;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

[Tool]
public partial class PresentationProfileWorkbench : VBoxContainer, IAuthoringWorkspaceParticipant
{
    private readonly TacticsAuthoringEditorService _authoring = new();
    private const string CatalogPath = "res://content/ContentCatalog.tres";
    private const string CatalogScriptPath = "res://src/Tactics.Godot.Adapter/Runtime/GodotResourceCatalog.cs";
    private readonly Dictionary<string, string> _paths = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GraphNode> _graphNodes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GraphElement.DraggedEventHandler> _dragHandlers = new(StringComparer.Ordinal);
    private EditorUndoRedoManager? _undoRedo; private OptionButton? _picker; private TextEdit? _snapshot; private Label? _status;
    private GraphEdit? _graph; private OptionButton? _nodePicker; private OptionButton? _targetPicker; private LineEdit? _propertyValue;
    private OptionButton? _previewScope; private OptionButton? _previewSpeed; private PresentationProfilePreviewStage? _previewStage;
    private Resource? _resource; private PresentationProfileAuthoringDocument? _draft; private string? _revision; private string _path = string.Empty; private int _loadAttempts;
    private PresentationGraphAuthoringDocument? _graphDraft;
    public void Configure(EditorUndoRedoManager undoRedo) => _undoRedo = undoRedo;
    public string WorkspaceName => "Presentation";
    public IReadOnlyList<AuthoringDocumentChange> CaptureWorkspaceChanges()
    {
        if (_snapshot is null || _draft is null || _revision is null) return Array.Empty<AuthoringDocumentChange>();
        PresentationProfileAuthoringDocument document = PresentationProfileAuthoringJson.Deserialize(_snapshot.Text);
        return AuthoringRevision.Compute(document) == _revision ? Array.Empty<AuthoringDocumentChange>()
            : [new AuthoringDocumentChange(AuthoringDocumentKind.Presentation, document.ContentId, _revision, PresentationProfileAuthoringJson.Serialize(document))];
    }
    public void ValidateWorkspaceDraft() { if (_resource is not null) ValidateAndLoadDraft(); }
    public void RevertWorkspaceDraft() => RevertAll();
    public void ReloadWorkspaceDocuments() { if (_picker is not null && _picker.Selected >= 0) LoadSelected(_picker.Selected); }
    public override void _Ready()
    {
        if (_undoRedo is null) throw new InvalidOperationException("Editor UndoRedo manager is required."); SizeFlagsHorizontal = SizeFlags.ExpandFill; SizeFlagsVertical = SizeFlags.ExpandFill;
        var toolbar = new HBoxContainer(); _picker = new OptionButton { CustomMinimumSize = new Vector2(320, 0) }; _picker.ItemSelected += LoadSelected; toolbar.AddChild(_picker); AddButton(toolbar, "Import To Draft", ImportDraft); AddButton(toolbar, "Validate", ValidateDraft); AddButton(toolbar, "Preview", PreviewDraft); AddButton(toolbar, "Play", PlayPreview); AddButton(toolbar, "Pause", PausePreview); AddButton(toolbar, "Stop", StopPreview); AddButton(toolbar, "Revert", RevertAll); AddChild(toolbar);
        var previewToolbar = new HBoxContainer(); previewToolbar.AddChild(new Label { Text = "Scope" }); _previewScope = new OptionButton(); foreach (string value in new[] { "Full", "Action", "Impact" }) _previewScope.AddItem(value); previewToolbar.AddChild(_previewScope); previewToolbar.AddChild(new Label { Text = "Speed" }); _previewSpeed = new OptionButton(); foreach (string value in new[] { "0.5", "1", "2", "4" }) _previewSpeed.AddItem(value); _previewSpeed.Select(1); previewToolbar.AddChild(_previewSpeed); AddChild(previewToolbar);
        var graphToolbar = new HBoxContainer(); _nodePicker = new OptionButton { CustomMinimumSize = new Vector2(180, 0) }; _nodePicker.ItemSelected += SelectNode; graphToolbar.AddChild(_nodePicker); AddButton(graphToolbar, "Add Marker", AddMarker); AddButton(graphToolbar, "Add Delay", AddDelay); AddButton(graphToolbar, "Add Parallel", AddParallel); AddButton(graphToolbar, "Delete", DeleteNode); AddButton(graphToolbar, "Toggle Enabled", ToggleEnabled); _targetPicker = new OptionButton { CustomMinimumSize = new Vector2(180, 0) }; graphToolbar.AddChild(_targetPicker); AddButton(graphToolbar, "Connect", AddEdge); AddButton(graphToolbar, "Disconnect", RemoveEdge); AddButton(graphToolbar, "Duplicate & Rebind", DuplicateAndRebind); AddButton(graphToolbar, "Auto Layout", AutoLayout); _propertyValue = new LineEdit { PlaceholderText = "selected leaf value", CustomMinimumSize = new Vector2(220, 0) }; graphToolbar.AddChild(_propertyValue); AddButton(graphToolbar, "Update Leaf", UpdateLeaf); AddChild(graphToolbar);
        AddChild(new Label { Text = "Godot-native Skill / Status / Unit presentation graph. Property leaves compile to runtime fields; Delay/Parallel are disabled layout notes until runtime supports them." });
        var split = new VSplitContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        var upper = new HSplitContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        _graph = new GraphEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 360) }; upper.AddChild(_graph);
        var previewFrame = new SubViewportContainer { Stretch = true, CustomMinimumSize = new Vector2(620, 360), SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        var viewport = new SubViewport { Size = new Vector2I(1100, 520), Size2DOverride = new Vector2I(1100, 520), Size2DOverrideStretch = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        _previewStage = new PresentationProfilePreviewStage(); viewport.AddChild(_previewStage); previewFrame.AddChild(viewport); upper.AddChild(previewFrame); split.AddChild(upper);
        _snapshot = new TextEdit { CustomMinimumSize = new Vector2(0, 150), SizeFlagsHorizontal = SizeFlags.ExpandFill }; split.AddChild(_snapshot); AddChild(split); _status = new Label(); AddChild(_status); CallDeferred(nameof(LoadCatalog));
    }
    public override void _Process(double delta) { _ = delta; HighlightActiveNodes(_previewStage?.IsPlaying == true); }
    public override void _ExitTree() { _previewStage?.Stop(); DisconnectGraphSignals(); _loadAttempts = 0; if (_picker is not null) _picker.ItemSelected -= LoadSelected; if (_nodePicker is not null) _nodePicker.ItemSelected -= SelectNode; _resource = null; _draft = null; _graphDraft = null; _previewStage = null; }
    public void LoadCatalog()
    {
        EditorResourceLoadResult<GodotResourceCatalog> result = ReloadSafeEditorResourceLoader.Load<GodotResourceCatalog>(CatalogPath, CatalogScriptPath, "Entries"); if (ReloadSafeEditorResourceLoader.RetryDeferred(this, MethodName.LoadCatalog, ref _loadAttempts, result, "Presentation profile workbench")) return;
        foreach (GodotResourceEntry entry in result.Resource!.Entries.Where(value => value.ResourceTypeIdValue == "presentation" && !value.DiagnosticPathValue.Contains("poison_spear/", StringComparison.Ordinal))) { _paths[entry.ContentIdValue] = entry.DiagnosticPathValue; _picker!.AddItem(entry.ContentIdValue); } if (_picker!.ItemCount > 0) LoadSelected(0);
    }
    private void LoadSelected(long index) { try { string id = _picker!.GetItemText((int)index); _path = _paths[id]; _resource = ResourceLoader.Load(_path, string.Empty, ResourceLoader.CacheMode.Ignore) ?? throw new InvalidOperationException($"Presentation cannot be loaded: {_path}"); _draft = PresentationProfileAuthoringEditorService.Read(_resource); _revision = AuthoringRevision.Compute(_draft); _snapshot!.Text = PresentationProfileAuthoringJson.Serialize(_draft); LoadGraph(); SetStatus($"Loaded {_draft.ResourceClass}: {_draft.Properties.Count - 1} profile fields / {_graphDraft!.Nodes.Count} authoring nodes."); } catch (Exception e) { SetStatus(e.Message, true); } }
    private void ImportDraft() { try { PresentationProfileAuthoringDocument value = PresentationProfileAuthoringJson.Deserialize(_snapshot!.Text); if (value.ContentId != _draft!.ContentId || value.ResourceClass != _draft.ResourceClass) throw new InvalidOperationException("Profile identity or class differs."); _draft = value; LoadGraph(); SetStatus("Profile imported to draft; formal Resource unchanged."); } catch (Exception e) { SetStatus(e.Message, true); } }
    private void ValidateDraft()
    {
        try { ValidateAndLoadDraft(); SetStatus("Presentation profile draft validation passed."); }
        catch (Exception e) { SetStatus(e.Message, true); }
    }
    private void PreviewDraft() => _ = TryCompilePreview(false);
    private void PlayPreview() => _ = TryCompilePreview(true);
    private void PausePreview() { _previewStage?.SetPaused(true); SetStatus("Playback state: paused; last valid frame retained."); }
    private void StopPreview() { _previewStage?.Stop(); HighlightActiveNodes(false); SetStatus($"Playback stopped; cleanup tweens={_previewStage?.ActiveTweenCount ?? 0}, temporary nodes={_previewStage?.TemporaryNodeCount ?? 0}."); }
    private void ApplyAll() { try { PresentationProfileAuthoringDocument current = PresentationProfileAuthoringEditorService.Read(_resource!); if (AuthoringRevision.Compute(current) != _revision) throw new InvalidOperationException("Profile changed outside this session."); PresentationProfileAuthoringDocument afterDocument = PresentationProfileAuthoringJson.Deserialize(_snapshot!.Text); string before = PresentationProfileAuthoringJson.Serialize(current), after = PresentationProfileAuthoringJson.Serialize(afterDocument); _undoRedo!.CreateAction("Apply presentation profile", UndoRedo.MergeMode.Disable, _resource); _undoRedo.AddDoMethod(this, MethodName.ApplySerializedProfile, after); _undoRedo.AddUndoMethod(this, MethodName.ApplySerializedProfile, before); _undoRedo.CommitAction(); } catch (Exception e) { SetStatus($"Apply failed: {e.Message}", true); } }
    public void ApplySerializedProfile(string json) { StoredAuthoringDocument current = _authoring.Get("presentation", _draft!.ContentId); StoredAuthoringDocument applied = _authoring.ApplySingle("presentation", current.Document.ContentId, current.Revision, json); _resource = applied.Resource; _draft = (PresentationProfileAuthoringDocument)applied.Document; _revision = applied.Revision; _snapshot!.Text = applied.Snapshot; LoadGraph(); SetStatus("Applied, saved and reload-validated presentation profile."); }
    private void RevertAll() { if (_resource is null) return; _draft = PresentationProfileAuthoringEditorService.Read(_resource); _revision = AuthoringRevision.Compute(_draft); _snapshot!.Text = PresentationProfileAuthoringJson.Serialize(_draft); LoadGraph(); SetStatus("Presentation profile draft reverted."); }

    private void ValidateAndLoadDraft()
    {
        PresentationProfileAuthoringDocument draft = PresentationProfileAuthoringJson.Deserialize(_snapshot!.Text);
        Resource duplicate = (Resource)_resource!.Duplicate(true);
        PresentationProfileAuthoringEditorService.Write(duplicate, draft);
        string graphJson = draft.Properties.GetValueOrDefault("AuthoringGraphJsonValue")?.Value ?? string.Empty;
        PresentationGraphAuthoringDocument graph = string.IsNullOrWhiteSpace(graphJson)
            ? PresentationGraphAuthoringDocument.CreateDefault(draft)
            : PresentationGraphAuthoringJson.Deserialize(graphJson);
        graph.Validate(draft.Properties.Keys.Where(value => value != "AuthoringGraphJsonValue"));
        graph.ValidateRuntimeCompatibility();
        _draft = draft;
        _graphDraft = graph;
    }

    private bool TryCompilePreview(bool play)
    {
        try
        {
            ValidateAndLoadDraft();
            double timeline = _draft!.Properties
                .Where(value => value.Key.Contains("Duration", StringComparison.Ordinal) && value.Value.Kind == PresentationAuthoringValueKind.Number)
                .Sum(value => double.Parse(value.Value.Value, System.Globalization.CultureInfo.InvariantCulture));
            int shared = _graphDraft!.Edges.GroupBy(value => value.TargetNodeId).Count(value => value.Count() > 1);
            Resource staged = (Resource)_resource!.Duplicate(true); PresentationProfileAuthoringEditorService.Write(staged, _draft);
            _previewStage!.Configure(staged);
            string playback = string.Empty;
            if (play)
            {
                float speed = float.Parse(_previewSpeed!.GetItemText(_previewSpeed.Selected), System.Globalization.CultureInfo.InvariantCulture);
                string scope = _previewScope!.GetItemText(_previewScope.Selected);
                _previewStage.Play(speed, scope); playback = $" Playback state: playing {scope} at {speed:0.#}×.";
            }
            SetStatus($"Compiled runtime preview plan: {_graphDraft.Nodes.Count} nodes, timeline scalar total {timeline:0.###}s, {shared} shared leaves; temporary nodes={_previewStage.TemporaryNodeCount}.{playback}");
            return true;
        }
        catch (Exception e)
        {
            SetStatus("Preview failed: " + e.Message, true);
            return false;
        }
    }

    private void LoadGraph()
    {
        string json = _draft!.Properties.GetValueOrDefault("AuthoringGraphJsonValue")?.Value ?? string.Empty;
        _graphDraft = string.IsNullOrWhiteSpace(json) ? PresentationGraphAuthoringDocument.CreateDefault(_draft) : PresentationGraphAuthoringJson.Deserialize(json);
        _graphDraft.Validate(_draft.Properties.Keys.Where(value => value != "AuthoringGraphJsonValue")); RebuildGraph();
    }
    private void SelectNode(long index) { if (_graphDraft is null || index < 0 || index >= _graphDraft.Nodes.Count) return; PresentationGraphNode node = _graphDraft.Nodes[(int)index]; _propertyValue!.Text = node.Kind == PresentationGraphNodeKind.Property && _draft!.Properties.TryGetValue(node.PropertyName, out PresentationAuthoringValue? value) ? value.Value : string.Empty; }
    private void AddMarker() { int suffix = 1; string id; do id = $"marker-{suffix++}"; while (_graphDraft!.Nodes.Any(value => value.NodeId == id)); UpdateGraph(_graphDraft.Nodes.Append(new PresentationGraphNode(id, PresentationGraphNodeKind.Marker, string.Empty, 200, 100 + _graphDraft.Nodes.Count * 45)), _graphDraft.Edges); }
    private void AddDelay() => AddControlNode(PresentationGraphNodeKind.Delay, "delay");
    private void AddParallel() => AddControlNode(PresentationGraphNodeKind.Parallel, "parallel");
    private void AddControlNode(PresentationGraphNodeKind kind, string prefix) { int suffix = 1; string id; do id = $"{prefix}-{suffix++}"; while (_graphDraft!.Nodes.Any(value => value.NodeId == id)); UpdateGraph(_graphDraft.Nodes.Append(new PresentationGraphNode(id, kind, string.Empty, 220, 100 + _graphDraft.Nodes.Count * 45, Enabled: false)), _graphDraft.Edges); }
    private void DeleteNode() { if (_graphDraft is null || _nodePicker!.Selected < 0) return; string id = _graphDraft.Nodes[_nodePicker.Selected].NodeId; if (id == "root") { SetStatus("The presentation Root cannot be deleted.", true); return; } UpdateGraph(_graphDraft.Nodes.Where(value => value.NodeId != id), _graphDraft.Edges.Where(value => value.SourceNodeId != id && value.TargetNodeId != id)); }
    private void ToggleEnabled() { if (_graphDraft is null || _nodePicker!.Selected < 0) return; string id = _graphDraft.Nodes[_nodePicker.Selected].NodeId; UpdateGraph(_graphDraft.Nodes.Select(value => value.NodeId == id ? value with { Enabled = !value.Enabled } : value), _graphDraft.Edges); }
    private void AddEdge() => ChangeEdge(true);
    private void RemoveEdge() => ChangeEdge(false);
    private void ChangeEdge(bool add) { if (_graphDraft is null || _nodePicker!.Selected < 0 || _targetPicker!.Selected < 0) return; var edge = new PresentationGraphEdge(_graphDraft.Nodes[_nodePicker.Selected].NodeId, _graphDraft.Nodes[_targetPicker.Selected].NodeId); UpdateGraph(_graphDraft.Nodes, add ? _graphDraft.Edges.Append(edge) : _graphDraft.Edges.Where(value => value != edge)); }
    private void DuplicateAndRebind()
    {
        if (_graphDraft is null || _nodePicker!.Selected < 0) return;
        PresentationGraphNode source = _graphDraft.Nodes[_nodePicker.Selected];
        if (source.Kind != PresentationGraphNodeKind.Property) { SetStatus("Duplicate & Rebind requires a Property leaf.", true); return; }
        PresentationGraphEdge? incoming = _graphDraft.Edges.FirstOrDefault(value => value.TargetNodeId == source.NodeId);
        if (incoming is null) { SetStatus("Selected leaf has no incoming edge to rebind.", true); return; }
        int suffix = 1; string id; do id = source.NodeId + "-copy-" + suffix++; while (_graphDraft.Nodes.Any(value => value.NodeId == id));
        PresentationGraphNode duplicate = source with { NodeId = id, X = source.X + 40, Y = source.Y + 55 };
        UpdateGraph(_graphDraft.Nodes.Append(duplicate), _graphDraft.Edges.Where(value => value != incoming)
            .Append(new PresentationGraphEdge(incoming.SourceNodeId, duplicate.NodeId)));
    }
    private void AutoLayout() { if (_graphDraft is null) return; PresentationGraphNode[] nodes = _graphDraft.Nodes.OrderBy(value => value.Kind).ThenBy(value => value.NodeId, StringComparer.Ordinal).Select((value, index) => value with { X = value.Kind == PresentationGraphNodeKind.Root ? 40 : 340 + (index % 3) * 280, Y = 50 + (index / 3) * 110 }).ToArray(); UpdateGraph(nodes, _graphDraft.Edges); }
    private void UpdateLeaf()
    {
        if (_graphDraft is null || _nodePicker!.Selected < 0) return; PresentationGraphNode node = _graphDraft.Nodes[_nodePicker.Selected];
        if (node.Kind != PresentationGraphNodeKind.Property) { SetStatus("Select a Property leaf first.", true); return; }
        Dictionary<string, PresentationAuthoringValue> properties = _draft!.Properties.ToDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal);
        properties[node.PropertyName] = properties[node.PropertyName] with { Value = _propertyValue!.Text.Trim() }; ReplaceDraft(properties, "Updated shared presentation leaf.");
    }
    private void MoveNode(string id, Vector2 position) { UpdateGraph(_graphDraft!.Nodes.Select(value => value.NodeId == id ? value with { X = position.X, Y = position.Y } : value), _graphDraft.Edges); }
    private void UpdateGraph(IEnumerable<PresentationGraphNode> nodes, IEnumerable<PresentationGraphEdge> edges)
    {
        try { var graph = new PresentationGraphAuthoringDocument(nodes, edges); graph.Validate(_draft!.Properties.Keys.Where(value => value != "AuthoringGraphJsonValue")); graph.ValidateRuntimeCompatibility(); _graphDraft = graph; Dictionary<string, PresentationAuthoringValue> properties = _draft.Properties.ToDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal); properties["AuthoringGraphJsonValue"] = new PresentationAuthoringValue(PresentationAuthoringValueKind.String, PresentationGraphAuthoringJson.Serialize(graph)); ReplaceDraft(properties, "Presentation graph draft changed."); RebuildGraph(); }
        catch (Exception exception) { SetStatus(exception.Message, true); }
    }
    private void ReplaceDraft(IReadOnlyDictionary<string, PresentationAuthoringValue> properties, string status) { _draft = new PresentationProfileAuthoringDocument(_draft!.ContentId, _draft.ResourceClass, properties); _snapshot!.Text = PresentationProfileAuthoringJson.Serialize(_draft); SetStatus(status + " Formal Resource unchanged."); }
    private void RebuildGraph()
    {
        if (_graph is null || _graphDraft is null) return; DisconnectGraphSignals(); foreach (GraphNode child in _graph.GetChildren().OfType<GraphNode>()) { _graph.RemoveChild(child); child.QueueFree(); } _graphNodes.Clear(); _nodePicker!.Clear(); _targetPicker!.Clear();
        foreach (PresentationGraphNode value in _graphDraft.Nodes) { int refs = value.Kind == PresentationGraphNodeKind.Property ? _graphDraft.Edges.Count(edge => edge.TargetNodeId == value.NodeId) : 0; var node = new GraphNode { Name = value.NodeId, Title = value.Kind + ": " + (string.IsNullOrWhiteSpace(value.PropertyName) ? value.NodeId : value.PropertyName) + (value.Kind == PresentationGraphNodeKind.Property ? $" [refs:{refs}]" : string.Empty), PositionOffset = new Vector2(value.X, value.Y) }; node.AddChild(new Label { Text = value.Enabled ? "enabled" : "disabled", CustomMinimumSize = new Vector2(210, 36) }); node.SetSlot(0, true, 0, Colors.White, true, 0, Colors.White); GraphElement.DraggedEventHandler handler = (_, to) => MoveNode(value.NodeId, to); node.Dragged += handler; _dragHandlers[value.NodeId] = handler; _graph.AddChild(node); _graphNodes[value.NodeId] = node; _nodePicker.AddItem(value.NodeId); _targetPicker.AddItem(value.NodeId); }
        foreach (PresentationGraphEdge edge in _graphDraft.Edges) if (_graphNodes.TryGetValue(edge.SourceNodeId, out GraphNode? from) && _graphNodes.TryGetValue(edge.TargetNodeId, out GraphNode? to)) _graph.ConnectNode(from.Name, 0, to.Name, 0);
        if (_graphDraft.Nodes.Count > 0) { _nodePicker.Select(0); _targetPicker.Select(Math.Min(1, _graphDraft.Nodes.Count - 1)); SelectNode(0); }
    }
    private void DisconnectGraphSignals() { foreach ((string id, GraphElement.DraggedEventHandler handler) in _dragHandlers) if (_graphNodes.TryGetValue(id, out GraphNode? node) && GodotObject.IsInstanceValid(node)) node.Dragged -= handler; _dragHandlers.Clear(); }
    private void HighlightActiveNodes(bool active) { foreach ((string id, GraphNode node) in _graphNodes) if (GodotObject.IsInstanceValid(node)) node.Modulate = active && (id == "root" || _graphDraft?.Nodes.First(value => value.NodeId == id).Enabled == true) ? new Color(1f, .82f, .42f) : Colors.White; }
    private static void AddButton(Container parent, string text, Action action) { var button = new Button { Text = text }; button.Pressed += action; parent.AddChild(button); }
    private void SetStatus(string text, bool error = false) { if (_status is null) return; _status.Text = text; _status.Modulate = error ? Colors.IndianRed : Colors.LightGreen; }
}
#endif
