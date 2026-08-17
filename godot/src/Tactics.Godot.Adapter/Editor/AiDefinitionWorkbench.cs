#if TOOLS
using Godot;
using Tactics.Application.Authoring;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

[Tool]
public partial class AiDefinitionWorkbench : VBoxContainer, IAuthoringWorkspaceParticipant
{
    private readonly TacticsAuthoringEditorService _authoring = new();
    private const string CatalogPath = "res://content/ContentCatalog.tres";
    private const string CatalogScriptPath = "res://src/Tactics.Godot.Adapter/Runtime/GodotResourceCatalog.cs";
    private readonly Dictionary<string, string> _paths = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GraphNode> _graphNodes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GraphElement.DraggedEventHandler> _dragHandlers = new(StringComparer.Ordinal);
    private EditorUndoRedoManager? _undoRedo;
    private OptionButton? _resourcePicker;
    private OptionButton? _nodePicker;
    private OptionButton? _edgeTarget;
    private OptionButton? _kind;
    private LineEdit? _type;
    private SpinBox? _parameter;
    private CheckBox? _enabled;
    private SpinBox? _curveTime;
    private SpinBox? _curveValue;
    private SpinBox? _distance;
    private SpinBox? _damage;
    private SpinBox? _targets;
    private SpinBox? _statusWeight;
    private OptionButton? _skillPicker;
    private OptionButton? _patternPicker;
    private SpinBox? _maximumCandidates;
    private SpinBox? _minimumRange;
    private SpinBox? _maximumRange;
    private SpinBox? _rangeBonus;
    private string[] _skillIds = Array.Empty<string>();
    private GraphEdit? _graph;
    private Label? _status;
    private AiDefinitionResource? _resource;
    private AiAuthoringDocument? _draft;
    private string? _expectedRevision;
    private string _path = string.Empty;
    private int _catalogLoadAttempts;

    public void Configure(EditorUndoRedoManager undoRedo) => _undoRedo = undoRedo;
    public string WorkspaceName => "AI";
    public IReadOnlyList<AuthoringDocumentChange> CaptureWorkspaceChanges() => _draft is not null && _expectedRevision is not null && AuthoringRevision.Compute(_draft) != _expectedRevision
        ? [new AuthoringDocumentChange(AuthoringDocumentKind.Ai, _draft.ContentId, _expectedRevision, AiAuthoringJson.Serialize(_draft))]
        : Array.Empty<AuthoringDocumentChange>();
    public void ValidateWorkspaceDraft() { if (_draft is not null) _ = _draft.ToCoreDefinition(); }
    public void RevertWorkspaceDraft() => RevertAll();
    public void ReloadWorkspaceDocuments() { if (_resourcePicker is not null && _resourcePicker.Selected >= 0) LoadSelected(_resourcePicker.Selected); }

