#if TOOLS
using Godot;
using Tactics.Application.Authoring;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

[Tool]
public partial class EncounterAuthoringWorkbench : VBoxContainer, IAuthoringWorkspaceParticipant
{
    private readonly TacticsAuthoringEditorService _authoring = new();
    private const string CatalogPath = "res://content/ContentCatalog.tres";
    private const string CatalogScriptPath = "res://src/Tactics.Godot.Adapter/Runtime/GodotResourceCatalog.cs";
    private readonly Dictionary<string, GodotResourceEntry> _entries = new(StringComparer.Ordinal);
    private EditorUndoRedoManager? _undoRedo;
    private OptionButton? _picker;
    private TextEdit? _encounterJson;
    private GridContainer? _grid;
    private Label? _status;
    private EncounterDefinitionResource? _encounterResource;
    private BattleLayoutResource? _layoutResource;
    private EncounterAuthoringDocument? _encounterDraft;
    private BattleLayoutAuthoringDocument? _layoutDraft;
    private string? _encounterRevision;
    private string? _layoutRevision;
    private string _encounterPath = string.Empty;
    private string _layoutPath = string.Empty;
    private int _catalogLoadAttempts;
    private EncounterFixtureWorkbench? _preview;
    private Label? _selectedCell;

    public void Configure(EditorUndoRedoManager undoRedo) => _undoRedo = undoRedo;
    public string WorkspaceName => "Encounter / Layout";
    public IReadOnlyList<AuthoringDocumentChange> CaptureWorkspaceChanges()
    {
        if (_encounterDraft is null || _layoutDraft is null || _encounterRevision is null || _layoutRevision is null)
            return Array.Empty<AuthoringDocumentChange>();
        EncounterAuthoringDocument encounter = EncounterAuthoringJson.Deserialize(_encounterJson?.Text ?? EncounterAuthoringJson.Serialize(_encounterDraft));
        var result = new List<AuthoringDocumentChange>();
        if (AuthoringRevision.Compute(encounter) != _encounterRevision)
            result.Add(new AuthoringDocumentChange(AuthoringDocumentKind.Encounter, encounter.ContentId, _encounterRevision, EncounterAuthoringJson.Serialize(encounter)));
        if (AuthoringRevision.Compute(_layoutDraft) != _layoutRevision)
            result.Add(new AuthoringDocumentChange(AuthoringDocumentKind.BattleLayout, _layoutDraft.ContentId, _layoutRevision, BattleLayoutAuthoringJson.Serialize(_layoutDraft)));
        return Array.AsReadOnly(result.ToArray());
    }
    public void ValidateWorkspaceDraft() { if (_encounterDraft is not null && _layoutDraft is not null) ValidatePair(EncounterAuthoringJson.Deserialize(_encounterJson?.Text ?? EncounterAuthoringJson.Serialize(_encounterDraft)), _layoutDraft); }
    public void RevertWorkspaceDraft() => RevertAll();
    public void ReloadWorkspaceDocuments() { if (_picker is not null && _picker.Selected >= 0) LoadSelected(_picker.Selected); }
    public void ConfigurePreview(EncounterFixtureWorkbench preview) => _preview = preview;
    public override void _Ready()
    {
        if (_undoRedo is null) throw new InvalidOperationException("Editor UndoRedo manager is required.");
        SizeFlagsHorizontal = SizeFlags.ExpandFill; SizeFlagsVertical = SizeFlags.ExpandFill;
        WorkbenchUi.StylePage(this);
        var toolbar = WorkbenchUi.Toolbar(this); toolbar.AddChild(new Label { Text = "ENCOUNTER / LAYOUT" }); _picker = new OptionButton { CustomMinimumSize = new Vector2(300, 0) }; _picker.ItemSelected += LoadSelected; toolbar.AddChild(_picker);
        AddButton(toolbar, "Import Draft", ImportEncounter); AddButton(toolbar, "Validate", ValidateDraft); AddButton(toolbar, "Preview", PreviewDraft); AddButton(toolbar, "Revert", RevertAll); AddChild(toolbar);
        var split = new HSplitContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        var left = WorkbenchUi.Pane(this, 280); left.AddChild(new Label { Text = "ENCOUNTER SNAPSHOT" }); left.AddChild(new Label { Text = "Unit/AI pairs, multipliers, mana and encounter class" });
        _encounterJson = new TextEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill }; left.AddChild(_encounterJson); split.AddChild(left);
        var board = WorkbenchUi.Pane(this, 460); board.AddChild(new Label { Text = "10×10 LAYOUT  • click cycles Empty → Party → Enemy → Blocked" });
        _grid = new GridContainer { Columns = 10, SizeFlagsHorizontal = SizeFlags.ShrinkCenter, SizeFlagsVertical = SizeFlags.ShrinkCenter }; board.AddChild(_grid); split.AddChild(board);
        var inspector = WorkbenchUi.Pane(this, WorkbenchUi.InspectorWidth);
        var cellInfo = WorkbenchUi.InspectorSection(this, "Selected cell", new Color("4a90d9")); _selectedCell = new Label { Text = "Click a cell to inspect and cycle its role." }; cellInfo.AddChild(_selectedCell); inspector.AddChild(cellInfo);
        var legend = WorkbenchUi.InspectorSection(this, "Spawn legend"); foreach ((string glyph, string label, Color color) in new[] { ("P", "Party", new Color("4a90d9")), ("E", "Enemy", new Color("ef5350")), ("X", "Blocked", new Color("9aa4b2")), ("·", "Empty", new Color("4a4d52")) }) { var row = new HBoxContainer(); var marker = new Label { Text = glyph, CustomMinimumSize = new Vector2(24, 24), HorizontalAlignment = HorizontalAlignment.Center }; marker.AddThemeColorOverride("font_color", color); row.AddChild(marker); row.AddChild(new Label { Text = label }); legend.AddChild(row); } inspector.AddChild(legend);
        inspector.AddChild(new Label { Text = "Use Preview to compile this draft into the isolated fixed-seed fixture. Formal resources remain unchanged." }); split.AddChild(inspector); AddChild(split);
        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(0, 48) }; WorkbenchUi.StyleStatus(_status); AddChild(_status); CallDeferred(nameof(LoadCatalog));
    }
    public override void _ExitTree() { _catalogLoadAttempts = 0; _encounterResource = null; _layoutResource = null; }
    public void LoadCatalog()
    {
        EditorResourceLoadResult<GodotResourceCatalog> result = ReloadSafeEditorResourceLoader.Load<GodotResourceCatalog>(CatalogPath, CatalogScriptPath, "Entries");
        if (ReloadSafeEditorResourceLoader.RetryDeferred(this, MethodName.LoadCatalog, ref _catalogLoadAttempts, result, "Encounter authoring")) return;
        foreach (GodotResourceEntry entry in result.Resource!.Entries) _entries[entry.ContentIdValue] = entry;
        foreach (GodotResourceEntry entry in result.Resource.Entries.Where(value => value.ResourceTypeIdValue == "encounter")) _picker!.AddItem(entry.ContentIdValue);
        if (_picker!.ItemCount > 0) LoadSelected(0);
    }
    private void LoadSelected(long index)
    {
        try
        {
            string id = _picker!.GetItemText((int)index); _encounterPath = _entries[id].DiagnosticPathValue;
            _encounterResource = ResourceLoader.Load<EncounterDefinitionResource>(_encounterPath, string.Empty, ResourceLoader.CacheMode.Ignore) ?? throw new InvalidOperationException($"Encounter cannot be loaded: {id}");
            _encounterDraft = EncounterAuthoringEditorService.Read(_encounterResource); if (!_entries.TryGetValue(_encounterDraft.LayoutContentId, out GodotResourceEntry? layoutEntry)) throw new InvalidOperationException($"Layout missing from Catalog: {_encounterDraft.LayoutContentId}");
            _layoutPath = layoutEntry.DiagnosticPathValue; _layoutResource = ResourceLoader.Load<BattleLayoutResource>(_layoutPath, string.Empty, ResourceLoader.CacheMode.Ignore) ?? throw new InvalidOperationException($"Layout cannot be loaded: {_layoutPath}");
            _layoutDraft = EncounterAuthoringEditorService.Read(_layoutResource); _encounterRevision = AuthoringRevision.Compute(_encounterDraft); _layoutRevision = AuthoringRevision.Compute(_layoutDraft); Refresh(); SetStatus($"Loaded {id} with {_layoutDraft.EnemySpawns.Count} enemy spawn cells.");
        }
        catch (Exception e) { SetStatus(e.Message, true); }
    }
    private void Refresh()
    {
        if (_encounterDraft is null || _layoutDraft is null) return; _encounterJson!.Text = EncounterAuthoringJson.Serialize(_encounterDraft);
        foreach (Node child in _grid!.GetChildren()) { _grid.RemoveChild(child); child.QueueFree(); }
        for (int y = 0; y < 10; y++) for (int x = 0; x < 10; x++) { int cellX = x, cellY = y; string state = CellState(cellX, cellY); var button = new Button { Text = state, CustomMinimumSize = new Vector2(42, 34), TooltipText = $"({x},{y}) {state}" }; Color color = state switch { "P" => new Color("4a90d9"), "E" => new Color("ef5350"), "X" => new Color("687382"), _ => new Color("2b2d31") }; button.AddThemeStyleboxOverride("normal", WorkbenchUi.PanelStyle(color.Darkened(.35f), 3, 1, color)); button.Pressed += () => CycleCell(cellX, cellY); _grid.AddChild(button); }
    }
    private string CellState(int x, int y) { var cell = new GridCellAuthoring(x, y); if (_layoutDraft!.PartySpawns.Contains(cell)) return "P"; if (_layoutDraft.EnemySpawns.Contains(cell)) return "E"; if (_layoutDraft.BlockedCells.Contains(cell)) return "X"; return "·"; }
    private void CycleCell(int x, int y)
    {
        try
        {
            var cell = new GridCellAuthoring(x, y); List<GridCellAuthoring> party = _layoutDraft!.PartySpawns.Where(value => value != cell).ToList(); List<GridCellAuthoring> enemies = _layoutDraft.EnemySpawns.Where(value => value != cell).ToList(); List<GridCellAuthoring> blocked = _layoutDraft.BlockedCells.Where(value => value != cell).ToList();
            switch (CellState(x, y)) { case "·": party.Add(cell); break; case "P": enemies.Add(cell); break; case "E": blocked.Add(cell); break; }
            _layoutDraft = new BattleLayoutAuthoringDocument(_layoutDraft.ContentId, party, enemies, blocked); if (_selectedCell is not null) _selectedCell.Text = $"Cell ({x}, {y}) → {CellState(x, y)}"; Refresh(); SetStatus("Layout draft changed; formal Resource unchanged.");
        }
        catch (Exception e) { SetStatus(e.Message, true); }
    }
    private void ImportEncounter() { try { EncounterAuthoringDocument value = EncounterAuthoringJson.Deserialize(_encounterJson!.Text); if (value.ContentId != _encounterDraft!.ContentId || value.LayoutContentId != _layoutDraft!.ContentId) throw new InvalidOperationException("Encounter/Layout identity cannot be changed in this session."); _encounterDraft = value; SetStatus("Encounter snapshot imported to draft."); } catch (Exception e) { SetStatus(e.Message, true); } }
    private void ValidateDraft() { try { _encounterDraft = EncounterAuthoringJson.Deserialize(_encounterJson!.Text); ValidatePair(_encounterDraft, _layoutDraft!); SetStatus("Encounter and 10×10 layout validation passed."); } catch (Exception e) { SetStatus(e.Message, true); } }
    private void PreviewDraft()
    {
        try
        {
            EncounterAuthoringDocument encounter = EncounterAuthoringJson.Deserialize(_encounterJson!.Text);
            BattleLayoutAuthoringDocument layout = _layoutDraft ?? throw new InvalidOperationException("Layout draft is not loaded.");
            ValidatePair(encounter, layout); _encounterDraft = encounter;
            (_preview ?? throw new InvalidOperationException("Encounter preview surface is not configured.")).LoadDraft(encounter.ToCoreDefinition(), layout.ToCoreDefinition());
            SetStatus("Draft compiled into the fixed-seed fixture. Switch to Fixed Seed Preview to step AI or run a round.");
        }
        catch (Exception exception) { SetStatus("Preview failed: " + exception.Message, true); }
    }
    private void ApplyAll()
    {
        try
        {
            EncounterAuthoringDocument currentEncounter = EncounterAuthoringEditorService.Read(_encounterResource!); BattleLayoutAuthoringDocument currentLayout = EncounterAuthoringEditorService.Read(_layoutResource!);
            if (AuthoringRevision.Compute(currentEncounter) != _encounterRevision || AuthoringRevision.Compute(currentLayout) != _layoutRevision) throw new InvalidOperationException("Encounter or Layout changed outside this session.");
            EncounterAuthoringDocument afterEncounter = EncounterAuthoringJson.Deserialize(_encounterJson!.Text); ValidatePair(afterEncounter, _layoutDraft!); string beforeEncounter = EncounterAuthoringJson.Serialize(currentEncounter), afterEncounterJson = EncounterAuthoringJson.Serialize(afterEncounter); string beforeLayout = BattleLayoutAuthoringJson.Serialize(currentLayout), afterLayout = BattleLayoutAuthoringJson.Serialize(_layoutDraft!);
            _undoRedo!.CreateAction("Apply Encounter and Layout authoring session", UndoRedo.MergeMode.Disable, _encounterResource); _undoRedo.AddDoMethod(this, MethodName.ApplySerializedBatch, afterEncounterJson, afterLayout); _undoRedo.AddUndoMethod(this, MethodName.ApplySerializedBatch, beforeEncounter, beforeLayout); _undoRedo.CommitAction();
        }
        catch (Exception e) { SetStatus($"Apply failed: {e.Message}", true); }
    }
    public void ApplySerializedBatch(string encounterJson, string layoutJson)
    {
        EncounterAuthoringDocument encounter = EncounterAuthoringJson.Deserialize(encounterJson); BattleLayoutAuthoringDocument layout = BattleLayoutAuthoringJson.Deserialize(layoutJson); ValidatePair(encounter, layout);
        StoredAuthoringDocument currentEncounter = _authoring.Get("encounter", encounter.ContentId); StoredAuthoringDocument currentLayout = _authoring.Get("battle-layout", layout.ContentId);
        IReadOnlyList<StoredAuthoringDocument> applied = _authoring.ApplyBatch(new AuthoringBatchChangeSet("encounter-layout-workbench", new[]
        {
            new AuthoringDocumentChange(AuthoringDocumentKind.Encounter, encounter.ContentId, currentEncounter.Revision, encounterJson),
            new AuthoringDocumentChange(AuthoringDocumentKind.BattleLayout, layout.ContentId, currentLayout.Revision, layoutJson)
        }));
        _encounterResource = (EncounterDefinitionResource)applied.Single(value => value.Entry.ResourceTypeIdValue == "encounter").Resource;
        _layoutResource = (BattleLayoutResource)applied.Single(value => value.Entry.ResourceTypeIdValue == "battle-layout").Resource;
        _encounterDraft = EncounterAuthoringEditorService.Read(_encounterResource); _layoutDraft = EncounterAuthoringEditorService.Read(_layoutResource); ValidatePair(_encounterDraft, _layoutDraft); _encounterRevision = AuthoringRevision.Compute(_encounterDraft); _layoutRevision = AuthoringRevision.Compute(_layoutDraft); Refresh(); SetStatus("Encounter + Layout atomically saved and reload-validated.");
    }
    private void RevertAll() { if (_encounterResource is null || _layoutResource is null) return; _encounterDraft = EncounterAuthoringEditorService.Read(_encounterResource); _layoutDraft = EncounterAuthoringEditorService.Read(_layoutResource); _encounterRevision = AuthoringRevision.Compute(_encounterDraft); _layoutRevision = AuthoringRevision.Compute(_layoutDraft); Refresh(); SetStatus("Encounter and Layout drafts reverted."); }
    private void ValidatePair(EncounterAuthoringDocument encounter, BattleLayoutAuthoringDocument layout)
    {
        EncounterLayoutAuthoringValidator.Validate(encounter, layout);
        if (!_entries.TryGetValue(encounter.LayoutContentId, out GodotResourceEntry? layoutEntry) || layoutEntry.ResourceTypeIdValue != "battle-layout") throw new InvalidOperationException($"Unknown BattleLayout ContentId '{encounter.LayoutContentId}'.");
        foreach (string id in encounter.MonsterUnitContentIds) if (!_entries.TryGetValue(id, out GodotResourceEntry? entry) || entry.ResourceTypeIdValue != "unit") throw new InvalidOperationException($"Unknown enemy Unit ContentId '{id}'.");
        foreach (string id in encounter.MonsterAiContentIds) if (!_entries.TryGetValue(id, out GodotResourceEntry? entry) || entry.ResourceTypeIdValue != "ai") throw new InvalidOperationException($"Unknown AI ContentId '{id}'.");
    }
    private static void AddButton(Container parent, string text, Action action) { var button = new Button { Text = text }; button.Pressed += action; parent.AddChild(button); }
    private void SetStatus(string message, bool error = false) { if (_status is null) return; _status.Text = message; WorkbenchUi.StyleStatus(_status, error); }
}
#endif
