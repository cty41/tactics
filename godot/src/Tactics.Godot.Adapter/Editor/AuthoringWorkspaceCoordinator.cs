#if TOOLS
using System.Text.Json;
using Godot;
using Tactics.Application.Authoring;

namespace Tactics.Godot.Adapter.Editor;

internal interface IAuthoringWorkspaceParticipant
{
    string WorkspaceName { get; }
    IReadOnlyList<AuthoringDocumentChange> CaptureWorkspaceChanges();
    void ValidateWorkspaceDraft();
    void RevertWorkspaceDraft();
    void ReloadWorkspaceDocuments();
}

[Tool]
internal sealed partial class AuthoringWorkspaceCoordinator : HBoxContainer
{
    private readonly List<IAuthoringWorkspaceParticipant> _participants = new();
    private readonly List<AuthoringAssetChange> _lifecycle = new();
    private readonly TacticsAuthoringEditorService _authoring = new();
    private EditorUndoRedoManager? _undoRedo;
    private Label? _status;

    public void Configure(EditorUndoRedoManager undoRedo) => _undoRedo = undoRedo;
    public void Register(IAuthoringWorkspaceParticipant participant)
    {
        ArgumentNullException.ThrowIfNull(participant);
        if (!_participants.Contains(participant)) _participants.Add(participant);
    }
    public void QueueLifecycle(AuthoringAssetChange change)
    {
        if (_lifecycle.Any(value => value.ContentId == change.ContentId))
            throw new InvalidOperationException($"A lifecycle operation for '{change.ContentId}' is already queued.");
        _lifecycle.Add(change);
    }

    public override void _Ready()
    {
        if (_undoRedo is null) throw new InvalidOperationException("Workspace coordinator requires Editor UndoRedo.");
        AddButton("Validate All", ValidateAll); AddButton("Apply All", ApplyAll); AddButton("Revert All", RevertAll);
        _status = new Label { Text = "Workspace: no dirty documents." }; AddChild(_status); SetProcess(true);
    }

    public override void _Process(double delta)
    {
        _ = delta;
        try
        {
            AuthoringDocumentChange[] changes = CaptureChanges();
            AuthoringEditorDiagnostics.RecordWorkspace(changes.Length, _lifecycle.Count);
            _status!.Text = changes.Length == 0 && _lifecycle.Count == 0 ? "Workspace: no dirty documents."
                : $"Workspace: {changes.Length} dirty documents, {_lifecycle.Count} lifecycle operations; Apply All is one Undo action.";
            _status.Modulate = changes.Length == 0 && _lifecycle.Count == 0 ? Colors.LightGray : Colors.Gold;
        }
        catch (Exception error)
        {
            _status!.Text = "Workspace conflict: " + error.Message; _status.Modulate = Colors.IndianRed;
        }
    }

    private void ValidateAll()
    {
        try
        {
            foreach (IAuthoringWorkspaceParticipant participant in _participants) participant.ValidateWorkspaceDraft();
            AuthoringDocumentChange[] changes = CaptureChanges();
            if (changes.Length > 0 || _lifecycle.Count > 0)
                _authoring.ValidateBatch(new AuthoringBatchChangeSet("workspace-validate", changes, _lifecycle));
            SetStatus($"Validated {_participants.Count} authoring pages, {changes.Length} dirty documents and {_lifecycle.Count} queued lifecycle operations.");
        }
        catch (Exception error) { SetStatus("Validate All failed: " + error.Message, true); }
    }

