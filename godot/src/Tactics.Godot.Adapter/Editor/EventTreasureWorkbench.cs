#if TOOLS
using Godot;
using Tactics.Application.Authoring;
using Tactics.Core.Runs;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

[Tool]
public partial class EventTreasureWorkbench : VBoxContainer, IAuthoringWorkspaceParticipant
{
    private readonly TacticsAuthoringEditorService _authoring = new();
    private const string CatalogPath = "res://content/ContentCatalog.tres";
    private const string CatalogScriptPath = "res://src/Tactics.Godot.Adapter/Runtime/GodotResourceCatalog.cs";
    private readonly bool _treasureMode;
    private readonly Dictionary<string, string> _paths = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string[]> _catalogIdsByType = new(StringComparer.Ordinal);
    private EditorUndoRedoManager? _undoRedo;
    private ItemList? _resources;
    private VBoxContainer? _form;
    private Label? _status;
    private EventAuthoringDocument? _eventDraft;
    private TreasureAuthoringDocument? _treasureDraft;
    private string? _expectedRevision;
    private PureRunLayerFourResource? _eventResource;
    private PureRunTreasureResource? _treasureResource;
    private int _selectedOption;
    private int _selectedEntry;
    private int _catalogLoadAttempts;
    private SpinBox? _previewSeed;

    public EventTreasureWorkbench() : this(false) { }
    public EventTreasureWorkbench(bool treasureMode) => _treasureMode = treasureMode;
    public void Configure(EditorUndoRedoManager undoRedo) => _undoRedo = undoRedo;
    public string WorkspaceName => _treasureMode ? "Treasure" : "Event";
    public IReadOnlyList<AuthoringDocumentChange> CaptureWorkspaceChanges()
    {
        if (_expectedRevision is null) return Array.Empty<AuthoringDocumentChange>();
        IAuthoringDocument? document = _treasureMode ? _treasureDraft : _eventDraft;
        if (document is null || AuthoringRevision.Compute(document) == _expectedRevision) return Array.Empty<AuthoringDocumentChange>();
        string snapshot = _treasureMode ? TreasureAuthoringJson.Serialize(_treasureDraft!) : EventAuthoringJson.SerializePayload(_eventDraft!);
        return [new AuthoringDocumentChange(_treasureMode ? AuthoringDocumentKind.Treasure : AuthoringDocumentKind.Event,
            document.ContentId, _expectedRevision, snapshot)];
    }
    public void ValidateWorkspaceDraft() { if (_treasureMode) _ = _treasureDraft?.ToCoreDefinition(); else _eventDraft?.Validate(); }
    public void RevertWorkspaceDraft() => RevertAll();
    public void ReloadWorkspaceDocuments() { if (_resources is not null && _resources.GetSelectedItems().Length > 0) SelectResource(_resources.GetSelectedItems()[0]); }

