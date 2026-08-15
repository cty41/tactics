#if TOOLS
using Godot;
using System.Text.Json;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

[Tool]
public partial class EventTreasureWorkbench : VBoxContainer
{
    private readonly bool _treasureMode;
    private ItemList? _items;
    private Tree? _details;
    private readonly Dictionary<string, string> _paths = new(StringComparer.Ordinal);

    public EventTreasureWorkbench() : this(false) { }
    public EventTreasureWorkbench(bool treasureMode) => _treasureMode = treasureMode;

    public override void _Ready()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        AddChild(new Label { Text = _treasureMode
            ? "Treasure reward tables and authoritative Map links"
            : "Mystery options, attribute checks, outcomes and authoritative Map links" });
        var split = new HSplitContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        _items = new ItemList { CustomMinimumSize = new Vector2(310, 420), SizeFlagsVertical = SizeFlags.ExpandFill };
        _items.ItemSelected += Select;
        split.AddChild(_items);
        _details = new Tree { Columns = 2, ColumnTitlesVisible = true, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        _details.SetColumnTitle(0, "Field"); _details.SetColumnTitle(1, "Value");
        split.AddChild(_details);
        AddChild(split);
        LoadCatalog();
    }

    private void LoadCatalog()
    {
        GodotResourceCatalog catalog = ResourceLoader.Load<GodotResourceCatalog>("res://content/ContentCatalog.tres", string.Empty, ResourceLoader.CacheMode.Ignore)
            ?? throw new InvalidOperationException("Canonical catalog is missing.");
        string type = _treasureMode ? "treasure" : "event";
        foreach (GodotResourceEntry entry in catalog.Entries.Where(value => value.ResourceTypeIdValue == type))
        {
            _paths[entry.ContentIdValue] = entry.DiagnosticPathValue;
            _items!.AddItem(entry.ContentIdValue);
        }
        if (_items!.ItemCount > 0) { _items.Select(0); Select(0); }
    }

    private void Select(long index)
    {
        string id = _items!.GetItemText((int)index);
        _details!.Clear();
        TreeItem root = _details.CreateItem();
        Add(root, "ContentId", id);
        Add(root, "Map nodes", FindMapNodes(id));
        if (_treasureMode) ShowTreasure(root, _paths[id]);
        else ShowEvent(root, _paths[id]);
    }

    private void ShowTreasure(TreeItem root, string path)
    {
        PureRunTreasureResource value = ResourceLoader.Load<PureRunTreasureResource>(path, string.Empty, ResourceLoader.CacheMode.Ignore)
            ?? throw new InvalidOperationException($"Treasure cannot be loaded: {path}");
        _ = value.ToCoreDefinition();
        Add(root, "Gold", $"{value.GoldMinimum}–{value.GoldMaximum}");
        AddTable(root, "Equipment", value.EquipmentContentIds, value.EquipmentWeights);
        AddTable(root, "Consumables", value.ConsumableContentIds, value.ConsumableWeights);
        AddTable(root, "Buffs", value.BuffContentIds, value.BuffWeights);
    }

    private void ShowEvent(TreeItem root, string path)
    {
        PureRunLayerFourResource value = ResourceLoader.Load<PureRunLayerFourResource>(path, string.Empty, ResourceLoader.CacheMode.Ignore)
            ?? throw new InvalidOperationException($"Event cannot be loaded: {path}");
        using JsonDocument document = JsonDocument.Parse(value.PayloadJson);
        JsonElement body = document.RootElement;
        Add(root, "Title", body.GetProperty("title").GetString() ?? string.Empty);
        Add(root, "Description", body.GetProperty("description").GetString() ?? string.Empty);
        TreeItem options = _details!.CreateItem(root); options.SetText(0, "Options");
        foreach (JsonElement option in body.GetProperty("options").EnumerateArray())
        {
            TreeItem row = _details.CreateItem(options);
            row.SetText(0, option.GetProperty("text").GetString() ?? option.GetProperty("id").GetString()!);
            row.SetText(1, $"{option.GetProperty("attribute").GetString()} @ {option.GetProperty("baseSuccessRate").GetInt32()}%");
            AddOutcome(row, "Success", option.GetProperty("success"));
            if (option.TryGetProperty("failure", out JsonElement failure) && failure.ValueKind != JsonValueKind.Null)
                AddOutcome(row, "Failure", failure);
        }
    }

    private void AddOutcome(TreeItem parent, string label, JsonElement outcome)
    {
        string type = outcome.GetProperty("type").GetString()!;
        string amount = outcome.TryGetProperty("amount", out JsonElement value) ? value.GetInt32().ToString() : "-";
        TreeItem row = _details!.CreateItem(parent); row.SetText(0, label); row.SetText(1, $"{type} {amount}");
    }

    private void AddTable(TreeItem root, string title, string[] ids, int[] weights)
    {
        TreeItem table = _details!.CreateItem(root); table.SetText(0, title);
        for (int i = 0; i < ids.Length; i++) Add(table, ids[i], weights[i].ToString());
    }

    private string FindMapNodes(string contentId)
    {
        PureRunMapResource map = ResourceLoader.Load<PureRunMapResource>(PureRunMapWorkbench.MapPath, string.Empty, ResourceLoader.CacheMode.Ignore)
            ?? throw new InvalidOperationException("Authoritative map cannot be loaded.");
        string[] nodes = map.NodeIds.Where((_, index) => map.NodeContentIds[index] == contentId).ToArray();
        return nodes.Length == 0 ? "not linked" : string.Join(", ", nodes);
    }

    private TreeItem Add(TreeItem parent, string field, string value)
    {
        TreeItem row = _details!.CreateItem(parent); row.SetText(0, field); row.SetText(1, value); return row;
    }
}
#endif
