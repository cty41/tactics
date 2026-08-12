#if TOOLS
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Godot;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

public static class RunPersistenceAssetFactory
{
    public const string BatchId="pure-run-persistence-v1";
    private const string Root="res://content/runs";
    private const string DefinitionPath=Root+"/PureRunThreeEncounterV1.tres";
    private const string CatalogPath=Root+"/ContentCatalog.tres";
    private const string FixturePath=Root+"/RunPersistenceFixture.tscn";
    private const string GlobalCatalogPath="res://content/ContentCatalog.tres";

    public static void Build()
    {
        string project=Path.TrimEndingDirectorySeparator(Path.GetFullPath(ProjectSettings.GlobalizePath("res://"))); string repo=Directory.GetParent(project)!.FullName;
        string draftPath=Path.Combine(repo,"Tools","migration","out",BatchId+".draft.json"); using JsonDocument document=JsonDocument.Parse(File.ReadAllText(draftPath));JsonElement definition=document.RootElement.GetProperty("definition");
        string ledgerPath=Path.Combine(repo,"Tools","migration","manifest","state",BatchId+".json");RegisterLedgerUids(ledgerPath);EnsureDirectory(Root);
        string id=definition.GetProperty("contentId").GetString()!;string[] encounters=definition.GetProperty("encounters").EnumerateArray().Select(v=>v.GetString()!).ToArray();JsonElement.ArrayEnumerator party=definition.GetProperty("party").EnumerateArray();JsonElement[] people=party.ToArray();
        var resource=new PureRunDefinitionResource{ContentIdValue=id,EncounterContentIds=encounters,CharacterIds=people.Select(v=>v.GetProperty("characterId").GetString()!).ToArray(),UnitContentIds=people.Select(v=>v.GetProperty("unitContentId").GetString()!).ToArray(),StartingSkillContentIds=people.Select(v=>v.GetProperty("startingSkillContentId").GetString()!).ToArray(),LayerFourMapContentId="run-map.pure-run.layer4-v1"};resource.ToCoreDefinition();Save(resource,DefinitionPath);
        string[] refs=encounters.Concat(resource.UnitContentIds).Concat(resource.StartingSkillContentIds).Order(StringComparer.Ordinal).ToArray();var entry=Entry(id,"run",DefinitionPath,refs);var batch=new GodotResourceCatalog{Entries=new[]{entry}};Save(batch,CatalogPath);batch.Validate();
        GodotResourceCatalog previous=ResourceLoader.Load<GodotResourceCatalog>(GlobalCatalogPath,string.Empty,ResourceLoader.CacheMode.Ignore)??throw new InvalidOperationException("Canonical Catalog is missing.");var entries=previous.Entries.Where(v=>v.ContentIdValue!=id).Select(Copy).ToList();entries.Add(Copy(entry));var global=new GodotResourceCatalog{Entries=entries.OrderBy(v=>v.ContentIdValue,StringComparer.Ordinal).ToArray()};if(global.Entries.Length is not (74 or 101 or 108 or 114 or 115 or 116 or 119))throw new InvalidOperationException($"Canonical Catalog count is invalid: {global.Entries.Length}.");Save(global,GlobalCatalogPath);global.Validate();
        if(!File.Exists(ProjectSettings.GlobalizePath(FixturePath))){var fixture=new GodotRunPersistenceFixture{Name="RunPersistenceFixture"};var scene=new PackedScene();if(scene.Pack(fixture)!=Error.Ok)throw new InvalidOperationException("Cannot pack Run fixture.");Save(scene,FixturePath);fixture.Free();}
        WriteLedger(ledgerPath,new[]{DefinitionPath,CatalogPath,FixturePath},document.RootElement.GetProperty("source"));
    }
    private static GodotResourceEntry Entry(string id,string type,string path,string[] refs)=>new(){ContentIdValue=id,ResourceTypeIdValue=type,ResourceUidValue=ResourceUid.IdToText(Uid(path)),DiagnosticPathValue=path,SchemaVersion=1,ReferenceContentIds=refs};
    private static GodotResourceEntry Copy(GodotResourceEntry v)=>new(){ContentIdValue=v.ContentIdValue,ResourceTypeIdValue=v.ResourceTypeIdValue,ResourceUidValue=v.ResourceUidValue,DiagnosticPathValue=v.DiagnosticPathValue,SchemaVersion=v.SchemaVersion,ReferenceContentIds=v.ReferenceContentIds.ToArray()};
    private static long Uid(string path){string text=ResourceUid.PathToUid(path);long uid=text.StartsWith("uid://",StringComparison.Ordinal)?ResourceUid.TextToId(text):ResourceUid.CreateIdForPath(path);if(!ResourceUid.HasId(uid))ResourceUid.AddId(uid,path);return uid;}
    private static void Save(Resource value,string path){long uid=Uid(path);if(ResourceSaver.Save(value,path)!=Error.Ok||ResourceSaver.SetUid(path,uid)!=Error.Ok)throw new InvalidOperationException($"Cannot save '{path}'.");}
    private static void EnsureDirectory(string path){Error error=DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(path));if(error is not Error.Ok and not Error.AlreadyExists)throw new InvalidOperationException($"Cannot create '{path}'.");}
    private static void WriteLedger(string path,IEnumerable<string> targets,JsonElement source){var artifacts=targets.Order(StringComparer.Ordinal).Select(resourcePath=>new{resourcePath,resourceUid=ResourceUid.IdToText(Uid(resourcePath)),targetHash="sha256:"+Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(ProjectSettings.GlobalizePath(resourcePath)))).ToLowerInvariant()}).ToArray();Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,JsonSerializer.Serialize(new{schemaVersion=1,batchId=BatchId,source=JsonSerializer.Deserialize<object>(source.GetRawText()),artifacts},new JsonSerializerOptions{WriteIndented=true})+"\n",new UTF8Encoding(false));}
    private static void RegisterLedgerUids(string path){if(!File.Exists(path))return;using JsonDocument ledger=JsonDocument.Parse(File.ReadAllText(path));foreach(JsonElement artifact in ledger.RootElement.GetProperty("artifacts").EnumerateArray()){string resourcePath=artifact.GetProperty("resourcePath").GetString()!;long uid=ResourceUid.TextToId(artifact.GetProperty("resourceUid").GetString()!);if(!ResourceUid.HasId(uid))ResourceUid.AddId(uid,resourcePath);}}
}
#endif
