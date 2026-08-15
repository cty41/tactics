#if TOOLS
using Godot;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

[Tool]
public partial class PureRunMapWorkbench : VBoxContainer
{
    public const string MapPath = "res://content/map/PureRunDefaultMap.tres";
    private EditorUndoRedoManager? _undoRedo;
    private PureRunMapResource? _map;
    private GraphEdit? _graph;
    private OptionButton? _nodePicker;
    private LineEdit? _title;
    private SpinBox? _lane;
    private Label? _status;
    private bool _initialized;

    public void Configure(EditorUndoRedoManager undoRedo) => _undoRedo = undoRedo;

    public override void _Ready() => CallDeferred(nameof(InitializeWorkbench));

    public void InitializeWorkbench()
    {
        if (_initialized || !IsInsideTree()) return;
        _initialized = true;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;

        var toolbar = new HBoxContainer();
        toolbar.AddChild(new Label { Text = "Node" });
        _nodePicker = new OptionButton { CustomMinimumSize = new Vector2(210, 0) };
        _nodePicker.ItemSelected += SelectNode;
        toolbar.AddChild(_nodePicker);
        toolbar.AddChild(new Label { Text = "Title" });
        _title = new LineEdit { CustomMinimumSize = new Vector2(170, 0) };
        toolbar.AddChild(_title);
        toolbar.AddChild(new Label { Text = "Lane" });
        _lane = new SpinBox { MinValue = -4, MaxValue = 4, Step = 0.25, CustomMinimumSize = new Vector2(90, 0) };
        toolbar.AddChild(_lane);
        Button apply = new() { Text = "Apply (Undoable)" };
        apply.Pressed += ApplySelectedNode;
        toolbar.AddChild(apply);
        Button layout = new() { Text = "Auto Layout" };
        layout.Pressed += AutoLayout;
        toolbar.AddChild(layout);
        Button validate = new() { Text = "Validate" };
        validate.Pressed += ValidateMap;
        toolbar.AddChild(validate);
        Button save = new() { Text = "Save" };
        save.Pressed += SaveMap;
        toolbar.AddChild(save);
        AddChild(toolbar);

        _status = new Label { Text = "Loading authoritative map..." };
        AddChild(_status);
        _graph = new GraphEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        AddChild(_graph);
        LoadMap();
    }

    public override void _ExitTree()
    {
        _initialized = false;
        if (_nodePicker is not null) _nodePicker.ItemSelected -= SelectNode;
    }

