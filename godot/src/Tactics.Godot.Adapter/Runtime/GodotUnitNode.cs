using Godot;
using Tactics.Core.Board;
using Tactics.Core.Content;

namespace Tactics.Godot.Adapter.Runtime;

public partial class GodotUnitNode : Node2D
{
    [Export] public string ContentIdValue { get; set; } = "unit.unknown";
    [Export] public int MoveRange { get; set; } = 3;
    [Export] public int Initiative { get; set; }

    public UnitState ToCoreState(GodotGridAdapter grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        return new UnitState(
            new ContentId(ContentIdValue),
            grid.ToCore(new Vector2I(Mathf.RoundToInt(Position.X), Mathf.RoundToInt(Position.Y))),
            MoveRange,
            Initiative);
    }
}
