#if TOOLS
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Godot;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

public static class IsometricPresentationAssetFactory
{
    public const string BatchId = "pure-run-isometric-presentation-v1";
    private const string Root = "res://content/presentation";
    private const string Global = "res://content/ContentCatalog.tres";
    private const string BoardPath = Root + "/BattleBoardPureRunIsometricV1.tres";
    private const string UnitPresentationPath = Root + "/StandardUnitPresentationV1.tres";
    private static readonly (string Id,string Path,string Branch,string Kind,Color Primary,Color Secondary,int Ghosts)[] SkillProfiles=
    [
        ("presentation.skill.mage.fireball",Root+"/FireballPresentation.tres","mage.fireball","fireball",new Color(1f,.28f,.04f),new Color(1f,.72f,.1f),0),
        ("presentation.skill.necromancer.bone-spear",Root+"/BoneSpearPresentation.tres","necromancer.bone-spear","bone-spear",new Color(.88f,.9f,.72f),new Color(.55f,.35f,.75f),2),
        ("presentation.skill.amazon.thrust",Root+"/ThrustPresentation.tres","amazon.thrust","thrust",new Color(1f,.88f,.3f),new Color(1f,.55f,.12f),0)
    ];

    public static void BuildBoard()
    {
        Ensure(Root);
        string project = Path.TrimEndingDirectorySeparator(Path.GetFullPath(ProjectSettings.GlobalizePath("res://")));
        string repo = Directory.GetParent(project)!.FullName;
        string ledger = Path.Combine(repo, "Tools", "migration", "manifest", "state", BatchId + ".json");
        RegisterLedgerUids(ledger);
        var board = new IsometricBattleBoardResource();
        Save(board, BoardPath);
        GodotResourceCatalog old = ResourceLoader.Load<GodotResourceCatalog>(Global, string.Empty, ResourceLoader.CacheMode.Ignore)
            ?? throw new InvalidOperationException("Canonical Catalog is missing.");
        GodotResourceEntry entry = Entry(board.ContentIdValue, "battle-board", BoardPath);
        GodotResourceEntry[] all = old.Entries.Where(value => value.ContentIdValue != entry.ContentIdValue).Select(Copy).Append(entry)
            .OrderBy(value => value.ContentIdValue, StringComparer.Ordinal).ToArray();
        if (all.Length is not (115 or 116 or 119)) throw new InvalidOperationException($"Unsupported presentation Catalog count: {all.Length}.");
        var catalog = new GodotResourceCatalog { Entries = all };
        Save(catalog, Global);
        catalog.Validate();
        File.WriteAllText(ledger, JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            batchId = BatchId,
            state = "Generated",
            ownership = "UnityOwned",
            visualAcceptance = "manual_isometric_and_presentation_qa_pending",
            catalogCount = all.Length,
            sourceAudit = new[]
            {
                new { sourcePath = "Assets/Tactics/Scripts/Common/Cells/TilemapCellGeometry.cs", gitBlobSha1 = "fffcbff3278cb8973926cb70d7b3c4decb253bbd" },
                new { sourcePath = "Assets/Tactics/Scripts/Common/Battle/BattleBoardCameraFitter.cs", gitBlobSha1 = "910847d0088086a8c9b5ff1addf4df2649484935" },
                new { sourcePath = "Assets/Tactics/Scripts/Common/Cells/ProceduralTileHighlightRenderer.cs", gitBlobSha1 = "55edd6bea0a2baea0f95cce8a204bd0f978e2708" }
            },
            artifacts = new[] { new { resourcePath = BoardPath, resourceUid = ResourceUid.IdToText(Uid(BoardPath)), targetHash = Hash(BoardPath) } }
        }, new JsonSerializerOptions { WriteIndented = true }) + "\n", new UTF8Encoding(false));
    }

    public static void BuildUnitPresentation()
    {
        BuildBoard();
        var profile = new StandardUnitPresentationResource();
        Save(profile, UnitPresentationPath);
        GodotResourceCatalog old = ResourceLoader.Load<GodotResourceCatalog>(Global, string.Empty, ResourceLoader.CacheMode.Ignore)!;
        GodotResourceEntry entry = Entry(profile.ContentIdValue, "presentation", UnitPresentationPath);
        GodotResourceEntry[] all = old.Entries.Where(value => value.ContentIdValue != entry.ContentIdValue).Select(Copy).Append(entry)
            .OrderBy(value => value.ContentIdValue, StringComparer.Ordinal).ToArray();
        if (all.Length is not (116 or 119)) throw new InvalidOperationException($"Unsupported unit presentation Catalog count: {all.Length}.");
        var catalog = new GodotResourceCatalog { Entries = all };
        Save(catalog, Global);
        string project = Path.TrimEndingDirectorySeparator(Path.GetFullPath(ProjectSettings.GlobalizePath("res://")));
        string repo = Directory.GetParent(project)!.FullName;
        string ledger = Path.Combine(repo, "Tools", "migration", "manifest", "state", BatchId + ".json");
        File.WriteAllText(ledger, JsonSerializer.Serialize(new
        {
            schemaVersion = 1, batchId = BatchId, state = "Generated", ownership = "UnityOwned",
            visualAcceptance = "manual_isometric_and_presentation_qa_pending", catalogCount = 116,
            sourceAudit = new[]
            {
                new { sourcePath = "Assets/Tactics/Arts/PureRun/Tween/StandardUnitTweenProfile.asset", gitBlobSha1 = "5a53ddb60794715ee2da4e24241347a2a2b2db20" },
                new { sourcePath = "Assets/Tactics/Scripts/Common/Units/Tween/StandardUnitTweenProfile.cs", gitBlobSha1 = "c6d7b9479c1888e50d0c520e757ab465907ccfab" },
                new { sourcePath = "Assets/Tactics/Scripts/Common/Units/Tween/UnitTweenVisual.cs", gitBlobSha1 = "bb9aa5c7391063e79f1454b4c0489cfae6f5b3ab" }
            },
            artifacts = new[] { BoardPath, UnitPresentationPath }.Select(path => new { resourcePath = path, resourceUid = ResourceUid.IdToText(Uid(path)), targetHash = Hash(path) })
        }, new JsonSerializerOptions { WriteIndented = true }) + "\n", new UTF8Encoding(false));
    }

    public static void BuildSkillPresentations()
    {
        BuildUnitPresentation();
        var entries=new List<GodotResourceEntry>();
        foreach(var value in SkillProfiles)
        {
            var profile=new SkillPresentationResource{ContentIdValue=value.Id,SkillBranch=value.Branch,ProgrammaticKind=value.Kind,PrimaryColor=value.Primary,SecondaryColor=value.Secondary,MaximumGhosts=value.Ghosts,LevelOneHasAreaEffect=false};
            Save(profile,value.Path);entries.Add(Entry(value.Id,"presentation",value.Path));
        }
        GodotResourceCatalog old=ResourceLoader.Load<GodotResourceCatalog>(Global,string.Empty,ResourceLoader.CacheMode.Ignore)!;
        var ids=entries.Select(value=>value.ContentIdValue).ToHashSet(StringComparer.Ordinal);
        GodotResourceEntry[] all=old.Entries.Where(value=>!ids.Contains(value.ContentIdValue)).Select(Copy).Concat(entries).OrderBy(value=>value.ContentIdValue,StringComparer.Ordinal).ToArray();
        if(all.Length!=119)throw new InvalidOperationException($"Expected 119 catalog entries, got {all.Length}.");
        Save(new GodotResourceCatalog{Entries=all},Global);
        string project=Path.TrimEndingDirectorySeparator(Path.GetFullPath(ProjectSettings.GlobalizePath("res://"))),repo=Directory.GetParent(project)!.FullName;
        string ledger=Path.Combine(repo,"Tools","migration","manifest","state",BatchId+".json");
        string[] artifacts=[BoardPath,UnitPresentationPath,..SkillProfiles.Select(value=>value.Path)];
        File.WriteAllText(ledger,JsonSerializer.Serialize(new{schemaVersion=1,batchId=BatchId,state="Generated",ownership="UnityOwned",visualAcceptance="manual_isometric_and_presentation_qa_pending",catalogCount=119,
            sourceAudit=new[]{
                new{sourcePath="Assets/Tactics/Arts/PureRun/Presentation/Fireball_Presentation.asset",gitBlobSha1="001b290bb3dac7ba61c7cad45f5e5fccd4bb7e38"},new{sourcePath="Assets/Tactics/Arts/PureRun/Presentation/Fireball_Lv2_Presentation.asset",gitBlobSha1="f8f0f37fcd10ec44265aafc571c34c6e2d1fc16c"},
                new{sourcePath="Assets/Tactics/Arts/PureRun/Presentation/BoneSpear_Presentation.asset",gitBlobSha1="d755ab34dec8d2535e6a4ed5ee862a4fa9f8360c"},new{sourcePath="Assets/Tactics/Arts/PureRun/Presentation/BoneSpear_Lv2_Presentation.asset",gitBlobSha1="95b09910a8248929b3019ebefc91095925c9ce8f"},
                new{sourcePath="Assets/Tactics/Arts/PureRun/Tween/SkillVfx/Recipes/FireballSkillVfxRecipe.asset",gitBlobSha1="b097ec1e78fb3d2b8cffd281aa10d122fa5198e5"},new{sourcePath="Assets/Tactics/Arts/PureRun/Tween/SkillVfx/Recipes/BoneSpearSkillVfxRecipe.asset",gitBlobSha1="3297e35fadfd273113e9bf263dce7aef7e32b829"},new{sourcePath="Assets/Tactics/Arts/PureRun/Tween/SkillVfx/Recipes/ThrustSkillVfxRecipe.asset",gitBlobSha1="e31ece07465740b34a688a2f6a40a68ce8e35d77"}},
            payloadBoundary="programmatic-only-no-piloto-prefab-texture-material-shader-audio",artifacts=artifacts.Select(path=>new{resourcePath=path,resourceUid=ResourceUid.IdToText(Uid(path)),targetHash=Hash(path)})},new JsonSerializerOptions{WriteIndented=true})+"\n",new UTF8Encoding(false));
    }

    private static GodotResourceEntry Entry(string id, string type, string path) => new()
    {
        ContentIdValue = id, ResourceTypeIdValue = type, ResourceUidValue = ResourceUid.IdToText(Uid(path)),
        DiagnosticPathValue = path, SchemaVersion = 1, ReferenceContentIds = Array.Empty<string>()
    };
    private static GodotResourceEntry Copy(GodotResourceEntry value) => new()
    {
        ContentIdValue = value.ContentIdValue, ResourceTypeIdValue = value.ResourceTypeIdValue,
        ResourceUidValue = value.ResourceUidValue, DiagnosticPathValue = value.DiagnosticPathValue,
        SchemaVersion = value.SchemaVersion, ReferenceContentIds = value.ReferenceContentIds
    };
    private static void Ensure(string path) { Error result = DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(path)); if (result is not Error.Ok and not Error.AlreadyExists) throw new InvalidOperationException(path); }
    private static long Uid(string path) { string text = ResourceUid.PathToUid(path); long id = text.StartsWith("uid://", StringComparison.Ordinal) ? ResourceUid.TextToId(text) : ResourceUid.CreateIdForPath(path); if (!ResourceUid.HasId(id)) ResourceUid.AddId(id, path); return id; }
    private static void Save(Resource value, string path) { long uid = Uid(path); if (ResourceSaver.Save(value, path) != Error.Ok || ResourceSaver.SetUid(path, uid) != Error.Ok) throw new InvalidOperationException(path); }
    private static string Hash(string path) => "sha256:" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(ProjectSettings.GlobalizePath(path)))).ToLowerInvariant();
    private static void RegisterLedgerUids(string path)
    {
        if (!File.Exists(path)) return;
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        foreach (JsonElement artifact in document.RootElement.GetProperty("artifacts").EnumerateArray())
        {
            string resourcePath = artifact.GetProperty("resourcePath").GetString()!;
            long uid = ResourceUid.TextToId(artifact.GetProperty("resourceUid").GetString()!);
            if (!ResourceUid.HasId(uid)) ResourceUid.AddId(uid, resourcePath);
        }
    }
}
#endif
