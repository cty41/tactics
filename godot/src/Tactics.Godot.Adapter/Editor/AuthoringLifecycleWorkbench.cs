#if TOOLS
using Godot;
using Tactics.Application.Authoring;

namespace Tactics.Godot.Adapter.Editor;

[Tool]
public partial class AuthoringLifecycleWorkbench : VBoxContainer
{
    private static readonly string[] Types = ["run-map", "event", "treasure", "battle-layout", "encounter", "ai", "skill", "presentation"];
    private readonly TacticsAuthoringEditorService _authoring = new();
    private readonly AuthoringResourceLifecycleService _lifecycle = new();
    private EditorUndoRedoManager? _undoRedo;
    private AuthoringWorkspaceCoordinator? _workspace;
    private OptionButton? _type;
    private OptionButton? _resources;
    private LineEdit? _newContentId;
    private Label? _status;

    public void Configure(EditorUndoRedoManager undoRedo) => _undoRedo = undoRedo;
    internal void Configure(EditorUndoRedoManager undoRedo, AuthoringWorkspaceCoordinator workspace) { _undoRedo = undoRedo; _workspace = workspace; }

    public override void _Ready()
    {
        if (_undoRedo is null) throw new InvalidOperationException("Editor UndoRedo manager is required.");
        WorkbenchUi.StylePage(this);
        var row = WorkbenchUi.Toolbar(this);
        row.AddChild(new Label { Text = "RESOURCE LIFECYCLE" });
        _type = new OptionButton { CustomMinimumSize = new Vector2(135, 0) };
        foreach (string type in Types) _type.AddItem(type == "run-map" ? "Map" : type);
        _type.ItemSelected += OnTypeSelected;
        row.AddChild(_type);
        _resources = new OptionButton { CustomMinimumSize = new Vector2(310, 0) };
        _resources.ItemSelected += OnResourceSelected;
        row.AddChild(_resources);
        _newContentId = new LineEdit { PlaceholderText = "new ContentId", CustomMinimumSize = new Vector2(280, 0) };
        row.AddChild(_newContentId);
        AddButton(row, "New", CreateFromTemplate);
        AddButton(row, "Duplicate", DuplicateSelected);
        AddButton(row, "Delete", DeleteSelected);
        AddButton(row, "Refresh", Refresh);
        AddChild(row);
        _status = new Label { Text = "Formal migration resources are protected; authored resources use the UID ledger." };
        WorkbenchUi.StyleStatus(_status);
        AddChild(_status);
        Refresh();
    }

    public override void _ExitTree()
    {
        // Descendant Controls are freed with this workbench; Godot disconnects their signals.
        // Explicit -= after an assembly reload targets a new managed delegate handle and produces
        // a false "nonexistent connection" error during deterministic teardown.
    }

    private void CreateFromTemplate()
    {
        Create(GetTypeId(), string.Empty, "Create");
    }

    private void DuplicateSelected()
    {
        if (_resources is null || _resources.Selected < 0) return;
        Create(GetTypeId(), _resources.GetItemText(_resources.Selected), "Duplicate");
    }

    private void Create(string type, string sourceId, string verb)
    {
        try
        {
            string newId = _newContentId?.Text.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(newId)) throw new InvalidOperationException("Enter a new ContentId first.");
            if (_workspace is null) throw new InvalidOperationException("Global authoring workspace is not configured.");
            _workspace.QueueLifecycle(new AuthoringAssetChange(string.IsNullOrEmpty(sourceId) ? AuthoringAssetChangeKind.Create : AuthoringAssetChangeKind.Duplicate,
                newId, string.IsNullOrEmpty(sourceId) ? null : sourceId, type));
            SetStatus($"Queued {verb.ToLowerInvariant()} for {newId}; use global Apply All to commit atomically.");
        }
        catch (Exception exception) { SetStatus($"{verb} failed: {exception.Message}", true); }
    }

    private void DeleteSelected()
    {
        if (_resources is null || _resources.Selected < 0) return;
        string contentId = _resources.GetItemText(_resources.Selected);
        try
        {
            AuthoringReferenceSnapshot references = _lifecycle.CaptureReferences(contentId);
            if (_workspace is null) throw new InvalidOperationException("Global authoring workspace is not configured.");
            _workspace.QueueLifecycle(new AuthoringAssetChange(AuthoringAssetChangeKind.Delete, contentId,
                ResourceType: GetTypeId(), ExpectedReferenceRevision: references.Revision));
            string rebind = references.ReverseReferences.Count == 0 ? "no rebind required"
                : "requires typed draft rebinds for: " + string.Join(", ", references.ReverseReferences);
            SetStatus($"Queued delete for {contentId}; {rebind}. Global Apply All will reject any residual reference.");
        }
        catch (Exception exception) { SetStatus("Delete failed: " + exception.Message, true); }
    }

    public void Refresh() => Refresh(null);

    private void Refresh(string? selectContentId)
    {
        if (_resources is null) return;
        try
        {
            _resources.Clear();
            IReadOnlyList<StoredAuthoringDocument> resources = _authoring.List(GetTypeId());
            foreach (StoredAuthoringDocument value in resources) _resources.AddItem(value.Document.ContentId);
            int selected = selectContentId is null ? 0 : resources.Select(value => value.Document.ContentId).ToList().IndexOf(selectContentId);
            if (_resources.ItemCount > 0) _resources.Select(Math.Max(0, selected));
            ShowOwnership();
        }
        catch (Exception exception) { SetStatus("Lifecycle refresh failed: " + exception.Message, true); }
    }

    private void ShowOwnership()
    {
        if (_resources is null || _resources.Selected < 0) return;
        string contentId = _resources.GetItemText(_resources.Selected);
        AuthoringResourceOwnership ownership = _lifecycle.GetOwnership(contentId);
        SetStatus($"{contentId}: {ownership}. {(_lifecycle.CaptureReferences(contentId).ReverseReferences.Count)} reverse references.");
    }

    private void OnTypeSelected(long _) => Refresh();
    private void OnResourceSelected(long _) => ShowOwnership();

    private string GetTypeId() => Types[Math.Clamp(_type?.Selected ?? 0, 0, Types.Length - 1)];
    private void SetStatus(string text, bool error = false) { if (_status is not null) { _status.Text = text; WorkbenchUi.StyleStatus(_status, error); } }
    private static void AddButton(Container parent, string text, Action action) { var button = new Button { Text = text }; button.Pressed += action; parent.AddChild(button); }
}
#endif
