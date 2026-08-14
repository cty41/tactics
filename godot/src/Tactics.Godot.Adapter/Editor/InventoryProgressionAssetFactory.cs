#if TOOLS
using System.Text.Json;
using Godot;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

public static class InventoryProgressionAssetFactory
{
    public const string BatchId = "pure-run-inventory-progression-v1";
    private const string Root = "res://content/skills";
    private const string GlobalCatalogPath = "res://content/ContentCatalog.tres";
    private const string BatchCatalogPath = Root + "/InventoryProgressionCatalog.tres";

    public static void Build()
    {
        string project = Path.TrimEndingDirectorySeparator(Path.GetFullPath(ProjectSettings.GlobalizePath("res://")));
        string repo = Directory.GetParent(project)!.FullName;
        string draftPath = Path.Combine(repo, "Tools", "migration", "out", "pure-run-inventory-progression-v1.draft.json");
        Draft draft = JsonSerializer.Deserialize<Draft>(File.ReadAllText(draftPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Inventory/progression draft is missing.");
        if (draft.BatchId != BatchId || draft.Definitions.Length != 36) throw new InvalidOperationException("Inventory/progression draft identity is invalid.");
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Root));
        var generated = new List<GodotResourceEntry>();
        foreach (Definition item in draft.Definitions.Where(value => value.GrowthVisible && !IsExisting(value.ContentId)).OrderBy(value => value.ContentId, StringComparer.Ordinal))
        {
            string path = ResourcePath(item.ContentId);
            var resource = File.Exists(ProjectSettings.GlobalizePath(path))
                ? ResourceLoader.Load<SkillDefinitionResource>(path, string.Empty, ResourceLoader.CacheMode.Ignore) ?? new SkillDefinitionResource()
                : new SkillDefinitionResource();
            Populate(resource, item); resource.ToCoreDefinition(); Save(resource, path);
            generated.Add(Entry(item.ContentId, path, References(item)));
        }
        Dependency fireDemon = draft.InternalSkillDependencies.Single(value => value.ContentId == "skill.summon.fire-demon-attack");
        const string fireDemonPath = Root + "/SummonFireDemonAttack.tres";
        var fireDemonResource = File.Exists(ProjectSettings.GlobalizePath(fireDemonPath))
            ? ResourceLoader.Load<SkillDefinitionResource>(fireDemonPath, string.Empty, ResourceLoader.CacheMode.Ignore) ?? new SkillDefinitionResource()
            : new SkillDefinitionResource();
        Populate(fireDemonResource, fireDemon);
        fireDemonResource.ToCoreDefinition();
        Save(fireDemonResource, fireDemonPath);
        generated.Add(Entry(fireDemon.ContentId, fireDemonPath, new[] { fireDemon.StatusContentId }));
        if (generated.Count != 28) throw new InvalidOperationException($"Expected 28 new and internal skill resources, got {generated.Count}.");
        Save(new GodotResourceCatalog { Entries = generated.ToArray() }, BatchCatalogPath);
        GodotResourceCatalog current = ResourceLoader.Load<GodotResourceCatalog>(GlobalCatalogPath, string.Empty, ResourceLoader.CacheMode.Ignore)!;
        var entries = current.Entries.ToDictionary(value => value.ContentIdValue, Copy, StringComparer.Ordinal);
        foreach (GodotResourceEntry entry in generated) entries[entry.ContentIdValue] = Copy(entry);
        var global = new GodotResourceCatalog { Entries = entries.Values.OrderBy(value => value.ContentIdValue, StringComparer.Ordinal).ToArray() };
        if (global.Entries.Length is not (101 or 102 or 108 or 114 or 115 or 116 or 119 or 123 or 124 or 125))
            throw new InvalidOperationException($"Canonical Catalog count is invalid: {global.Entries.Length}.");
        Save(global, GlobalCatalogPath); global.Validate();
    }