    private void ApplyAll()
    {
        try
        {
            foreach (IAuthoringWorkspaceParticipant participant in _participants) participant.ValidateWorkspaceDraft();
            AuthoringDocumentChange[] after = CaptureChanges();
            if (after.Length == 0 && _lifecycle.Count == 0) { SetStatus("Workspace has nothing to apply."); return; }
            string changeId = Guid.NewGuid().ToString("N");
            AuthoringAssetChange[] lifecycle = _lifecycle.ToArray();
            var batch = new AuthoringBatchChangeSet(changeId, after, lifecycle); if (lifecycle.Length == 0) _authoring.ValidateBatch(batch);
            AuthoringDocumentChange[] before = after.Select(value =>
            {
                string type = TacticsAuthoringEditorService.TypeId(value.Kind);
                StoredAuthoringDocument stored = _authoring.Get(type, value.ContentId);
                IAuthoringResourceHandler handler = AuthoringResourceHandlerRegistry.CreateDefault().Get(type);
                IAuthoringDocument draft = handler.Deserialize(value.Snapshot);
                return new AuthoringDocumentChange(value.Kind, value.ContentId, AuthoringRevision.Compute(draft), stored.Snapshot);
            }).ToArray();
            string afterPayload = JsonSerializer.Serialize(new WorkspaceBatchPayload(changeId, after, lifecycle));
            string beforePayload = JsonSerializer.Serialize(new WorkspaceBatchPayload(
                Guid.NewGuid().ToString("N"), before, Array.Empty<AuthoringAssetChange>()));
            _undoRedo!.CreateAction($"Apply {after.Length + lifecycle.Length} Content Workbench changes", UndoRedo.MergeMode.Disable);
            _undoRedo.AddDoMethod(this, MethodName.ApplySerializedWorkspace, afterPayload);
            if (lifecycle.Length > 0) _undoRedo.AddUndoMethod(this, MethodName.UndoWorkspaceLifecycle, changeId);
            else _undoRedo.AddUndoMethod(this, MethodName.ApplySerializedWorkspace, beforePayload);
            _undoRedo.CommitAction();
        }
        catch (Exception error) { SetStatus("Apply All failed: " + error.Message, true); }
    }

    public void ApplySerializedWorkspace(string payload)
    {
        WorkspaceBatchPayload value = JsonSerializer.Deserialize<WorkspaceBatchPayload>(payload)
            ?? throw new InvalidOperationException("Workspace batch payload is invalid.");
        _ = _authoring.ApplyBatch(new AuthoringBatchChangeSet(value.ChangeId, value.Changes, value.Lifecycle));
        _lifecycle.Clear();
        string[] reloadErrors = ReloadParticipants();
        SetStatus(reloadErrors.Length == 0
                ? $"Applied {value.Changes.Length} documents and {value.Lifecycle.Length} lifecycle operations atomically; typed reload completed."
                : $"Apply succeeded, but {reloadErrors.Length} authoring pages failed to reload. See Godot Output.",
            reloadErrors.Length > 0);
    }

    public void UndoWorkspaceLifecycle(string changeId)
    {
        _authoring.UndoLifecycleBatch(changeId);
        string[] reloadErrors = ReloadParticipants();
        SetStatus(reloadErrors.Length == 0
                ? "Lifecycle batch undone with original Catalog, ledger and UIDs restored."
                : $"Lifecycle batch was undone, but {reloadErrors.Length} authoring pages failed to reload. See Godot Output.",
            reloadErrors.Length > 0);
    }

    private void RevertAll()
    {
        foreach (IAuthoringWorkspaceParticipant participant in _participants) participant.RevertWorkspaceDraft();
        _lifecycle.Clear();
        SetStatus("All registered drafts reverted.");
    }

    private AuthoringDocumentChange[] CaptureChanges() => _participants
        .SelectMany(value => value.CaptureWorkspaceChanges()).ToArray();
    private string[] ReloadParticipants()
    {
        var errors = new List<string>();
        foreach (IAuthoringWorkspaceParticipant participant in _participants)
        {
            try { participant.ReloadWorkspaceDocuments(); }
            catch (Exception error)
            {
                string diagnostic = $"{participant.WorkspaceName}: {error.Message}";
                errors.Add(diagnostic);
                GD.PushError("Content Workbench reload failed after a committed transaction: " + diagnostic);
            }
        }
        return errors.ToArray();
    }
    private void AddButton(string text, Action action) { var button = new Button { Text = text }; button.Pressed += action; AddChild(button); }
    private void SetStatus(string text, bool error = false) { if (_status is null) return; _status.Text = text; _status.Modulate = error ? Colors.IndianRed : Colors.LightGreen; }
    private sealed record WorkspaceBatchPayload(string ChangeId, AuthoringDocumentChange[] Changes,
        AuthoringAssetChange[] Lifecycle);
}
#endif
