#if TOOLS
using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Godot;
using Tactics.Application.Authoring;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

[Tool]
public partial class TacticsAuthoringEditorBridge : Node
{
    private const string CatalogPath = "res://content/ContentCatalog.tres";
    private readonly ConcurrentQueue<PendingRequest> _pending = new();
    private CancellationTokenSource? _cancellation;
    private Task? _server;
    private EditorUndoRedoManager? _undoRedo;
    private string _token = string.Empty;
    private string _pipeName = string.Empty;
    private string _descriptorPath = string.Empty;
    private string _projectRoot = string.Empty;
    private bool _ready;
    private readonly TacticsAuthoringEditorService _authoring = new();

    public void Configure(EditorUndoRedoManager undoRedo) => _undoRedo = undoRedo;
    public override void _EnterTree()
    {
        if (_undoRedo is null) throw new InvalidOperationException("Authoring bridge requires Editor UndoRedo.");
        _token = Guid.NewGuid().ToString("N"); _pipeName = $"tactics-authoring-{System.Environment.ProcessId}-{Guid.NewGuid():N}";
        _projectRoot = Path.GetFullPath(ProjectSettings.GlobalizePath("res://..")).TrimEnd(Path.DirectorySeparatorChar);
        _descriptorPath = ProjectSettings.GlobalizePath($"res://.godot/tactics-authoring-session-{System.Environment.ProcessId}.json");
        _ready = false; WriteDescriptor("initializing"); _cancellation = new CancellationTokenSource(); _server = RunServerAsync(_cancellation.Token); SetProcess(true);
        GD.Print($"[Tactics Tooling] Authoring bridge initializing on {_pipeName}.");
    }
    public override void _ExitTree()
    {
        _ready = false; try { WriteDescriptor("reloading"); } catch { }
        _cancellation?.Cancel(); _cancellation?.Dispose(); _cancellation = null; _server = null;
        if (!string.IsNullOrWhiteSpace(_descriptorPath) && File.Exists(_descriptorPath)) File.Delete(_descriptorPath);
        while (_pending.TryDequeue(out PendingRequest? request)) request.Completion.TrySetException(new InvalidOperationException("Editor bridge is reloading."));
    }
    public override void _Process(double delta)
    {
        _ = delta;
        while (_pending.TryDequeue(out PendingRequest? pending))
        {
            try { pending.Completion.SetResult(HandleOnMainThread(pending.Json)); }
            catch (Exception error) { pending.Completion.SetResult(JsonSerializer.Serialize(new { succeeded = false, error = error.Message })); }
        }
    }