    private static bool IsExisting(string id) => id is "skill.mage.fireball.lv1" or "skill.mage.ice-bolt.lv1" or "skill.mage.lightning.lv1" or
        "skill.necromancer.summon-skeleton.lv1" or "skill.necromancer.amplify-damage.lv1" or "skill.necromancer.bone-spear.lv1" or
        "skill.amazon.thrust.lv1" or "skill.poison-spear.lv1" or "skill.amazon.combat-techniques.lv1";
    private static string ResourcePath(string id) => Root + "/" + string.Concat(id["skill.".Length..].Split('.', '-').Select(value => char.ToUpperInvariant(value[0]) + value[1..])) + ".tres";
    private static void Populate(SkillDefinitionResource r, Definition d)
    {
        r.SchemaVersion=1;r.ContentIdValue=d.ContentId;r.SourceId=d.SourcePath.Length>0?d.SourcePath:d.ContentId;r.DisplayName=d.DisplayName;r.Description=d.Description;
        r.RoleValue=d.Role;r.KindValue=d.Kind;r.Level=d.Level;r.ManaCost=d.ManaCost;r.MinRange=d.Kind=="Passive"||d.ExecutionKind is "IceArmor" or "BoneShield"?0:1;r.MaxRange=d.TargetRange;
        r.ExecutionKindValue=d.ExecutionKind.Length>0?d.ExecutionKind:"CombatTechniques";r.Damage=d.Damage;r.DamageKindValue=d.DamageKind.Length>0?d.DamageKind:"None";
        r.StatusContentIdValue=d.StatusContentId;r.StatusDuration=d.StatusDuration;r.IsBasicAbility=d.IsBasicAbility;r.MaxUsesPerTurn=d.MaxUsesPerTurn;
        r.CanCrit=d.CanCrit;
        r.BranchId=d.BranchId;r.PrerequisiteContentIdValue=PrerequisiteId(d);r.PrerequisiteBranchId=d.PrerequisiteBranchId;r.GrowthVisible=d.GrowthVisible;
        r.RequiredAttribute=d.RequiredAttribute;r.MinimumAttribute=d.MinimumAttribute;
        r.AreaRadius=d.AreaRadius;r.OrderedTargetCount=d.OrderedTargetCount;r.SummonCount=d.SummonCount;r.SummonLimit=d.SummonLimit;r.SummonCategory=d.SummonCategory;
        r.RequiresCorpse=d.RequiresCorpse;r.IgnoreLineOfSight=d.IgnoreLineOfSight;r.ShieldMultiplier=d.ShieldMultiplier;r.ShieldAbsorbsAllDamage=d.ShieldAbsorbsAllDamage;r.CleanseHarmful=d.CleanseHarmful;r.SecondaryDamage=d.SecondaryDamage;
        r.SourcePath=d.SourcePath;r.SourceGuid=d.SourceGuid;r.SourceLocalFileId=d.SourceLocalFileId;r.GraphPath=d.GraphPath;r.GraphDependencyHash=d.GraphDependencyHash;
    }
    private static void Populate(SkillDefinitionResource r, Dependency d)
    {
        r.SchemaVersion=1;r.ContentIdValue=d.ContentId;r.SourceId=d.SourcePath;r.DisplayName=d.DisplayName;r.Description=d.Description;
        r.RoleValue=d.Role;r.KindValue=d.Kind;r.Level=d.Level;r.ManaCost=d.ManaCost;r.MinRange=d.MinRange;r.MaxRange=d.MaxRange;
        r.ExecutionKindValue=d.ExecutionKind;r.Damage=d.Damage;r.DamageKindValue=d.DamageKind;r.StatusContentIdValue=d.StatusContentId;
        r.StatusDuration=d.StatusDuration;r.IsBasicAbility=d.IsBasicAbility;r.MaxUsesPerTurn=d.MaxUsesPerTurn;r.CanCrit=d.CanCrit;
        r.BranchId="summon.fire-demon-attack";r.GrowthVisible=d.GrowthVisible;r.SourcePath=d.SourcePath;r.SourceGuid=d.SourceGuid;
        r.SourceLocalFileId=d.SourceLocalFileId;r.GraphPath=d.GraphPath;r.GraphDependencyHash=d.GraphDependencyHash;
    }
    private static string PrerequisiteId(Definition d) => d.Level <= 1 ? string.Empty : d.BranchId == "amazon.poison-spear" ? "skill.poison-spear.lv1" : $"skill.{d.BranchId}.lv{d.Level-1}";
    private static string[] References(Definition d) => new[] { d.StatusContentId, PrerequisiteId(d) }.Where(value=>!string.IsNullOrEmpty(value)).ToArray();
    private static GodotResourceEntry Entry(string id,string path,string[] refs)=>new(){ContentIdValue=id,ResourceTypeIdValue="skill",ResourceUidValue=ResourceUid.IdToText(Uid(path)),DiagnosticPathValue=path,SchemaVersion=1,ReferenceContentIds=refs};
    private static GodotResourceEntry Copy(GodotResourceEntry v)=>new(){ContentIdValue=v.ContentIdValue,ResourceTypeIdValue=v.ResourceTypeIdValue,ResourceUidValue=v.ResourceUidValue,DiagnosticPathValue=v.DiagnosticPathValue,SchemaVersion=v.SchemaVersion,ReferenceContentIds=v.ReferenceContentIds.ToArray()};
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
        string text=ResourceUid.PathToUid(path);long uid=text.StartsWith("uid://")?ResourceUid.TextToId(text):ResourceUid.CreateIdForPath(path);if(!ResourceUid.HasId(uid))ResourceUid.AddId(uid,path);return uid;
    }
    private static void Save(Resource value,string path)=>DeterministicResourceSaver.Save(value,path,Uid(path));
    private sealed class Draft{public string BatchId{get;init;}="";public Definition[] Definitions{get;init;}=Array.Empty<Definition>();public Dependency[] InternalSkillDependencies{get;init;}=Array.Empty<Dependency>();}
    private sealed class Definition
    {
        public string ContentId{get;init;}="";public string BranchId{get;init;}="";public string Role{get;init;}="";public string Kind{get;init;}="";public int Level{get;init;}public int ManaCost{get;init;}public int TargetRange{get;init;}
        public string DisplayName{get;init;}="";public string Description{get;init;}="";public string ExecutionKind{get;init;}="";public int Damage{get;init;}public string DamageKind{get;init;}="";public string StatusContentId{get;init;}="";public int StatusDuration{get;init;}
        public bool IsBasicAbility{get;init;}public int MaxUsesPerTurn{get;init;}public bool CanCrit{get;init;}=true;public bool GrowthVisible{get;init;}public int AreaRadius{get;init;}public int OrderedTargetCount{get;init;}public int SummonCount{get;init;}public int SummonLimit{get;init;}public string SummonCategory{get;init;}="";public bool RequiresCorpse{get;init;}public bool IgnoreLineOfSight{get;init;}public int ShieldMultiplier{get;init;}public bool ShieldAbsorbsAllDamage{get;init;}public bool CleanseHarmful{get;init;}public int SecondaryDamage{get;init;}
        public string RequiredAttribute{get;init;}="";public int MinimumAttribute{get;init;}public string PrerequisiteBranchId{get;init;}="";
        public string SourcePath{get;init;}="";public string SourceGuid{get;init;}="";public long SourceLocalFileId{get;init;}public string GraphPath{get;init;}="";public string GraphDependencyHash{get;init;}="";
    }
    private sealed class Dependency
    {
        public string ContentId{get;init;}="";public string DisplayName{get;init;}="";public string Description{get;init;}="";
        public string ExecutionKind{get;init;}="";public string Role{get;init;}="";public string Kind{get;init;}="";public int Level{get;init;}
        public int ManaCost{get;init;}public int MinRange{get;init;}public int MaxRange{get;init;}public int Damage{get;init;}public string DamageKind{get;init;}="";
        public string StatusContentId{get;init;}="";public int StatusDuration{get;init;}public bool IsBasicAbility{get;init;}public int MaxUsesPerTurn{get;init;}
        public bool CanCrit{get;init;}public bool GrowthVisible{get;init;}public string SourcePath{get;init;}="";public string SourceGuid{get;init;}="";
        public long SourceLocalFileId{get;init;}public string GraphPath{get;init;}="";public string GraphDependencyHash{get;init;}="";
    }
}
#endif
