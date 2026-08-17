#if TOOLS
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Godot;
using Tactics.Core.Content;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

public static class StartingSkillAssetFactory
{
    public const string BatchId = "pure-run-starting-skills-v1";
    public const string Root = "res://content/skills";
    public const string BatchCatalogPath = Root + "/ContentCatalog.tres";
    public const string FixturePath = Root + "/SkillFixture.tscn";
    public const string GlobalCatalogPath = "res://content/ContentCatalog.tres";
    private const string PoisonPath = "res://content/poison_spear/PoisonSpearSkillLv1.tres";

    public static void Build(string? draftPath = null)
    {
        string projectPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(ProjectSettings.GlobalizePath("res://")));
        string repositoryPath = Directory.GetParent(projectPath)?.FullName ?? throw new InvalidOperationException("Cannot resolve repository root.");
        draftPath ??= Path.Combine(repositoryPath, "Tools", "migration", "out", "pure-run-starting-skills-v1.draft.json");
        StartingSkillMigrationDraft draft = StartingSkillMigrationDraft.Load(draftPath);
        IReadOnlyDictionary<ContentId, Tactics.Core.Skills.SkillDefinition> compiled = draft.Compile();
        if (compiled.Count != 12) throw new InvalidOperationException("Starting-skill typed batch is incomplete.");
        EnsureDirectory(Root);

        string[] definitionPaths = draft.Definitions.Where(item => !item.ExternalDependency).Select(item => ResourcePath(item.ContentId)).Order(StringComparer.Ordinal).ToArray();
        string[] targets = definitionPaths.Append(BatchCatalogPath).Append(FixturePath).Order(StringComparer.Ordinal).ToArray();
        string ledgerPath = Path.Combine(repositoryPath, "Tools", "migration", "manifest", "state", BatchId + ".json");
        Preflight(ledgerPath, targets, draft);

        foreach (StartingSkillDraftDefinition item in draft.Definitions.Where(item => !item.ExternalDependency).OrderBy(item => item.ContentId, StringComparer.Ordinal))
        {
            string path = ResourcePath(item.ContentId);
            SkillDefinitionResource resource = File.Exists(ProjectSettings.GlobalizePath(path)) ? ResourceLoader.Load<SkillDefinitionResource>(path, string.Empty, ResourceLoader.CacheMode.Ignore) ?? new SkillDefinitionResource() : new SkillDefinitionResource();
            Populate(resource, item);
            resource.ToCoreDefinition();
            Save(resource, path);
        }

        var batch = new GodotResourceCatalog
        {
            Entries = draft.Definitions.OrderBy(item => item.ContentId, StringComparer.Ordinal).Select(item => Entry(item.ContentId, item.ExternalDependency ? PoisonPath : ResourcePath(item.ContentId), string.IsNullOrEmpty(item.StatusContentId) ? Array.Empty<string>() : new[] { item.StatusContentId })).ToArray()
        };
        Save(batch, BatchCatalogPath);
        batch.Validate();

        GodotResourceCatalog previousGlobal = ResourceLoader.Load<GodotResourceCatalog>(GlobalCatalogPath, string.Empty, ResourceLoader.CacheMode.Ignore) ?? throw new InvalidOperationException("Canonical Catalog is missing.");
        HashSet<string> ownedStartingSkills = draft.Definitions.Where(item => !item.ExternalDependency)
            .Select(item => item.ContentId).ToHashSet(StringComparer.Ordinal);
        var entries = previousGlobal.Entries.Where(item => !ownedStartingSkills.Contains(item.ContentIdValue))
            .ToDictionary(item => item.ContentIdValue, Copy, StringComparer.Ordinal);
        foreach (GodotResourceEntry entry in batch.Entries.Where(item => item.ContentIdValue != "skill.poison-spear.lv1")) entries.Add(entry.ContentIdValue, Copy(entry));
        var global = new GodotResourceCatalog { Entries = entries.Values.OrderBy(item => item.ContentIdValue, StringComparer.Ordinal).ToArray() };
        if (global.Entries.Length is not (58 or 74 or 101 or 108 or 114 or 115 or 116 or 119 or 123 or 124 or 125 or 131 or 141 or 142 or 143))
            throw new InvalidOperationException($"Canonical Catalog count is invalid: {global.Entries.Length}.");
        Save(global, GlobalCatalogPath);
        global.Validate();