    private async Task RunServerAsync(CancellationToken cancellation)
    {
        while (!cancellation.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await pipe.WaitForConnectionAsync(cancellation); using var reader = new StreamReader(pipe, Encoding.UTF8, false, leaveOpen: true); await using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
                string requestJson = await reader.ReadLineAsync(cancellation) ?? throw new InvalidOperationException("Bridge request is empty.");
                var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously); _pending.Enqueue(new PendingRequest(requestJson, completion));
                string response = await completion.Task.WaitAsync(TimeSpan.FromSeconds(8), cancellation); await writer.WriteLineAsync(response.AsMemory(), cancellation);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { break; }
            catch (Exception error) { GD.PushError($"[Tactics Tooling] Authoring bridge request failed: {error.Message}"); }
        }
    }

    private string HandleOnMainThread(string json)
    {
        if (!_ready || EditorInterface.Singleton.GetResourceFilesystem().IsScanning()) throw new InvalidOperationException("Editor bridge is not ready (filesystem scan or reload in progress).");
        using JsonDocument payload = JsonDocument.Parse(json); JsonElement root = payload.RootElement;
        if (root.GetProperty("sessionToken").GetString() != _token) throw new UnauthorizedAccessException("Authoring session token mismatch.");
        if (!string.Equals(Path.GetFullPath(root.GetProperty("projectRoot").GetString()!).TrimEnd(Path.DirectorySeparatorChar), _projectRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Authoring project root mismatch.");
        string tool = root.GetProperty("tool").GetString()!; JsonElement arguments = root.GetProperty("arguments");
        object response = tool switch
        {
            "tactics_authoring_list" => List(arguments),
            "tactics_authoring_get" => Get(arguments),
            "tactics_authoring_validate" => Validate(arguments),
            "tactics_authoring_apply" => Apply(arguments),
            "tactics_authoring_preview" => Preview(arguments),
            "tactics_authoring_reference_audit" => ReferenceAudit(arguments),
            _ => throw new InvalidOperationException($"Unknown authoring tool '{tool}'.")
        };
        return JsonSerializer.Serialize(response);
    }

    private object List(JsonElement arguments)
    {
        string? kind = arguments.TryGetProperty("kind", out JsonElement supplied) ? supplied.GetString() : null;
        StoredAuthoringDocument[] documents = _authoring.List(kind).ToArray();
        return new { succeeded = true, documents = documents.Select(value => new { kind = value.Entry.ResourceTypeIdValue, contentId = value.Document.ContentId, revision = value.Revision, path = value.Entry.DiagnosticPathValue, diagnostics = Array.Empty<object>() }).ToArray() };
    }
    private object Get(JsonElement arguments)
    {
        StoredAuthoringDocument stored = LoadDocument(arguments);
        return new { succeeded = true, kind = stored.Entry.ResourceTypeIdValue, contentId = stored.Document.ContentId, revision = stored.Revision, snapshot = stored.Snapshot, dependencies = stored.Document.Dependencies };
    }
    private object Validate(JsonElement arguments)
    {
        StoredAuthoringDocument stored = LoadDocument(arguments); string snapshot = arguments.TryGetProperty("snapshot", out JsonElement supplied) && supplied.ValueKind == JsonValueKind.String ? supplied.GetString()! : stored.Snapshot;
        string? expected = arguments.TryGetProperty("expectedRevision", out JsonElement expectedValue) && expectedValue.ValueKind == JsonValueKind.String ? expectedValue.GetString() : null;
        AuthoringValidationResult validation = _authoring.Validate(stored.Entry.ResourceTypeIdValue, stored.Document.ContentId, snapshot, expected);
        return new { succeeded = validation.Succeeded, contentId = stored.Document.ContentId, predictedRevision = validation.PredictedRevision, diagnostics = validation.Diagnostics, previewAvailable = validation.PreviewAvailable };
    }
    private object Apply(JsonElement arguments)
    {
        if ((arguments.TryGetProperty("changes", out JsonElement changes) && changes.ValueKind == JsonValueKind.Array) ||
            (arguments.TryGetProperty("lifecycle", out JsonElement lifecycle) && lifecycle.ValueKind == JsonValueKind.Array))
            return ApplyBatch(arguments);
        StoredAuthoringDocument stored = LoadDocument(arguments); string expected = arguments.GetProperty("expectedRevision").GetString()!, afterJson = arguments.GetProperty("snapshot").GetString()!;
        AuthoringValidationResult validation = _authoring.Validate(stored.Entry.ResourceTypeIdValue, stored.Document.ContentId, afterJson, expected); if (!validation.Succeeded) throw new InvalidOperationException(string.Join("; ", validation.Diagnostics.Select(value => value.Message)));
        _undoRedo!.CreateAction($"MCP apply {stored.Entry.ContentIdValue}", UndoRedo.MergeMode.Disable, stored.Resource); _undoRedo.AddDoMethod(this, MethodName.ApplySerializedDocument, stored.Entry.ResourceTypeIdValue, stored.Entry.ContentIdValue, expected, afterJson); _undoRedo.AddUndoMethod(this, MethodName.ApplySerializedDocument, stored.Entry.ResourceTypeIdValue, stored.Entry.ContentIdValue, validation.PredictedRevision, stored.Snapshot); _undoRedo.CommitAction();
        StoredAuthoringDocument applied = _authoring.Get(stored.Entry.ResourceTypeIdValue, stored.Entry.ContentIdValue);
        return new { succeeded = true, contentId = applied.Document.ContentId, revision = applied.Revision, modified = new[] { applied.Entry.DiagnosticPathValue }, evidence = "EditorUndoRedo+staged-ResourceSaver+typed-reload" };
    }
    private object ApplyBatch(JsonElement arguments)
    {
        var after = new List<BridgeDocumentChange>();
        var before = new List<BridgeDocumentChange>();
        if (arguments.TryGetProperty("changes", out JsonElement changes))
        foreach (JsonElement change in changes.EnumerateArray())
        {
            string kind = change.GetProperty("kind").GetString()!;
            string contentId = change.GetProperty("contentId").GetString()!;
            string expected = change.GetProperty("expectedRevision").GetString()!;
            string snapshot = change.GetProperty("snapshot").GetString()!;
            if (expected == "new")
            {
                after.Add(new BridgeDocumentChange(kind, contentId, expected, snapshot));
                continue;
            }
            StoredAuthoringDocument stored = _authoring.Get(kind, contentId);
            AuthoringValidationResult validation = _authoring.Validate(kind, contentId, snapshot, expected);
            if (!validation.Succeeded) throw new InvalidOperationException(string.Join("; ", validation.Diagnostics.Select(value => value.Message)));
            after.Add(new BridgeDocumentChange(kind, contentId, expected, snapshot));
            before.Add(new BridgeDocumentChange(kind, contentId, validation.PredictedRevision, stored.Snapshot));
        }
        var assets = new List<BridgeAssetChange>();
        if (arguments.TryGetProperty("lifecycle", out JsonElement lifecycle))
        foreach (JsonElement item in lifecycle.EnumerateArray())
        {
            assets.Add(new BridgeAssetChange(
                item.GetProperty("operation").GetString()!,
                item.GetProperty("contentId").GetString()!,
                item.TryGetProperty("sourceContentId", out JsonElement source) ? source.GetString() : null,
                item.TryGetProperty("resourceType", out JsonElement resourceType) ? resourceType.GetString() : null,
                item.TryGetProperty("path", out JsonElement path) ? path.GetString() : null,
                item.TryGetProperty("expectedReferenceRevision", out JsonElement reference) ? reference.GetString() : null));
        }
        if (after.Count == 0 && assets.Count == 0)
            throw new InvalidOperationException("MCP batch apply requires document or lifecycle changes.");
        string changeId = Guid.NewGuid().ToString("N");
        var payload = new BridgeBatchPayload(changeId, after.ToArray(), assets.ToArray());
        AuthoringBatchChangeSet batch = BuildBatch(payload);
        _authoring.ValidateBatch(batch);
        string afterPayload = JsonSerializer.Serialize(payload);
        string beforePayload = JsonSerializer.Serialize(new BridgeBatchPayload(Guid.NewGuid().ToString("N"), before.ToArray(), Array.Empty<BridgeAssetChange>()));
        _undoRedo!.CreateAction($"MCP apply {after.Count + assets.Count} authored changes", UndoRedo.MergeMode.Disable);
        _undoRedo.AddDoMethod(this, MethodName.ApplySerializedAuthoringBatch, afterPayload);
        if (assets.Count > 0)
            _undoRedo.AddUndoMethod(this, MethodName.UndoSerializedLifecycleBatch, changeId);
        else
            _undoRedo.AddUndoMethod(this, MethodName.ApplySerializedAuthoringBatch, beforePayload);
        _undoRedo.CommitAction();
        HashSet<string> deletedIds = assets.Where(value => ParseAssetKind(value.Operation) == AuthoringAssetChangeKind.Delete)
            .Select(value => value.ContentId).ToHashSet(StringComparer.Ordinal);
        string[] resultIds = after.Select(value => value.ContentId)
            .Concat(assets.Where(value => ParseAssetKind(value.Operation) is AuthoringAssetChangeKind.Create or AuthoringAssetChangeKind.Duplicate)
                .Select(value => value.ContentId))
            .Distinct(StringComparer.Ordinal).Where(value => !deletedIds.Contains(value)).ToArray();
        StoredAuthoringDocument[] applied = resultIds.Select(value =>
        {
            BridgeDocumentChange? change = after.FirstOrDefault(item => item.ContentId == value);
            BridgeAssetChange lifecycleChange = assets.FirstOrDefault(item => item.ContentId == value)!;
            string? kind = change?.Kind ?? lifecycleChange?.ResourceType;
            if (kind is null && lifecycleChange?.SourceContentId is { } sourceId)
                kind = _authoring.List().Single(item => item.Document.ContentId == sourceId).Entry.ResourceTypeIdValue;
            if (kind is null) throw new InvalidOperationException($"Lifecycle Resource type missing for '{value}'.");
            return _authoring.Get(kind, value);
        }).ToArray();
        return new
        {
            succeeded = true,
            revisions = applied.ToDictionary(value => value.Document.ContentId, value => value.Revision, StringComparer.Ordinal),
            created = assets.Where(value => ParseAssetKind(value.Operation) is AuthoringAssetChangeKind.Create or AuthoringAssetChangeKind.Duplicate).Select(value => value.ContentId).ToArray(),
            modified = after.Where(value => value.ExpectedRevision != "new").Select(value => value.ContentId).ToArray(),
            deleted = deletedIds.ToArray(),
            resources = applied.Select(value => new { contentId = value.Document.ContentId, path = value.Entry.DiagnosticPathValue, uid = value.Entry.ResourceUidValue, revision = value.Revision, reloadValidated = true }).ToArray(),
            evidence = "single-EditorUndoRedo+atomic-Resource-Catalog-UID-ledger+typed-reload"
        };
    }
    private object Preview(JsonElement arguments)
    {
        StoredAuthoringDocument stored = LoadDocument(arguments); string snapshotText = arguments.TryGetProperty("snapshot", out JsonElement snapshot) && snapshot.ValueKind == JsonValueKind.String ? snapshot.GetString()! : stored.Snapshot;
        int seedValue = arguments.TryGetProperty("seed", out JsonElement seed) ? seed.GetInt32() : 0;
        AuthoringValidationResult validation = _authoring.Validate(stored.Entry.ResourceTypeIdValue, stored.Document.ContentId, snapshotText);
        object? evidence = null;
        if (validation.Succeeded && stored.Entry.ResourceTypeIdValue == "skill" &&
            arguments.TryGetProperty("context", out JsonElement context))
        {
            var typed = new SkillBattlePreviewContext(
                context.GetProperty("encounterContentId").GetString()!,
                context.GetProperty("casterUnitInstanceId").GetString()!,
                context.TryGetProperty("targetUnitInstanceId", out JsonElement target) && target.ValueKind == JsonValueKind.String ? target.GetString() : null,
                new GridCellAuthoring(context.GetProperty("targetX").GetInt32(), context.GetProperty("targetY").GetInt32()),
                context.TryGetProperty("seed", out JsonElement contextSeed) ? contextSeed.GetUInt64() : (ulong)seedValue,
                context.TryGetProperty("casterUnitContentId", out JsonElement casterContent) ? casterContent.GetString() : null);
            evidence = _authoring.PreviewSkillBattle(stored.Document.ContentId, snapshotText, typed);
        }
        else if (validation.Succeeded) evidence = _authoring.Preview(stored.Entry.ResourceTypeIdValue, stored.Document.ContentId, snapshotText, seedValue);
        return new { succeeded = validation.Succeeded, contentId = stored.Document.ContentId, revision = validation.PredictedRevision, previewKind = stored.Entry.ResourceTypeIdValue, seed = seedValue, available = validation.PreviewAvailable, diagnostics = validation.Diagnostics, evidence };
    }
    private object ReferenceAudit(JsonElement arguments)
    {
        string? id = arguments.TryGetProperty("contentId", out JsonElement supplied) ? supplied.GetString() : null; AuthoringCatalogAuditRow[] rows = AuthoringCatalogAuditService.Audit(LoadCatalog()).Where(value => id is null || value.ContentId == id).ToArray();
        return new { succeeded = true, rows };
    }

    public void ApplySerializedDocument(string kind, string contentId, string expectedRevision, string snapshot)
    {
        _ = _authoring.ApplySingle(kind, contentId, expectedRevision, snapshot);
    }

    public void ApplySerializedAuthoringBatch(string payload)
    {
        BridgeBatchPayload batch = JsonSerializer.Deserialize<BridgeBatchPayload>(payload)
            ?? throw new InvalidOperationException("Serialized authoring batch is invalid.");
        _ = _authoring.ApplyBatch(BuildBatch(batch));
    }

    public void UndoSerializedLifecycleBatch(string changeId) => _authoring.UndoLifecycleBatch(changeId);

    private static AuthoringBatchChangeSet BuildBatch(BridgeBatchPayload payload) => new(
        payload.ChangeId,
        payload.Changes.Select(value => new AuthoringDocumentChange(ParseKind(value.Kind), value.ContentId,
            value.ExpectedRevision, value.Snapshot)),
        payload.Lifecycle.Select(value => new AuthoringAssetChange(ParseAssetKind(value.Operation), value.ContentId,
            value.SourceContentId, value.ResourceType, value.Path, value.ExpectedReferenceRevision)));

    private StoredAuthoringDocument LoadDocument(JsonElement arguments)
    {
        string kind = arguments.GetProperty("kind").GetString()!, contentId = arguments.GetProperty("contentId").GetString()!; return _authoring.Get(kind, contentId);
    }
    private GodotResourceCatalog LoadCatalog() => _authoring.LoadCatalog();
    private static AuthoringDocumentKind ParseKind(string value) => AuthoringResourceHandlerRegistry.Normalize(value) switch
    {
        "run-map" => AuthoringDocumentKind.Map,
        "event" => AuthoringDocumentKind.Event,
        "treasure" => AuthoringDocumentKind.Treasure,
        "encounter" => AuthoringDocumentKind.Encounter,
        "battle-layout" => AuthoringDocumentKind.BattleLayout,
        "ai" => AuthoringDocumentKind.Ai,
        "skill" => AuthoringDocumentKind.Skill,
        "presentation" => AuthoringDocumentKind.Presentation,
        _ => throw new InvalidOperationException($"Unsupported authoring kind '{value}'.")
    };
    private static AuthoringAssetChangeKind ParseAssetKind(string value) =>
        Enum.TryParse(value, true, out AuthoringAssetChangeKind parsed)
            ? parsed
            : throw new InvalidOperationException($"Unsupported lifecycle operation '{value}'.");
    private void WriteDescriptor(string state) { Directory.CreateDirectory(Path.GetDirectoryName(_descriptorPath)!); File.WriteAllText(_descriptorPath, JsonSerializer.Serialize(new { projectRoot = _projectRoot, pipeName = _pipeName, sessionToken = _token, processId = System.Environment.ProcessId, state })); }
    public void MarkReady()
    {
        if (!IsInsideTree() || EditorInterface.Singleton.GetResourceFilesystem().IsScanning()) return;
        _ready = true; WriteDescriptor("ready"); GD.Print($"[Tactics Tooling] Authoring bridge ready on {_pipeName}.");
    }
    private sealed record PendingRequest(string Json, TaskCompletionSource<string> Completion);
    private sealed record BridgeDocumentChange(string Kind, string ContentId, string ExpectedRevision, string Snapshot);
    private sealed record BridgeAssetChange(string Operation, string ContentId, string? SourceContentId,
        string? ResourceType, string? Path, string? ExpectedReferenceRevision);
    private sealed record BridgeBatchPayload(string ChangeId, BridgeDocumentChange[] Changes, BridgeAssetChange[] Lifecycle);
}
#endif
