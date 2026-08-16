using Godot;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Units;

namespace Tactics.Godot.Adapter.Runtime;

public partial class GodotUnitNode : Node2D
{
    [Export] public string InstanceIdValue { get; set; } = "unit.unknown.0";
    [Export] public string ContentIdValue { get; set; } = "unit.unknown";
    [Export] public int MoveRange { get; set; } = 3;
    [Export] public float Initiative { get; set; }
    [Export] public int PlayerNumber { get; set; }
    [Export] public int SpawnOrdinal { get; set; }

    public UnitState ToCoreState(GodotGridAdapter grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        return new UnitState(
            new UnitInstanceId(InstanceIdValue),
            new ContentId(ContentIdValue),
            grid.ToCore(new Vector2I(Mathf.RoundToInt(Position.X), Mathf.RoundToInt(Position.Y))),
            MoveRange,
            Initiative,
            PlayerNumber,
            SpawnOrdinal);
    }
}
