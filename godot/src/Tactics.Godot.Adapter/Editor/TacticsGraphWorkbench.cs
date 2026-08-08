#if TOOLS
using Godot;

namespace Tactics.Godot.Adapter.Editor;

[Tool]
public partial class TacticsGraphWorkbench : VBoxContainer
{
    private const string PresentationPath = "res://content/poison_spear/PoisonSpearPresentationLv1.tres";
    private EditorUndoRedoManager? _undoRedo;
    private int _markerCount;
    private Button? _addMarkerButton;
    private Button? _savePresentationButton;
    private GraphEdit? _graph;
    private SubViewport? _preview;
    private Resource? _presentation;
    private string[] _presentationNodeIds = Array.Empty<string>();
    private string[] _presentationNodeTypes = Array.Empty<string>();
    private string[] _presentationNodeChildren = Array.Empty<string>();
    private readonly Dictionary<string, GraphNode> _graphNodes = new(StringComparer.Ordinal);
    private bool _initialized;

    public int MarkerCount => _markerCount;

    public void Configure(EditorUndoRedoManager undoRedo)
    {
        _undoRedo = undoRedo ?? throw new ArgumentNullException(nameof(undoRedo));
    }

    public override void _Ready() => CallDeferred(nameof(InitializeWorkbench));

    public void InitializeWorkbench()
    {
        if (_initialized || !IsInsideTree())
            return;

        _initialized = true;
        try
        {
            _addMarkerButton = new Button { Text = "Add Presentation Marker" };
            _addMarkerButton.Pressed += AddMarker;
            AddChild(_addMarkerButton);

            _savePresentationButton = new Button { Text = "Save Poison Spear Presentation" };
            _savePresentationButton.Pressed += SavePresentation;
            AddChild(_savePresentationButton);

            Resource? rawPresentation = ResourceLoader.Load(PresentationPath);
            _presentation = rawPresentation;
            if (_presentation is null)
                GD.PushError($"[Tactics Tooling] Presentation resource type is unavailable: {PresentationPath}");
            else
            {
                _presentationNodeIds = _presentation.Get("NodeIds").AsStringArray();
                _presentationNodeTypes = _presentation.Get("NodeTypes").AsStringArray();
                _presentationNodeChildren = _presentation.Get("NodeChildren").AsStringArray();
                GD.Print($"[Tactics Tooling] Presentation loaded: {PresentationPath} (nodes={_presentationNodeIds.Length}).");
            }

            _graph = new GraphEdit { CustomMinimumSize = new Vector2(520, 260) };
            AddChild(_graph);

            var previewContainer = new SubViewportContainer { CustomMinimumSize = new Vector2(520, 120) };
            _preview = new SubViewport { Size = new Vector2I(520, 120), RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
            _preview.AddChild(new PoisonSpearPreviewCanvas());
            previewContainer.AddChild(_preview);
            AddChild(previewContainer);
            SyncGraphNodes();
            GD.Print("[Tactics Tooling] GraphEdit and SubViewport workbench ready.");
        }
        catch (Exception exception)
        {
            GD.PushError($"[Tactics Tooling] Workbench failed to initialize: {exception}");
        }
    }

    public override void _ExitTree()
    {
        _initialized = false;
        if (_addMarkerButton is not null)
            _addMarkerButton.Pressed -= AddMarker;
        if (_savePresentationButton is not null)
            _savePresentationButton.Pressed -= SavePresentation;
        _preview?.QueueFree();
        _preview = null;
        _graph = null;
        _presentation = null;
        _presentationNodeIds = Array.Empty<string>();
        _presentationNodeTypes = Array.Empty<string>();
        _presentationNodeChildren = Array.Empty<string>();
        _graphNodes.Clear();
    }

    private void AddMarker()
    {
        if (_undoRedo is null)
        {
            GD.PushError("[Tactics Tooling] UndoRedo manager is not configured.");
            return;
        }

        int nextCount = _markerCount + 1;
        _undoRedo.CreateAction("Add Presentation Marker", UndoRedo.MergeMode.Disable, this);
        _undoRedo.AddDoMethod(this, MethodName.SetMarkerCount, nextCount);
        _undoRedo.AddUndoMethod(this, MethodName.SetMarkerCount, _markerCount);
        _undoRedo.CommitAction();
    }

    public void SetMarkerCount(int value)
    {
        _markerCount = Math.Max(0, value);
        SyncGraphNodes();
    }

    public void SyncGraphNodes()
    {
        if (_graph is null)
            return;

        foreach (Node child in _graph.GetChildren())
        {
            if (child is GraphNode)
                child.Free();
        }
        _graphNodes.Clear();

        if (_presentation is not null)
        {
            for (int index = 0; index < _presentationNodeIds.Length; index++)
            {
                GraphNode node = AddGraphNode(
                    _presentationNodeIds[index],
                    _presentationNodeTypes[index],
                    new Vector2(24 + index * 180, 40));
                _graphNodes[_presentationNodeIds[index]] = node;
            }

            for (int index = 0; index < _presentationNodeIds.Length && index < _presentationNodeChildren.Length; index++)
            {
                string parentId = _presentationNodeIds[index];
                foreach (string childId in _presentationNodeChildren[index].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (_graphNodes.ContainsKey(childId))
                        _graph.ConnectNode(GraphNodeName(parentId), 0, GraphNodeName(childId), 0, true);
                }
            }
        }

        for (int index = 0; index < _markerCount; index++)
        {
            AddGraphNode($"marker.{index + 1}", "editor.marker", new Vector2(24 + (_presentationNodeIds.Length + index) * 180, 160));
        }
    }

    private GraphNode AddGraphNode(string nodeId, string nodeType, Vector2 position)
    {
        if (_graph is null)
            throw new InvalidOperationException("GraphEdit is not initialized.");

        var node = new GraphNode
        {
            Title = nodeId,
            Position = position,
            Name = GraphNodeName(nodeId)
        };
        node.AddChild(new Label { Text = nodeType });
        node.SetSlot(0, true, 0, NodeColor(nodeType), true, 0, NodeColor(nodeType), null, null, true);
        _graph.AddChild(node);
        return node;
    }

    private static string GraphNodeName(string nodeId) => nodeId.Replace('.', '_');

    private static Color NodeColor(string nodeType) => nodeType switch
    {
        "sequence" => new Color(0.35f, 0.55f, 0.95f),
        "projectile.flight" => new Color(0.25f, 0.9f, 0.7f),
        "projectile.impact" => new Color(0.95f, 0.55f, 0.2f),
        _ => Colors.Gray
    };

    private void SavePresentation()
    {
        if (_presentation is null)
        {
            GD.PushError($"[Tactics Tooling] Cannot save missing presentation: {PresentationPath}");
            return;
        }

        Error error = ResourceSaver.Save(_presentation, PresentationPath);
        if (error != Error.Ok)
            GD.PushError($"[Tactics Tooling] Failed to save presentation: {error}");
        else
            GD.Print($"[Tactics Tooling] Saved Poison Spear presentation: {PresentationPath}");
    }

}
#endif
