#if TOOLS
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Text;
using Godot;
using Tactics.Application.Authoring;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

public sealed record AuthoringOwnershipRecord(
    string ContentId,
    string ResourceTypeId,
    string ResourcePath,
    string ResourceUid,
    AuthoringResourceOwnership Ownership,
    string OperationId,
    DateTimeOffset TimestampUtc);

internal sealed record AuthoringLifecycleBatchResult(
    IReadOnlyList<StoredAuthoringDocument> Documents,
    WorkbenchTransactionReceipt Receipt);

public sealed class AuthoringResourceLifecycleService
{
    public const string AuthoredRoot = "res://content/authored";
    public const string LedgerPath = AuthoredRoot + "/authoring-uid-ledger.jsonl";

    private readonly TacticsAuthoringEditorService _authoring;
    private readonly AuthoringResourceHandlerRegistry _handlers;

    public AuthoringResourceLifecycleService(
        TacticsAuthoringEditorService? authoring = null,
        AuthoringResourceHandlerRegistry? handlers = null)
    {
        _authoring = authoring ?? new TacticsAuthoringEditorService();
        _handlers = handlers ?? AuthoringResourceHandlerRegistry.CreateDefault();
    }

    public AuthoringResourceOwnership GetOwnership(string contentId)
    {
        AuthoringOwnershipRecord? latest = ReadLedger().Where(value => value.ContentId == contentId)
            .OrderBy(value => value.TimestampUtc).LastOrDefault();
        return latest?.Ownership ?? AuthoringResourceOwnership.Protected;
    }

    internal void ValidateBatch(AuthoringBatchChangeSet batch) =>
        _ = ApplyBatch(batch, validateOnly: true);

