#if TOOLS
using Godot;
using Tactics.Application.Authoring;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

public sealed record StoredAuthoringDocument(
    GodotResourceEntry Entry,
    Resource Resource,
    IAuthoringDocument Document,
    string Snapshot,
    string Revision);

public sealed class TacticsAuthoringEditorService
{
    public const string CatalogPath = "res://content/ContentCatalog.tres";
    private readonly AuthoringResourceHandlerRegistry _handlers = AuthoringResourceHandlerRegistry.CreateDefault();
    private readonly Dictionary<string, LifecycleTransactionState> _lifecycleTransactions = new(StringComparer.Ordinal);

    public IReadOnlyList<StoredAuthoringDocument> List(string? resourceTypeId = null)
    {
        resourceTypeId = resourceTypeId is null ? null : AuthoringResourceHandlerRegistry.Normalize(resourceTypeId);
        GodotResourceCatalog catalog = LoadCatalog(); var values = new List<StoredAuthoringDocument>();
        foreach (GodotResourceEntry entry in catalog.Entries.Where(value => resourceTypeId is null || value.ResourceTypeIdValue.Equals(resourceTypeId, StringComparison.OrdinalIgnoreCase)))
        {
            if (!_handlers.TryGet(entry.ResourceTypeIdValue, out IAuthoringResourceHandler? handler)) continue;
            Resource? resource = ResourceLoader.Load(entry.DiagnosticPathValue, string.Empty, ResourceLoader.CacheMode.Ignore);
            if (resource is null || !handler!.CanHandle(resource)) continue;
            IAuthoringDocument document = handler.Read(resource); values.Add(new StoredAuthoringDocument(entry, resource, document, handler.Serialize(document), AuthoringRevision.Compute(document)));
        }
        return Array.AsReadOnly(values.ToArray());
    }

    public StoredAuthoringDocument Get(string resourceTypeId, string contentId)
    {
        resourceTypeId = AuthoringResourceHandlerRegistry.Normalize(resourceTypeId);
        GodotResourceEntry entry = LoadCatalog().Entries.Single(value => value.ContentIdValue == contentId && value.ResourceTypeIdValue.Equals(resourceTypeId, StringComparison.OrdinalIgnoreCase));
        IAuthoringResourceHandler handler = _handlers.Get(entry.ResourceTypeIdValue);
        Resource resource = ResourceLoader.Load(entry.DiagnosticPathValue, string.Empty, ResourceLoader.CacheMode.Ignore) ?? throw new InvalidOperationException($"Resource cannot be loaded: {entry.DiagnosticPathValue}");
        if (!handler.CanHandle(resource)) throw new InvalidOperationException($"Resource '{contentId}' is not an editable {resourceTypeId} document.");
        IAuthoringDocument document = handler.Read(resource);
        return new StoredAuthoringDocument(entry, resource, document, handler.Serialize(document), AuthoringRevision.Compute(document));
    }

    public AuthoringValidationResult Validate(string resourceTypeId, string contentId, string snapshot, string? expectedRevision = null)
    {
        StoredAuthoringDocument stored = Get(resourceTypeId, contentId);
        if (expectedRevision is not null && !string.Equals(expectedRevision, stored.Revision, StringComparison.Ordinal))
            return Failed(stored.Revision, "authoring.revision_conflict", "Expected revision conflicts with the stored Resource.");
        IAuthoringResourceHandler handler = _handlers.Get(resourceTypeId); IAuthoringDocument document = handler.Deserialize(snapshot);
        if (!string.Equals(document.ContentId, contentId, StringComparison.Ordinal)) return Failed(stored.Revision, "authoring.identity_mismatch", "Snapshot identity differs from the stored Resource.");
        GodotResourceCatalog catalog = LoadCatalog();
        List<AuthoringDiagnostic> diagnostics = handler.Validate(document).Concat(ValidateDependencies(document, catalog)).Concat(ValidateTypedDependencies(document, catalog)).ToList();
        return new AuthoringValidationResult(diagnostics.All(value => value.Severity != AuthoringDiagnosticSeverity.Error), stored.Revision,
            AuthoringRevision.Compute(document), Array.AsReadOnly(diagnostics.ToArray()), PreviewAvailable: PreviewAvailable(resourceTypeId));
    }

