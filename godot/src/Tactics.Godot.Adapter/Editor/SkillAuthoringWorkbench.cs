#if TOOLS
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using Tactics.Application.Authoring;
using Tactics.Core.Skills;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

[Tool]
public partial class SkillAuthoringWorkbench : VBoxContainer, IAuthoringWorkspaceParticipant
{
    private readonly TacticsAuthoringEditorService _authoring = new();
    private const string CatalogPath = "res://content/ContentCatalog.tres";
    private const string CatalogScriptPath = "res://src/Tactics.Godot.Adapter/Runtime/GodotResourceCatalog.cs";
    private readonly Dictionary<string, string> _paths = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string[]> _catalogIdsByType = new(StringComparer.Ordinal);
    private EditorUndoRedoManager? _undoRedo;
    private OptionButton? _picker;
    private TextEdit? _snapshot;
    private VBoxContainer? _form;
    private Label? _status;
    private OptionButton? _previewEncounter;
    private LineEdit? _previewTarget;
    private SpinBox? _previewX;
    private SpinBox? _previewY;
    private SpinBox? _previewSeed;
    private SkillDefinitionResource? _resource;
    private SkillAuthoringDocument? _draft;
    private string _path = string.Empty;
    private string? _expectedRevision;
    private int _catalogLoadAttempts;