    internal AuthoringLifecycleBatchResult? ApplyBatch(
        AuthoringBatchChangeSet batch,
        Action<WorkbenchResourceSaveCheckpoint, int>? faultInjection = null,
        bool validateOnly = false)
    {
        ArgumentNullException.ThrowIfNull(batch);
        GodotResourceCatalog catalog = _authoring.LoadCatalog();
        GodotResourceCatalog stagedCatalog = (GodotResourceCatalog)catalog.Duplicate(true);
        Dictionary<string, StoredAuthoringDocument> storedById = _authoring.List()
            .ToDictionary(value => value.Document.ContentId, StringComparer.Ordinal);
        Dictionary<string, IAuthoringDocument> prospectiveDocuments = storedById.Values
            .ToDictionary(value => value.Document.ContentId, value => value.Document, StringComparer.Ordinal);
        Dictionary<string, AuthoringDocumentChange> changesById = batch.DocumentChanges
            .ToDictionary(value => value.ContentId, StringComparer.Ordinal);
        var consumedChanges = new HashSet<string>(StringComparer.Ordinal);
        var staged = new List<(string ContentId, string Type, string Path, IAuthoringResourceHandler Handler, IAuthoringDocument Document, Resource Resource, long? AssignedUid)>();
        var createdUids = new List<long>();
        var ownershipRecords = new List<AuthoringOwnershipRecord>();
        var deletePaths = new List<string>();
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;

        try
        {
            foreach (AuthoringAssetChange asset in batch.AssetChanges.Where(value => value.Kind is AuthoringAssetChangeKind.Create or AuthoringAssetChangeKind.Duplicate))
            {
                RequireIdentity(asset.ContentId, nameof(asset.ContentId));
                if (storedById.ContainsKey(asset.ContentId) || stagedCatalog.Entries.Any(value => value.ContentIdValue == asset.ContentId))
                    throw new InvalidOperationException($"ContentId '{asset.ContentId}' already exists.");
                if (ReadLedger().Any(value => value.ContentId == asset.ContentId))
                    throw new InvalidOperationException($"ContentId '{asset.ContentId}' is present in the append-only authoring ledger and cannot be reused.");

                StoredAuthoringDocument source;
                string type;
                if (asset.Kind == AuthoringAssetChangeKind.Duplicate)
                {
                    source = storedById.TryGetValue(asset.SourceContentId!, out StoredAuthoringDocument? value)
                        ? value
                        : throw new InvalidOperationException($"Duplicate source '{asset.SourceContentId}' does not exist.");
                    type = AuthoringResourceHandlerRegistry.Normalize(asset.ResourceType ?? source.Entry.ResourceTypeIdValue);
                    if (!string.Equals(type, source.Entry.ResourceTypeIdValue, StringComparison.Ordinal))
                        throw new InvalidOperationException("Duplicate source and target Resource types differ.");
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(asset.ResourceType))
                        throw new InvalidOperationException("Create requires ResourceType.");
                    type = AuthoringResourceHandlerRegistry.Normalize(asset.ResourceType);
                    source = storedById.Values.FirstOrDefault(value => value.Entry.ResourceTypeIdValue == type)
                        ?? throw new InvalidOperationException($"No valid {type} Resource exists to supply the typed Godot Resource class.");
                }

                IAuthoringResourceHandler handler = _handlers.Get(type);
                IAuthoringDocument document;
                if (changesById.TryGetValue(asset.ContentId, out AuthoringDocumentChange? supplied))
                {
                    if (!string.Equals(supplied.ExpectedRevision, "new", StringComparison.Ordinal))
                        throw new InvalidOperationException($"New document '{asset.ContentId}' must use expected revision 'new'.");
                    if (TacticsAuthoringEditorService.TypeId(supplied.Kind) != type)
                        throw new InvalidOperationException($"New document '{asset.ContentId}' has a mismatched Resource type.");
                    document = handler.Deserialize(supplied.Snapshot);
                    consumedChanges.Add(asset.ContentId);
                }
                else
                {
                    document = asset.Kind == AuthoringAssetChangeKind.Create
                        ? CreateTemplate(type, asset.ContentId, catalog) ?? DuplicateDocument(handler, source.Snapshot, asset.ContentId, type)
                        : DuplicateDocument(handler, source.Snapshot, asset.ContentId, type);
                }
                if (document.ContentId != asset.ContentId)
                    throw new InvalidOperationException($"Lifecycle document identity differs from '{asset.ContentId}'.");

                Resource identitySource = (Resource)source.Resource.Duplicate(true);
                identitySource.Set("ContentIdValue", asset.ContentId);
                Resource resource = handler.Stage(identitySource, document);
                string path = ValidateAuthoredResourcePath(asset.Path ?? BuildPath(type, asset.ContentId));
                if (File.Exists(ProjectSettings.GlobalizePath(path)))
                    throw new InvalidOperationException($"Authored Resource path already exists: {path}.");
                long uid = ResourceUid.CreateId();
                createdUids.Add(uid);
                string uidText = ResourceUid.IdToText(uid);
                var entry = new GodotResourceEntry
                {
                    ContentIdValue = asset.ContentId,
                    ResourceTypeIdValue = type,
                    ResourceUidValue = uidText,
                    DiagnosticPathValue = path,
                    SchemaVersion = document.SchemaVersion,
                    ReferenceContentIds = document.Dependencies.ToArray()
                };
                stagedCatalog.Entries = stagedCatalog.Entries.Append(entry)
                    .OrderBy(value => value.ContentIdValue, StringComparer.Ordinal).ToArray();
                prospectiveDocuments[asset.ContentId] = document;
                staged.Add((asset.ContentId, type, path, handler, document, resource, uid));
                ownershipRecords.Add(new AuthoringOwnershipRecord(asset.ContentId, type, path, uidText,
                    AuthoringResourceOwnership.WorkbenchOwned, batch.ChangeId, timestamp));
            }

            foreach (AuthoringDocumentChange change in batch.DocumentChanges.Where(value => !consumedChanges.Contains(value.ContentId)))
            {
                if (batch.AssetChanges.Any(value => value.Kind == AuthoringAssetChangeKind.Delete && value.ContentId == change.ContentId))
                    throw new InvalidOperationException($"Delete target '{change.ContentId}' cannot also be modified.");
                StoredAuthoringDocument stored = storedById.TryGetValue(change.ContentId, out StoredAuthoringDocument? value)
                    ? value
                    : throw new InvalidOperationException($"Document '{change.ContentId}' does not exist.");
                string type = TacticsAuthoringEditorService.TypeId(change.Kind);
                if (stored.Entry.ResourceTypeIdValue != type)
                    throw new InvalidOperationException($"Document '{change.ContentId}' has a mismatched Resource type.");
                if (stored.Revision != change.ExpectedRevision)
                    throw new InvalidOperationException($"Revision conflict for '{change.ContentId}'.");
                IAuthoringResourceHandler handler = _handlers.Get(type);
                IAuthoringDocument document = handler.Deserialize(change.Snapshot);
                if (document.ContentId != change.ContentId)
                    throw new InvalidOperationException($"Identity mismatch for '{change.ContentId}'.");
                prospectiveDocuments[change.ContentId] = document;
                Resource resource = handler.Stage(stored.Resource, document);
                staged.Add((change.ContentId, type, stored.Entry.DiagnosticPathValue, handler, document, resource, null));
            }

            foreach (var value in staged)
            {
                GodotResourceEntry entry = stagedCatalog.Entries.Single(item => item.ContentIdValue == value.ContentId);
                entry.SchemaVersion = value.Document.SchemaVersion;
                entry.ReferenceContentIds = value.Document.Dependencies.ToArray();
            }

            foreach (AuthoringAssetChange asset in batch.AssetChanges.Where(value => value.Kind == AuthoringAssetChangeKind.Delete))
            {
                if (GetOwnership(asset.ContentId) != AuthoringResourceOwnership.WorkbenchOwned)
                    throw new InvalidOperationException($"'{asset.ContentId}' is protected; only Workbench-owned resources can be deleted.");
                AuthoringReferenceSnapshot snapshot = CaptureReferences(asset.ContentId);
                if (snapshot.Revision != asset.ExpectedReferenceRevision)
                    throw new InvalidOperationException($"Reference snapshot changed for '{asset.ContentId}'.");
                GodotResourceEntry entry = stagedCatalog.Entries.Single(value => value.ContentIdValue == asset.ContentId);
                _ = ValidateAuthoredResourcePath(entry.DiagnosticPathValue);
                prospectiveDocuments.Remove(asset.ContentId);
                string[] blockers = prospectiveDocuments.Values
                    .Where(value => value.Dependencies.Contains(asset.ContentId, StringComparer.Ordinal))
                    .Select(value => value.ContentId).Order(StringComparer.Ordinal).ToArray();
                if (blockers.Length > 0)
                    throw new InvalidOperationException($"'{asset.ContentId}' is still referenced by: {string.Join(", ", blockers)}.");
                stagedCatalog.Entries = stagedCatalog.Entries.Where(value => value.ContentIdValue != asset.ContentId).ToArray();
                deletePaths.Add(entry.DiagnosticPathValue);
                ownershipRecords.Add(new AuthoringOwnershipRecord(asset.ContentId, entry.ResourceTypeIdValue,
                    entry.DiagnosticPathValue, entry.ResourceUidValue, AuthoringResourceOwnership.Tombstoned,
                    batch.ChangeId, timestamp));
            }

            _authoring.ValidateProspectiveDocuments(prospectiveDocuments.Values.ToArray(), stagedCatalog);
            if (validateOnly)
            {
                try
                {
                    foreach (var value in staged.Where(value => value.AssignedUid.HasValue))
                        ResourceUid.AddId(value.AssignedUid!.Value, value.Path);
                    stagedCatalog.Validate();
                    return null;
                }
                finally
                {
                    foreach (long uid in createdUids)
                        if (ResourceUid.HasId(uid)) ResourceUid.RemoveId(uid);
                }
            }

            var resourceRequests = staged.Select(value => new WorkbenchResourceSaveRequest(value.Resource, value.Path, resource =>
            {
                if (!value.Handler.CanHandle(resource))
                    throw new InvalidOperationException($"Authored Resource '{value.ContentId}' reloaded with the wrong type.");
                IAuthoringDocument reloaded = value.Handler.Read(resource);
                if (AuthoringRevision.Compute(reloaded) != AuthoringRevision.Compute(value.Document))
                    throw new InvalidOperationException($"Authored Resource '{value.ContentId}' differs from its staged document.");
            }, value.AssignedUid)).Append(new WorkbenchResourceSaveRequest(stagedCatalog,
                TacticsAuthoringEditorService.CatalogPath, resource => ((GodotResourceCatalog)resource).Validate()));
            IEnumerable<WorkbenchFileMutationRequest> mutations = deletePaths
                .Select(path => new WorkbenchFileMutationRequest(path, null));
            if (ownershipRecords.Count > 0)
                mutations = new[] { new WorkbenchFileMutationRequest(LedgerPath, BuildLedgerBytes(ownershipRecords)) }
                    .Concat(mutations);
            WorkbenchTransactionReceipt receipt = WorkbenchResourceBatchSaveService.SaveWithRollbackAndReceipt(
                resourceRequests, mutations, faultInjection);

            string[] resultIds = staged.Select(value => value.ContentId).Distinct(StringComparer.Ordinal)
                .Where(value => prospectiveDocuments.ContainsKey(value)).ToArray();
            StoredAuthoringDocument[] documents = resultIds.Select(value =>
            {
                GodotResourceEntry entry = stagedCatalog.Entries.Single(item => item.ContentIdValue == value);
                return _authoring.Get(entry.ResourceTypeIdValue, value);
            }).ToArray();
            return new AuthoringLifecycleBatchResult(Array.AsReadOnly(documents), receipt);
        }
        catch
        {
            foreach (long uid in createdUids)
                if (ResourceUid.HasId(uid)) ResourceUid.RemoveId(uid);
            throw;
        }
    }

    private static IAuthoringDocument? CreateTemplate(string resourceTypeId, string newContentId, GodotResourceCatalog catalog)
    {
        string First(string type, Func<GodotResourceEntry, bool>? predicate = null) => catalog.Entries
            .Where(value => value.ResourceTypeIdValue == type && (predicate?.Invoke(value) ?? true))
            .Select(value => value.ContentIdValue).FirstOrDefault()
            ?? throw new InvalidOperationException($"Catalog has no {type} template dependency.");
        return resourceTypeId switch
        {
            "run-map" => new MapAuthoringDocument(newContentId, 3,
                [
                    new MapAuthoringNode("start", 0, Tactics.Core.Runs.PureRunNodeKind.Battle, First("run"), "Start", 0),
                    new MapAuthoringNode("boss", 1, Tactics.Core.Runs.PureRunNodeKind.Boss, First("encounter", value => value.ContentIdValue.EndsWith(".special", StringComparison.Ordinal)), "Boss", 0)
                ], [new MapAuthoringConnection("start", "boss")]),
            "event" => new EventAuthoringDocument(newContentId, newContentId, "New Event", string.Empty,
                [new EventOptionAuthoring("continue", "Continue", Tactics.Core.Runs.RunEventAttribute.None, 100,
                    new EventOutcomeAuthoring(EventOutcomeType.Nothing, EventOutcomeTarget.All, 0, null, string.Empty), null)]),
            "treasure" => new TreasureAuthoringDocument(newContentId, 0, 0, Array.Empty<TreasureEntryAuthoring>()),
            "battle-layout" => new BattleLayoutAuthoringDocument(newContentId,
                [new GridCellAuthoring(1, 4)], [new GridCellAuthoring(7, 4)], Array.Empty<GridCellAuthoring>()),
            "encounter" => new EncounterAuthoringDocument(newContentId, First("battle-layout"),
                [First("unit", value => value.ContentIdValue.Contains("goat-", StringComparison.Ordinal))],
                [First("ai")], 1, 1, 0, Tactics.Core.Encounters.EncounterClass.Normal),
            "ai" => CreateAiTemplate(newContentId, First("skill")),
            _ => null
        };
    }

    private byte[] BuildLedgerBytes(IEnumerable<AuthoringOwnershipRecord> appended)
    {
        string[] lines = ReadLedger().Concat(appended)
            .Select(value => JsonSerializer.Serialize(value)).ToArray();
        return Encoding.UTF8.GetBytes(string.Join(System.Environment.NewLine, lines) + System.Environment.NewLine);
    }

    private static AiAuthoringDocument CreateAiTemplate(string contentId, string skillId)
    {
        string hash = "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(contentId))).ToLowerInvariant();
        AiAuthoringNode[] nodes =
        [
            new("intent", AiAuthoringNodeKind.Intent, "BasicAttack", true, 1, Array.Empty<AiCurveKeyAuthoring>(), 80, 80),
            new("score", AiAuthoringNodeKind.Score, "TargetHealth", true, 1,
                [new AiCurveKeyAuthoring(0, 0, 0, 0), new AiCurveKeyAuthoring(1, 1, 0, 0)], 340, 80)
        ];
        return new AiAuthoringDocument(contentId, Tactics.Core.AI.AiArchetype.Charger, [skillId], Array.Empty<string>(),
            1, 1, 0, 0, nodes, [new AiAuthoringEdge("intent", "score")], hash, 3, 1, 2, .5f);
    }

    public AuthoringReferenceSnapshot CaptureReferences(string contentId)
    {
        GodotResourceCatalog catalog = _authoring.LoadCatalog();
        GodotResourceEntry target = catalog.Entries.Single(value => value.ContentIdValue == contentId);
        string[] forward = target.ReferenceContentIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        string[] reverse = catalog.Entries.Where(value => value.ReferenceContentIds.Contains(contentId, StringComparer.Ordinal))
            .Select(value => value.ContentIdValue).Order(StringComparer.Ordinal).ToArray();
        string revision = AuthoringRevision.ComputeStrings(new[] { contentId, target.ResourceUidValue }
            .Concat(forward.Select(value => "f:" + value)).Concat(reverse.Select(value => "r:" + value)));
        return new AuthoringReferenceSnapshot(contentId, forward, reverse, revision);
    }

    public IReadOnlyList<AuthoringOwnershipRecord> ReadLedger()
    {
        string absolute = ProjectSettings.GlobalizePath(LedgerPath);
        if (!File.Exists(absolute)) return Array.Empty<AuthoringOwnershipRecord>();
        return Array.AsReadOnly(File.ReadLines(absolute).Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => JsonSerializer.Deserialize<AuthoringOwnershipRecord>(value)
                ?? throw new InvalidOperationException("Authoring UID ledger contains an invalid record."))
            .ToArray());
    }

    private static IAuthoringDocument DuplicateDocument(IAuthoringResourceHandler handler, string snapshot, string newContentId, string resourceTypeId)
    {
        JsonObject root = JsonNode.Parse(snapshot)?.AsObject() ?? throw new InvalidOperationException("Authoring snapshot is not a JSON object.");
        root["contentId"] = newContentId;
        if (resourceTypeId == "skill")
        {
            root["sourceId"] = newContentId;
            root["sourceKind"] = SkillAuthoringSourceKind.GodotAuthored.ToString();
            root["sourcePath"] = string.Empty;
            root["sourceGuid"] = string.Empty;
            root["sourceLocalFileId"] = 0;
            root["graphPath"] = string.Empty;
            root["graphDependencyHash"] = string.Empty;
        }
        return handler.Deserialize(root.ToJsonString());
    }

    private static string BuildPath(string resourceTypeId, string contentId)
    {
        string safe = new(contentId.Select(value => char.IsLetterOrDigit(value) || value is '-' or '_' ? value : '-').ToArray());
        return $"{AuthoredRoot}/{resourceTypeId}/{safe}.tres";
    }

    private static string ValidateAuthoredResourcePath(string path)
    {
        if (!path.StartsWith(AuthoredRoot + "/", StringComparison.Ordinal) ||
            !path.EndsWith(".tres", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Workbench-authored Resources must stay below the authored content root and use .tres.");
        string root = Path.GetFullPath(ProjectSettings.GlobalizePath(AuthoredRoot))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string absolute = Path.GetFullPath(ProjectSettings.GlobalizePath(path));
        if (!absolute.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Workbench-authored Resource path escapes the authored content root.");
        return path;
    }

    private static void RequireIdentity(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A non-empty identity is required.", name);
    }

}
#endif
