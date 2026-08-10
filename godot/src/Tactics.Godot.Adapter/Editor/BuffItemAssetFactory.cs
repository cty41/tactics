#if TOOLS
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Godot;
using Tactics.Core.Content;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

/// <summary>
/// Generates the frozen Buff/Item resources and composes the canonical cross-batch catalog.
/// </summary>
public static class BuffItemAssetFactory
{
    private const string BatchId = "pure-run-buffs-items-v1";
    private const string DefaultRoot = "res://content/buffs_items";
    private const string BatchCatalogPath = DefaultRoot + "/ContentCatalog.tres";
    private const string GlobalCatalogPath = "res://content/ContentCatalog.tres";
    private const string PoisonPath = "res://content/poison_spear/PoisonBuff.tres";
    private const string PoisonCatalogPath = "res://content/poison_spear/ContentCatalog.tres";
    private const string UnitCatalogPath = "res://content/units/ContentCatalog.tres";

    public static void Build(string? draftPath = null, string root = DefaultRoot)
    {
        if (root != DefaultRoot)
            throw new ArgumentException($"Buff/Item assets must use the canonical root '{DefaultRoot}'.", nameof(root));
        string projectPath = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(ProjectSettings.GlobalizePath("res://")));
        string repositoryPath = Directory.GetParent(projectPath)?.FullName
            ?? throw new InvalidOperationException("Cannot resolve migration repository root.");
        draftPath ??= Path.Combine(
            repositoryPath,
            "Tools",
            "migration",
            "out",
            "pure-run-buffs-items-v1.draft.json");
        BuffItemMigrationDraft draft = BuffItemMigrationDraft.Load(draftPath);
        CompiledBuffItemDefinitions compiled = draft.CompileApplicationDefinitions();
        if (compiled.Statuses.Count != 14 || compiled.Consumables.Count != 3 ||
            compiled.Equipment.Count != 12 || compiled.Snapshot.Entries.Count != 29)
        {
            throw new InvalidOperationException("Pure Run Buff/Item draft has incomplete compiled content.");
        }
        PoisonBuffResource poison = ResourceLoader.Load<PoisonBuffResource>(
            PoisonPath,
            string.Empty,
            ResourceLoader.CacheMode.Ignore)
            ?? throw new InvalidOperationException("The externally owned Poison resource is missing.");
        if (poison.ContentIdValue != "buff.poison")
            throw new InvalidOperationException("The externally owned Poison resource has the wrong ContentId.");