    public void Configure(EditorUndoRedoManager undoRedo) => _undoRedo = undoRedo;
    public string WorkspaceName => "Skill";
    public IReadOnlyList<AuthoringDocumentChange> CaptureWorkspaceChanges()
    {
        if (_snapshot is null || _draft is null || _expectedRevision is null) return Array.Empty<AuthoringDocumentChange>();
        SkillAuthoringDocument document = SkillAuthoringJson.Deserialize(_snapshot.Text);
        return AuthoringRevision.Compute(document) == _expectedRevision ? Array.Empty<AuthoringDocumentChange>()
            : [new AuthoringDocumentChange(AuthoringDocumentKind.Skill, document.ContentId, _expectedRevision, SkillAuthoringJson.Serialize(document))];
    }
    public void ValidateWorkspaceDraft() { if (_snapshot is not null) _ = SkillAuthoringJson.Deserialize(_snapshot.Text).Definition; }
    public void RevertWorkspaceDraft() => RevertAll();
    public void ReloadWorkspaceDocuments() { if (_picker is not null && _picker.Selected >= 0) LoadSelected(_picker.Selected); }
    public override void _Ready()
    {
        if (_undoRedo is null) throw new InvalidOperationException("Editor UndoRedo manager is required.");
        SizeFlagsHorizontal = SizeFlags.ExpandFill; SizeFlagsVertical = SizeFlags.ExpandFill;
        var toolbar = new HBoxContainer(); _picker = new OptionButton { CustomMinimumSize = new Vector2(300, 0) }; _picker.ItemSelected += LoadSelected; toolbar.AddChild(_picker);
        AddButton(toolbar, "Validate / Compile", ValidateDraft); AddButton(toolbar, "Preview Battle", PreviewDraft); AddButton(toolbar, "Revert", RevertAll); AddButton(toolbar, "Advanced JSON", ToggleAdvancedJson); AddChild(toolbar);
        var preview = new HBoxContainer(); preview.AddChild(new Label { Text = "Encounter" });
        _previewEncounter = new OptionButton { CustomMinimumSize = new Vector2(260, 0) }; preview.AddChild(_previewEncounter);
        preview.AddChild(new Label { Text = "Target instance" }); _previewTarget = new LineEdit { Text = "preview.enemy.0", CustomMinimumSize = new Vector2(150, 0) }; preview.AddChild(_previewTarget);
        preview.AddChild(new Label { Text = "Cell X/Y" }); _previewX = new SpinBox { MinValue = 0, MaxValue = 9, Step = 1, Value = 7 }; preview.AddChild(_previewX);
        _previewY = new SpinBox { MinValue = 0, MaxValue = 9, Step = 1, Value = 4 }; preview.AddChild(_previewY);
        preview.AddChild(new Label { Text = "Seed" }); _previewSeed = new SpinBox { MinValue = 0, MaxValue = int.MaxValue, Step = 1, Value = 42 }; preview.AddChild(_previewSeed); AddChild(preview);
        AddChild(new Label { Text = "Structured SkillDefinition inspector — fields are grouped by runtime execution contract; frozen migration provenance is read-only." });
        var scroll = new ScrollContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        _form = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; scroll.AddChild(_form); AddChild(scroll);
        _snapshot = new TextEdit { Visible = false, CustomMinimumSize = new Vector2(0, 180), SizeFlagsHorizontal = SizeFlags.ExpandFill }; AddChild(_snapshot);
        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart }; AddChild(_status); CallDeferred(nameof(LoadCatalog));
    }
    public override void _ExitTree() { _catalogLoadAttempts = 0; if (_picker is not null) _picker.ItemSelected -= LoadSelected; _resource = null; _draft = null; }
    public void LoadCatalog()
    {
        EditorResourceLoadResult<GodotResourceCatalog> result = ReloadSafeEditorResourceLoader.Load<GodotResourceCatalog>(CatalogPath, CatalogScriptPath, "Entries");
        if (ReloadSafeEditorResourceLoader.RetryDeferred(this, MethodName.LoadCatalog, ref _catalogLoadAttempts, result, "Skill authoring workbench")) return;
        foreach (IGrouping<string, GodotResourceEntry> group in result.Resource!.Entries.GroupBy(value => value.ResourceTypeIdValue, StringComparer.Ordinal))
            _catalogIdsByType[group.Key] = group.Select(value => value.ContentIdValue).Order(StringComparer.Ordinal).ToArray();
        foreach (string encounterId in _catalogIdsByType.GetValueOrDefault("encounter") ?? Array.Empty<string>())
            _previewEncounter!.AddItem(encounterId);
        foreach (GodotResourceEntry entry in result.Resource.Entries.Where(value => value.ResourceTypeIdValue == "skill")) { _paths[entry.ContentIdValue] = entry.DiagnosticPathValue; _picker!.AddItem(entry.ContentIdValue); }
        if (_picker!.ItemCount > 0) LoadSelected(0);
    }
    private void LoadSelected(long index)
    {
        try
        {
            string id = _picker!.GetItemText((int)index); _path = _paths[id]; _resource = ResourceLoader.Load<SkillDefinitionResource>(_path, string.Empty, ResourceLoader.CacheMode.Ignore) ?? throw new InvalidOperationException($"Skill cannot be loaded: {_path}");
            _draft = SkillAuthoringEditorService.Read(_resource); _expectedRevision = AuthoringRevision.Compute(_draft); _snapshot!.Text = SkillAuthoringJson.Serialize(_draft); RefreshForm(); SetStatus($"Loaded and Core-compiled {id} ({_draft.Definition.ExecutionKind}).");
        }
        catch (Exception e) { SetStatus(e.Message, true); }
    }
    private void ImportDraft()
    {
        try { SkillAuthoringDocument imported = SkillAuthoringJson.Deserialize(_snapshot!.Text); if (imported.ContentId != _draft!.ContentId) throw new InvalidOperationException("Imported ContentId differs from the active Skill."); _draft = imported; RefreshForm(); SetStatus("Imported typed snapshot into draft; formal Resource unchanged."); }
        catch (Exception e) { SetStatus($"Import failed: {e.Message}", true); }
    }
    private void ValidateDraft() { try { _draft = SkillAuthoringJson.Deserialize(_snapshot!.Text); AuthoringValidationResult validation = _authoring.Validate("skill", _draft.ContentId, _snapshot.Text, _expectedRevision); if (!validation.Succeeded) throw new InvalidOperationException(string.Join("; ", validation.Diagnostics.Select(value => value.Message))); SetStatus($"Typed compile passed: {_draft.Definition.ExecutionKind}, range {_draft.Definition.MinRange}–{_draft.Definition.MaxRange}."); } catch (Exception e) { SetStatus($"Validation failed: {e.Message}", true); } }
    private void PreviewDraft()
    {
        try
        {
            SkillAuthoringDocument draft = SkillAuthoringJson.Deserialize(_snapshot!.Text);
            if (_previewEncounter is null || _previewEncounter.Selected < 0)
                throw new InvalidOperationException("Select an Encounter for the battle preview.");
            string? target = draft.Definition.ExecutionKind is SkillExecutionKind.SummonSkeleton or
                SkillExecutionKind.SummonSkeletonMage or SkillExecutionKind.PickupSpear ? null : _previewTarget!.Text.Trim();
            if (draft.Definition.ExecutionKind == SkillExecutionKind.CombatTechniques) target = "preview.party.0";
            var context = new SkillBattlePreviewContext(
                _previewEncounter.GetItemText(_previewEncounter.Selected), "preview.party.0", target,
                new GridCellAuthoring((int)_previewX!.Value, (int)_previewY!.Value), (ulong)_previewSeed!.Value,
                "unit.pure-run.amazon");
            SkillBattlePreviewResult result = _authoring.PreviewSkillBattle(draft.ContentId, _snapshot.Text, context);
            AuthoringEditorDiagnostics.RecordCleanup("Skill", draft.ContentId, 0, 0,
                result.Succeeded ? "transition accepted" : "transition rejected");
            SetStatus($"BattleTransition {(result.Succeeded ? "accepted" : "rejected: " + result.RejectionReason)}; " +
                $"mana={result.Values["manaSpent"]}, healthΔ={result.Values["healthDelta"]}, unitsΔ={result.Values["unitCountDelta"]}; " +
                $"events={string.Join(" -> ", result.Events.Select(value => value.Split(':')[0]))}; fingerprint={result.AfterFingerprint[..20]}…",
                !result.Succeeded);
        }
        catch (Exception e) { SetStatus($"Preview failed: {e.Message}", true); }
    }
    private void ApplyAll()
    {
        try
        {
            SkillAuthoringDocument current = SkillAuthoringEditorService.Read(_resource!); if (AuthoringRevision.Compute(current) != _expectedRevision) throw new InvalidOperationException("Skill changed outside this session; reload before applying.");
            SkillAuthoringDocument afterDocument = SkillAuthoringJson.Deserialize(_snapshot!.Text); if (afterDocument.ContentId != current.ContentId) throw new InvalidOperationException("Draft ContentId differs from the Resource.");
            string before = SkillAuthoringJson.Serialize(current), after = SkillAuthoringJson.Serialize(afterDocument); if (before == after) { SetStatus("Nothing to apply."); return; }
            _undoRedo!.CreateAction("Apply Skill authoring session", UndoRedo.MergeMode.Disable, _resource); _undoRedo.AddDoMethod(this, MethodName.ApplySerializedSkill, after); _undoRedo.AddUndoMethod(this, MethodName.ApplySerializedSkill, before); _undoRedo.CommitAction();
        }
        catch (Exception e) { SetStatus($"Apply failed: {e.Message}", true); }
    }
    public void ApplySerializedSkill(string json)
    {
        StoredAuthoringDocument current = _authoring.Get("skill", _resource!.ContentIdValue); StoredAuthoringDocument applied = _authoring.ApplySingle("skill", current.Document.ContentId, current.Revision, json);
        _resource = (SkillDefinitionResource)applied.Resource; _draft = (SkillAuthoringDocument)applied.Document; _expectedRevision = applied.Revision; _snapshot!.Text = applied.Snapshot; RefreshForm(); SetStatus("Applied, saved and reload-validated Skill.");
    }
    private void RevertAll() { if (_resource is null) return; _draft = SkillAuthoringEditorService.Read(_resource); _expectedRevision = AuthoringRevision.Compute(_draft); _snapshot!.Text = SkillAuthoringJson.Serialize(_draft); RefreshForm(); SetStatus("Skill draft reverted."); }
    private void ToggleAdvancedJson() { if (_snapshot is null) return; _snapshot.Visible = !_snapshot.Visible; if (_snapshot.Visible) SetStatus("Advanced canonical JSON is draft interchange only; use the structured inspector for normal edits."); }

    private void RefreshForm()
    {
        if (_form is null || _snapshot is null || string.IsNullOrWhiteSpace(_snapshot.Text)) return;
        foreach (Node child in _form.GetChildren()) { _form.RemoveChild(child); child.QueueFree(); }
        JsonObject root = JsonNode.Parse(_snapshot.Text)!.AsObject();
        _form.AddChild(Heading("Identity, targeting and damage"));
        foreach (string name in new[] { "contentId", "displayName", "description", "role", "kind", "level", "manaCost", "minRange", "maxRange", "executionKind", "damage", "damageKind", "canCrit" })
            AddProperty(_form, root, name, name);
        _form.AddChild(Heading("Status, growth and prerequisites"));
        foreach (string name in new[] { "statusContentId", "statusDuration", "hidden", "externalDependency", "isBasicAbility", "maxUsesPerTurn", "branchId", "prerequisiteContentId", "prerequisiteBranchId", "growthVisible", "requiredAttribute", "minimumAttribute" })
            AddProperty(_form, root, name, name);
        _form.AddChild(Heading("Execution profile — " + root["executionKind"]!.GetValue<string>()));
        JsonObject profile = root["executionProfile"]!.AsObject();
        foreach ((string name, _) in profile) AddProperty(_form, profile, name, "executionProfile." + name);
        _form.AddChild(Heading("Provenance (frozen fields are read-only)"));
        foreach (string name in new[] { "sourceKind", "sourceId", "sourcePath", "sourceGuid", "sourceLocalFileId", "graphPath", "graphDependencyHash" })
            AddProperty(_form, root, name, name);
    }

    private void AddProperty(Container parent, JsonObject owner, string name, string path)
    {
        JsonNode? node = owner[name]; if (node is null && name is not ("statusContentId" or "prerequisiteContentId" or "summonDefinitionId" or "detonateStatusContentId" or "summonAttackContentId")) return;
        bool frozen = name is "contentId" or "sourceKind" or "sourceId" or "sourcePath" or "sourceGuid" or "sourceLocalFileId" or "graphPath" or "graphDependencyHash";
        string? catalogType = name switch { "statusContentId" or "detonateStatusContentId" => "buff", "prerequisiteContentId" or "summonAttackContentId" => "skill", "summonDefinitionId" => "unit", _ => null };
        var row = new HBoxContainer(); row.AddChild(new Label { Text = name, CustomMinimumSize = new Vector2(220, 0) });
        if (catalogType is not null) row.AddChild(ContentPicker(path, node?.GetValue<string>() ?? string.Empty, catalogType));
        else if (EnumValues(name) is { } values) row.AddChild(EnumPicker(path, node?.GetValue<string>() ?? values[0], values, frozen));
        else if (node is JsonValue value && value.TryGetValue<bool>(out bool boolean))
        {
            var edit = new CheckBox { ButtonPressed = boolean, Disabled = frozen }; edit.Toggled += changed => UpdateProperty(path, JsonValue.Create(changed), false); row.AddChild(edit);
        }
        else if (node is JsonValue number && number.TryGetValue<int>(out int integer))
        {
            var edit = new SpinBox { Value = integer, MinValue = name is "minRange" or "maxRange" or "level" or "manaCost" or "damage" ? 0 : -100000, MaxValue = 100000, Step = 1, Editable = !frozen, SizeFlagsHorizontal = SizeFlags.ExpandFill }; edit.ValueChanged += changed => UpdateProperty(path, JsonValue.Create((int)changed), false); row.AddChild(edit);
        }
        else
        {
            var edit = new LineEdit { Text = node?.GetValue<string>() ?? string.Empty, Editable = !frozen, SizeFlagsHorizontal = SizeFlags.ExpandFill }; edit.TextSubmitted += changed => UpdateProperty(path, JsonValue.Create(changed), name == "executionKind"); edit.FocusExited += () => UpdateProperty(path, JsonValue.Create(edit.Text), name == "executionKind"); row.AddChild(edit);
        }
        parent.AddChild(row);
    }

    private Control ContentPicker(string path, string current, string type)
    {
        var picker = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill }; picker.AddItem("(none)");
        string[] ids = _catalogIdsByType.GetValueOrDefault(type) ?? Array.Empty<string>(); foreach (string id in ids) picker.AddItem(id);
        int selected = Array.IndexOf(ids, current); picker.Select(selected < 0 ? 0 : selected + 1);
        picker.ItemSelected += index => UpdateProperty(path, index == 0 ? null : JsonValue.Create(ids[(int)index - 1]), false); return picker;
    }

    private Control EnumPicker(string path, string current, string[] values, bool disabled)
    {
        var picker = new OptionButton { Disabled = disabled, SizeFlagsHorizontal = SizeFlags.ExpandFill }; foreach (string value in values) picker.AddItem(value);
        picker.Select(Math.Max(0, Array.IndexOf(values, current))); picker.ItemSelected += index => UpdateProperty(path, JsonValue.Create(values[(int)index]), path == "executionKind"); return picker;
    }

    private void UpdateProperty(string path, JsonNode? value, bool rebuild)
    {
        try
        {
            JsonObject root = JsonNode.Parse(_snapshot!.Text)!.AsObject(); string[] parts = path.Split('.');
            JsonObject owner = parts.Length == 1 ? root : root[parts[0]]!.AsObject(); owner[parts[^1]] = value;
            _snapshot.Text = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            _draft = SkillAuthoringJson.Deserialize(_snapshot.Text);
            SetStatus("Skill draft changed; formal Resource unchanged.");
            if (rebuild) RefreshForm();
        }
        catch (Exception exception) { SetStatus("Draft is not yet valid: " + exception.Message, true); }
    }

    private static string[]? EnumValues(string name) => name switch
    {
        "role" => Enum.GetNames<SkillRole>(), "kind" => Enum.GetNames<SkillKind>(), "executionKind" => Enum.GetNames<SkillExecutionKind>(),
        "damageKind" => Enum.GetNames<SkillDamageKind>(), "sourceKind" => Enum.GetNames<SkillAuthoringSourceKind>(), _ => null
    };
    private static Label Heading(string text) { var label = new Label { Text = text }; label.AddThemeFontSizeOverride("font_size", 20); return label; }
    private static void AddButton(Container parent, string text, Action action) { var button = new Button { Text = text }; button.Pressed += action; parent.AddChild(button); }
    private void SetStatus(string message, bool error = false) { if (_status is null) return; _status.Text = message; _status.Modulate = error ? Colors.IndianRed : Colors.LightGreen; }
}
#endif