    public override void _Ready()
    {
        if (_undoRedo is null) throw new InvalidOperationException("Editor UndoRedo manager is required.");
        SizeFlagsHorizontal = SizeFlags.ExpandFill; SizeFlagsVertical = SizeFlags.ExpandFill;
        WorkbenchUi.StylePage(this);
        var heading = WorkbenchUi.Toolbar(this); heading.AddChild(new Label { Text = _treasureMode ? "TREASURE WEIGHTED TABLE" : "EVENT AUTHORING" }); AddChild(heading);
        var split = new HSplitContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        _resources = new ItemList { CustomMinimumSize = new Vector2(WorkbenchUi.ResourcePaneWidth, 420), SizeFlagsVertical = SizeFlags.ExpandFill };
        _resources.ItemSelected += SelectResource; split.AddChild(_resources);
        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        var toolbar = WorkbenchUi.Toolbar(this); toolbar.AddChild(Button("Validate", ValidateDraft)); toolbar.AddChild(new Label { Text = "Seed" }); _previewSeed = new SpinBox { MinValue = 0, MaxValue = int.MaxValue, Step = 1, Value = 42, CustomMinimumSize = new Vector2(110, 0) }; toolbar.AddChild(_previewSeed); toolbar.AddChild(Button("Preview", PreviewDraft)); toolbar.AddChild(Button("Revert", RevertAll)); right.AddChild(toolbar);
        var scroll = new ScrollContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        _form = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; scroll.AddChild(_form); right.AddChild(scroll);
        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(0, 54) }; WorkbenchUi.StyleStatus(_status); right.AddChild(_status); split.AddChild(right); AddChild(split);
        CallDeferred(nameof(LoadCatalog));
    }

    public override void _ExitTree()
    {
        _catalogLoadAttempts = 0;
        _eventDraft = null; _treasureDraft = null; _eventResource = null; _treasureResource = null;
    }

    public void LoadCatalog()
    {
        EditorResourceLoadResult<GodotResourceCatalog> result = ReloadSafeEditorResourceLoader.Load<GodotResourceCatalog>(CatalogPath, CatalogScriptPath, "Entries");
        if (ReloadSafeEditorResourceLoader.RetryDeferred(this, MethodName.LoadCatalog, ref _catalogLoadAttempts, result, _treasureMode ? "Treasure workbench" : "Event workbench")) return;
        string type = _treasureMode ? "treasure" : "event";
        foreach (IGrouping<string, GodotResourceEntry> group in result.Resource!.Entries.GroupBy(value => value.ResourceTypeIdValue, StringComparer.Ordinal))
            _catalogIdsByType[group.Key] = group.Select(value => value.ContentIdValue).Order(StringComparer.Ordinal).ToArray();
        string[] itemIds = _catalogIdsByType.GetValueOrDefault("item") ?? Array.Empty<string>();
        _catalogIdsByType["equipment"] = itemIds.Where(value => value.StartsWith("item.equipment.", StringComparison.Ordinal)).ToArray();
        _catalogIdsByType["consumable"] = itemIds.Where(value => value.StartsWith("item.consumable.", StringComparison.Ordinal)).ToArray();
        foreach (GodotResourceEntry entry in result.Resource.Entries.Where(value => value.ResourceTypeIdValue == type))
        { _paths[entry.ContentIdValue] = entry.DiagnosticPathValue; _resources!.AddItem(entry.ContentIdValue); }
        if (_resources!.ItemCount > 0) { _resources.Select(0); SelectResource(0); }
    }

    private void SelectResource(long index)
    {
        string id = _resources!.GetItemText((int)index); string path = _paths[id];
        try
        {
            if (_treasureMode)
            {
                _treasureResource = ResourceLoader.Load<PureRunTreasureResource>(path, string.Empty, ResourceLoader.CacheMode.Ignore) ?? throw new InvalidOperationException($"Treasure cannot be loaded: {path}");
                _treasureDraft = EventTreasureAuthoringEditorService.Read(_treasureResource); _eventResource = null; _eventDraft = null; _expectedRevision = AuthoringRevision.Compute(_treasureDraft);
            }
            else
            {
                _eventResource = ResourceLoader.Load<PureRunLayerFourResource>(path, string.Empty, ResourceLoader.CacheMode.Ignore) ?? throw new InvalidOperationException($"Event cannot be loaded: {path}");
                _eventDraft = EventTreasureAuthoringEditorService.Read(_eventResource); _treasureResource = null; _treasureDraft = null; _expectedRevision = AuthoringRevision.Compute(_eventDraft);
            }
            _selectedOption = 0; _selectedEntry = 0; RefreshForm(); SetStatus($"Loaded {id}. Map nodes: {FindMapNodes(id)}");
        }
        catch (Exception exception) { SetStatus(exception.Message, true); }
    }

    private void RefreshForm()
    {
        if (_form is null) return;
        foreach (Node child in _form.GetChildren()) { _form.RemoveChild(child); child.QueueFree(); }
        if (_treasureMode && _treasureDraft is not null) BuildTreasureForm(_treasureDraft); else if (_eventDraft is not null) BuildEventForm(_eventDraft);
    }

    private void BuildEventForm(EventAuthoringDocument value)
    {
        _form!.AddChild(Field("ContentId", value.ContentId, _ => { }, false));
        _form.AddChild(Field("Title", value.Title, text => ReplaceEvent(title: text)));
        _form.AddChild(Multiline("Description", value.Description, text => ReplaceEvent(description: text)));
        var bar = new HBoxContainer(); var picker = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (EventOptionAuthoring option in value.Options) picker.AddItem($"{option.OptionId} — {option.Text}");
        if (value.Options.Count > 0) picker.Select(Math.Clamp(_selectedOption, 0, value.Options.Count - 1));
        picker.ItemSelected += index => { _selectedOption = (int)index; RefreshForm(); }; bar.AddChild(picker);
        bar.AddChild(Button("Add option", AddOption)); bar.AddChild(Button("Delete option", DeleteOption)); _form.AddChild(bar);
        if (value.Options.Count == 0) return;
        EventOptionAuthoring selected = value.Options[Math.Clamp(_selectedOption, 0, value.Options.Count - 1)];
        _form.AddChild(Field("Option ID", selected.OptionId, text => UpdateOption(selected with { OptionId = text })));
        _form.AddChild(Field("Option text", selected.Text, text => UpdateOption(selected with { Text = text })));
        _form.AddChild(EnumField("Attribute", selected.Attribute, item => UpdateOption(selected with { Attribute = item })));
        _form.AddChild(IntField("Base success rate", selected.BaseSuccessRate, 0, 100, number => UpdateOption(selected with { BaseSuccessRate = number })));
        _form.AddChild(new HSeparator()); _form.AddChild(new Label { Text = "Success" }); BuildOutcome(selected, true, selected.Success);
        _form.AddChild(new Label { Text = "Failure (omitted means runtime uses Success)" });
        if (selected.Failure is null) _form.AddChild(Button("Add failure", () => UpdateOption(selected with { Failure = new EventOutcomeAuthoring(EventOutcomeType.Nothing, EventOutcomeTarget.Self, 0, null, string.Empty) })));
        else { BuildOutcome(selected, false, selected.Failure); _form.AddChild(Button("Remove failure", () => UpdateOption(selected with { Failure = null }))); }
    }

    private void BuildOutcome(EventOptionAuthoring option, bool success, EventOutcomeAuthoring outcome)
    {
        void Apply(EventOutcomeAuthoring changed) => UpdateOption(success ? option with { Success = changed } : option with { Failure = changed });
        _form!.AddChild(EnumField("Type", outcome.Type, type => Apply(outcome with { Type = type, EffectContentId = DefaultOutcomeReference(type, outcome.EffectContentId) })));
        _form.AddChild(EnumField("Target", outcome.Target, target => Apply(outcome with { Target = target })));
        _form.AddChild(IntField("Amount", outcome.Amount, 0, 10000, number => Apply(outcome with { Amount = number })));
        if (outcome.Type is EventOutcomeType.Item or EventOutcomeType.Buff or EventOutcomeType.Debuff)
            _form.AddChild(ContentIdPicker("Effect ContentId", outcome.EffectContentId ?? string.Empty,
                outcome.Type == EventOutcomeType.Item ? "consumable" : "buff", text => Apply(outcome with { EffectContentId = text })));
        _form.AddChild(Multiline("Result description", outcome.Description, text => Apply(outcome with { Description = text })));
    }

    private void BuildTreasureForm(TreasureAuthoringDocument value)
    {
        _form!.AddChild(Field("ContentId", value.ContentId, _ => { }, false));
        _form.AddChild(IntField("Gold minimum", value.GoldMinimum, 0, 50, number => ReplaceTreasure(number, value.GoldMaximum, value.Entries)));
        _form.AddChild(IntField("Gold maximum", value.GoldMaximum, 0, 50, number => ReplaceTreasure(value.GoldMinimum, number, value.Entries)));
        var bar = new HBoxContainer(); var picker = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        int totalWeight = value.Entries.Sum(entry => entry.Weight);
        foreach (TreasureEntryAuthoring entry in value.Entries)
            picker.AddItem($"{entry.Kind,-10}  {entry.ContentId}  weight={entry.Weight}  p={(totalWeight == 0 ? 0 : 100d * entry.Weight / totalWeight):0.##}%");
        if (value.Entries.Count > 0) picker.Select(Math.Clamp(_selectedEntry, 0, value.Entries.Count - 1));
        picker.ItemSelected += index => { _selectedEntry = (int)index; RefreshForm(); }; bar.AddChild(picker);
        bar.AddChild(Button("Add entry", AddEntry)); bar.AddChild(Button("Up", () => MoveEntry(-1))); bar.AddChild(Button("Down", () => MoveEntry(1))); bar.AddChild(Button("Delete entry", DeleteEntry)); _form.AddChild(bar);
        if (value.Entries.Count == 0) return;
        TreasureEntryAuthoring selected = value.Entries[Math.Clamp(_selectedEntry, 0, value.Entries.Count - 1)];
        _form.AddChild(EnumField("Table", selected.Kind, item => UpdateEntry(selected with { Kind = item, ContentId = FirstContentId(item) })));
        _form.AddChild(ContentIdPicker("ContentId", selected.ContentId, selected.Kind switch { TreasureEntryKind.Equipment => "equipment", TreasureEntryKind.Consumable => "consumable", _ => "buff" }, text => UpdateEntry(selected with { ContentId = text })));
        _form.AddChild(IntField("Weight", selected.Weight, 1, 10000, number => UpdateEntry(selected with { Weight = number })));
    }

    private void ReplaceEvent(string? title = null, string? description = null, IReadOnlyList<EventOptionAuthoring>? options = null)
    {
        EventAuthoringDocument source = _eventDraft!;
        TryDraft(() => _eventDraft = new EventAuthoringDocument(source.ContentId, source.SourceId, title ?? source.Title, description ?? source.Description, options ?? source.Options, source.SourcePath, source.SourceSha256));
    }
    private void UpdateOption(EventOptionAuthoring changed) { EventOptionAuthoring[] values = _eventDraft!.Options.ToArray(); values[_selectedOption] = changed; ReplaceEvent(options: values); }
    private void AddOption() { ReplaceEvent(options: _eventDraft!.Options.Append(new EventOptionAuthoring($"option_{_eventDraft.Options.Count + 1}", "New option", RunEventAttribute.None, 100, new EventOutcomeAuthoring(EventOutcomeType.Nothing, EventOutcomeTarget.All, 0, null, string.Empty), null)).ToArray()); _selectedOption = _eventDraft.Options.Count - 1; RefreshForm(); }
    private void DeleteOption() { if (_eventDraft!.Options.Count <= 1) { SetStatus("An event must retain one option.", true); return; } ReplaceEvent(options: _eventDraft.Options.Where((_, i) => i != _selectedOption).ToArray()); _selectedOption = Math.Max(0, _selectedOption - 1); RefreshForm(); }
    private void ReplaceTreasure(int minimum, int maximum, IEnumerable<TreasureEntryAuthoring> entries) => TryDraft(() => _treasureDraft = new TreasureAuthoringDocument(_treasureDraft!.ContentId, minimum, maximum, entries));
    private void UpdateEntry(TreasureEntryAuthoring changed) { TreasureEntryAuthoring[] values = _treasureDraft!.Entries.ToArray(); values[_selectedEntry] = changed; ReplaceTreasure(_treasureDraft.GoldMinimum, _treasureDraft.GoldMaximum, values); }
    private void AddEntry() { ReplaceTreasure(_treasureDraft!.GoldMinimum, _treasureDraft.GoldMaximum, _treasureDraft.Entries.Append(new TreasureEntryAuthoring(TreasureEntryKind.Equipment, FirstContentId(TreasureEntryKind.Equipment), 1))); _selectedEntry = _treasureDraft.Entries.Count - 1; RefreshForm(); }
    private void DeleteEntry() { if (_treasureDraft!.Entries.Count == 0) return; ReplaceTreasure(_treasureDraft.GoldMinimum, _treasureDraft.GoldMaximum, _treasureDraft.Entries.Where((_, i) => i != _selectedEntry)); _selectedEntry = Math.Max(0, _selectedEntry - 1); RefreshForm(); }

    private void MoveEntry(int delta)
    {
        if (_treasureDraft is null || _treasureDraft.Entries.Count < 2) return;
        int target = Math.Clamp(_selectedEntry + delta, 0, _treasureDraft.Entries.Count - 1);
        if (target == _selectedEntry) return;
        TreasureEntryAuthoring[] entries = _treasureDraft.Entries.ToArray();
        (entries[_selectedEntry], entries[target]) = (entries[target], entries[_selectedEntry]);
        ReplaceTreasure(_treasureDraft.GoldMinimum, _treasureDraft.GoldMaximum, entries);
        _selectedEntry = target;
        RefreshForm();
    }

    private void ValidateDraft() { try { if (_treasureMode) _ = _treasureDraft!.ToCoreDefinition(); else _eventDraft!.Validate(); SetStatus("Draft validation passed."); } catch (Exception e) { SetStatus($"Validation failed: {e.Message}", true); } }
    private void PreviewDraft()
    {
        try
        {
            IAuthoringDocument document = _treasureMode ? _treasureDraft! : _eventDraft!;
            AuthoringPreviewEvidence preview = AuthoringPreviewCompiler.Compile(document, (int)(_previewSeed?.Value ?? 0));
            SetStatus(preview.Summary + " " + string.Join("; ", preview.Values.Where(value => value.Key is not ("revision" or "contentId")).Select(value => value.Key + "=" + value.Value)));
        }
        catch (Exception exception) { SetStatus("Preview failed: " + exception.Message, true); }
    }
    private void ApplyAll() { try { if (_treasureMode) ApplyTreasure(); else ApplyEvent(); } catch (Exception e) { SetStatus($"Apply failed: {e.Message}", true); } }
    private void ApplyEvent()
    {
        EventAuthoringDocument current = EventTreasureAuthoringEditorService.Read(_eventResource!); EnsureRevision(current);
        string before = EventAuthoringJson.SerializePayload(current), after = EventAuthoringJson.SerializePayload(_eventDraft!); if (before == after) { SetStatus("Nothing to apply."); return; }
        _undoRedo!.CreateAction("Apply event authoring session", UndoRedo.MergeMode.Disable, _eventResource); _undoRedo.AddDoMethod(this, MethodName.ApplySerializedEvent, after); _undoRedo.AddUndoMethod(this, MethodName.ApplySerializedEvent, before); _undoRedo.CommitAction();
    }
    private void ApplyTreasure()
    {
        TreasureAuthoringDocument current = EventTreasureAuthoringEditorService.Read(_treasureResource!); EnsureRevision(current);
        string before = TreasureAuthoringJson.Serialize(current), after = TreasureAuthoringJson.Serialize(_treasureDraft!); if (before == after) { SetStatus("Nothing to apply."); return; }
        _undoRedo!.CreateAction("Apply treasure authoring session", UndoRedo.MergeMode.Disable, _treasureResource); _undoRedo.AddDoMethod(this, MethodName.ApplySerializedTreasure, after); _undoRedo.AddUndoMethod(this, MethodName.ApplySerializedTreasure, before); _undoRedo.CommitAction();
    }
    public void ApplySerializedEvent(string json)
    {
        StoredAuthoringDocument current = _authoring.Get("event", _eventResource!.ContentIdValue); StoredAuthoringDocument applied = _authoring.ApplySingle("event", current.Document.ContentId, current.Revision, json);
        _eventResource = (PureRunLayerFourResource)applied.Resource; _eventDraft = (EventAuthoringDocument)applied.Document; _expectedRevision = applied.Revision; RefreshForm(); SetStatus("Applied, saved and reload-validated event.");
    }
    public void ApplySerializedTreasure(string json)
    {
        StoredAuthoringDocument current = _authoring.Get("treasure", _treasureResource!.ContentIdValue); StoredAuthoringDocument applied = _authoring.ApplySingle("treasure", current.Document.ContentId, current.Revision, json);
        _treasureResource = (PureRunTreasureResource)applied.Resource; _treasureDraft = (TreasureAuthoringDocument)applied.Document; _expectedRevision = applied.Revision; RefreshForm(); SetStatus("Applied, saved and reload-validated treasure.");
    }
    private void RevertAll() { if (_treasureMode) _treasureDraft = EventTreasureAuthoringEditorService.Read(_treasureResource!); else _eventDraft = EventTreasureAuthoringEditorService.Read(_eventResource!); _expectedRevision = AuthoringRevision.Compute(_treasureMode ? (IAuthoringDocument)_treasureDraft! : _eventDraft!); RefreshForm(); SetStatus("Draft reverted."); }
    private void EnsureRevision(IAuthoringDocument current) { if (AuthoringRevision.Compute(current) != _expectedRevision) throw new InvalidOperationException("Resource changed outside this session; reload before applying."); }
    private void TryDraft(Action action) { try { action(); RefreshForm(); SetStatus("Draft changed; formal Resource unchanged."); } catch (Exception e) { SetStatus(e.Message, true); } }
    private string FindMapNodes(string contentId) { PureRunMapResource map = ResourceLoader.Load<PureRunMapResource>(PureRunMapWorkbench.MapPath, string.Empty, ResourceLoader.CacheMode.Ignore) ?? throw new InvalidOperationException("Map cannot be loaded."); string[] nodes = map.NodeIds.Where((_, i) => map.NodeContentIds[i] == contentId).ToArray(); return nodes.Length == 0 ? "not linked" : string.Join(", ", nodes); }
    private string? DefaultOutcomeReference(EventOutcomeType type, string? current)
    {
        if (type is not (EventOutcomeType.Item or EventOutcomeType.Buff or EventOutcomeType.Debuff)) return null;
        string catalogType = type == EventOutcomeType.Item ? "consumable" : "buff";
        if (!string.IsNullOrWhiteSpace(current) && _catalogIdsByType.TryGetValue(catalogType, out string[]? existing) && existing.Contains(current, StringComparer.Ordinal)) return current;
        return _catalogIdsByType.TryGetValue(catalogType, out string[]? ids) && ids.Length > 0 ? ids[0] : throw new InvalidOperationException($"Catalog has no {catalogType} entry for {type} outcomes.");
    }
    private string FirstContentId(TreasureEntryKind kind)
    {
        string type = kind switch { TreasureEntryKind.Equipment => "equipment", TreasureEntryKind.Consumable => "consumable", _ => "buff" };
        return _catalogIdsByType.TryGetValue(type, out string[]? ids) && ids.Length > 0 ? ids[0] : throw new InvalidOperationException($"Catalog has no {type} content.");
    }

    private static Button Button(string text, Action action) { var value = new Button { Text = text }; value.Pressed += action; return value; }
    private static Control Field(string label, string value, Action<string> changed, bool editable = true) { var row = Row(label); var edit = new LineEdit { Text = value, Editable = editable, SizeFlagsHorizontal = SizeFlags.ExpandFill }; edit.TextSubmitted += text => changed(text); edit.FocusExited += () => changed(edit.Text); row.AddChild(edit); return row; }
    private static Control Multiline(string label, string value, Action<string> changed) { var row = new VBoxContainer(); row.AddChild(new Label { Text = label }); var edit = new TextEdit { Text = value, CustomMinimumSize = new Vector2(0, 72), SizeFlagsHorizontal = SizeFlags.ExpandFill }; edit.FocusExited += () => changed(edit.Text); row.AddChild(edit); return row; }
    private static Control IntField(string label, int value, int min, int max, Action<int> changed) { var row = Row(label); var edit = new SpinBox { Value = value, MinValue = min, MaxValue = max, Step = 1, SizeFlagsHorizontal = SizeFlags.ExpandFill }; edit.ValueChanged += number => changed((int)number); row.AddChild(edit); return row; }
    private static Control EnumField<T>(string label, T value, Action<T> changed) where T : struct, Enum { var row = Row(label); var edit = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill }; T[] values = Enum.GetValues<T>(); foreach (T item in values) edit.AddItem(item.ToString()); edit.Select(Array.IndexOf(values, value)); edit.ItemSelected += index => changed(values[(int)index]); row.AddChild(edit); return row; }
    private Control ContentIdPicker(string label, string value, string catalogType, Action<string> changed)
    {
        var row = Row(label); var picker = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        string[] ids = _catalogIdsByType.GetValueOrDefault(catalogType) ?? Array.Empty<string>();
        foreach (string id in ids) picker.AddItem(id);
        int selected = Array.IndexOf(ids, value); if (selected >= 0) picker.Select(selected);
        picker.ItemSelected += index => changed(ids[(int)index]); row.AddChild(picker); return row;
    }
    private static HBoxContainer Row(string label) { var row = new HBoxContainer(); row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(170, 0) }); return row; }
    private void SetStatus(string message, bool error = false) { if (_status is null) return; _status.Text = message; WorkbenchUi.StyleStatus(_status, error); }
}
#endif
