#if TOOLS
using Godot;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

[Tool]
public partial class ContentCatalogWorkbench : VBoxContainer
{
    private const string CatalogPath = "res://content/ContentCatalog.tres";
    private const string CatalogScriptPath = "res://src/Tactics.Godot.Adapter/Runtime/GodotResourceCatalog.cs";
    private readonly string _resourceType;
    private readonly string _heading;
    private ItemList? _items;
    private RichTextLabel? _details;
    private int _catalogLoadAttempts;

    public ContentCatalogWorkbench() : this(string.Empty, "Content") { }
    public ContentCatalogWorkbench(string resourceType, string heading)
    {
        _resourceType = resourceType;
        _heading = heading;
    }

    public override void _Ready()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        AddChild(new Label { Text = $"{_heading} — canonical resources are inspected read-only until their typed editor is selected." });
        var split = new HSplitContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        _items = new ItemList { CustomMinimumSize = new Vector2(360, 400), SizeFlagsVertical = SizeFlags.ExpandFill };
        _items.ItemSelected += ShowDetails;
        split.AddChild(_items);
        _details = new RichTextLabel { BbcodeEnabled = true, SelectionEnabled = true, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        split.AddChild(_details);
        AddChild(split);
        CallDeferred(nameof(LoadCatalog));
    }

    public override void _ExitTree()
    {
        _catalogLoadAttempts = 0;
        if (_items is not null) _items.ItemSelected -= ShowDetails;
    }

    public void LoadCatalog()
    {
        EditorResourceLoadResult<GodotResourceCatalog> result = ReloadSafeEditorResourceLoader.Load<GodotResourceCatalog>(
            CatalogPath, CatalogScriptPath, "Entries");
        if (ReloadSafeEditorResourceLoader.RetryDeferred(this, MethodName.LoadCatalog, ref _catalogLoadAttempts, result, _heading))
            return;
        GodotResourceCatalog catalog = result.Resource!;
        catalog.Validate();
        foreach (GodotResourceEntry entry in catalog.Entries.Where(value => string.IsNullOrEmpty(_resourceType) || value.ResourceTypeIdValue == _resourceType))
        {
            int index = _items!.AddItem(entry.ContentIdValue);
            _items.SetItemMetadata(index, entry.DiagnosticPathValue);
        }
        if (_items!.ItemCount > 0) { _items.Select(0); ShowDetails(0); }
    }

    private void ShowDetails(long index)
    {
        string path = _items!.GetItemMetadata((int)index).AsString();
        Resource? resource = ResourceLoader.Load<Resource>(path, string.Empty, ResourceLoader.CacheMode.Ignore);
        _details!.Text = resource is null
            ? $"[color=red]Failed to load[/color]\n{path}"
            : $"[font_size=24]{_items.GetItemText((int)index)}[/font_size]\n\nType: {resource.GetClass()}\nPath: {path}\nUID: {ResourceUid.PathToUid(path)}\n\nValidated through the canonical catalog.";
    }
}
#endif
