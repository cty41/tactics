#if TOOLS
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Godot;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

public static class FullRunAssetFactory
{
    public const string BatchId = "pure-run-full-seven-layer-v1";
    private const string Root = "res://content/full_run";
    private const string Global = "res://content/ContentCatalog.tres";

    public static void Build()
    {
        string project = Path.TrimEndingDirectorySeparator(Path.GetFullPath(ProjectSettings.GlobalizePath("res://")));
        string repo = Directory.GetParent(project)!.FullName;
        string ledger=Path.Combine(repo,"Tools","migration","manifest","state",BatchId+".json");
        RegisterLedgerUids(ledger);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo,"Tools","migration","out",BatchId+".draft.json")));
        JsonElement root = document.RootElement;
        if (root.GetProperty("batchId").GetString() != BatchId || root.GetProperty("encounters").GetArrayLength() != 5)
            throw new InvalidOperationException("Full Run draft identity is invalid.");
        Ensure(Root); var entries = new List<GodotResourceEntry>(); var targets = new List<string>();
        string layoutPath = Root + "/BattleLayoutPureRunSpecialOpen.tres";
        var layout = new BattleLayoutResource { ContentIdValue="battle-layout.pure-run.special-open",
            PartySpawnsValue="1,4;1,5;2,4", EnemySpawnsValue="7,4", BlockedCellsValue="" };
        Save(layout,layoutPath); targets.Add(layoutPath); entries.Add(Entry(layout.ContentIdValue,"battle-layout",layoutPath,[]));
        foreach (JsonElement value in root.GetProperty("encounters").EnumerateArray())
        {
            string id=value.GetProperty("contentId").GetString()!; string path=Root+"/"+Safe(id)+".tres";
            string[] names=value.TryGetProperty("units",out JsonElement units)
                ? units.EnumerateArray().Select(item=>item.GetString()!).ToArray()
                : new[]{value.GetProperty("variants")[0][0].GetString()!};
            string[] unitIds=names.Select(UnitId).ToArray(); string[] aiIds=names.Select(AiId).ToArray();
            bool boss=id.EndsWith(".special",StringComparison.Ordinal); bool elite=id.EndsWith(".e1",StringComparison.Ordinal)||id.EndsWith(".e2",StringComparison.Ordinal);
            var encounter=new EncounterDefinitionResource{ContentIdValue=id,LayoutContentId=value.GetProperty("layout").GetString()!,
                MonsterUnitContentIds=unitIds,MonsterAiContentIds=aiIds,HealthMultiplier=value.GetProperty("health").GetSingle(),
                OutputMultiplier=value.GetProperty("output").GetSingle(),MinimumStartingMana=elite?8:boss?12:0,
                EncounterClassValue=boss?"Boss":elite?"Elite":"Normal"};
            string[] references=encounter.LayoutContentId=="battle-layout.pure-run.split-flank"
                ? [..unitIds,..aiIds] : [encounter.LayoutContentId,..unitIds,..aiIds];
            Save(encounter,path);targets.Add(path);entries.Add(Entry(id,"encounter",path,references));
        }
        GodotResourceCatalog old=ResourceLoader.Load<GodotResourceCatalog>(Global,string.Empty,ResourceLoader.CacheMode.Ignore)!;
        var ids=entries.Select(value=>value.ContentIdValue).ToHashSet(StringComparer.Ordinal);
        GodotResourceEntry[] all=old.Entries.Where(value=>!ids.Contains(value.ContentIdValue)).Select(Copy).Concat(entries)
            .OrderBy(value=>value.ContentIdValue,StringComparer.Ordinal).ToArray();
        if(all.Length is not (114 or 115 or 116 or 119))throw new InvalidOperationException($"Expected a supported full-run aggregate, got {all.Length} entries.");
        var catalog=new GodotResourceCatalog{Entries=all};Save(catalog,Global);catalog.Validate();
        File.WriteAllText(ledger,JsonSerializer.Serialize(new{schemaVersion=1,batchId=BatchId,state="Generated",ownership="UnityOwned",
            visualAcceptance="not_applicable_functional_ui_only",manualGameplayAcceptance="pending",catalogCount=all.Length,
            artifacts=targets.Order().Select(path=>new{resourcePath=path,resourceUid=ResourceUid.IdToText(Uid(path)),targetHash=Hash(path)})},
            new JsonSerializerOptions{WriteIndented=true})+"\n",new UTF8Encoding(false));
    }
    private static string UnitId(string name)=>name switch{"elite-charger"=>"unit.pure-run.goat-elite-charger","elite-poison-caster"=>"unit.pure-run.goat-elite-poison-caster",_=>"unit.pure-run.goat-"+name};
    private static string AiId(string name)=>"ai.pure-run."+name;
    private static string Safe(string value)=>value.Replace('.','_').Replace('-','_');
    private static void Ensure(string path){Error e=DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(path));if(e is not Error.Ok and not Error.AlreadyExists)throw new InvalidOperationException(path);}
    private static GodotResourceEntry Entry(string id,string type,string path,string[] refs)=>new(){ContentIdValue=id,ResourceTypeIdValue=type,ResourceUidValue=ResourceUid.IdToText(Uid(path)),DiagnosticPathValue=path,SchemaVersion=1,ReferenceContentIds=refs.Order(StringComparer.Ordinal).ToArray()};
    private static GodotResourceEntry Copy(GodotResourceEntry value)=>new(){ContentIdValue=value.ContentIdValue,ResourceTypeIdValue=value.ResourceTypeIdValue,ResourceUidValue=value.ResourceUidValue,DiagnosticPathValue=value.DiagnosticPathValue,SchemaVersion=value.SchemaVersion,ReferenceContentIds=value.ReferenceContentIds};
    private static long Uid(string path){string text=ResourceUid.PathToUid(path);long id=text.StartsWith("uid://",StringComparison.Ordinal)?ResourceUid.TextToId(text):ResourceUid.CreateIdForPath(path);if(!ResourceUid.HasId(id))ResourceUid.AddId(id,path);return id;}
    private static void Save(Resource value,string path)=>DeterministicResourceSaver.Save(value,path,Uid(path));
    private static string Hash(string path)=>"sha256:"+Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(ProjectSettings.GlobalizePath(path)))).ToLowerInvariant();
    private static void RegisterLedgerUids(string path)
    {
        if(!File.Exists(path))return;
        using JsonDocument document=JsonDocument.Parse(File.ReadAllText(path));
        foreach(JsonElement artifact in document.RootElement.GetProperty("artifacts").EnumerateArray())
        {
            string resourcePath=artifact.GetProperty("resourcePath").GetString()!;
            long uid=ResourceUid.TextToId(artifact.GetProperty("resourceUid").GetString()!);
            if(!ResourceUid.HasId(uid))ResourceUid.AddId(uid,resourcePath);
        }
    }
}
#endif