        if (!File.Exists(ProjectSettings.GlobalizePath(FixturePath)))
        {
            var fixture = new GodotStartingSkillFixture { Name = "SkillFixture" };
            var scene = new PackedScene();
            if (scene.Pack(fixture) != Error.Ok)
                throw new InvalidOperationException("Cannot pack SkillFixture.");
            Save(scene, FixturePath);
            fixture.Free();
        }
        else if (ResourceLoader.Load<PackedScene>(FixturePath, string.Empty, ResourceLoader.CacheMode.Ignore) is null)
        {
            throw new InvalidOperationException("Existing SkillFixture cannot be loaded.");
        }
        WriteLedger(ledgerPath, targets, draft);
    }

    private static void Populate(SkillDefinitionResource resource, StartingSkillDraftDefinition item)
    {
        resource.SchemaVersion = 1; resource.ContentIdValue = item.ContentId; resource.SourceId = item.SourceId; resource.DisplayName = item.DisplayName; resource.Description = item.Description;
        resource.RoleValue = item.Role; resource.KindValue = item.Kind; resource.Level = item.Level; resource.ManaCost = item.ManaCost; resource.MinRange = item.MinRange; resource.MaxRange = item.MaxRange;
        resource.ExecutionKindValue = item.ExecutionKind; resource.Damage = item.Damage; resource.DamageKindValue = item.DamageKind; resource.StatusContentIdValue = item.StatusContentId; resource.StatusDuration = item.StatusDuration;
        resource.Hidden = item.Hidden; resource.ExternalDependency = item.ExternalDependency; resource.SourcePath = item.SourcePath; resource.SourceGuid = item.SourceGuid; resource.SourceLocalFileId = item.SourceLocalFileId;
        resource.IsBasicAbility = item.IsBasicAbility; resource.MaxUsesPerTurn = item.MaxUsesPerTurn;
        resource.BranchId = item.BranchId;
        resource.RequiredAttribute = item.RequiredAttribute;
        resource.MinimumAttribute = item.MinimumAttribute;
        resource.GrowthVisible = item.GrowthVisible;
        resource.GraphPath = item.GraphPath; resource.GraphDependencyHash = item.GraphDependencyHash; resource.PresentationPayloadCopied = item.SourceAudit.PresentationPayloadCopied; resource.ThirdPartyPayloadCopied = item.SourceAudit.ThirdPartyPayloadCopied;
    }

    private static string ResourcePath(string contentId) => Root + "/" + string.Concat(contentId["skill.".Length..].Split(new[] { '.', '-' }).Select(segment => char.ToUpperInvariant(segment[0]) + segment[1..])) + ".tres";

    private static GodotResourceEntry Entry(string contentId, string path, string[] references) => new() { ContentIdValue = contentId, ResourceTypeIdValue = "skill", ResourceUidValue = ResourceUid.IdToText(Uid(path)), DiagnosticPathValue = path, SchemaVersion = 1, ReferenceContentIds = references.Order(StringComparer.Ordinal).ToArray() };
    private static GodotResourceEntry Copy(GodotResourceEntry value) => new() { ContentIdValue = value.ContentIdValue, ResourceTypeIdValue = value.ResourceTypeIdValue, ResourceUidValue = value.ResourceUidValue, DiagnosticPathValue = value.DiagnosticPathValue, SchemaVersion = value.SchemaVersion, ReferenceContentIds = value.ReferenceContentIds.ToArray() };
    private static long Uid(string path) { string text = ResourceUid.PathToUid(path); long uid = text.StartsWith("uid://", StringComparison.Ordinal) ? ResourceUid.TextToId(text) : ResourceUid.CreateIdForPath(path); if (!ResourceUid.HasId(uid)) ResourceUid.AddId(uid, path); return uid; }
    private static void Save(Resource resource,string path)=>DeterministicResourceSaver.Save(resource,path,Uid(path));
    private static void EnsureDirectory(string path) { Error error = DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(path)); if (error is not Error.Ok and not Error.AlreadyExists) throw new InvalidOperationException($"Cannot create '{path}': {error}"); }

    private static void Preflight(string ledgerPath, IEnumerable<string> targets, StartingSkillMigrationDraft draft)
    {
        if (!File.Exists(ledgerPath))
        {
            string? unmanaged = targets.FirstOrDefault(path =>
                path != GlobalCatalogPath && File.Exists(ProjectSettings.GlobalizePath(path)));
            if (unmanaged is not null)
                throw new InvalidOperationException($"Starting-skill target exists without ledger: {unmanaged}");
            return;
        }
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(ledgerPath));
        JsonElement source = document.RootElement.GetProperty("source");
        JsonElement recordedExportHash = source.TryGetProperty("exportHash", out JsonElement camelCaseHash)
            ? camelCaseHash
            : source.GetProperty("ExportHash");
        if (recordedExportHash.GetString() != draft.Source.ExportHash)
            throw new InvalidOperationException("Starting-skill ledger source changed.");
        foreach (JsonElement artifact in document.RootElement.GetProperty("artifacts").EnumerateArray()
                     .Where(artifact => artifact.GetProperty("resourcePath").GetString() != GlobalCatalogPath))
        {
            string path = artifact.GetProperty("resourcePath").GetString()!; string absolute = ProjectSettings.GlobalizePath(path);
            bool changed = !File.Exists(absolute) || Hash(File.ReadAllBytes(absolute)) != artifact.GetProperty("targetHash").GetString();
            bool expectedContractUpgrade = changed && path.StartsWith(Root + "/", StringComparison.Ordinal) &&
                draft.Definitions.FirstOrDefault(item => !item.ExternalDependency && ResourcePath(item.ContentId) == path) is { } definition &&
                ResourceLoader.Load<SkillDefinitionResource>(path, string.Empty, ResourceLoader.CacheMode.Ignore) is { } resource &&
                Matches(resource, definition);
            if (changed && !expectedContractUpgrade)
                throw new InvalidOperationException($"Generated starting-skill target changed: {path}");
            long uid = ResourceUid.TextToId(artifact.GetProperty("resourceUid").GetString()!); if (!ResourceUid.HasId(uid)) ResourceUid.AddId(uid, path);
        }
    }

    private static bool Matches(SkillDefinitionResource resource, StartingSkillDraftDefinition item) =>
        resource.ContentIdValue == item.ContentId && resource.SourceId == item.SourceId && resource.RoleValue == item.Role &&
        resource.KindValue == item.Kind && resource.Level == item.Level && resource.ManaCost == item.ManaCost &&
        resource.MinRange == item.MinRange && resource.MaxRange == item.MaxRange && resource.ExecutionKindValue == item.ExecutionKind &&
        resource.Damage == item.Damage && resource.DamageKindValue == item.DamageKind &&
        resource.StatusContentIdValue == item.StatusContentId && resource.StatusDuration == item.StatusDuration &&
        resource.Hidden == item.Hidden && resource.IsBasicAbility == item.IsBasicAbility && resource.MaxUsesPerTurn == item.MaxUsesPerTurn &&
        resource.BranchId == item.BranchId && resource.RequiredAttribute == item.RequiredAttribute &&
        resource.MinimumAttribute == item.MinimumAttribute && resource.GrowthVisible == item.GrowthVisible &&
        resource.DisplayName == item.DisplayName && resource.Description == item.Description;

    private static void WriteLedger(string ledgerPath, IEnumerable<string> targets, StartingSkillMigrationDraft draft)
    {
        var artifacts = targets.Select(path => new { resourcePath = path, resourceUid = ResourceUid.IdToText(Uid(path)), targetHash = Hash(File.ReadAllBytes(ProjectSettings.GlobalizePath(path))) }).OrderBy(item => item.resourcePath, StringComparer.Ordinal).ToArray();
        var payload = new
        {
            schemaVersion = 1,
            batchId = BatchId,
            source = new
            {
                sourceTag = draft.Source.SourceTag,
                sourceCommit = draft.Source.SourceCommit,
                unityVersion = draft.Source.UnityVersion,
                exporterVersion = draft.Source.ExporterVersion,
                exportHash = draft.Source.ExportHash
            },
            artifacts
        };
        Directory.CreateDirectory(Path.GetDirectoryName(ledgerPath)!); File.WriteAllText(ledgerPath, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }) + "\n", new UTF8Encoding(false));
    }
    private static string Hash(byte[] bytes) => "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
#endif
