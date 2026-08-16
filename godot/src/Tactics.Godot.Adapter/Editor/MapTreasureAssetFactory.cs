#if TOOLS
using Godot;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

public static class MapTreasureAssetFactory
{
    public const string BatchId = "pure-run-map-treasure-v1";
    private const string Root = "res://content/map";
    private const string MapPath = Root + "/PureRunDefaultMap.tres";
    private const string TreasurePath = Root + "/PureRunStandardTreasure.tres";
    private const string CatalogPath = "res://content/ContentCatalog.tres";

    public static void Build()
    {
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Root));
        PureRunMapResource map = BuildMap();
        map.ToCoreDefinition();
        Save(map, MapPath);
        var treasure = new PureRunTreasureResource
        {
            ContentIdValue = "treasure.pure-run.standard-v1",
            GoldMinimum = 2,
            GoldMaximum = 5,
            EquipmentContentIds = ["item.equipment.lucky-ring-01"],
            EquipmentWeights = [1],
            ConsumableContentIds = Array.Empty<string>(),
            ConsumableWeights = Array.Empty<int>(),
            BuffContentIds = ["buff.event-damage-reduction"],
            BuffWeights = [1]
        };
        treasure.ToCoreDefinition();
        Save(treasure, TreasurePath);

        GodotResourceCatalog current = ResourceLoader.Load<GodotResourceCatalog>(CatalogPath, string.Empty,
            ResourceLoader.CacheMode.Ignore) ?? throw new InvalidOperationException("Canonical Catalog is missing.");
        var entries = current.Entries.ToDictionary(value => value.ContentIdValue, Copy, StringComparer.Ordinal);
        entries[map.ContentIdValue] = Entry(map.ContentIdValue, "run-map", MapPath,
            map.NodeContentIds.Concat([treasure.ContentIdValue]).Distinct().ToArray());
        entries[treasure.ContentIdValue] = Entry(treasure.ContentIdValue, "treasure", TreasurePath,
            treasure.EquipmentContentIds.Concat(treasure.ConsumableContentIds).Concat(treasure.BuffContentIds).ToArray());
        var catalog = new GodotResourceCatalog
        {
            Entries = entries.Values.OrderBy(value => value.ContentIdValue, StringComparer.Ordinal).ToArray()
        };
        if (catalog.Entries.Length != 142)
            throw new InvalidOperationException($"Canonical Catalog count is invalid: {catalog.Entries.Length}.");
        Save(catalog, CatalogPath);
        catalog.Validate();
    }

    private static PureRunMapResource BuildMap()
    {
        (string Id, int Layer, string Kind, string Content, string Title, float Lane) Node(
            string id, int layer, string kind, string content, string title, float lane = 0) =>
            (id, layer, kind, content, title, lane);
        var nodes = new List<(string Id, int Layer, string Kind, string Content, string Title, float Lane)>
        {
            Node("start", 0, "Battle", "run.pure-run.three-encounter-v1", "Start"),
            Node("layer_01_battle", 1, "Battle", "encounter.pure-run.n1", "N1"),
            Node("layer_02_battle", 2, "Battle", "encounter.pure-run.n2", "N2"),
            Node("layer_03_battle", 3, "Battle", "encounter.pure-run.n3", "N3"),
            Node("layer_05_battle", 5, "Elite", "encounter.pure-run.e1", "Elite"),
            Node("layer_07_battle", 7, "Boss", "encounter.pure-run.special", "Special Boss")
        };
        foreach (int layer in new[] { 4, 6 })
        {
            string prefix = $"layer_{layer:00}";
            nodes.Add(Node(prefix + "_battle", layer, "Battle", layer == 4 ? "encounter.pure-run.n4" : "encounter.pure-run.e1", "Battle", -2));
            nodes.Add(Node(prefix + "_rest", layer, "Rest", "rest.pure-run.standard-v1", "Rest", -1));
            nodes.Add(Node(prefix + "_store", layer, "Store", "store.pure-run.standard-v1", "Store", 0));
            nodes.Add(Node(prefix + "_event", layer, "Mystery", "event.pure-run.cursed-chest", "Mystery", 1));
            nodes.Add(Node(prefix + "_treasure", layer, "Treasure", "treasure.pure-run.standard-v1", "Treasure", 2));
        }
        var edges = new List<(string From, string To)>
        {
            ("start", "layer_01_battle"), ("layer_01_battle", "layer_02_battle"),
            ("layer_02_battle", "layer_03_battle")
        };
        foreach (string suffix in new[] { "battle", "rest", "store", "event", "treasure" })
        {
            edges.Add(("layer_03_battle", $"layer_04_{suffix}"));
            edges.Add(($"layer_04_{suffix}", "layer_05_battle"));
            edges.Add(("layer_05_battle", $"layer_06_{suffix}"));
            edges.Add(($"layer_06_{suffix}", "layer_07_battle"));
        }
        var ordered = nodes.OrderBy(value => value.Layer).ThenBy(value => value.Lane).ToArray();
        return new PureRunMapResource
        {
            ContentIdValue = "run-map.pure-run.layer4-v1", LayoutVersion = 3,
            NodeIds = ordered.Select(value => value.Id).ToArray(),
            NodeLayers = ordered.Select(value => value.Layer).ToArray(),
            NodeKinds = ordered.Select(value => value.Kind).ToArray(),
            NodeContentIds = ordered.Select(value => value.Content).ToArray(),
            NodeTitles = ordered.Select(value => value.Title).ToArray(),
            NodeLanes = ordered.Select(value => value.Lane).ToArray(),
            ConnectionFromNodeIds = edges.Select(value => value.From).ToArray(),
            ConnectionToNodeIds = edges.Select(value => value.To).ToArray()
        };
    }

    private static GodotResourceEntry Entry(string id, string type, string path, string[] references) => new()
    {
        ContentIdValue = id, ResourceTypeIdValue = type, ResourceUidValue = ResourceUid.IdToText(Uid(path)),
        DiagnosticPathValue = path, SchemaVersion = 1,
        ReferenceContentIds = references.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().ToArray()
    };
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
}
#endif