    public static void ValidateResource(PureRunMapResource resource)
    {
        if (resource.NodeIds.Length == 0) throw new InvalidOperationException("Map must contain nodes.");
        if (resource.NodeIds.Distinct(StringComparer.Ordinal).Count() != resource.NodeIds.Length)
            throw new InvalidOperationException("Map node IDs must be unique.");
        var ids = resource.NodeIds.ToHashSet(StringComparer.Ordinal);
        if (resource.ConnectionFromNodeIds.Zip(resource.ConnectionToNodeIds)
            .Any(edge => !ids.Contains(edge.First) || !ids.Contains(edge.Second)))
            throw new InvalidOperationException("Map connection references an unknown node.");
        try { _ = resource.ToCoreDefinition(); }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            throw new InvalidOperationException("Map does not compile to the runtime definition.", exception);
        }
    }

    private void LoadMap()
    {
        _map = ResourceLoader.Load<PureRunMapResource>(MapPath, string.Empty, ResourceLoader.CacheMode.Ignore)
            ?? throw new InvalidOperationException($"Authoritative map is missing: {MapPath}");
        ValidateResource(_map);
        _nodePicker!.Clear();
        foreach (string id in _map.NodeIds) _nodePicker.AddItem(id);
        if (_map.NodeIds.Length > 0) SelectNode(0);
        RebuildGraph();
        SetStatus($"Loaded {_map.NodeIds.Length} nodes / {_map.ConnectionFromNodeIds.Length} connections.");
    }

    private void SelectNode(long index)
    {
        if (_map is null || index < 0 || index >= _map.NodeIds.Length) return;
        _title!.Text = _map.NodeTitles[index];
        _lane!.Value = _map.NodeLanes[index];
    }

    private void ApplySelectedNode()
    {
        if (_map is null || _undoRedo is null || _nodePicker is null || _title is null || _lane is null) return;
        int index = _nodePicker.Selected;
        if (index < 0) return;
        string[] oldTitles = _map.NodeTitles.ToArray();
        float[] oldLanes = _map.NodeLanes.ToArray();
        string[] newTitles = oldTitles.ToArray();
        float[] newLanes = oldLanes.ToArray();
        newTitles[index] = _title.Text.Trim();
        newLanes[index] = (float)_lane.Value;
        _undoRedo.CreateAction("Edit Pure Run map node");
        _undoRedo.AddDoMethod(this, nameof(SetNodeArrays), newTitles, newLanes);
        _undoRedo.AddUndoMethod(this, nameof(SetNodeArrays), oldTitles, oldLanes);
        _undoRedo.CommitAction();
    }

    private void AutoLayout()
    {
        if (_map is null || _undoRedo is null) return;
        float[] oldLanes = _map.NodeLanes.ToArray();
        float[] lanes = _map.NodeLayers.Select((layer, index) =>
        {
            int[] peers = Enumerable.Range(0, _map.NodeLayers.Length).Where(i => _map.NodeLayers[i] == layer).ToArray();
            int peerIndex = Array.IndexOf(peers, index);
            return peerIndex - (peers.Length - 1) / 2f;
        }).ToArray();
        _undoRedo.CreateAction("Auto-layout Pure Run map");
        _undoRedo.AddDoMethod(this, nameof(SetNodeArrays), _map.NodeTitles.ToArray(), lanes);
        _undoRedo.AddUndoMethod(this, nameof(SetNodeArrays), _map.NodeTitles.ToArray(), oldLanes);
        _undoRedo.CommitAction();
    }

    public void SetNodeArrays(string[] titles, float[] lanes)
    {
        if (_map is null) return;
        _map.NodeTitles = titles;
        _map.NodeLanes = lanes;
        ValidateResource(_map);
        RebuildGraph();
        SelectNode(_nodePicker?.Selected ?? 0);
        SetStatus("Map has unsaved validated changes.");
    }

    private void ValidateMap()
    {
        try { ValidateResource(_map ?? throw new InvalidOperationException("Map is not loaded.")); SetStatus("Validation passed."); }
        catch (Exception exception) { SetStatus($"Validation failed: {exception.Message}", true); }
    }

    private void SaveMap()
    {
        try
        {
            WorkbenchResourceSaveService.SaveWithRollback(_map ?? throw new InvalidOperationException("Map is not loaded."), MapPath, ValidateResource);
            SetStatus("Saved and reload-validated authoritative map.");
        }
        catch (Exception exception) { SetStatus($"Save rolled back: {exception.Message}", true); }
    }

    private void RebuildGraph()
    {
        if (_graph is null || _map is null) return;
        foreach (Node child in _graph.GetChildren())
        {
            _graph.RemoveChild(child);
            child.QueueFree();
        }
        var graphNodes = new Dictionary<string, GraphNode>(StringComparer.Ordinal);
        for (int i = 0; i < _map.NodeIds.Length; i++)
        {
            var node = new GraphNode
            {
                Name = _map.NodeIds[i], Title = $"{_map.NodeTitles[i]} [{_map.NodeKinds[i]}]",
                PositionOffset = new Vector2(90 + _map.NodeLayers[i] * 210, 280 + _map.NodeLanes[i] * 105),
                Resizable = false
            };
            node.AddChild(new Label { Text = _map.NodeContentIds[i], CustomMinimumSize = new Vector2(175, 32) });
            node.SetSlot(0, true, 0, Colors.White, true, 0, Colors.White);
            _graph.AddChild(node);
            graphNodes[_map.NodeIds[i]] = node;
        }
        for (int i = 0; i < _map.ConnectionFromNodeIds.Length; i++)
            _graph.ConnectNode(graphNodes[_map.ConnectionFromNodeIds[i]].Name, 0, graphNodes[_map.ConnectionToNodeIds[i]].Name, 0);
    }

    private void SetStatus(string text, bool error = false)
    {
        if (_status is null) return;
        _status.Text = text;
        _status.Modulate = error ? Colors.IndianRed : Colors.LightGreen;
    }
}
#endif
