#if TOOLS
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Godot;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

public static class AiEncounterAssetFactory
{
    public const string BatchId="pure-run-ai-encounter-v1";
    public const string Root="res://content/ai_encounters";
    public const string BatchCatalogPath=Root+"/ContentCatalog.tres";
    public const string FixturePath=Root+"/AiEncounterFixture.tscn";
    public const string GlobalCatalogPath="res://content/ContentCatalog.tres";

    public static void Build(string? draftPath=null)
    {
        string project=Path.TrimEndingDirectorySeparator(Path.GetFullPath(ProjectSettings.GlobalizePath("res://"))); string repo=Directory.GetParent(project)?.FullName??throw new InvalidOperationException("Cannot resolve repository root."); draftPath??=Path.Combine(repo,"Tools","migration","out",BatchId+".draft.json"); using JsonDocument document=JsonDocument.Parse(File.ReadAllText(draftPath)); JsonElement root=document.RootElement;
        if(root.GetProperty("batchId").GetString()!=BatchId||root.GetProperty("definitions").GetArrayLength()!=10||root.GetProperty("layouts").GetArrayLength()!=2||root.GetProperty("encounters").GetArrayLength()!=3)throw new InvalidOperationException("AI/Encounter draft identity is invalid.");
        string ledgerPath=Path.Combine(repo,"Tools","migration","manifest","state",BatchId+".json"); RegisterLedgerUids(ledgerPath);
        EnsureDirectory(Root); var entries=new List<GodotResourceEntry>(); var targets=new List<string>();
        foreach(JsonElement item in root.GetProperty("definitions").EnumerateArray().OrderBy(item=>item.GetProperty("contentId").GetString(),StringComparer.Ordinal))
        {
            string id=item.GetProperty("contentId").GetString()!; string path=PathFor(id); targets.Add(path);
            if(item.GetProperty("kind").GetString()=="skill")
            {
                var value=new SkillDefinitionResource{SchemaVersion=1,ContentIdValue=id,SourceId=item.GetProperty("sourceId").GetString()!,DisplayName=item.GetProperty("displayName").GetString()!,Description=item.GetProperty("description").GetString()!,RoleValue="Any",KindValue="Active",Level=1,ManaCost=item.GetProperty("manaCost").GetInt32(),MinRange=item.GetProperty("minRange").GetInt32(),MaxRange=item.GetProperty("maxRange").GetInt32(),ExecutionKindValue=item.GetProperty("executionKind").GetString()!,Damage=item.GetProperty("damage").GetInt32(),DamageKindValue="Magical",SourcePath=item.GetProperty("sourcePath").GetString()!,SourceGuid=item.GetProperty("sourceGuid").GetString()!,SourceLocalFileId=item.GetProperty("sourceLocalFileId").GetInt64(),GraphPath=item.GetProperty("graphPath").GetString()!,GraphDependencyHash=item.GetProperty("graphDependencyHash").GetString()!}; value.ToCoreDefinition(); Save(value,path); entries.Add(Entry(id,"skill",path,Array.Empty<string>()));
            }
            else
            {
                string archetype=item.GetProperty("archetype").GetString()!; string[] skills=Skills(archetype); string[] pattern=item.GetProperty("pattern").EnumerateArray().Select(value=>PatternId(value.GetString()!)).ToArray(); var weights=Weights(archetype);
                var value=new AiDefinitionResource{ContentIdValue=id,ArchetypeValue=archetype,SkillContentIds=skills,PatternSkillContentIds=pattern,DistanceWeight=weights.distance,DamageWeight=weights.damage,TargetCountWeight=weights.targets,HarmfulStatusWeight=weights.status,BrainPath=item.GetProperty("brainPath").GetString()!,BrainGuid=item.GetProperty("brainGuid").GetString()!,BrainLocalFileId=item.GetProperty("brainLocalFileId").GetInt64(),ProfilePath=item.GetProperty("profilePath").GetString()!,ProfileGuid=item.GetProperty("profileGuid").GetString()!,DecisionGraphPath=item.GetProperty("decisionGraphPath").GetString()!,DecisionGraphHash=item.GetProperty("decisionGraphHash").GetString()!,DecisionGraphJson=item.GetProperty("decisionGraph").GetRawText(),MaximumEngageCandidatesPerTarget=item.GetProperty("maximumEngageCandidatesPerTarget").GetInt32(),PreferredMinimumRange=item.GetProperty("preferredMinimumRange").GetInt32(),PreferredMaximumRange=item.GetProperty("preferredMaximumRange").GetInt32(),PreferredRangeRepositionBonus=item.GetProperty("preferredRangeRepositionBonus").GetSingle()}; Save(value,path); entries.Add(Entry(id,"ai",path,skills));
            }
        }
        foreach(JsonElement item in root.GetProperty("layouts").EnumerateArray()) {string id=item.GetProperty("contentId").GetString()!;string path=PathFor(id);targets.Add(path);var value=new BattleLayoutResource{ContentIdValue=id,PartySpawnsValue="1,4;1,5;2,4",EnemySpawnsValue=Points(item.GetProperty("enemySpawns")),BlockedCellsValue=Points(item.GetProperty("blocked"))};Save(value,path);entries.Add(Entry(id,"battle-layout",path,Array.Empty<string>()));}
        foreach(JsonElement item in root.GetProperty("encounters").EnumerateArray()){string id=item.GetProperty("contentId").GetString()!;string path=PathFor(id);targets.Add(path);string layout=item.GetProperty("layout").GetString()!;string[] units=item.GetProperty("monsters").EnumerateArray().Select(value=>value.GetString()!).ToArray();string[] ai=units.Select(UnitAi).ToArray();var value=new EncounterDefinitionResource{ContentIdValue=id,LayoutContentId=layout,MonsterUnitContentIds=units,MonsterAiContentIds=ai};Save(value,path);entries.Add(Entry(id,"encounter",path,new[]{layout}.Concat(units).Concat(ai).ToArray()));}
        var batch=new GodotResourceCatalog{Entries=entries.OrderBy(item=>item.ContentIdValue,StringComparer.Ordinal).ToArray()};Save(batch,BatchCatalogPath);batch.Validate();targets.Add(BatchCatalogPath);
        GodotResourceCatalog previous=ResourceLoader.Load<GodotResourceCatalog>(GlobalCatalogPath,string.Empty,ResourceLoader.CacheMode.Ignore)??throw new InvalidOperationException("Canonical Catalog is missing.");var globalEntries=previous.Entries.Where(item=>!item.ContentIdValue.StartsWith("ai.pure-run.",StringComparison.Ordinal)&&!item.ContentIdValue.StartsWith("battle-layout.pure-run.",StringComparison.Ordinal)&&!item.ContentIdValue.StartsWith("encounter.pure-run.",StringComparison.Ordinal)&&!item.ContentIdValue.StartsWith("skill.enemy.",StringComparison.Ordinal)).Select(Copy).ToDictionary(item=>item.ContentIdValue,StringComparer.Ordinal);foreach(GodotResourceEntry entry in batch.Entries)globalEntries.Add(entry.ContentIdValue,Copy(entry));var global=new GodotResourceCatalog{Entries=globalEntries.Values.OrderBy(item=>item.ContentIdValue,StringComparer.Ordinal).ToArray()};if(global.Entries.Length is not (73 or 74 or 101 or 108 or 114 or 115 or 116 or 119 or 131))throw new InvalidOperationException($"Canonical Catalog count is invalid: {global.Entries.Length}.");Save(global,GlobalCatalogPath);global.Validate();
        if(!File.Exists(ProjectSettings.GlobalizePath(FixturePath))){var fixture=new GodotAiEncounterFixture{Name="AiEncounterFixture"};var scene=new PackedScene();if(scene.Pack(fixture)!=Error.Ok)throw new InvalidOperationException("Cannot pack AI Fixture.");Save(scene,FixturePath);fixture.Free();}targets.Add(FixturePath);WriteLedger(ledgerPath,targets,root.GetProperty("source"));
    }
    private static string Points(JsonElement values)=>string.Join(';',values.EnumerateArray().Select(value=>$"{value[0].GetInt32()},{value[1].GetInt32()}"));
    private static string[] Skills(string a)=>a switch{"aoe"=>new[]{"skill.basic.melee","skill.enemy.area-blast.lv1"},"charger" or "elite-charger"=>new[]{"skill.basic.melee","skill.enemy.charge-strike.lv1"},"ranged"=>new[]{"skill.enemy.ranged-attack.lv1","skill.enemy.heavy-shot.lv1"},"support"=>new[]{"skill.basic.melee","skill.necromancer.amplify-damage.lv1"},_=>new[]{"skill.basic.melee","skill.enemy.area-blast.lv1"}};
    private static (float distance,float damage,float targets,float status) Weights(string a)=>a switch{"ranged"=>(2,1,0,0),"aoe"=>(1,1,3,0),"support"=>(1,0,0,4),"charger" or "elite-charger"=>(3,2,0,0),_=>(1,2,2,0)};
    private static string PatternId(string name)=>name switch{"Charge Strike Lv1"=>"skill.enemy.charge-strike.lv1","Area Blast Lv1"=>"skill.enemy.area-blast.lv1","Melee Attack"=>"skill.basic.melee",_=>throw new InvalidOperationException($"Unknown pattern skill '{name}'.")};
    private static string UnitAi(string unit)=>"ai.pure-run."+unit["unit.pure-run.goat-".Length..];
    private static string PathFor(string id)=>Root+"/"+string.Concat(id.Split(new[]{'.','-'}).Select(segment=>char.ToUpperInvariant(segment[0])+segment[1..]))+".tres";
    private static GodotResourceEntry Entry(string id,string type,string path,string[] refs)=>new(){ContentIdValue=id,ResourceTypeIdValue=type,ResourceUidValue=ResourceUid.IdToText(Uid(path)),DiagnosticPathValue=path,SchemaVersion=1,ReferenceContentIds=refs.Order(StringComparer.Ordinal).ToArray()};
    private static GodotResourceEntry Copy(GodotResourceEntry v)=>new(){ContentIdValue=v.ContentIdValue,ResourceTypeIdValue=v.ResourceTypeIdValue,ResourceUidValue=v.ResourceUidValue,DiagnosticPathValue=v.DiagnosticPathValue,SchemaVersion=v.SchemaVersion,ReferenceContentIds=v.ReferenceContentIds.ToArray()};
    private static long Uid(string path){string text=ResourceUid.PathToUid(path);long uid=text.StartsWith("uid://",StringComparison.Ordinal)?ResourceUid.TextToId(text):ResourceUid.CreateIdForPath(path);if(!ResourceUid.HasId(uid))ResourceUid.AddId(uid,path);return uid;}
    private static void Save(Resource value,string path)=>DeterministicResourceSaver.Save(value,path,Uid(path));
    private static void EnsureDirectory(string path){Error error=DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(path));if(error is not Error.Ok and not Error.AlreadyExists)throw new InvalidOperationException($"Cannot create '{path}'.");}
    private static void WriteLedger(string path,IEnumerable<string> targets,JsonElement source){var artifacts=targets.Distinct().Order(StringComparer.Ordinal).Select(resourcePath=>new{resourcePath,resourceUid=ResourceUid.IdToText(Uid(resourcePath)),targetHash=Hash(File.ReadAllBytes(ProjectSettings.GlobalizePath(resourcePath)))}).ToArray();var payload=new{schemaVersion=1,batchId=BatchId,source=JsonSerializer.Deserialize<object>(source.GetRawText()),artifacts};Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,JsonSerializer.Serialize(payload,new JsonSerializerOptions{WriteIndented=true})+"\n",new UTF8Encoding(false));}
    private static void RegisterLedgerUids(string path){if(!File.Exists(path))return;using JsonDocument ledger=JsonDocument.Parse(File.ReadAllText(path));foreach(JsonElement artifact in ledger.RootElement.GetProperty("artifacts").EnumerateArray()){string resourcePath=artifact.GetProperty("resourcePath").GetString()!;long uid=ResourceUid.TextToId(artifact.GetProperty("resourceUid").GetString()!);if(!ResourceUid.HasId(uid))ResourceUid.AddId(uid,resourcePath);}}
    private static string Hash(byte[] bytes)=>"sha256:"+Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
#endif