    public override void _Ready()
    {
        if (_undoRedo is null) throw new InvalidOperationException("Editor UndoRedo manager is required.");
        SizeFlagsHorizontal = SizeFlags.ExpandFill; SizeFlagsVertical = SizeFlags.ExpandFill;
        WorkbenchUi.StylePage(this);
        var toolbar = WorkbenchUi.Toolbar(this); toolbar.AddChild(new Label { Text = "AI DECISION GRAPH" }); _resourcePicker = new OptionButton { CustomMinimumSize = new Vector2(240, 0) };
        _resourcePicker.ItemSelected += LoadSelected; toolbar.AddChild(_resourcePicker); AddButton(toolbar, "Auto Layout", AutoLayout); AddButton(toolbar, "Validate", ValidateDraft); AddButton(toolbar, "Preview", PreviewDraft); AddButton(toolbar, "Revert", RevertAll); AddChild(toolbar);
        var split = new HSplitContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        _graph = new GraphEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(520, 400) }; WorkbenchUi.StyleGraph(_graph); split.AddChild(_graph);
        var inspectorScroll = new ScrollContainer { CustomMinimumSize = new Vector2(360, 0), SizeFlagsVertical = SizeFlags.ExpandFill };
        var inspector = WorkbenchUi.Pane(this, 350);
        var profile = WorkbenchUi.InspectorSection(this, "Scoring profile", new Color("d49a32")); var profileGrid = new GridContainer { Columns = 2 };
        _distance = AddWeight(profileGrid, "Distance"); _damage = AddWeight(profileGrid, "Damage"); _targets = AddWeight(profileGrid, "Targets"); _statusWeight = AddWeight(profileGrid, "Status"); profile.AddChild(profileGrid); AddButton(profile, "Update Profile Draft", UpdateProfile); inspector.AddChild(profile);
        var bindingBar = WorkbenchUi.InspectorSection(this, "Skills, patterns and movement", new Color("4a90d9")); bindingBar.AddChild(new Label { Text = "Skill" }); _skillPicker = new OptionButton(); bindingBar.AddChild(_skillPicker); var skillActions = new HBoxContainer(); AddButton(skillActions, "Add Skill", AddSkill); AddButton(skillActions, "Remove Skill", RemoveSkill); bindingBar.AddChild(skillActions); bindingBar.AddChild(new Label { Text = "Pattern" }); _patternPicker = new OptionButton(); bindingBar.AddChild(_patternPicker); var patternActions = new HBoxContainer(); AddButton(patternActions, "Add Pattern", AddPattern); AddButton(patternActions, "Remove Pattern", RemovePattern); bindingBar.AddChild(patternActions);
        var movement = new GridContainer { Columns = 2 }; movement.AddChild(new Label { Text = "Candidates" }); _maximumCandidates = Integer(movement, 1, 20); movement.AddChild(new Label { Text = "Range min/max" }); var range = new HBoxContainer(); _minimumRange = Integer(range, 0, 20); _maximumRange = Integer(range, 0, 20); movement.AddChild(range); movement.AddChild(new Label { Text = "Reposition bonus" }); _rangeBonus = AddWeight(movement, string.Empty); bindingBar.AddChild(movement); AddButton(bindingBar, "Update Movement Draft", UpdateMovement); inspector.AddChild(bindingBar);
        var nodeBar = WorkbenchUi.InspectorSection(this, "Selected node", new Color("4fb06f")); _nodePicker = new OptionButton(); _nodePicker.ItemSelected += SelectNode; nodeBar.AddChild(_nodePicker); var nodeActions = new HBoxContainer(); AddButton(nodeActions, "Add", AddNode); AddButton(nodeActions, "Delete", DeleteNode); nodeBar.AddChild(nodeActions); _kind = new OptionButton(); foreach (string name in Enum.GetNames<AiAuthoringNodeKind>()) _kind.AddItem(name); nodeBar.AddChild(_kind); _type = new LineEdit { PlaceholderText = "Runtime node type" }; nodeBar.AddChild(_type); _parameter = new SpinBox { MinValue = -100, MaxValue = 100, Step = .1 }; nodeBar.AddChild(_parameter); _enabled = new CheckBox { Text = "Enabled" }; nodeBar.AddChild(_enabled); AddButton(nodeBar, "Update Node", UpdateNode); _edgeTarget = new OptionButton(); nodeBar.AddChild(_edgeTarget); var edgeActions = new HBoxContainer(); AddButton(edgeActions, "Connect", AddEdge); AddButton(edgeActions, "Disconnect", RemoveEdge); nodeBar.AddChild(edgeActions); inspector.AddChild(nodeBar);
        var curveBar = WorkbenchUi.InspectorSection(this, "Score curve", new Color("d49a32")); curveBar.AddChild(new Label { Text = "Time" }); _curveTime = new SpinBox { MinValue = -1000, MaxValue = 1000, Step = .1 }; curveBar.AddChild(_curveTime); curveBar.AddChild(new Label { Text = "Value" }); _curveValue = new SpinBox { MinValue = -1000, MaxValue = 1000, Step = .1 }; curveBar.AddChild(_curveValue); var curveActions = new HBoxContainer(); AddButton(curveActions, "Add Key", AddCurveKey); AddButton(curveActions, "Delete Last Key", DeleteCurveKey); curveBar.AddChild(curveActions); inspector.AddChild(curveBar);
        inspectorScroll.AddChild(inspector); split.AddChild(inspectorScroll); AddChild(split);
        _status = new Label { Text = "Loading typed AI authoring documents...", AutowrapMode = TextServer.AutowrapMode.WordSmart }; WorkbenchUi.StyleStatus(_status); AddChild(_status);
        CallDeferred(nameof(LoadCatalog));
    }

    public override void _ExitTree()
    {
        _dragHandlers.Clear(); _catalogLoadAttempts = 0;
        _resource = null; _draft = null;
    }

    public static void ValidateResource(AiDefinitionResource value) => _ = AiAuthoringEditorService.Read(value).ToCoreDefinition();

    public void LoadCatalog()
    {
        EditorResourceLoadResult<GodotResourceCatalog> result = ReloadSafeEditorResourceLoader.Load<GodotResourceCatalog>(CatalogPath, CatalogScriptPath, "Entries");
        if (ReloadSafeEditorResourceLoader.RetryDeferred(this, MethodName.LoadCatalog, ref _catalogLoadAttempts, result, "AI workbench")) return;
        _skillIds = result.Resource!.Entries.Where(value => value.ResourceTypeIdValue == "skill").Select(value => value.ContentIdValue).Order(StringComparer.Ordinal).ToArray();
        foreach (string skillId in _skillIds) { _skillPicker!.AddItem(skillId); _patternPicker!.AddItem(skillId); }
        foreach (GodotResourceEntry entry in result.Resource.Entries.Where(value => value.ResourceTypeIdValue == "ai")) { _paths[entry.ContentIdValue] = entry.DiagnosticPathValue; _resourcePicker!.AddItem(entry.ContentIdValue); }
        if (_resourcePicker!.ItemCount > 0) LoadSelected(0);
    }

    private void LoadSelected(long index)
    {
        string id = _resourcePicker!.GetItemText((int)index); _path = _paths[id];
        try
        {
            _resource = ResourceLoader.Load<AiDefinitionResource>(_path, string.Empty, ResourceLoader.CacheMode.Ignore) ?? throw new InvalidOperationException($"AI cannot be loaded: {_path}");
            _draft = AiAuthoringEditorService.Read(_resource); _expectedRevision = AuthoringRevision.Compute(_draft); Refresh(); SetStatus($"Loaded {id}; {_draft.Nodes.Count} typed graph nodes.");
        }
        catch (Exception e) { SetStatus(e.Message, true); }
    }

    private void Refresh()
    {
        if (_draft is null) return;
        _distance!.Value = _draft.DistanceWeight; _damage!.Value = _draft.DamageWeight; _targets!.Value = _draft.TargetCountWeight; _statusWeight!.Value = _draft.HarmfulStatusWeight;
        _maximumCandidates!.Value = _draft.MaximumEngageCandidatesPerTarget; _minimumRange!.Value = _draft.PreferredMinimumRange; _maximumRange!.Value = _draft.PreferredMaximumRange; _rangeBonus!.Value = _draft.PreferredRangeRepositionBonus;
        int selected = Math.Clamp(_nodePicker!.Selected, 0, Math.Max(0, _draft.Nodes.Count - 1)); _nodePicker.Clear(); _edgeTarget!.Clear();
        foreach (AiAuthoringNode node in _draft.Nodes) { _nodePicker.AddItem(node.NodeId); _edgeTarget.AddItem(node.NodeId); }
        if (_draft.Nodes.Count > 0) { _nodePicker.Select(selected); _edgeTarget.Select(selected); SelectNode(selected); }
        RebuildGraph();
    }

    private void SelectNode(long index)
    {
        if (_draft is null || index < 0 || index >= _draft.Nodes.Count) return; AiAuthoringNode node = _draft.Nodes[(int)index];
        _kind!.Select((int)node.Kind); _type!.Text = node.Type; _parameter!.Value = node.Parameter; _enabled!.ButtonPressed = node.Enabled;
        bool ignoredRule = node.Kind == AiAuthoringNodeKind.Rule; _type.Editable = !ignoredRule; _parameter.Editable = !ignoredRule; _enabled.Disabled = ignoredRule;
        if (ignoredRule) SetStatus("Rule node fields are retained for round-trip only and are not consumed by AiDecisionService.");
        if (node.Curve.LastOrDefault() is { } key) { _curveTime!.Value = key.Time; _curveValue!.Value = key.Value; }
    }

    private void UpdateProfile() => ReplaceDraft(new AiAuthoringDocument(_draft!.ContentId, _draft.Archetype, _draft.SkillContentIds, _draft.PatternSkillContentIds,
        (float)_distance!.Value, (float)_damage!.Value, (float)_targets!.Value, (float)_statusWeight!.Value, _draft.Nodes, _draft.Edges, _draft.SourceSha256,
        _draft.MaximumEngageCandidatesPerTarget, _draft.PreferredMinimumRange, _draft.PreferredMaximumRange, _draft.PreferredRangeRepositionBonus));

    private void UpdateMovement() => ReplaceDraft(new AiAuthoringDocument(_draft!.ContentId, _draft.Archetype, _draft.SkillContentIds, _draft.PatternSkillContentIds,
        _draft.DistanceWeight, _draft.DamageWeight, _draft.TargetCountWeight, _draft.HarmfulStatusWeight, _draft.Nodes, _draft.Edges, _draft.SourceSha256,
        (int)_maximumCandidates!.Value, (int)_minimumRange!.Value, (int)_maximumRange!.Value, (float)_rangeBonus!.Value));
    private void AddSkill() => ChangeSkill(false, true);
    private void RemoveSkill() => ChangeSkill(false, false);
    private void AddPattern() => ChangeSkill(true, true);
    private void RemovePattern() => ChangeSkill(true, false);
    private void ChangeSkill(bool pattern, bool add)
    {
        OptionButton picker = pattern ? _patternPicker! : _skillPicker!; if (picker.Selected < 0) return;
        string selected = picker.GetItemText(picker.Selected);
        string[] skills = (pattern ? _draft!.PatternSkillContentIds : _draft!.SkillContentIds).ToArray();
        skills = add ? skills.Append(selected).Distinct(StringComparer.Ordinal).ToArray() : skills.Where(value => value != selected).ToArray();
        ReplaceDraft(new AiAuthoringDocument(_draft.ContentId, _draft.Archetype, pattern ? _draft.SkillContentIds : skills,
            pattern ? skills : _draft.PatternSkillContentIds, _draft.DistanceWeight, _draft.DamageWeight, _draft.TargetCountWeight,
            _draft.HarmfulStatusWeight, _draft.Nodes, _draft.Edges, _draft.SourceSha256, _draft.MaximumEngageCandidatesPerTarget,
            _draft.PreferredMinimumRange, _draft.PreferredMaximumRange, _draft.PreferredRangeRepositionBonus));
    }

    private void AddNode()
    {
        int number = 1; string id; do id = $"node-{number++}"; while (_draft!.Nodes.Any(value => value.NodeId == id));
        AiAuthoringNodeKind kind = (AiAuthoringNodeKind)_kind!.Selected; IReadOnlyList<AiCurveKeyAuthoring> curve = kind == AiAuthoringNodeKind.Score ? new[] { new AiCurveKeyAuthoring(0, 0, 0, 0), new AiCurveKeyAuthoring(1, 1, 0, 0) } : Array.Empty<AiCurveKeyAuthoring>();
        string type = kind switch { AiAuthoringNodeKind.Intent => "BasicAttack", AiAuthoringNodeKind.Score => "TargetHealth", _ => "RuntimeIgnoredRule" };
        ReplaceGraph(_draft.Nodes.Append(new AiAuthoringNode(id, kind, type, true, 1, curve, 80 + _draft.Nodes.Count % 4 * 240, 80 + _draft.Nodes.Count / 4 * 145)), _draft.Edges);
        _nodePicker!.Select(_draft.Nodes.Count - 1); SelectNode(_draft.Nodes.Count - 1);
    }
    private void DeleteNode() { if (_draft is null || _nodePicker!.Selected < 0) return; string id = _draft.Nodes[_nodePicker.Selected].NodeId; ReplaceGraph(_draft.Nodes.Where(value => value.NodeId != id), _draft.Edges.Where(value => value.SourceNodeId != id && value.TargetNodeId != id)); }
    private void UpdateNode()
    {
        if (_draft is null || _nodePicker!.Selected < 0) return; AiAuthoringNode old = _draft.Nodes[_nodePicker.Selected]; AiAuthoringNodeKind kind = (AiAuthoringNodeKind)_kind!.Selected;
        IReadOnlyList<AiCurveKeyAuthoring> curve = kind == AiAuthoringNodeKind.Score ? (old.Curve.Count > 0 ? old.Curve : new[] { new AiCurveKeyAuthoring(0, 0, 0, 0), new AiCurveKeyAuthoring(1, 1, 0, 0) }) : Array.Empty<AiCurveKeyAuthoring>();
        bool preserveIgnoredRule = old.Kind == AiAuthoringNodeKind.Rule && kind == AiAuthoringNodeKind.Rule;
        AiAuthoringNode[] nodes = _draft.Nodes.ToArray(); nodes[_nodePicker.Selected] = old with { Kind = kind,
            Type = preserveIgnoredRule ? old.Type : _type!.Text.Trim(), Parameter = preserveIgnoredRule ? old.Parameter : (float)_parameter!.Value,
            Enabled = preserveIgnoredRule ? old.Enabled : _enabled!.ButtonPressed, Curve = curve }; ReplaceGraph(nodes, _draft.Edges);
    }
    private void AddEdge() => ChangeEdge(true);
    private void RemoveEdge() => ChangeEdge(false);
    private void ChangeEdge(bool add)
    {
        if (_draft is null || _nodePicker!.Selected < 0 || _edgeTarget!.Selected < 0) return; var edge = new AiAuthoringEdge(_draft.Nodes[_nodePicker.Selected].NodeId, _draft.Nodes[_edgeTarget.Selected].NodeId);
        ReplaceGraph(_draft.Nodes, add ? _draft.Edges.Append(edge) : _draft.Edges.Where(value => value != edge));
    }
    private void AddCurveKey()
    {
        if (_draft is null || _nodePicker!.Selected < 0) return;
        AiAuthoringNode selected = _draft.Nodes[_nodePicker.Selected];
        if (selected.Kind != AiAuthoringNodeKind.Score) { SetStatus("Only Score nodes consume curves; Rule fields round-trip but do not affect runtime.", true); return; }
        AiCurveKeyAuthoring key = new((float)_curveTime!.Value, (float)_curveValue!.Value, 0, 0);
        AiAuthoringNode[] nodes = _draft.Nodes.ToArray(); nodes[_nodePicker.Selected] = selected with { Curve = selected.Curve.Append(key).OrderBy(value => value.Time).ToArray() }; ReplaceGraph(nodes, _draft.Edges);
    }
    private void DeleteCurveKey()
    {
        if (_draft is null || _nodePicker!.Selected < 0) return;
        AiAuthoringNode selected = _draft.Nodes[_nodePicker.Selected];
        if (selected.Kind != AiAuthoringNodeKind.Score || selected.Curve.Count <= 1) { SetStatus("Score nodes must retain at least one curve key.", true); return; }
        AiAuthoringNode[] nodes = _draft.Nodes.ToArray(); nodes[_nodePicker.Selected] = selected with { Curve = selected.Curve.Take(selected.Curve.Count - 1).ToArray() }; ReplaceGraph(nodes, _draft.Edges);
    }
    private void AutoLayout()
    {
        if (_draft is null) return;
        AiAuthoringNode[] nodes = _draft.Nodes.GroupBy(value => value.Kind).OrderBy(value => value.Key).SelectMany((group, column) =>
            group.OrderBy(value => value.NodeId, StringComparer.Ordinal).Select((value, row) => value with { X = 80 + column * 260, Y = 80 + row * 145 })).ToArray();
        ReplaceGraph(nodes, _draft.Edges);
    }
    private void ReplaceGraph(IEnumerable<AiAuthoringNode> nodes, IEnumerable<AiAuthoringEdge> edges)
    {
        try { ReplaceDraft(new AiAuthoringDocument(_draft!.ContentId, _draft.Archetype, _draft.SkillContentIds, _draft.PatternSkillContentIds, _draft.DistanceWeight, _draft.DamageWeight, _draft.TargetCountWeight, _draft.HarmfulStatusWeight, nodes, edges, _draft.SourceSha256, _draft.MaximumEngageCandidatesPerTarget, _draft.PreferredMinimumRange, _draft.PreferredMaximumRange, _draft.PreferredRangeRepositionBonus)); }
        catch (Exception e) { SetStatus(e.Message, true); }
    }
    private void ReplaceDraft(AiAuthoringDocument value) { try { _draft = value; Refresh(); SetStatus("AI draft changed; formal Resource unchanged."); } catch (Exception e) { SetStatus(e.Message, true); } }
    private void ValidateDraft() { try { AuthoringValidationResult result = _authoring.Validate("ai", _draft!.ContentId, AiAuthoringJson.Serialize(_draft), _expectedRevision); if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Diagnostics.Select(value => value.Message))); string warning = _draft.Nodes.Any(value => value.Kind == AiAuthoringNodeKind.Rule) ? " Rule nodes round-trip but are not consumed by runtime." : string.Empty; SetStatus("AI draft validation passed." + warning); } catch (Exception e) { SetStatus(e.Message, true); } }
    private void PreviewDraft() { try { AuthoringPreviewEvidence preview = AuthoringPreviewCompiler.Compile(_draft!, 42); SetStatus(preview.Summary + " nodes=" + preview.Values["nodes"] + ", edges=" + preview.Values["edges"] + ". Fixed-seed fixture playback remains in Encounter Fixture."); } catch (Exception e) { SetStatus("Preview failed: " + e.Message, true); } }

    private void ApplyAll()
    {
        try
        {
            AiAuthoringDocument current = AiAuthoringEditorService.Read(_resource!); if (AuthoringRevision.Compute(current) != _expectedRevision) throw new InvalidOperationException("AI changed outside this session; reload before applying.");
            string before = AiAuthoringJson.Serialize(current), after = AiAuthoringJson.Serialize(_draft!); if (before == after) { SetStatus("Nothing to apply."); return; }
            _undoRedo!.CreateAction("Apply AI authoring session", UndoRedo.MergeMode.Disable, _resource); _undoRedo.AddDoMethod(this, MethodName.ApplySerializedAi, after); _undoRedo.AddUndoMethod(this, MethodName.ApplySerializedAi, before); _undoRedo.CommitAction();
        }
        catch (Exception e) { SetStatus($"Apply failed: {e.Message}", true); }
    }
    public void ApplySerializedAi(string json)
    {
        StoredAuthoringDocument current = _authoring.Get("ai", _resource!.ContentIdValue); StoredAuthoringDocument applied = _authoring.ApplySingle("ai", current.Document.ContentId, current.Revision, json);
        _resource = (AiDefinitionResource)applied.Resource; _draft = (AiAuthoringDocument)applied.Document; _expectedRevision = applied.Revision; Refresh(); SetStatus("Applied, saved and reload-validated AI.");
    }
    private void RevertAll() { if (_resource is null) return; _draft = AiAuthoringEditorService.Read(_resource); _expectedRevision = AuthoringRevision.Compute(_draft); Refresh(); SetStatus("AI draft reverted."); }

    private void RebuildGraph()
    {
        DisconnectGraphSignals();
        foreach (GraphNode child in _graph!.GetChildren().OfType<GraphNode>()) { _graph.RemoveChild(child); child.QueueFree(); }
        _graphNodes.Clear(); if (_draft is null) return;
        foreach (AiAuthoringNode value in _draft.Nodes)
        {
            var node = new GraphNode { Name = value.NodeId, Title = $"{value.Kind}: {value.Type}", PositionOffset = new Vector2(value.X, value.Y) };
            string parameter = value.Kind == AiAuthoringNodeKind.Rule ? "runtime ignored / read-only" : value.Kind == AiAuthoringNodeKind.Score ? $"weight {value.Parameter:0.##}" : $"priority {value.Parameter:0.##}";
            node.AddChild(new Label { Text = $"{value.NodeId}\n{(value.Enabled ? "enabled" : "disabled")} | {parameter}", CustomMinimumSize = new Vector2(220, 48) }); node.SetSlot(0, true, 0, Colors.White, true, 0, Colors.White);
            WorkbenchUi.StyleGraphNode(node, value.Kind switch { AiAuthoringNodeKind.Intent => new Color("4a90d9"), AiAuthoringNodeKind.Rule => new Color("4fb06f"), _ => new Color("d49a32") }, value.Enabled,
                value.Kind != AiAuthoringNodeKind.Intent && !_draft.Edges.Any(edge => edge.SourceNodeId == value.NodeId || edge.TargetNodeId == value.NodeId));
            GraphElement.DraggedEventHandler handler = (_, to) => MoveNode(value.NodeId, to); node.Dragged += handler; _dragHandlers[value.NodeId] = handler;
            _graph.AddChild(node); _graphNodes[value.NodeId] = node;
        }
        foreach (AiAuthoringEdge edge in _draft.Edges) if (_graphNodes.TryGetValue(edge.SourceNodeId, out GraphNode? from) && _graphNodes.TryGetValue(edge.TargetNodeId, out GraphNode? to)) _graph.ConnectNode(from.Name, 0, to.Name, 0);
    }

    private void MoveNode(string nodeId, Vector2 to)
    {
        if (_draft is null) return; AiAuthoringNode[] nodes = _draft.Nodes.Select(value => value.NodeId == nodeId ? value with { X = to.X, Y = to.Y } : value).ToArray(); ReplaceGraph(nodes, _draft.Edges);
    }
    private void DisconnectGraphSignals()
    {
        foreach ((string id, GraphElement.DraggedEventHandler handler) in _dragHandlers) if (_graphNodes.TryGetValue(id, out GraphNode? node) && GodotObject.IsInstanceValid(node)) node.Dragged -= handler;
        _dragHandlers.Clear();
    }

    private static SpinBox AddWeight(Container parent, string name) { parent.AddChild(new Label { Text = name }); var field = new SpinBox { MinValue = 0, MaxValue = 20, Step = .1, CustomMinimumSize = new Vector2(70, 0) }; parent.AddChild(field); return field; }
    private static SpinBox Integer(Container parent, int minimum, int maximum) { var field = new SpinBox { MinValue = minimum, MaxValue = maximum, Step = 1, CustomMinimumSize = new Vector2(58, 0) }; parent.AddChild(field); return field; }
    private static void AddButton(Container parent, string text, Action action) { var button = new Button { Text = text }; button.Pressed += action; parent.AddChild(button); }
    private void SetStatus(string text, bool error = false) { if (_status is null) return; _status.Text = text; WorkbenchUi.StyleStatus(_status, error); }
}
#endif
