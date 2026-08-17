#if TOOLS
using System.Text;
using System.Text.Json;
using Godot;
using Tactics.Application.Authoring;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

public static class SplitFlankLayoutClosureFactory
{
    public const string ContentId = "battle-layout.pure-run.split-flank";
    public const string ResourcePath = "res://content/full_run/BattleLayoutPureRunSplitFlank.tres";
    private const string ReceiptPath = "Tools/godot/authoring/manifest/split-flank-layout-closure-v1.json";

    public static void Build()
    {
        var document = new BattleLayoutAuthoringDocument(ContentId,
            [new GridCellAuthoring(1, 4), new GridCellAuthoring(1, 5), new GridCellAuthoring(2, 4)],
            [new GridCellAuthoring(6, 2), new GridCellAuthoring(6, 7), new GridCellAuthoring(7, 2), new GridCellAuthoring(7, 7)],
            [new GridCellAuthoring(4, 3), new GridCellAuthoring(5, 4), new GridCellAuthoring(4, 6), new GridCellAuthoring(5, 5)]);
        string revision = AuthoringRevision.Compute(document);
        Resource? existing = File.Exists(ProjectSettings.GlobalizePath(ResourcePath))
            ? ResourceLoader.Load(ResourcePath, string.Empty, ResourceLoader.CacheMode.Ignore)
            : null;
        if (existing is not null)
        {
            if (existing is not BattleLayoutResource existingLayout ||
                AuthoringRevision.Compute(EncounterAuthoringEditorService.Read(existingLayout)) != revision)
                throw new InvalidOperationException("Refusing to overwrite an unexpected split-flank layout change.");
        }

        GodotResourceCatalog catalog = ResourceLoader.Load<GodotResourceCatalog>(TacticsAuthoringEditorService.CatalogPath,
            string.Empty, ResourceLoader.CacheMode.Ignore) ?? throw new InvalidOperationException("Catalog cannot be loaded.");
        long uid = ResolveUid(ResourcePath, catalog);
        var layout = new BattleLayoutResource { ContentIdValue = ContentId };
        EncounterAuthoringEditorService.Write(layout, document);
        var stagedCatalog = new GodotResourceCatalog();
        GodotResourceEntry[] retained = catalog.Entries.Where(value => value.ContentIdValue != ContentId).Select(CopyEntry).ToArray();
        foreach (GodotResourceEntry encounter in retained.Where(value => value.ContentIdValue is "encounter.pure-run.n6" or "encounter.pure-run.e2"))
            encounter.ReferenceContentIds = encounter.ReferenceContentIds.Append(ContentId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        stagedCatalog.Entries = retained.Append(new GodotResourceEntry
        {
            ContentIdValue = ContentId,
            ResourceTypeIdValue = "battle-layout",
            ResourceUidValue = ResourceUid.IdToText(uid),
            DiagnosticPathValue = ResourcePath,
            SchemaVersion = 1,
            ReferenceContentIds = Array.Empty<string>()
        }).OrderBy(value => value.ContentIdValue, StringComparer.Ordinal).ToArray();

        WorkbenchResourceBatchSaveService.SaveWithRollback(new[]
        {
            new WorkbenchResourceSaveRequest(layout, ResourcePath, value =>
            {
                if (value is not BattleLayoutResource reloaded || AuthoringRevision.Compute(EncounterAuthoringEditorService.Read(reloaded)) != revision)
                    throw new InvalidOperationException("Split-flank layout reload validation failed.");
            }, uid),
            new WorkbenchResourceSaveRequest(stagedCatalog, TacticsAuthoringEditorService.CatalogPath,
                value => ((GodotResourceCatalog)value).Validate())
        });
        WriteReceipt(revision, uid);
    }

    private static long ResolveUid(string path, GodotResourceCatalog catalog)
    {
        string? catalogUid = catalog.Entries.FirstOrDefault(value => value.ContentIdValue == ContentId)?.ResourceUidValue;
        string text = catalogUid ?? ResourceUid.PathToUid(path);
        long uid = text.StartsWith("uid://", StringComparison.Ordinal) ? ResourceUid.TextToId(text) : ResourceUid.CreateIdForPath(path);
        if (!ResourceUid.HasId(uid)) ResourceUid.AddId(uid, path);
        return uid;
    }

    private static GodotResourceEntry CopyEntry(GodotResourceEntry value) => new()
    {
        ContentIdValue = value.ContentIdValue,
        ResourceTypeIdValue = value.ResourceTypeIdValue,
        ResourceUidValue = value.ResourceUidValue,
        DiagnosticPathValue = value.DiagnosticPathValue,
        SchemaVersion = value.SchemaVersion,
        ReferenceContentIds = value.ReferenceContentIds.ToArray()
    };

    private static void WriteReceipt(string revision, long uid)
    {
        string project = Path.TrimEndingDirectorySeparator(Path.GetFullPath(ProjectSettings.GlobalizePath("res://")));
        string repo = Directory.GetParent(project)!.FullName;
        string path = Path.Combine(repo, ReceiptPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string json = JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            batchId = "split-flank-layout-closure-v1",
            ownership = "GodotOwned",
            manualQa = "pending",
            source = "retired runtime split-flank fallback",
            contentId = ContentId,
            resourcePath = ResourcePath,
            resourceUid = ResourceUid.IdToText(uid),
            targetHash = revision
        }, new JsonSerializerOptions { WriteIndented = true }) + "\n";
        File.WriteAllText(path, json, new UTF8Encoding(false));
    }
}
#endif
