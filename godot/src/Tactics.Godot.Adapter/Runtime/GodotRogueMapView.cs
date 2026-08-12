using Godot;
using Tactics.Application.Runs;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>Draws the engine-neutral Pure Run map snapshot without owning run mutations.</summary>
public partial class GodotRogueMapView : Control
{
    public static readonly Vector2 PreferredSize = new(980, 720);
    private const float LayerSpacing = 82f;
    private const float LaneSpacing = 145f;
    private const float NodeRadius = 28f;
    private PureRunMapSnapshot? _snapshot;
    private Vector2 _pan;
    private Vector2 _dragOrigin;
    private Vector2 _panOrigin;
    private bool _dragging;
    private bool _moved;
    private string? _hoveredNodeId;

    public event Action<string>? NodePressed;
    public event Action<PureRunMapNodeSnapshot?>? NodeHovered;

    public PureRunMapSnapshot? Snapshot => _snapshot;

    public override void _Ready()
    {
        CustomMinimumSize = PreferredSize;
        MouseFilter = MouseFilterEnum.Stop;
        ClipContents = true;
        FocusMode = FocusModeEnum.All;
    }

    public void SetSnapshot(PureRunMapSnapshot snapshot, bool centerOnFocus)
    {
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        if (centerOnFocus) CenterOn(snapshot.FocusNodeId);
        QueueRedraw();
    }

    public Vector2 NodeCenter(string nodeId)
    {
        PureRunMapNodeSnapshot node = _snapshot?.Nodes.Single(value => value.NodeId == nodeId)
            ?? throw new InvalidOperationException($"Unknown map node: {nodeId}");
        float x = Size.X * .5f + node.Lane * LaneSpacing;
        float y = Size.Y - 62f - node.Layer * LayerSpacing;
        return new Vector2(x, y) + _pan;
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("263742"));
        if (_snapshot is null) return;
        foreach (PureRunMapConnectionSnapshot connection in _snapshot.Connections)
        {
            if (!connection.Revealed) continue;
            Color color = connection.Traversed ? new Color("d8c58a") : new Color("526772");
            DrawLine(NodeCenter(connection.FromNodeId), NodeCenter(connection.ToNodeId), color,
                connection.Traversed ? 4f : 2f, true);
        }
        foreach (PureRunMapNodeSnapshot node in _snapshot.Nodes)
            DrawNode(node);
    }

    public override void _GuiInput(InputEvent inputEvent)
    {
        switch (inputEvent)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } pressed:
                _dragging = true; _moved = false; _dragOrigin = pressed.Position; _panOrigin = _pan;
                AcceptEvent();
                break;
            case InputEventMouseMotion motion when _dragging:
                Vector2 delta = motion.Position - _dragOrigin;
                if (delta.LengthSquared() > 16f) _moved = true;
                _pan = ClampPan(_panOrigin + delta); QueueRedraw(); AcceptEvent();
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } released:
                _dragging = false;
                if (!_moved && FindNode(released.Position) is PureRunMapNodeSnapshot node &&
                    node.State is PureRunMapNodeState.Available or PureRunMapNodeState.Current)
                    NodePressed?.Invoke(node.NodeId);
                AcceptEvent();
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp }:
                _pan = ClampPan(_pan + new Vector2(0, 42)); QueueRedraw(); AcceptEvent();
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelDown }:
                _pan = ClampPan(_pan - new Vector2(0, 42)); QueueRedraw(); AcceptEvent();
                break;
            case InputEventMouseMotion hover when !_dragging:
                PureRunMapNodeSnapshot? current = FindNode(hover.Position);
                if (current?.NodeId != _hoveredNodeId)
                {
                    _hoveredNodeId = current?.NodeId;
                    NodeHovered?.Invoke(current);
                    QueueRedraw();
                }
                break;
        }
    }

    private void DrawNode(PureRunMapNodeSnapshot node)
    {
        Vector2 center = NodeCenter(node.NodeId);
        Color fill = node.State switch
        {
            PureRunMapNodeState.Available => new Color("4fa9c6"),
            PureRunMapNodeState.Current => new Color("e4b85d"),
            PureRunMapNodeState.Selected => new Color("d99257"),
            PureRunMapNodeState.Pending => new Color("d56b5f"),
            PureRunMapNodeState.Completed => new Color("d9d6c7"),
            _ => new Color("52616a")
        };
        DrawCircle(center, NodeRadius, fill);
        Color outline = node.NodeId == _hoveredNodeId ? Colors.White : new Color("17242c");
        DrawArc(center, NodeRadius, 0, Mathf.Tau, 40, outline, node.NodeId == _hoveredNodeId ? 4f : 2f, true);
        string glyph = node.Kind switch
        {
            Tactics.Core.Runs.PureRunNodeKind.Battle => "B",
            Tactics.Core.Runs.PureRunNodeKind.Rest => "R",
            Tactics.Core.Runs.PureRunNodeKind.Store => "$",
            _ => "?"
        };
        Font font = ThemeDB.FallbackFont;
        DrawString(font, center + new Vector2(-8, 8), glyph, HorizontalAlignment.Left, -1, 22,
            node.State == PureRunMapNodeState.Locked ? new Color("9ba8ae") : new Color("17242c"));
        DrawString(font, center + new Vector2(-58, 49), node.Title, HorizontalAlignment.Center, 116, 16,
            node.State == PureRunMapNodeState.Locked ? new Color("87969d") : new Color("edf1ed"));
        DrawString(font, center + new Vector2(-48, -37), $"L{node.Layer}", HorizontalAlignment.Center, 96, 13,
            new Color("b7c6cc"));
    }

    private PureRunMapNodeSnapshot? FindNode(Vector2 point) => _snapshot?.Nodes
        .Where(node => point.DistanceSquaredTo(NodeCenter(node.NodeId)) <= NodeRadius * NodeRadius)
        .OrderBy(node => point.DistanceSquaredTo(NodeCenter(node.NodeId)))
        .ThenBy(node => node.NodeId, StringComparer.Ordinal).FirstOrDefault();

    private void CenterOn(string nodeId)
    {
        _pan = Vector2.Zero;
        Vector2 center = NodeCenter(nodeId);
        _pan = ClampPan(new Vector2(0, Size.Y * .5f - center.Y));
    }

    private Vector2 ClampPan(Vector2 value) => new(
        Mathf.Clamp(value.X, -110f, 110f),
        Mathf.Clamp(value.Y, -180f, 180f));
}
