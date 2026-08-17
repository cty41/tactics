#if TOOLS
using Godot;
using Tactics.Application.Authoring;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

[Tool]
public partial class ContentCatalogWorkbench : VBoxContainer
{
    private const string CatalogPath = "res://content/ContentCatalog.tres";
    private const string CatalogScriptPath = "res://src/Tactics.Godot.Adapter/Runtime/GodotResourceCatalog.cs";
    private readonly string _resourceType;
    private readonly string _heading;
    private ItemList? _items;
    private RichTextLabel? _details;
    private Label? _summary;
    private IReadOnlyDictionary<string, AuthoringCatalogAuditRow> _audit = new Dictionary<string, AuthoringCatalogAuditRow>();
    private int _catalogLoadAttempts;

    public ContentCatalogWorkbench() : this(string.Empty, "Content") { }
    public ContentCatalogWorkbench(string resourceType, string heading)
    {
        _resourceType = resourceType;
        _heading = heading;
    }

    public override void _Ready()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        AddChild(new Label { Text = $"{_heading} — canonical resources are inspected read-only until their typed editor is selected." });
        if (string.IsNullOrEmpty(_resourceType))
        {
            var toolbar = new HBoxContainer();
            var audit = new Button { Text = "Run Catalog + Reference + Revision Audit" }; audit.Pressed += RunAudit; toolbar.AddChild(audit);
            _summary = new Label { Text = "Audit not run in this Editor session." }; toolbar.AddChild(_summary); AddChild(toolbar);
        }
        var split = new HSplitContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        _items = new ItemList { CustomMinimumSize = new Vector2(360, 400), SizeFlagsVertical = SizeFlags.ExpandFill };
        _items.ItemSelected += ShowDetails;
        split.AddChild(_items);
        _details = new RichTextLabel { BbcodeEnabled = true, SelectionEnabled = true, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        split.AddChild(_details);
        AddChild(split);
        CallDeferred(nameof(LoadCatalog));
    }

    public override void _ExitTree()
    {
        _catalogLoadAttempts = 0;
        if (_items is not null) _items.ItemSelected -= ShowDetails;
    }

    public void LoadCatalog()
    {
        EditorResourceLoadResult<GodotResourceCatalog> result = ReloadSafeEditorResourceLoader.Load<GodotResourceCatalog>(
            CatalogPath, CatalogScriptPath, "Entries");
        if (ReloadSafeEditorResourceLoader.RetryDeferred(this, MethodName.LoadCatalog, ref _catalogLoadAttempts, result, _heading))
            return;
        GodotResourceCatalog catalog = result.Resource!;
        catalog.Validate();
        foreach (GodotResourceEntry entry in catalog.Entries.Where(value => string.IsNullOrEmpty(_resourceType) || value.ResourceTypeIdValue == _resourceType))
        {
            int index = _items!.AddItem(entry.ContentIdValue);
            _items.SetItemMetadata(index, entry.DiagnosticPathValue);
        }
        if (_items!.ItemCount > 0) { _items.Select(0); ShowDetails(0); }
        if (string.IsNullOrEmpty(_resourceType)) RunAudit();
    }

    private void ShowDetails(long index)
    {
        string path = _items!.GetItemMetadata((int)index).AsString();
        Resource? resource = ResourceLoader.Load<Resource>(path, string.Empty, ResourceLoader.CacheMode.Ignore);
        string id = _items.GetItemText((int)index);
        string audit = _audit.TryGetValue(id, out AuthoringCatalogAuditRow? row)
            ? $"\nAuthoring revision: {row.Revision ?? "not an authoring document"}\nOwnership: {row.Ownership}\nForward refs: {string.Join(", ", row.ForwardReferences)}\nReverse refs: {string.Join(", ", row.ReverseReferences)}\nDiagnostics: {(row.Diagnostics.Count == 0 ? "none" : string.Join("; ", row.Diagnostics.Select(value => value.Code + ": " + value.Message)))}"
            : string.Empty;
        _details!.Text = resource is null
            ? $"[color=red]Failed to load[/color]\n{path}"
            : $"[font_size=24]{id}[/font_size]\n\nType: {resource.GetClass()}\nPath: {path}\nUID: {ResourceUid.PathToUid(path)}\n{audit}\n\nValidated through the canonical catalog.";
    }

    private void RunAudit()
    {
        try
        {
            GodotResourceCatalog catalog = ResourceLoader.Load<GodotResourceCatalog>(CatalogPath, string.Empty, ResourceLoader.CacheMode.Ignore)
                ?? throw new InvalidOperationException("Catalog cannot be loaded.");
            AuthoringCatalogAuditRow[] rows = AuthoringCatalogAuditService.Audit(catalog).ToArray();
            _audit = rows.ToDictionary(value => value.ContentId, StringComparer.Ordinal);
            int errors = rows.Sum(value => value.Diagnostics.Count(item => item.Severity == AuthoringDiagnosticSeverity.Error));
            if (_summary is not null)
            {
                string descriptorRoot = ProjectSettings.GlobalizePath("res://.godot");
                string[] descriptors = Directory.Exists(descriptorRoot) ? Directory.GetFiles(descriptorRoot, "tactics-authoring-session-*.json") : Array.Empty<string>();
                string mcp = descriptors.Length == 1 ? "one Editor bridge session" : $"{descriptors.Length} Editor bridge sessions (MCP fail-closed)";
                string gameplayReport = ProjectSettings.GlobalizePath("res://../artifacts/gameplay-specs/godot/godot-gameplay-spec-result-v1.json");
                string gameplay = File.Exists(gameplayReport) ? "Gameplay Spec report present" : "Gameplay Spec report pending";
                var editor = AuthoringEditorDiagnostics.Snapshot();
                int cleanupLeaks = editor.Cleanup.Count(value => value.ActiveTweens != 0 || value.TemporaryNodes != 0);
                _summary.Text = $"{rows.Length} resources; {rows.Count(value => value.Revision is not null)} authored; {rows.Count(value => value.Ownership == AuthoringResourceOwnership.WorkbenchOwned)} Workbench-owned; {errors} errors; dirty={editor.DirtyDocuments}, lifecycle={editor.QueuedLifecycle}; preview cleanup leaks={cleanupLeaks}; {mcp}; {gameplay}.";
                _summary.Modulate = errors == 0 && descriptors.Length <= 1 ? Colors.LightGreen : Colors.IndianRed;
            }
            if (_items is not null && _items.GetSelectedItems().Length > 0) ShowDetails(_items.GetSelectedItems()[0]);
        }
        catch (Exception error) { if (_summary is not null) { _summary.Text = "Audit failed: " + error.Message; _summary.Modulate = Colors.IndianRed; } }
    }
}
#endif