    public StoredAuthoringDocument ApplySingle(string resourceTypeId, string contentId, string expectedRevision, string snapshot)
    {
        AuthoringValidationResult validation = Validate(resourceTypeId, contentId, snapshot, expectedRevision);
        if (!validation.Succeeded) throw new InvalidOperationException(string.Join("; ", validation.Diagnostics.Select(value => value.Message)));
        IAuthoringResourceHandler handler = _handlers.Get(resourceTypeId);
        return ApplyBatch(new AuthoringBatchChangeSet(Guid.NewGuid().ToString("N"),
            [new AuthoringDocumentChange(handler.Kind, contentId, expectedRevision, snapshot)])).Single();
    }

    public IReadOnlyList<StoredAuthoringDocument> ApplyBatch(AuthoringBatchChangeSet batch)
    {
        if (batch.AssetChanges.Count > 0 &&
            _lifecycleTransactions.TryGetValue(batch.ChangeId, out LifecycleTransactionState? replay) && replay.Undone)
        {
            replay.Receipt.RestoreAfter();
            _lifecycleTransactions[batch.ChangeId] = replay with { Undone = false };
            return ReloadTransactionDocuments(replay.DocumentIdentities);
        }
        AuthoringLifecycleBatchResult unified = new AuthoringResourceLifecycleService(this, _handlers).ApplyBatch(batch)
            ?? throw new InvalidOperationException("Authoring apply unexpectedly returned validation-only state.");
        if (batch.AssetChanges.Count > 0)
        {
            (string Type, string ContentId)[] identities = unified.Documents
                .Select(value => (value.Entry.ResourceTypeIdValue, value.Document.ContentId)).ToArray();
            _lifecycleTransactions[batch.ChangeId] = new LifecycleTransactionState(unified.Receipt, identities, false);
        }
        return unified.Documents;
    }

    public void UndoLifecycleBatch(string changeId)
    {
        if (!_lifecycleTransactions.TryGetValue(changeId, out LifecycleTransactionState? state) || state.Undone)
            throw new InvalidOperationException($"Lifecycle transaction '{changeId}' cannot be undone.");
        state.Receipt.RestoreBefore();
        _lifecycleTransactions[changeId] = state with { Undone = true };
    }

    private IReadOnlyList<StoredAuthoringDocument> ReloadTransactionDocuments(
        IEnumerable<(string Type, string ContentId)> identities) =>
        Array.AsReadOnly(identities.Select(value => Get(value.Type, value.ContentId)).ToArray());

    public void ValidateBatch(AuthoringBatchChangeSet batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (batch.AssetChanges.Count > 0)
        {
            new AuthoringResourceLifecycleService(this, _handlers).ValidateBatch(batch);
            return;
        }
        GodotResourceCatalog catalog = LoadCatalog();
        var documents = new List<IAuthoringDocument>();
        foreach (AuthoringDocumentChange change in batch.DocumentChanges)
        {
            string type = TypeId(change.Kind);
            StoredAuthoringDocument stored = Get(type, change.ContentId);
            if (stored.Revision != change.ExpectedRevision) throw new InvalidOperationException($"Revision conflict for '{change.ContentId}'.");
            IAuthoringResourceHandler handler = _handlers.Get(type);
            IAuthoringDocument document = handler.Deserialize(change.Snapshot);
            if (document.ContentId != change.ContentId) throw new InvalidOperationException($"Identity mismatch for '{change.ContentId}'.");
            AuthoringDiagnostic[] errors = handler.Validate(document).Concat(ValidateDependencies(document, catalog)).Concat(ValidateTypedDependencies(document, catalog))
                .Where(value => value.Severity == AuthoringDiagnosticSeverity.Error).ToArray();
            if (errors.Length > 0) throw new InvalidOperationException(string.Join("; ", errors.Select(value => value.Message)));
            documents.Add(document);
        }
        ValidateCrossDocument(documents);
    }

    public GodotResourceCatalog LoadCatalog()
    {
        GodotResourceCatalog value = ResourceLoader.Load<GodotResourceCatalog>(CatalogPath, string.Empty, ResourceLoader.CacheMode.Ignore) ?? throw new InvalidOperationException("Catalog cannot be loaded."); value.Validate(); return value;
    }

