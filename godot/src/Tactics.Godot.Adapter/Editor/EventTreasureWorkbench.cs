#if TOOLS
using Godot;
using Tactics.Application.Authoring;
using Tactics.Core.Runs;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

/// <summary>Authors Event and Treasure resources through one constrained graph surface.</summary>
/// <remarks>Edges project typed documents; only node positions are persisted.</remarks>
[Tool]
public partial class EventTreasureWorkbench : VBoxContainer, IAuthoringWorkspaceParticipant
{
    private const string CatalogPath = "res://content/ContentCatalog.tres";
    private const string CatalogScriptPath = "res://src/Tactics.Godot.Adapter/Runtime/GodotResourceCatalog.cs";
    private readonly List<(string Kind, string Id, string Path)> _rows = [];
    private readonly Dictionary<string, string[]> _catalog = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GraphNode> _nodes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GraphElement.DraggedEventHandler> _drag = new(StringComparer.Ordinal);
    private EditorUndoRedoManager? _undoRedo;
    private ItemList? _resources;
    private GraphEdit? _graph;
    private VBoxContainer? _inspector;
    private Label? _status;
    private SpinBox? _seed;
    private EventAuthoringDocument? _event;
    private TreasureAuthoringDocument? _treasure;
    private PureRunLayerFourResource? _eventResource;
    private PureRunTreasureResource? _treasureResource;
    private string? _revision;
    private string _selectedNode = "start";
    private int _loadAttempts;

    public void Configure(EditorUndoRedoManager undoRedo) => _undoRedo = undoRedo;
    public string WorkspaceName => "Event";
    public IReadOnlyList<AuthoringDocumentChange> CaptureWorkspaceChanges()
    {
        IAuthoringDocument? document = Document();
        if (_revision is null || document is null || AuthoringRevision.Compute(document) == _revision) return [];
        return [new AuthoringDocumentChange(_event is null ? AuthoringDocumentKind.Treasure : AuthoringDocumentKind.Event,
            document.ContentId, _revision, Serialize(document))];
    }
    public void ValidateWorkspaceDraft() { if (_event is not null) _event.Validate(); else _ = _treasure?.ToCoreDefinition(); }
    public void RevertWorkspaceDraft() => RevertAll();
    public void ReloadWorkspaceDocuments() { if (_resources?.GetSelectedItems() is { Length: > 0 } selected) SelectResource(selected[0]); }

