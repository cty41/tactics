using Godot;
using Tactics.Core.Board;
using Tactics.Core.Units;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>Draws persistent, presentation-only spear markers from the committed battle snapshot.</summary>
public partial class GodotDroppedSpearLayer : Node2D
{
    private readonly Dictionary<UnitInstanceId, Vector2> _markers = new();

    public int MarkerCount => _markers.Count;
    public IReadOnlyDictionary<UnitInstanceId, Vector2> MarkerPositions => _markers;

    public void Sync(IReadOnlyDictionary<UnitInstanceId, GridPoint> droppedSpears)
    {
        ArgumentNullException.ThrowIfNull(droppedSpears);
        _markers.Clear();
        foreach ((UnitInstanceId owner, GridPoint cell) in droppedSpears.OrderBy(value => value.Key.Value, StringComparer.Ordinal))
            _markers[owner] = IsometricBattleBoardLayout.GridToScreen(cell);
        QueueRedraw();
    }

    public override void _Draw()
    {
        foreach (Vector2 center in _markers.Values)
        {
            Vector2 start = center + new Vector2(-15f, 8f);
            Vector2 end = center + new Vector2(15f, -8f);
            DrawLine(start, end, new Color(.34f, .16f, .08f, 1f), 4f, true);
            DrawLine(start, end, new Color(.92f, .56f, .22f, 1f), 2f, true);
            Vector2 direction = start.DirectionTo(end);
            Vector2 side = new(-direction.Y, direction.X);
            DrawColoredPolygon([end + direction * 7f, end - direction * 3f + side * 4f,
                end - direction * 3f - side * 4f], new Color(.88f, .9f, .94f, 1f));
        }
    }
}