    public AuthoringPreviewEvidence Preview(string resourceTypeId, string contentId, string snapshot, int seed = 0)
    {
        AuthoringValidationResult validation = Validate(resourceTypeId, contentId, snapshot);
        if (!validation.Succeeded) throw new InvalidOperationException(string.Join("; ", validation.Diagnostics.Select(value => value.Message)));
        IAuthoringResourceHandler handler = _handlers.Get(resourceTypeId);
        return handler.Preview(handler.Deserialize(snapshot), seed);
    }

    public SkillBattlePreviewResult PreviewSkillBattle(
        string contentId,
        string snapshot,
        SkillBattlePreviewContext context)
    {
        AuthoringValidationResult validation = Validate("skill", contentId, snapshot);
        if (!validation.Succeeded)
            throw new InvalidOperationException(string.Join("; ", validation.Diagnostics.Select(value => value.Message)));
        SkillAuthoringDocument document = (SkillAuthoringDocument)_handlers.Get("skill").Deserialize(snapshot);
        return new SkillBattlePreviewAdapter().Preview(LoadCatalog(), document, context);
    }

    internal void ValidateProspectiveDocuments(IReadOnlyList<IAuthoringDocument> documents, GodotResourceCatalog catalog)
    {
        foreach (IAuthoringDocument document in documents)
        {
            string type = TypeId(document switch
            {
                MapAuthoringDocument => AuthoringDocumentKind.Map,
                EventAuthoringDocument => AuthoringDocumentKind.Event,
                TreasureAuthoringDocument => AuthoringDocumentKind.Treasure,
                EncounterAuthoringDocument => AuthoringDocumentKind.Encounter,
                BattleLayoutAuthoringDocument => AuthoringDocumentKind.BattleLayout,
                AiAuthoringDocument => AuthoringDocumentKind.Ai,
                SkillAuthoringDocument => AuthoringDocumentKind.Skill,
                PresentationProfileAuthoringDocument => AuthoringDocumentKind.Presentation,
                _ => throw new InvalidOperationException($"Unsupported authoring document type '{document.GetType().Name}'.")
            });
            IAuthoringResourceHandler handler = _handlers.Get(type);
            AuthoringDiagnostic[] errors = handler.Validate(document)
                .Concat(ValidateDependencies(document, catalog))
                .Concat(ValidateTypedDependencies(document, catalog))
                .Where(value => value.Severity == AuthoringDiagnosticSeverity.Error)
                .ToArray();
            if (errors.Length > 0)
                throw new InvalidOperationException(string.Join("; ", errors.Select(value => value.Message)));
        }
        ValidateCrossDocument(documents);
    }

    private void ValidateCrossDocument(IReadOnlyList<IAuthoringDocument> documents)
    {
        foreach (EncounterAuthoringDocument encounter in documents.OfType<EncounterAuthoringDocument>())
        {
            BattleLayoutAuthoringDocument? layout = documents.OfType<BattleLayoutAuthoringDocument>().SingleOrDefault(value => value.ContentId == encounter.LayoutContentId);
            layout ??= Get("battle-layout", encounter.LayoutContentId).Document as BattleLayoutAuthoringDocument;
            EncounterLayoutAuthoringValidator.Validate(encounter, layout ?? throw new InvalidOperationException($"Encounter layout '{encounter.LayoutContentId}' cannot be loaded."));
        }
    }

