#if TOOLS
using System.Text.Json;
using Godot;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

public static class OwnershipClosureAssetFactory
{
    public const string BatchId = "pure-run-ownership-closure-v1";
    private const string Root = "res://content/skills";
    private const string BatchCatalogPath = Root + "/OwnershipClosureCatalog.tres";
    private const string GlobalCatalogPath = "res://content/ContentCatalog.tres";

    public static void Build()
    {
        string project = Path.TrimEndingDirectorySeparator(Path.GetFullPath(ProjectSettings.GlobalizePath("res://")));
        string repo = Directory.GetParent(project)!.FullName;
        string draftPath = Path.Combine(repo, "Tools", "migration", "out", "pure-run-ownership-closure-v1.draft.json");
        Draft draft = JsonSerializer.Deserialize<Draft>(File.ReadAllText(draftPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Ownership-closure draft is missing.");
        Definition[] definitions = draft.PlayerSkillDefinitions.Concat(draft.InternalSkillDefinitions)
            .OrderBy(value => value.ContentId, StringComparer.Ordinal).ToArray();
        if (draft.BatchId != BatchId || definitions.Length != 10)
            throw new InvalidOperationException("Ownership-closure Lv3 draft identity is invalid.");

        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Root));
        var generated = new List<GodotResourceEntry>();
        foreach (Definition definition in definitions)
        {
            string path = ResourcePath(definition.ContentId);
            var resource = File.Exists(ProjectSettings.GlobalizePath(path))
                ? ResourceLoader.Load<SkillDefinitionResource>(path, string.Empty, ResourceLoader.CacheMode.Ignore) ?? new SkillDefinitionResource()
                : new SkillDefinitionResource();
            Populate(resource, definition);
            resource.ToCoreDefinition();
            Save(resource, path);
            generated.Add(new GodotResourceEntry
            {
                ContentIdValue = definition.ContentId,
                ResourceTypeIdValue = "skill",
                ResourceUidValue = ResourceUid.IdToText(Uid(path)),
                DiagnosticPathValue = path,
                SchemaVersion = 1,
                ReferenceContentIds = References(definition)
            });
        }
        Save(new GodotResourceCatalog { Entries = generated.ToArray() }, BatchCatalogPath);
        GodotResourceCatalog current = ResourceLoader.Load<GodotResourceCatalog>(GlobalCatalogPath, string.Empty,
            ResourceLoader.CacheMode.Ignore) ?? throw new InvalidOperationException("Canonical Catalog is missing.");
        var entries = current.Entries.ToDictionary(value => value.ContentIdValue, Copy, StringComparer.Ordinal);
        foreach (GodotResourceEntry entry in generated) entries[entry.ContentIdValue] = Copy(entry);
        var global = new GodotResourceCatalog
        {
            Entries = entries.Values.OrderBy(value => value.ContentIdValue, StringComparer.Ordinal).ToArray()
        };
        if (global.Entries.Length is not (141 or 142 or 143))
            throw new InvalidOperationException($"Canonical Catalog count is invalid: {global.Entries.Length}.");
        Save(global, GlobalCatalogPath);
        global.Validate();
    }

    private static void Populate(SkillDefinitionResource resource, Definition value)
    {
        resource.SchemaVersion = 1;
        resource.ContentIdValue = value.ContentId;
        resource.SourceId = value.SourcePath;
        resource.DisplayName = value.DisplayName;
        resource.Description = value.Description;
        resource.RoleValue = value.Role;
        resource.KindValue = value.Kind;
        resource.Level = value.Level;
        resource.ManaCost = value.ManaCost;
        resource.MinRange = value.MinRange;
        resource.MaxRange = value.MaxRange;
        resource.ExecutionKindValue = value.ExecutionKind;
        resource.Damage = value.Damage;
        resource.DamageKindValue = value.DamageKind;
        resource.StatusContentIdValue = value.StatusContentId;
        resource.StatusDuration = value.StatusDuration;
        resource.IsBasicAbility = value.IsBasicAbility;
        resource.MaxUsesPerTurn = value.MaxUsesPerTurn;
        resource.CanCrit = value.CanCrit;
        resource.BranchId = value.BranchId;
        resource.PrerequisiteContentIdValue = value.PrerequisiteContentId;
        resource.GrowthVisible = value.GrowthVisible;
        resource.RequiredAttribute = value.RequiredAttribute;
        resource.MinimumAttribute = value.MinimumAttribute;
        resource.AreaRadius = value.AreaRadius;
        resource.AreaShape = value.AreaShape;
        resource.StatusChancePercent = value.StatusChancePercent;
        resource.DetonateStatusContentIdValue = value.DetonateStatusContentId;
        resource.BounceRange = value.BounceRange;
        resource.BounceCount = value.BounceCount;
        resource.PierceAll = value.PierceAll;
        resource.AllowsEmptyTarget = value.AllowsEmptyTarget;
        resource.MovementDamagePerCell = value.MovementDamagePerCell;
        resource.SummonLimit = value.SummonLimit;
        resource.SummonCategory = value.SummonCategory;
        resource.RequiresCorpse = value.RequiresCorpse;
        resource.IgnoreLineOfSight = value.IgnoreLineOfSight;
        resource.SummonAttackContentIdValue = value.SummonAttackContentId;
        resource.SourcePath = value.SourcePath;
        resource.SourceGuid = value.SourceGuid;
        resource.SourceLocalFileId = value.SourceLocalFileId;
        resource.GraphPath = value.GraphPath;
        resource.GraphDependencyHash = value.GraphDependencyHash;
        resource.PresentationPayloadCopied = false;
        resource.ThirdPartyPayloadCopied = false;
    }