        EnsureDirectory(root);
        string[] generatedDefinitionPaths = draft.Buffs
            .Where(buff => !buff.ExternalDependency)
            .Select(buff => ResourcePath(buff.ContentId))
            .Concat(draft.Consumables.Select(item => ResourcePath(item.ContentId)))
            .Concat(draft.Equipment.Select(item => ResourcePath(item.ContentId)))
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] targetPaths = generatedDefinitionPaths
            .Append(BatchCatalogPath)
            .Append(GlobalCatalogPath)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string ledgerPath = Path.Combine(
            repositoryPath,
            "Tools",
            "migration",
            "manifest",
            "state",
            $"{BatchId}.json");
        var transaction = new ResourceSaveTransaction(ledgerPath, draft.Source);
        transaction.Preflight(targetPaths);

        try
        {
            var statusResources = new Dictionary<string, StatusDefinitionResource>(StringComparer.Ordinal);
            foreach (BuffItemDraftStatus status in draft.Buffs
                         .Where(status => !status.ExternalDependency)
                         .OrderBy(status => status.ContentId, StringComparer.Ordinal))
            {
                string path = ResourcePath(status.ContentId);
                StatusDefinitionResource resource = LoadOrCreate<StatusDefinitionResource>(path);
                Populate(resource, status);
                resource.ToCoreDefinition();
                SaveResource(resource, path);
                statusResources.Add(status.ContentId, resource);
            }

            var consumableResources = new Dictionary<string, ConsumableDefinitionResource>(StringComparer.Ordinal);
            foreach (BuffItemDraftConsumable item in draft.Consumables.OrderBy(
                         item => item.ContentId,
                         StringComparer.Ordinal))
            {
                string path = ResourcePath(item.ContentId);
                ConsumableDefinitionResource resource = LoadOrCreate<ConsumableDefinitionResource>(path);
                Populate(resource, item);
                resource.ToCoreDefinition();
                SaveResource(resource, path);
                consumableResources.Add(item.ContentId, resource);
            }

            var equipmentResources = new Dictionary<string, EquipmentDefinitionResource>(StringComparer.Ordinal);
            foreach (BuffItemDraftEquipment item in draft.Equipment.OrderBy(
                         item => item.ContentId,
                         StringComparer.Ordinal))
            {
                string path = ResourcePath(item.ContentId);
                EquipmentDefinitionResource resource = LoadOrCreate<EquipmentDefinitionResource>(path);
                Populate(resource, item);
                resource.ToCoreDefinition();
                SaveResource(resource, path);
                equipmentResources.Add(item.ContentId, resource);
            }

            var batchCatalog = new GodotResourceCatalog();
            batchCatalog.Entries = draft.Buffs.Select(status => Entry(
                    status.ContentId,
                    "buff",
                    status.ExternalDependency ? PoisonPath : ResourcePath(status.ContentId),
                    string.IsNullOrEmpty(status.MeleeRetaliationBuffContentId)
                        ? Array.Empty<string>()
                        : new[] { status.MeleeRetaliationBuffContentId }))
                .Concat(draft.Consumables.Select(item => Entry(item.ContentId, "item", ResourcePath(item.ContentId))))
                .Concat(draft.Equipment.Select(item => Entry(item.ContentId, "item", ResourcePath(item.ContentId))))
                .OrderBy(entry => entry.ContentIdValue, StringComparer.Ordinal)
                .ToArray();
            SaveResource(batchCatalog, BatchCatalogPath);
            batchCatalog.Validate();

            GodotResourceCatalog poisonCatalog = LoadCatalog(PoisonCatalogPath);
            GodotResourceCatalog unitCatalog = LoadCatalog(UnitCatalogPath);
            var globalCatalog = new GodotResourceCatalog();
            globalCatalog.Entries = ComposeGlobalEntries(poisonCatalog, unitCatalog, batchCatalog);
            SaveResource(globalCatalog, GlobalCatalogPath);
            globalCatalog.Validate();
            BuffItemBatchValidator.Validate(batchCatalog, globalCatalog);

            var semantics = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (BuffItemDraftStatus status in draft.Buffs.Where(status => !status.ExternalDependency))
                semantics[ResourcePath(status.ContentId)] = JsonSerializer.SerializeToElement(status);
            foreach (BuffItemDraftConsumable item in draft.Consumables)
                semantics[ResourcePath(item.ContentId)] = JsonSerializer.SerializeToElement(item);
            foreach (BuffItemDraftEquipment item in draft.Equipment)
                semantics[ResourcePath(item.ContentId)] = JsonSerializer.SerializeToElement(item);
            semantics[BatchCatalogPath] = CatalogSemantic(batchCatalog);
            semantics[GlobalCatalogPath] = CatalogSemantic(globalCatalog);
            transaction.Commit(targetPaths, semantics);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static void Populate(StatusDefinitionResource resource, BuffItemDraftStatus status)
    {
        resource.SchemaVersion = 1;
        resource.ContentIdValue = status.ContentId;
        resource.SourceId = status.SourceId;
        resource.DefaultDuration = status.DefaultDuration;
        resource.CanAct = status.CanAct;
        resource.PolarityValue = status.Polarity;
        resource.EffectKindValue = status.EffectType;
        resource.TriggerTimingValue = status.TriggerTiming;
        resource.RefreshStrategyValue = status.RefreshStrategy;
        resource.CurseCategory = status.CurseCategory;
        resource.DamagePerTurn = status.DamagePerTurn;
        resource.ElementKindValue = status.ElementType;
        resource.DamageCategoryValue = status.DamageCategory;
        resource.SpeedModifier = status.SpeedModifier;
        resource.DamageReductionPercent = status.DamageReductionPercent;
        resource.MeleeRetaliationStatusIdValue = status.MeleeRetaliationBuffContentId;
        resource.MeleeRetaliationDuration = status.MeleeRetaliationDuration;
        resource.SourcePath = status.SourcePath;
        resource.SourceGuid = status.SourceGuid;
        resource.SourceLocalFileId = status.SourceLocalFileId;
        resource.IconSourcePath = status.IconAudit.SourcePath;
        resource.IconSourceGuid = status.IconAudit.SourceGuid;
        resource.IconSourceLocalFileId = status.IconAudit.SourceLocalFileId;
        resource.IconDependencyHash = status.IconAudit.DependencyHash;
        resource.IconPayloadCopied = status.IconAudit.PayloadCopied;
    }

    private static void Populate(ConsumableDefinitionResource resource, BuffItemDraftConsumable item)
    {
        resource.SchemaVersion = 1;
        resource.ContentIdValue = item.ContentId;
        resource.SourceId = item.SourceId;
        resource.DisplayName = item.DisplayName;
        resource.Description = item.Description;
        resource.RarityValue = item.Rarity;
        resource.Price = item.Price;
        resource.MaxCharges = item.MaxCharges;
        resource.EffectKindValue = item.EffectKind;
        resource.Magnitude = checked((int)item.Magnitude);
        resource.MaxRange = item.MaxRange;
        resource.TargetModeValue = item.TargetMode;
    }

    private static void Populate(EquipmentDefinitionResource resource, BuffItemDraftEquipment item)
    {
        resource.SchemaVersion = 1;
        resource.ContentIdValue = item.ContentId;
        resource.SourceId = item.SourceId;
        resource.DisplayName = item.DisplayName;
        resource.SlotValue = item.Slot;
        resource.RarityValue = item.Rarity;
        resource.Price = item.Price;
        resource.StrengthBonus = item.StrengthBonus;
        resource.AgilityBonus = item.AgilityBonus;
        resource.ConstitutionBonus = item.ConstitutionBonus;
        resource.IntelligenceBonus = item.IntelligenceBonus;
        resource.CharismaBonus = item.CharismaBonus;
        resource.LuckBonus = item.LuckBonus;
    }

    private static GodotResourceEntry[] ComposeGlobalEntries(params GodotResourceCatalog[] catalogs)
    {
        var entries = new Dictionary<string, GodotResourceEntry>(StringComparer.Ordinal);
        foreach (GodotResourceEntry source in catalogs.SelectMany(catalog => catalog.Entries))
        {
            if (entries.TryGetValue(source.ContentIdValue, out GodotResourceEntry? existing))
            {
                if (source.ContentIdValue != "buff.poison" ||
                    existing.ResourceUidValue != source.ResourceUidValue ||
                    existing.DiagnosticPathValue != source.DiagnosticPathValue ||
                    existing.ResourceTypeIdValue != source.ResourceTypeIdValue)
                {
                    throw new InvalidOperationException(
                        $"Cross-batch catalog contains conflicting ContentId '{source.ContentIdValue}'.");
                }
                continue;
            }
            entries.Add(source.ContentIdValue, CopyEntry(source));
        }
        if (entries.Count != 47)
            throw new InvalidOperationException($"Canonical global Catalog must contain 47 entries, got {entries.Count}.");
        return entries.Values.OrderBy(entry => entry.ContentIdValue, StringComparer.Ordinal).ToArray();
    }

    private static GodotResourceEntry CopyEntry(GodotResourceEntry source) => new()
    {
        ContentIdValue = source.ContentIdValue,
        ResourceTypeIdValue = source.ResourceTypeIdValue,
        ResourceUidValue = source.ResourceUidValue,
        DiagnosticPathValue = source.DiagnosticPathValue,
        SchemaVersion = source.SchemaVersion,
        ReferenceContentIds = source.ReferenceContentIds.Order(StringComparer.Ordinal).ToArray()
    };

    private static JsonElement CatalogSemantic(GodotResourceCatalog catalog) => JsonSerializer.SerializeToElement(
        catalog.Entries.Select(entry => new
        {
            entry.ContentIdValue,
            entry.ResourceTypeIdValue,
            entry.ResourceUidValue,
            entry.DiagnosticPathValue,
            entry.SchemaVersion,
            references = entry.ReferenceContentIds.Order(StringComparer.Ordinal).ToArray()
        }).OrderBy(entry => entry.ContentIdValue, StringComparer.Ordinal).ToArray());

    private static GodotResourceCatalog LoadCatalog(string path) => ResourceLoader.Load<GodotResourceCatalog>(
        path,
        string.Empty,
        ResourceLoader.CacheMode.Ignore)
        ?? throw new InvalidOperationException($"Required source Catalog '{path}' is missing.");

    private static T LoadOrCreate<T>(string path) where T : Resource, new() =>
        File.Exists(ProjectSettings.GlobalizePath(path))
            ? ResourceLoader.Load<T>(path, string.Empty, ResourceLoader.CacheMode.Ignore)
                ?? throw new InvalidOperationException(
                    $"Existing resource '{path}' could not be loaded as {typeof(T).Name}.")
            : new T();

    private static GodotResourceEntry Entry(
        string contentId,
        string resourceTypeId,
        string path,
        params string[] references)
    {
        long uid = UidForPath(path);
        if (uid == ResourceUid.InvalidId)
            throw new InvalidOperationException($"Saved Buff/Item resource '{path}' has no UID.");
        return new GodotResourceEntry
        {
            ContentIdValue = contentId,
            ResourceTypeIdValue = resourceTypeId,
            ResourceUidValue = ResourceUid.IdToText(uid),
            DiagnosticPathValue = path,
            SchemaVersion = 1,
            ReferenceContentIds = references.Order(StringComparer.Ordinal).ToArray()
        };
    }

    private static string ResourcePath(string contentId)
    {
        (string prefix, int prefixLength) = contentId switch
        {
            _ when contentId.StartsWith("buff.", StringComparison.Ordinal) => ("Buff", "buff.".Length),
            _ when contentId.StartsWith("item.consumable.", StringComparison.Ordinal) =>
                ("Consumable", "item.consumable.".Length),
            _ when contentId.StartsWith("item.equipment.", StringComparison.Ordinal) =>
                ("Equipment", "item.equipment.".Length),
            _ => throw new InvalidOperationException($"Unexpected Buff/Item ContentId '{contentId}'.")
        };
        string suffix = contentId[prefixLength..];
        string name = string.Concat(suffix.Split(new[] { '.', '-' }).Select(segment =>
            char.ToUpperInvariant(segment[0]) + segment[1..]));
        return $"{DefaultRoot}/{prefix}{name}.tres";
    }

    private static void EnsureDirectory(string resourceDirectory)
    {
        Error error = DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(resourceDirectory));
        if (error != Error.Ok && error != Error.AlreadyExists)
            throw new InvalidOperationException($"Cannot create '{resourceDirectory}': {error}");
    }

    private static void SaveResource(Resource resource, string path)
    {
        long existingUid = UidForPath(path);
        long uid = existingUid != ResourceUid.InvalidId
            ? existingUid
            : ResourceUid.CreateIdForPath(path);
        if (!ResourceUid.HasId(uid))
            ResourceUid.AddId(uid, path);
        else if (!string.Equals(ResourceUid.GetIdPath(uid), path, StringComparison.Ordinal))
            throw new InvalidOperationException($"Resource UID collision for '{path}'.");
        Error error = ResourceSaver.Save(resource, path);
        if (error != Error.Ok)
            throw new InvalidOperationException($"Cannot save '{path}': {error}");
        Error uidError = ResourceSaver.SetUid(path, uid);
        if (uidError != Error.Ok)
            throw new InvalidOperationException($"Cannot persist Resource UID for '{path}': {uidError}");
    }

    private static long UidForPath(string path)
    {
        string uidText = ResourceUid.PathToUid(path);
        return uidText.StartsWith("uid://", StringComparison.Ordinal)
            ? ResourceUid.TextToId(uidText)
            : ResourceUid.InvalidId;
    }

    private sealed class ResourceSaveTransaction
    {
        private readonly string _ledgerPath;
        private readonly BuffItemDraftSource _source;
        private readonly Dictionary<string, byte[]?> _backups = new(StringComparer.OrdinalIgnoreCase);

        public ResourceSaveTransaction(string ledgerPath, BuffItemDraftSource source)
        {
            _ledgerPath = ledgerPath;
            _source = source;
        }

        public void Preflight(IEnumerable<string> resourcePaths)
        {
            string[] paths = resourcePaths.ToArray();
            foreach (string resourcePath in paths)
            {
                string absolutePath = ProjectSettings.GlobalizePath(resourcePath);
                _backups[absolutePath] = File.Exists(absolutePath) ? File.ReadAllBytes(absolutePath) : null;
            }
            _backups[_ledgerPath] = File.Exists(_ledgerPath) ? File.ReadAllBytes(_ledgerPath) : null;
            if (!File.Exists(_ledgerPath))
            {
                string? unmanaged = paths.FirstOrDefault(path => File.Exists(ProjectSettings.GlobalizePath(path)));
                if (unmanaged is not null)
                    throw new InvalidOperationException($"Buff/Item target exists without a migration ledger: {unmanaged}");
                return;
            }

            using JsonDocument previous = JsonDocument.Parse(File.ReadAllText(_ledgerPath));
            JsonElement root = previous.RootElement;
            if (root.GetProperty("batchId").GetString() != BatchId ||
                root.GetProperty("source").GetProperty("exportHash").GetString() != _source.ExportHash)
            {
                throw new InvalidOperationException("Pure Run Buff/Item migration ledger source binding changed.");
            }
            JsonElement[] artifacts = root.GetProperty("artifacts").EnumerateArray().ToArray();
            if (artifacts.Length != paths.Length)
                throw new InvalidOperationException("Pure Run Buff/Item migration ledger artifact count changed.");
            var expected = artifacts.ToDictionary(
                artifact => artifact.GetProperty("resourcePath").GetString()!,
                artifact => artifact.GetProperty("targetHash").GetString()!,
                StringComparer.Ordinal);
            foreach (JsonElement artifact in artifacts)
            {
                string resourcePath = artifact.GetProperty("resourcePath").GetString()!;
                long uid = ResourceUid.TextToId(artifact.GetProperty("resourceUid").GetString()!);
                if (!ResourceUid.HasId(uid))
                    ResourceUid.AddId(uid, resourcePath);
                else if (ResourceUid.GetIdPath(uid) != resourcePath)
                    throw new InvalidOperationException($"Recorded UID now maps to another resource: {resourcePath}");
            }
            foreach (string resourcePath in paths)
            {
                string absolutePath = ProjectSettings.GlobalizePath(resourcePath);
                if (!expected.TryGetValue(resourcePath, out string? expectedHash) ||
                    !File.Exists(absolutePath) || Hash(File.ReadAllBytes(absolutePath)) != expectedHash)
                {
                    throw new InvalidOperationException($"Generated Buff/Item target changed: {resourcePath}");
                }
            }
        }

        public void Commit(
            IEnumerable<string> resourcePaths,
            IReadOnlyDictionary<string, JsonElement> semanticModels)
        {
            var artifacts = resourcePaths.Order(StringComparer.Ordinal).Select(resourcePath =>
            {
                string absolutePath = ProjectSettings.GlobalizePath(resourcePath);
                long uid = UidForPath(resourcePath);
                if (!File.Exists(absolutePath) || uid == ResourceUid.InvalidId)
                    throw new InvalidOperationException($"Generated Buff/Item target is incomplete: {resourcePath}");
                return new
                {
                    resourcePath,
                    resourceUid = ResourceUid.IdToText(uid),
                    targetHash = Hash(File.ReadAllBytes(absolutePath)),
                    semanticHash = Hash(Encoding.UTF8.GetBytes(semanticModels[resourcePath].GetRawText()))
                };
            }).ToArray();
            var ledger = new
            {
                schemaVersion = 1,
                batchId = BatchId,
                source = new
                {
                    sourceTag = _source.SourceTag,
                    sourceCommit = _source.SourceCommit,
                    unityVersion = _source.UnityVersion,
                    exporterVersion = _source.ExporterVersion,
                    exportHash = _source.ExportHash,
                    consumablesJsonSha256 = _source.ConsumablesJson.Sha256,
                    equipmentJsonSha256 = _source.EquipmentJson.Sha256
                },
                artifacts
            };
            string payload = JsonSerializer.Serialize(ledger, new JsonSerializerOptions { WriteIndented = true }) + "\n";
            Directory.CreateDirectory(Path.GetDirectoryName(_ledgerPath)!);
            string temporaryPath = _ledgerPath + ".tmp";
            File.WriteAllText(temporaryPath, payload, new UTF8Encoding(false));
            File.Move(temporaryPath, _ledgerPath, overwrite: true);
        }

        public void Rollback()
        {
            foreach ((string path, byte[]? payload) in _backups.Reverse())
            {
                if (payload is null)
                {
                    if (File.Exists(path))
                        File.Delete(path);
                    continue;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, payload);
            }
        }

        private static string Hash(byte[] payload) =>
            "sha256:" + Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
    }
}
#endif