    private static List<AuthoringDiagnostic> ValidateDependencies(IAuthoringDocument document, GodotResourceCatalog catalog)
    {
        HashSet<string> ids = catalog.Entries.Select(value => value.ContentIdValue).ToHashSet(StringComparer.Ordinal); var diagnostics = new List<AuthoringDiagnostic>();
        foreach (string missing in document.Dependencies.Where(value => !ids.Contains(value))) diagnostics.Add(new AuthoringDiagnostic("catalog.reference_missing", AuthoringDiagnosticSeverity.Error, $"Missing ContentId '{missing}'.", missing));
        return diagnostics;
    }
    private static IEnumerable<AuthoringDiagnostic> ValidateTypedDependencies(IAuthoringDocument document, GodotResourceCatalog catalog)
    {
        Dictionary<string, GodotResourceEntry> entries = catalog.Entries.ToDictionary(value => value.ContentIdValue, StringComparer.Ordinal);
        var diagnostics = new List<AuthoringDiagnostic>();
        void Require(string id, string type, string? prefix = null)
        {
            if (!entries.TryGetValue(id, out GodotResourceEntry? entry)) return;
            if (entry.ResourceTypeIdValue != type || (prefix is not null && !id.StartsWith(prefix, StringComparison.Ordinal)))
                diagnostics.Add(new AuthoringDiagnostic("catalog.reference_type_mismatch", AuthoringDiagnosticSeverity.Error,
                    $"ContentId '{id}' must reference {type}{(prefix is null ? string.Empty : " " + prefix)} content.", id));
        }
        switch (document)
        {
            case MapAuthoringDocument map:
                foreach (MapAuthoringNode node in map.Nodes)
                    Require(node.ContentId, node.Layer == 0 ? "run" : node.Kind switch { Tactics.Core.Runs.PureRunNodeKind.Rest => "rest", Tactics.Core.Runs.PureRunNodeKind.Store => "store", Tactics.Core.Runs.PureRunNodeKind.Mystery => "event", Tactics.Core.Runs.PureRunNodeKind.Treasure => "treasure", _ => "encounter" });
                break;
            case EventAuthoringDocument value:
                foreach (EventOutcomeAuthoring outcome in value.Options.SelectMany(option => option.Failure is null ? new[] { option.Success } : new[] { option.Success, option.Failure }))
                    if (outcome.EffectContentId is { } id) Require(id, outcome.Type == EventOutcomeType.Item ? "item" : "buff");
                break;
            case TreasureAuthoringDocument value:
                foreach (TreasureEntryAuthoring entry in value.Entries)
                    Require(entry.ContentId, entry.Kind == TreasureEntryKind.Buff ? "buff" : "item", entry.Kind switch { TreasureEntryKind.Equipment => "item.equipment.", TreasureEntryKind.Consumable => "item.consumable.", _ => null });
                break;
            case EncounterAuthoringDocument value:
                Require(value.LayoutContentId, "battle-layout");
                foreach (string id in value.MonsterUnitContentIds) Require(id, "unit");
                foreach (string id in value.MonsterAiContentIds) Require(id, "ai");
                break;
            case AiAuthoringDocument value:
                foreach (string id in value.SkillContentIds.Concat(value.PatternSkillContentIds)) Require(id, "skill");
                break;
            case SkillAuthoringDocument value:
                if (value.Definition.StatusContentId is { } status) Require(status.Value, "buff");
                if (value.Definition.PrerequisiteContentId is { } prerequisite) Require(prerequisite.Value, "skill");
                if (value.Definition.ExecutionProfile.DetonateStatusContentId is { } detonate) Require(detonate.Value, "buff");
                if (value.Definition.ExecutionProfile.SummonAttackContentId is { } attack) Require(attack.Value, "skill");
                if (value.Definition.ExecutionProfile.SummonDefinitionId is { } summon) Require(summon.Value, "unit");
                break;
        }
        return diagnostics;
    }
    private static AuthoringValidationResult Failed(string revision, string code, string message) => new(false, revision, revision, new[] { new AuthoringDiagnostic(code, AuthoringDiagnosticSeverity.Error, message) });
    private static bool PreviewAvailable(string type) => type is "event" or "treasure" or "encounter" or "ai" or "skill" or "presentation";
    internal static string TypeId(AuthoringDocumentKind kind) => kind switch { AuthoringDocumentKind.Map => "run-map", AuthoringDocumentKind.Event => "event", AuthoringDocumentKind.Treasure => "treasure", AuthoringDocumentKind.Encounter => "encounter", AuthoringDocumentKind.BattleLayout => "battle-layout", AuthoringDocumentKind.Ai => "ai", AuthoringDocumentKind.Skill => "skill", AuthoringDocumentKind.Presentation => "presentation", _ => throw new ArgumentOutOfRangeException(nameof(kind)) };

    private sealed record LifecycleTransactionState(
        WorkbenchTransactionReceipt Receipt,
        IReadOnlyList<(string Type, string ContentId)> DocumentIdentities,
        bool Undone);
}
#endif