    public override void _Ready()
    {
        if (_undoRedo is null) throw new InvalidOperationException("Editor UndoRedo manager is required.");
        SizeFlagsHorizontal = SizeFlags.ExpandFill; SizeFlagsVertical = SizeFlags.ExpandFill; WorkbenchUi.StylePage(this);
        var toolbar = WorkbenchUi.Toolbar(this); toolbar.AddChild(new Label { Text = "EVENT & TREASURE GRAPH" });
        toolbar.AddChild(Button("Validate", ValidateDraft)); toolbar.AddChild(Button("Auto Layout", AutoLayout)); toolbar.AddChild(new Label { Text = "Seed" });
        _seed = new SpinBox { MinValue = 0, MaxValue = int.MaxValue, Value = 42, CustomMinimumSize = new Vector2(100, 0) }; toolbar.AddChild(_seed);
        toolbar.AddChild(Button("Preview", PreviewDraft)); toolbar.AddChild(Button("Revert", RevertAll)); AddChild(toolbar);
        var split = new HSplitContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        var left = WorkbenchUi.Pane(this, WorkbenchUi.ResourcePaneWidth); left.SizeFlagsHorizontal = SizeFlags.ShrinkBegin; left.AddChild(new Label { Text = "EVENTS & TREASURE" });
        _resources = new ItemList { SizeFlagsVertical = SizeFlags.ExpandFill }; _resources.ItemSelected += SelectResource; left.AddChild(_resources); split.AddChild(left);
        _graph = new GraphEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill }; WorkbenchUi.StyleGraph(_graph);
        _graph.NodeSelected += node => { _selectedNode = node.Name; RefreshInspector(); }; split.AddChild(_graph);
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(WorkbenchUi.InspectorWidth, 0), SizeFlagsVertical = SizeFlags.ExpandFill };
        _inspector = WorkbenchUi.Pane(this, WorkbenchUi.InspectorWidth); _inspector.SizeFlagsHorizontal = SizeFlags.ShrinkEnd; scroll.AddChild(_inspector); split.AddChild(scroll); AddChild(split);
        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(0, 44) }; WorkbenchUi.StyleStatus(_status); AddChild(_status);
        CallDeferred(nameof(LoadCatalog));
    }

    public override void _ExitTree() { DisconnectGraphSignals(); _event = null; _treasure = null; _eventResource = null; _treasureResource = null; _nodes.Clear(); _rows.Clear(); }

    public void LoadCatalog()
    {
        EditorResourceLoadResult<GodotResourceCatalog> result = ReloadSafeEditorResourceLoader.Load<GodotResourceCatalog>(CatalogPath, CatalogScriptPath, "Entries");
        if (ReloadSafeEditorResourceLoader.RetryDeferred(this, MethodName.LoadCatalog, ref _loadAttempts, result, "Event workbench")) return;
        foreach (IGrouping<string, GodotResourceEntry> group in result.Resource!.Entries.GroupBy(value => value.ResourceTypeIdValue, StringComparer.Ordinal))
            _catalog[group.Key] = group.Select(value => value.ContentIdValue).Order(StringComparer.Ordinal).ToArray();
        string[] items = _catalog.GetValueOrDefault("item") ?? [];
        _catalog["equipment"] = items.Where(value => value.StartsWith("item.equipment.", StringComparison.Ordinal)).ToArray();
        _catalog["consumable"] = items.Where(value => value.StartsWith("item.consumable.", StringComparison.Ordinal)).ToArray();
        foreach (GodotResourceEntry entry in result.Resource.Entries.Where(value => value.ResourceTypeIdValue is "event" or "treasure")
                     .OrderBy(value => value.ResourceTypeIdValue).ThenBy(value => value.ContentIdValue, StringComparer.Ordinal))
        { _rows.Add((entry.ResourceTypeIdValue, entry.ContentIdValue, entry.DiagnosticPathValue)); _resources!.AddItem($"{(entry.ResourceTypeIdValue == "event" ? "Event" : "Treasure")}  {entry.ContentIdValue}"); }
        if (_resources!.ItemCount > 0) { _resources.Select(0); SelectResource(0); }
    }

    private void SelectResource(long index)
    {
        if (index < 0 || index >= _rows.Count) return;
        (string kind, string id, string path) = _rows[(int)index];
        try
        {
            if (kind == "treasure")
            {
                _treasureResource = ResourceLoader.Load<PureRunTreasureResource>(path, string.Empty, ResourceLoader.CacheMode.Ignore) ?? throw new InvalidOperationException("Treasure cannot be loaded: " + path);
                _treasure = EventTreasureAuthoringEditorService.Read(_treasureResource); _event = null; _eventResource = null; _selectedNode = "treasure:root";
            }
            else
            {
                _eventResource = ResourceLoader.Load<PureRunLayerFourResource>(path, string.Empty, ResourceLoader.CacheMode.Ignore) ?? throw new InvalidOperationException("Event cannot be loaded: " + path);
                _event = EventTreasureAuthoringEditorService.Read(_eventResource); _treasure = null; _treasureResource = null; _selectedNode = "start";
            }
            _revision = AuthoringRevision.Compute(Document()!); RebuildGraph(); RefreshInspector(); SetStatus($"Loaded {id}. Map nodes: {FindMapNodes(id)}");
        }
        catch (Exception error) { SetStatus(error.Message, true); }
    }

    private void RebuildGraph()
    {
        if (_graph is null) return; DisconnectGraphSignals();
        foreach (GraphNode child in _graph.GetChildren().OfType<GraphNode>()) { _graph.RemoveChild(child); child.QueueFree(); }
        _nodes.Clear(); if (_event is not null) BuildEventGraph(_event); else if (_treasure is not null) BuildTreasureGraph(_treasure);
    }

    private void BuildEventGraph(EventAuthoringDocument document)
    {
        AddNode("start", "Start", document.Title, new Color("4caf63"), NodePosition("start", 40, 80));
        AddNode("end", "End", "Event complete", new Color("60656d"), NodePosition("end", 1080, 80));
        for (int index = 0; index < document.Options.Count; index++)
        {
            EventOptionAuthoring option = document.Options[index]; float y = 50 + index * 190;
            string optionId = $"option:{option.OptionId}", checkId = $"check:{option.OptionId}", successId = $"success:{option.OptionId}", failureId = $"failure:{option.OptionId}";
            AddNode(optionId, "Option", option.Text, new Color("4a90d9"), NodePosition(optionId, 230, y));
            AddNode(checkId, "Check", option.Attribute == RunEventAttribute.None ? "Auto Success" : $"{option.Attribute} {option.BaseSuccessRate}%", new Color("b18742"), NodePosition(checkId, 470, y));
            AddNode(successId, "Success", Outcome(option.Success), new Color("4fb06f"), NodePosition(successId, 720, y - 30));
            Connect("start", optionId); Connect(optionId, checkId); Connect(checkId, successId); Connect(successId, "end");
            if (option.Attribute != RunEventAttribute.None && option.Failure is not null)
            { AddNode(failureId, "Failure", Outcome(option.Failure), new Color("d45656"), NodePosition(failureId, 720, y + 70)); Connect(checkId, failureId); Connect(failureId, "end"); }
        }
    }

    private void BuildTreasureGraph(TreasureAuthoringDocument document)
    {
        AddNode("treasure:root", "Treasure", document.ContentId, new Color("b18742"), NodePosition("treasure:root", 80, 180));
        AddNode("treasure:gold", "Gold", $"{document.GoldMinimum}–{document.GoldMaximum}", new Color("d2aa3d"), NodePosition("treasure:gold", 390, 40)); Connect("treasure:root", "treasure:gold");
        int row = 0;
        foreach (TreasureEntryKind kind in Enum.GetValues<TreasureEntryKind>())
        {
            TreasureEntryAuthoring[] entries = document.Entries.Where(value => value.Kind == kind).ToArray(); string id = "treasure:" + kind.ToString().ToLowerInvariant();
            AddNode(id, kind.ToString(), $"{entries.Length} rows / weight {entries.Sum(value => value.Weight)}", new Color("4a90d9"), NodePosition(id, 390, 130 + row++ * 110)); Connect("treasure:root", id);
        }
    }

    private void AddNode(string id, string title, string summary, Color color, Vector2 position)
    {
        var node = new GraphNode { Name = id, Title = title, PositionOffset = position, Resizable = false };
        node.AddChild(new Label { Text = summary, CustomMinimumSize = new Vector2(180, 42), AutowrapMode = TextServer.AutowrapMode.WordSmart }); node.SetSlot(0, true, 0, Colors.White, true, 0, Colors.White); WorkbenchUi.StyleGraphNode(node, color);
        GraphElement.DraggedEventHandler handler = (_, to) => StorePosition(id, to); node.Dragged += handler; _drag[id] = handler; _graph!.AddChild(node); _nodes[id] = node;
    }
    private void Connect(string from, string to) { if (_nodes.TryGetValue(from, out GraphNode? source) && _nodes.TryGetValue(to, out GraphNode? target)) _graph!.ConnectNode(source.Name, 0, target.Name, 0); }
    private Vector2 NodePosition(string id, float x, float y)
    {
        AuthoringGraphNodeLayout? stored = Document() switch { EventAuthoringDocument value => value.GraphLayout.Nodes.FirstOrDefault(node => node.NodeId == id), TreasureAuthoringDocument value => value.GraphLayout.Nodes.FirstOrDefault(node => node.NodeId == id), _ => null };
        return stored is null ? new Vector2(x, y) : new Vector2((float)stored.X, (float)stored.Y);
    }
    private void StorePosition(string id, Vector2 position)
    {
        IEnumerable<AuthoringGraphNodeLayout> current = Document() switch { EventAuthoringDocument value => value.GraphLayout.Nodes, TreasureAuthoringDocument value => value.GraphLayout.Nodes, _ => [] };
        var layout = new AuthoringGraphLayout(current.Where(value => value.NodeId != id).Append(new AuthoringGraphNodeLayout(id, position.X, position.Y)));
        if (_event is not null) ReplaceEvent(layout: layout, rebuild: false); else ReplaceTreasure(_treasure!.GoldMinimum, _treasure.GoldMaximum, _treasure.Entries, layout, false);
    }
    private void AutoLayout() { if (_event is not null) ReplaceEvent(layout: new AuthoringGraphLayout()); else if (_treasure is not null) ReplaceTreasure(_treasure.GoldMinimum, _treasure.GoldMaximum, _treasure.Entries, new AuthoringGraphLayout()); }

    private void RefreshInspector()
    {
        if (_inspector is null) return; foreach (Node child in _inspector.GetChildren()) { _inspector.RemoveChild(child); child.QueueFree(); }
        if (_event is not null) BuildEventInspector(_event); else if (_treasure is not null) BuildTreasureInspector(_treasure);
    }
    private void BuildEventInspector(EventAuthoringDocument document)
    {
        var section = WorkbenchUi.InspectorSection(this, "Selected Event node", new Color("4a90d9")); _inspector!.AddChild(section);
        if (_selectedNode == "start") { section.AddChild(Field("Title", document.Title, value => ReplaceEvent(title: value))); section.AddChild(Multiline("Description", document.Description, value => ReplaceEvent(description: value))); section.AddChild(Button("Add option", AddOption)); return; }
        if (_selectedNode == "end") { section.AddChild(new Label { Text = "Terminal node" }); return; }
        string[] parts = _selectedNode.Split(':', 2); if (parts.Length != 2) return; int index = document.Options.ToList().FindIndex(value => value.OptionId == parts[1]); if (index < 0) return; EventOptionAuthoring option = document.Options[index];
        if (parts[0] == "option") { section.AddChild(Field("Option ID", option.OptionId, value => UpdateOption(index, option with { OptionId = value }))); section.AddChild(Multiline("Text", option.Text, value => UpdateOption(index, option with { Text = value }))); section.AddChild(Button("Delete option", () => DeleteOption(index))); }
        else if (parts[0] == "check")
        {
            section.AddChild(EnumField("Attribute", option.Attribute, value => UpdateOption(index, option with { Attribute = value }))); section.AddChild(IntField("Base rate", option.BaseSuccessRate, 0, 100, value => UpdateOption(index, option with { BaseSuccessRate = value })));
            if (option.Attribute == RunEventAttribute.None && option.Failure is not null) { var hidden = WorkbenchUi.InspectorSection(this, "Unreachable Failure data", WorkbenchThemeTokens.Resolve(this).Warning, true); hidden.AddChild(new Label { Text = Outcome(option.Failure) + "\nPreserved; runtime always succeeds." }); _inspector.AddChild(hidden); }
            else if (option.Failure is null) section.AddChild(Button("Add failure outcome", () => UpdateOption(index, option with { Failure = NewNothingOutcome() })));
        }
        else if (parts[0] is "success" or "failure")
        {
            bool success = parts[0] == "success"; EventOutcomeAuthoring? outcome = success ? option.Success : option.Failure;
            if (outcome is null) { section.AddChild(Button("Add failure", () => UpdateOption(index, option with { Failure = new EventOutcomeAuthoring(EventOutcomeType.Nothing, EventOutcomeTarget.Self, 0, null, string.Empty) }))); return; }
            BuildOutcomeInspector(section, index, option, success, outcome);
        }
    }
    private void BuildOutcomeInspector(VBoxContainer section, int index, EventOptionAuthoring option, bool success, EventOutcomeAuthoring outcome)
    {
        void Apply(EventOutcomeAuthoring changed) => UpdateOption(index, success ? option with { Success = changed } : option with { Failure = changed });
        section.AddChild(EnumField("Type", outcome.Type, value => Apply(outcome with { Type = value, EffectContentId = DefaultReference(value, outcome.EffectContentId) })));
        section.AddChild(EnumField("Target", outcome.Target, value => Apply(outcome with { Target = value }))); section.AddChild(IntField("Amount", outcome.Amount, 0, 10000, value => Apply(outcome with { Amount = value })));
        if (outcome.Type is EventOutcomeType.Item or EventOutcomeType.Buff or EventOutcomeType.Debuff) section.AddChild(ContentIdPicker("ContentId", outcome.EffectContentId ?? string.Empty, outcome.Type == EventOutcomeType.Item ? "consumable" : "buff", value => Apply(outcome with { EffectContentId = value })));
        section.AddChild(Multiline("Description", outcome.Description, value => Apply(outcome with { Description = value }))); if (!success) section.AddChild(Button("Remove failure", () => UpdateOption(index, option with { Failure = null })));
    }
    private void BuildTreasureInspector(TreasureAuthoringDocument document)
    {
        var section = WorkbenchUi.InspectorSection(this, "Selected Treasure node", new Color("b18742")); _inspector!.AddChild(section);
        if (_selectedNode == "treasure:root") { section.AddChild(new Label { Text = document.ContentId }); return; }
        if (_selectedNode == "treasure:gold") { section.AddChild(IntField("Gold minimum", document.GoldMinimum, 0, 10000, value => ReplaceTreasure(value, document.GoldMaximum, document.Entries))); section.AddChild(IntField("Gold maximum", document.GoldMaximum, 0, 10000, value => ReplaceTreasure(document.GoldMinimum, value, document.Entries))); return; }
        TreasureEntryKind kind = Enum.Parse<TreasureEntryKind>(_selectedNode.Split(':')[1], true);
        (TreasureEntryAuthoring Entry, int Index)[] entries = document.Entries.Select((entry, index) => (entry, index)).Where(value => value.entry.Kind == kind).Select(value => (value.entry, value.index)).ToArray();
        int total = entries.Sum(value => value.Entry.Weight); section.AddChild(new Label { Text = total == 0 ? "Empty weighted table" : $"{entries.Length} rows · total weight {total}" });
        section.AddChild(Button("Add entry", () => AddEntry(kind)));
        string catalogType = kind switch { TreasureEntryKind.Equipment => "equipment", TreasureEntryKind.Consumable => "consumable", _ => "buff" };
        for (int localIndex = 0; localIndex < entries.Length; localIndex++)
        {
            (TreasureEntryAuthoring entry, int globalIndex) = entries[localIndex];
            var row = WorkbenchUi.InspectorSection(this, $"{localIndex + 1}. {entry.ContentId}", new Color("4a90d9"), true); section.AddChild(row);
            row.AddChild(ContentIdPicker("ContentId", entry.ContentId, catalogType, value => UpdateEntry(globalIndex, entry with { ContentId = value })));
            row.AddChild(IntField("Weight", entry.Weight, 0, 100000, value => UpdateEntry(globalIndex, entry with { Weight = value })));
            row.AddChild(new Label { Text = total <= 0 ? "Probability 0%" : $"Probability {100d * entry.Weight / total:0.###}%" });
            var actions = new HBoxContainer();
            actions.AddChild(Button("Up", () => MoveEntry(globalIndex, -1))); actions.AddChild(Button("Down", () => MoveEntry(globalIndex, 1))); actions.AddChild(Button("Delete", () => DeleteEntryAt(globalIndex))); row.AddChild(actions);
        }
    }

    private void ReplaceEvent(string? title = null, string? description = null, IReadOnlyList<EventOptionAuthoring>? options = null, AuthoringGraphLayout? layout = null, bool rebuild = true)
    { EventAuthoringDocument source = _event!; Draft(() => _event = new EventAuthoringDocument(source.ContentId, source.SourceId, title ?? source.Title, description ?? source.Description, options ?? source.Options, source.SourcePath, source.SourceSha256, layout ?? source.GraphLayout), rebuild); }
    private void UpdateOption(int index, EventOptionAuthoring changed)
    {
        string oldId = _event!.Options[index].OptionId; EventOptionAuthoring[] values = _event.Options.ToArray(); values[index] = changed;
        AuthoringGraphLayout layout = _event.GraphLayout;
        if (!string.Equals(oldId, changed.OptionId, StringComparison.Ordinal))
        {
            string[] roles = ["option", "check", "success", "failure"];
            layout = new AuthoringGraphLayout(layout.Nodes.Select(node => roles.Select(role => $"{role}:{oldId}").Contains(node.NodeId, StringComparer.Ordinal)
                ? new AuthoringGraphNodeLayout(node.NodeId.Replace(oldId, changed.OptionId, StringComparison.Ordinal), node.X, node.Y) : node));
            _selectedNode = _selectedNode.Replace(oldId, changed.OptionId, StringComparison.Ordinal);
        }
        ReplaceEvent(options: values, layout: layout);
    }
    private void AddOption() { int suffix = 1; string id; do id = $"option_{suffix++}"; while (_event!.Options.Any(value => value.OptionId == id)); _selectedNode = "option:" + id; ReplaceEvent(options: _event.Options.Append(new EventOptionAuthoring(id, "New option", RunEventAttribute.None, 100, new EventOutcomeAuthoring(EventOutcomeType.Nothing, EventOutcomeTarget.All, 0, null, string.Empty), null)).ToArray()); }
    private void DeleteOption(int index) { if (_event!.Options.Count <= 1) { SetStatus("An event must retain one option.", true); return; } string id = _event.Options[index].OptionId; string[] roles = ["option", "check", "success", "failure"]; var layout = new AuthoringGraphLayout(_event.GraphLayout.Nodes.Where(node => !roles.Select(role => $"{role}:{id}").Contains(node.NodeId, StringComparer.Ordinal))); _selectedNode = "start"; ReplaceEvent(options: _event.Options.Where((_, item) => item != index).ToArray(), layout: layout); }
    private void ReplaceTreasure(int min, int max, IEnumerable<TreasureEntryAuthoring> entries, AuthoringGraphLayout? layout = null, bool rebuild = true) => Draft(() => _treasure = new TreasureAuthoringDocument(_treasure!.ContentId, min, max, entries, layout ?? _treasure.GraphLayout), rebuild);
    private void AddEntry(TreasureEntryKind kind) => ReplaceTreasure(_treasure!.GoldMinimum, _treasure.GoldMaximum, _treasure.Entries.Append(new TreasureEntryAuthoring(kind, FirstContentId(kind), 1)));
    private void UpdateEntry(int index, TreasureEntryAuthoring changed) { TreasureEntryAuthoring[] entries = _treasure!.Entries.ToArray(); entries[index] = changed; ReplaceTreasure(_treasure.GoldMinimum, _treasure.GoldMaximum, entries); }
    private void MoveEntry(int index, int direction) { TreasureEntryAuthoring[] entries = _treasure!.Entries.ToArray(); int[] peers = entries.Select((entry, item) => (entry, item)).Where(value => value.entry.Kind == entries[index].Kind).Select(value => value.item).ToArray(); int position = Array.IndexOf(peers, index), targetPosition = position + direction; if (targetPosition < 0 || targetPosition >= peers.Length) return; int target = peers[targetPosition]; (entries[index], entries[target]) = (entries[target], entries[index]); ReplaceTreasure(_treasure.GoldMinimum, _treasure.GoldMaximum, entries); }
    private void DeleteEntryAt(int index) => ReplaceTreasure(_treasure!.GoldMinimum, _treasure.GoldMaximum, _treasure.Entries.Where((_, item) => item != index));
    private void ValidateDraft() { try { ValidateWorkspaceDraft(); SetStatus("Draft validation passed."); } catch (Exception error) { SetStatus("Validation failed: " + error.Message, true); } }
    private void PreviewDraft() { try { AuthoringPreviewEvidence preview = AuthoringPreviewCompiler.Compile(Document()!, (int)(_seed?.Value ?? 0)); SetStatus(preview.Summary + " " + string.Join("; ", preview.Values.Where(value => value.Key is not ("revision" or "contentId")).Select(value => value.Key + "=" + value.Value))); } catch (Exception error) { SetStatus("Preview failed: " + error.Message, true); } }
    private void RevertAll() { if (_eventResource is not null) _event = EventTreasureAuthoringEditorService.Read(_eventResource); else if (_treasureResource is not null) _treasure = EventTreasureAuthoringEditorService.Read(_treasureResource); if (Document() is not { } document) return; _revision = AuthoringRevision.Compute(document); RebuildGraph(); RefreshInspector(); SetStatus("Draft reverted."); }
    private IAuthoringDocument? Document() => _event ?? (IAuthoringDocument?)_treasure;
    private static string Serialize(IAuthoringDocument value) => value switch { EventAuthoringDocument document => EventAuthoringJson.SerializePayload(document), TreasureAuthoringDocument document => TreasureAuthoringJson.Serialize(document), _ => throw new ArgumentOutOfRangeException(nameof(value)) };
    private void Draft(Action action, bool rebuild = true) { try { action(); if (rebuild) RebuildGraph(); RefreshInspector(); SetStatus("Draft changed; formal Resource unchanged."); } catch (Exception error) { SetStatus(error.Message, true); } }
    private void DisconnectGraphSignals() { foreach ((string id, GraphElement.DraggedEventHandler handler) in _drag) if (_nodes.TryGetValue(id, out GraphNode? node) && GodotObject.IsInstanceValid(node)) node.Dragged -= handler; _drag.Clear(); }
    private string FindMapNodes(string id) { PureRunMapResource map = ResourceLoader.Load<PureRunMapResource>(PureRunMapWorkbench.MapPath, string.Empty, ResourceLoader.CacheMode.Ignore) ?? throw new InvalidOperationException("Map cannot be loaded."); string[] nodes = map.NodeIds.Where((_, index) => map.NodeContentIds[index] == id).ToArray(); return nodes.Length == 0 ? "not linked" : string.Join(", ", nodes); }
    private static string Outcome(EventOutcomeAuthoring value) => value.Type + (value.Amount == 0 ? string.Empty : " " + value.Amount) + (string.IsNullOrWhiteSpace(value.EffectContentId) ? string.Empty : "\n" + value.EffectContentId);
    private static EventOutcomeAuthoring NewNothingOutcome() => new(EventOutcomeType.Nothing, EventOutcomeTarget.Self, 0, null, string.Empty);
    private string? DefaultReference(EventOutcomeType type, string? current) { if (type is not (EventOutcomeType.Item or EventOutcomeType.Buff or EventOutcomeType.Debuff)) return null; string catalogType = type == EventOutcomeType.Item ? "consumable" : "buff"; string[] values = _catalog.GetValueOrDefault(catalogType) ?? []; return !string.IsNullOrWhiteSpace(current) && values.Contains(current, StringComparer.Ordinal) ? current : values.FirstOrDefault() ?? throw new InvalidOperationException($"Catalog has no {catalogType} content."); }
    private string FirstContentId(TreasureEntryKind kind) { string type = kind switch { TreasureEntryKind.Equipment => "equipment", TreasureEntryKind.Consumable => "consumable", _ => "buff" }; return (_catalog.GetValueOrDefault(type) ?? []).FirstOrDefault() ?? throw new InvalidOperationException($"Catalog has no {type} content."); }
    private static Button Button(string text, Action action) { var value = new Button { Text = text }; value.Pressed += action; return value; }
    private static Control Field(string label, string value, Action<string> changed) { var row = Row(label); var edit = new LineEdit { Text = value, SizeFlagsHorizontal = SizeFlags.ExpandFill }; edit.TextSubmitted += text => changed(text); edit.FocusExited += () => changed(edit.Text); row.AddChild(edit); return row; }
    private static Control Multiline(string label, string value, Action<string> changed) { var box = new VBoxContainer(); box.AddChild(new Label { Text = label }); var edit = new TextEdit { Text = value, CustomMinimumSize = new Vector2(0, 72) }; edit.FocusExited += () => changed(edit.Text); box.AddChild(edit); return box; }
    private static Control IntField(string label, int value, int min, int max, Action<int> changed) { var row = Row(label); var edit = new SpinBox { Value = value, MinValue = min, MaxValue = max, Step = 1, SizeFlagsHorizontal = SizeFlags.ExpandFill }; edit.ValueChanged += number => changed((int)number); row.AddChild(edit); return row; }
    private static Control EnumField<T>(string label, T value, Action<T> changed) where T : struct, Enum { var row = Row(label); var edit = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill }; T[] values = Enum.GetValues<T>(); foreach (T item in values) edit.AddItem(item.ToString()); edit.Select(Array.IndexOf(values, value)); edit.ItemSelected += index => changed(values[(int)index]); row.AddChild(edit); return row; }
    private Control ContentIdPicker(string label, string value, string type, Action<string> changed) { var row = Row(label); var picker = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill }; string[] ids = _catalog.GetValueOrDefault(type) ?? []; foreach (string id in ids) picker.AddItem(id); int selected = Array.IndexOf(ids, value); if (selected >= 0) picker.Select(selected); picker.ItemSelected += index => changed(ids[(int)index]); row.AddChild(picker); return row; }
    private static HBoxContainer Row(string label) { var row = new HBoxContainer(); row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(105, 0) }); return row; }
    private void SetStatus(string message, bool error = false) { if (_status is null) return; _status.Text = message; WorkbenchUi.StyleStatus(_status, error); }
}
#endif
