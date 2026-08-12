using Godot;
using Tactics.Core.Board;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>Presentation-only board surface; legal sets are supplied by the Application snapshot.</summary>
[GlobalClass]
public partial class GodotIsometricBattleBoard : Control
{
    private readonly Dictionary<GridPoint, Color> _colors = new();
    private readonly HashSet<GridPoint> _blocked = new();
    private GridPoint? _hovered;

    public event Action<GridPoint>? CellPressed;
    public event Action<GridPoint>? CellHovered;
    public event Action? HoverCleared;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        QueueRedraw();
    }

    public void SetVisuals(IReadOnlyDictionary<GridPoint, Color> colors, IEnumerable<GridPoint> blocked)
    {
        _colors.Clear();
        foreach ((GridPoint cell, Color color) in colors) _colors[cell] = color;
        _blocked.Clear();
        foreach (GridPoint cell in blocked) _blocked.Add(cell);
        QueueRedraw();
    }

    public override void _GuiInput(InputEvent inputEvent)
    {
        switch (inputEvent)
        {
            case InputEventMouseMotion motion:
                UpdateHover(motion.Position);
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } button
                when IsometricBattleBoardLayout.TryScreenToGrid(button.Position, out GridPoint cell):
                AcceptEvent();
                CellPressed?.Invoke(cell);
                break;
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationMouseExit)
        {
            _hovered = null;
            HoverCleared?.Invoke();
        }
    }

    public override void _Draw()
    {
        for (int sum = 0; sum <= 18; sum++)
        for (int x = 0; x < IsometricBattleBoardLayout.GridSize; x++)
        {
            int y = sum - x;
            if (y < 0 || y >= IsometricBattleBoardLayout.GridSize) continue;
            GridPoint cell = new(x, y);
            Color baseColor = _blocked.Contains(cell) ? new Color(0.14f, 0.19f, 0.22f, 0.96f) : new Color(0.25f, 0.32f, 0.36f, 0.96f);
            Color fill = _colors.TryGetValue(cell, out Color overlay) ? baseColor.Lerp(overlay, Math.Clamp(overlay.A, 0f, 1f)) : baseColor;
            Vector2[] diamond = IsometricBattleBoardLayout.Diamond(cell);
            DrawColoredPolygon(diamond, fill);
            DrawPolyline([..diamond, diamond[0]], new Color(0.10f, 0.17f, 0.20f, 0.95f), 1.4f, true);
        }
    }

    private void UpdateHover(Vector2 position)
    {
        GridPoint? next = IsometricBattleBoardLayout.TryScreenToGrid(position, out GridPoint cell) ? cell : null;
        if (next == _hovered) return;
        _hovered = next;
        if (next is GridPoint value) CellHovered?.Invoke(value);
        else HoverCleared?.Invoke();
    }
}