    private static string[] References(Definition value) => new[]
        {
            value.StatusContentId, value.PrerequisiteContentId, value.DetonateStatusContentId,
            value.SummonAttackContentId
        }.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.Ordinal).ToArray();

    private static string ResourcePath(string id) => Root + "/" +
        string.Concat(id["skill.".Length..].Split('.', '-').Select(value => char.ToUpperInvariant(value[0]) + value[1..])) + ".tres";
    private static GodotResourceEntry Copy(GodotResourceEntry value) => new()
    {
        ContentIdValue = value.ContentIdValue, ResourceTypeIdValue = value.ResourceTypeIdValue,
        ResourceUidValue = value.ResourceUidValue, DiagnosticPathValue = value.DiagnosticPathValue,
        SchemaVersion = value.SchemaVersion, ReferenceContentIds = value.ReferenceContentIds.ToArray()
    };
    private static long Uid(string path)
    {
        string absolute = ProjectSettings.GlobalizePath(path);
        if (File.Exists(absolute))
        {
            string header = File.ReadLines(absolute).FirstOrDefault() ?? string.Empty;
            int marker = header.IndexOf("uid=\"", StringComparison.Ordinal);
            if (marker >= 0)
            {
                int start = marker + 5;
                int end = header.IndexOf('"', start);
                if (end > start)
                {
                    long persisted = ResourceUid.TextToId(header[start..end]);
                    if (persisted != ResourceUid.InvalidId)
                    {
                        if (!ResourceUid.HasId(persisted)) ResourceUid.AddId(persisted, path);
                        return persisted;
                    }
                }
            }
        }
        string text = ResourceUid.PathToUid(path);
        long uid = text.StartsWith("uid://", StringComparison.Ordinal) ? ResourceUid.TextToId(text) : ResourceUid.CreateIdForPath(path);
        if (!ResourceUid.HasId(uid)) ResourceUid.AddId(uid, path);
        return uid;
    }
    private static void Save(Resource value, string path) => DeterministicResourceSaver.Save(value, path, Uid(path));

    private sealed class Draft
    {
        public string BatchId { get; init; } = string.Empty;
        public Definition[] PlayerSkillDefinitions { get; init; } = Array.Empty<Definition>();
        public Definition[] InternalSkillDefinitions { get; init; } = Array.Empty<Definition>();
    }
    private sealed class Definition
    {
        public string ContentId { get; init; } = string.Empty;
        public string BranchId { get; init; } = string.Empty;
        public string Role { get; init; } = string.Empty;
        public string Kind { get; init; } = string.Empty;
        public int Level { get; init; }
        public int ManaCost { get; init; }
        public int MinRange { get; init; }
        public int MaxRange { get; init; }
        public string ExecutionKind { get; init; } = string.Empty;
        public int Damage { get; init; }
        public string DamageKind { get; init; } = string.Empty;
        public string StatusContentId { get; init; } = string.Empty;
        public int StatusDuration { get; init; }
        public bool IsBasicAbility { get; init; }
        public int MaxUsesPerTurn { get; init; }
        public bool CanCrit { get; init; } = true;
        public bool GrowthVisible { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string RequiredAttribute { get; init; } = string.Empty;
        public int MinimumAttribute { get; init; }
        public string PrerequisiteContentId { get; init; } = string.Empty;
        public int AreaRadius { get; init; }
        public string AreaShape { get; init; } = string.Empty;
        public int StatusChancePercent { get; init; } = 100;
        public string DetonateStatusContentId { get; init; } = string.Empty;
        public int BounceRange { get; init; }
        public int BounceCount { get; init; }
        public bool PierceAll { get; init; }
        public bool AllowsEmptyTarget { get; init; }
        public int MovementDamagePerCell { get; init; }
        public int SummonLimit { get; init; }
        public string SummonCategory { get; init; } = string.Empty;
        public bool RequiresCorpse { get; init; }
        public bool IgnoreLineOfSight { get; init; }
        public string SummonAttackContentId { get; init; } = string.Empty;
        public string SourcePath { get; init; } = string.Empty;
        public string SourceGuid { get; init; } = string.Empty;
        public long SourceLocalFileId { get; init; }
        public string GraphPath { get; init; } = string.Empty;
        public string GraphDependencyHash { get; init; } = string.Empty;
    }
}
#endif
