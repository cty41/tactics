#if TOOLS
using Godot;
using System.Text.Json;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

[Tool]
public partial class AiDefinitionWorkbench : VBoxContainer
{
    private EditorUndoRedoManager? _undoRedo;
    private readonly Dictionary<string, string> _paths = new(StringComparer.Ordinal);
    private OptionButton? _picker;
    private SpinBox? _distance;
    private SpinBox? _damage;
    private SpinBox? _targets;
    private SpinBox? _statusWeight;
    private GraphEdit? _graph;
    private Label? _status;
    private AiDefinitionResource? _resource;
    private string _path = string.Empty;

    public void Configure(EditorUndoRedoManager undoRedo) => _undoRedo = undoRedo;

    public override void _Ready()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        var toolbar = new HBoxContainer();
        _picker = new OptionButton { CustomMinimumSize = new Vector2(250, 0) };
        _picker.ItemSelected += LoadSelected;
        toolbar.AddChild(_picker);
        _distance = AddWeight(toolbar, "Distance");
        _damage = AddWeight(toolbar, "Damage");
        _targets = AddWeight(toolbar, "Targets");
        _statusWeight = AddWeight(toolbar, "Status");
        Button apply = new() { Text = "Apply Safe Profile (Undoable)" };
        apply.Pressed += ApplyWeights;
        toolbar.AddChild(apply);
        Button save = new() { Text = "Save" };
        save.Pressed += Save;
        toolbar.AddChild(save);
        AddChild(toolbar);
        _status = new Label { Text = "Intent/Rule/Score topology is read-only; profile weights are safe authoring fields." };
        AddChild(_status);
        _graph = new GraphEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        AddChild(_graph);
        LoadCatalog();
    }

    public static void ValidateResource(AiDefinitionResource value)
    {
        if (value.DistanceWeight < 0 || value.DamageWeight < 0 || value.TargetCountWeight < 0 || value.HarmfulStatusWeight < 0)
            throw new InvalidOperationException("AI profile weights cannot be negative.");
        _ = value.ToCoreDefinition();
    }

    private static SpinBox AddWeight(HBoxContainer parent, string name)
    {
        parent.AddChild(new Label { Text = name });
        var field = new SpinBox { MinValue = 0, MaxValue = 20, Step = 0.1, CustomMinimumSize = new Vector2(75, 0) };
        parent.AddChild(field);
        return field;
    }

    private void LoadCatalog()
    {
        GodotResourceCatalog catalog = ResourceLoader.Load<GodotResourceCatalog>("res://content/ContentCatalog.tres", string.Empty, ResourceLoader.CacheMode.Ignore)
            ?? throw new InvalidOperationException("Canonical catalog is missing.");
        foreach (GodotResourceEntry entry in catalog.Entries.Where(value => value.ResourceTypeIdValue == "ai"))
        {
            _paths[entry.ContentIdValue] = entry.DiagnosticPathValue;
            _picker!.AddItem(entry.ContentIdValue);
        }
        if (_picker!.ItemCount > 0) LoadSelected(0);
    }

    private void LoadSelected(long index)
    {
        string id = _picker!.GetItemText((int)index);
        _path = _paths[id];
        _resource = ResourceLoader.Load<AiDefinitionResource>(_path, string.Empty, ResourceLoader.CacheMode.Ignore)
            ?? throw new InvalidOperationException($"AI resource cannot be loaded: {_path}");
        ValidateResource(_resource);
        SetWeights(_resource.DistanceWeight, _resource.DamageWeight, _resource.TargetCountWeight, _resource.HarmfulStatusWeight);
        RebuildGraph();
        SetStatus($"Loaded {id}; {_resource.SkillContentIds.Length} skills.", false);
    }

    private void ApplyWeights()
    {
        if (_resource is null || _undoRedo is null) return;
        float[] before = [_resource.DistanceWeight, _resource.DamageWeight, _resource.TargetCountWeight, _resource.HarmfulStatusWeight];
        float[] after = [(float)_distance!.Value, (float)_damage!.Value, (float)_targets!.Value, (float)_statusWeight!.Value];
        _undoRedo.CreateAction("Edit AI safe profile weights");
        _undoRedo.AddDoMethod(this, nameof(SetWeights), after[0], after[1], after[2], after[3]);
        _undoRedo.AddUndoMethod(this, nameof(SetWeights), before[0], before[1], before[2], before[3]);
        _undoRedo.CommitAction();
    }

    public void SetWeights(float distance, float damage, float targets, float status)
    {
        if (_resource is null) return;
        _resource.DistanceWeight = distance;
        _resource.DamageWeight = damage;
        _resource.TargetCountWeight = targets;
        _resource.HarmfulStatusWeight = status;
        _distance!.Value = distance; _damage!.Value = damage; _targets!.Value = targets; _statusWeight!.Value = status;
        ValidateResource(_resource);
        SetStatus("AI profile has unsaved validated changes.", false);
    }

    private void Save()
    {
        try
        {
            WorkbenchResourceSaveService.SaveWithRollback(_resource ?? throw new InvalidOperationException("AI is not loaded."), _path, ValidateResource);
            SetStatus("Saved and reload-validated AI profile.", false);
        }
        catch (Exception exception) { SetStatus($"Save rolled back: {exception.Message}", true); }
    }

    private void RebuildGraph()
    {
        foreach (Node child in _graph!.GetChildren())
        {
            _graph.RemoveChild(child);
            child.QueueFree();
        }
        if (_resource is null || string.IsNullOrWhiteSpace(_resource.DecisionGraphJson)) return;
        using JsonDocument document = JsonDocument.Parse(_resource.DecisionGraphJson);
        var nodes = new Dictionary<string, GraphNode>(StringComparer.Ordinal);
        int index = 0;
        foreach (JsonElement value in document.RootElement.GetProperty("nodes").EnumerateArray())
        {
            string id = value.GetProperty("nodeId").GetString()!;
            string kind = value.GetProperty("kind").GetString()!;
            string type = value.GetProperty("type").GetString()!;
            var node = new GraphNode { Name = id, Title = $"{kind}: {type}", PositionOffset = new Vector2(80 + index % 4 * 240, 80 + index / 4 * 145) };
            node.AddChild(new Label { Text = id, CustomMinimumSize = new Vector2(190, 30) });
            node.SetSlot(0, true, 0, Colors.White, true, 0, Colors.White);
            _graph.AddChild(node); nodes[id] = node; index++;
        }
        foreach (JsonElement edge in document.RootElement.GetProperty("edges").EnumerateArray())
        {
            string source = edge.GetProperty("sourceNodeId").GetString()!;
            string target = edge.GetProperty("targetNodeId").GetString()!;
            if (nodes.TryGetValue(source, out GraphNode? from) && nodes.TryGetValue(target, out GraphNode? to))
                _graph.ConnectNode(from.Name, 0, to.Name, 0);
        }
    }

    private void SetStatus(string text, bool error)
    {
        _status!.Text = text;
        _status.Modulate = error ? Colors.IndianRed : Colors.LightGreen;
    }
}
#endif
