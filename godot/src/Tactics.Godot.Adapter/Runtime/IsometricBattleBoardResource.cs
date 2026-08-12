using Godot;

namespace Tactics.Godot.Adapter.Runtime;

[GlobalClass]
public partial class IsometricBattleBoardResource : Resource
{
    [Export] public string ContentIdValue { get; set; } = "battle-board.pure-run.isometric-v1";
    [Export] public int CanvasWidth { get; set; } = 1600;
    [Export] public int CanvasHeight { get; set; } = 900;
    [Export] public int GridSize { get; set; } = 10;
    [Export] public Vector2 TileSize { get; set; } = new(96f, 48f);
    [Export] public Vector2 TopCenter { get; set; } = new(550f, 145f);
    [Export] public Vector2 FirstCellCenter { get; set; } = new(550f, 169f);
    [Export] public string DrawOrder { get; set; } = "x+y,x,stable-instance-id";
    [Export] public string HighlightPriority { get; set; } = "base,unit,range,legal,path-aoe-spear,hover";
}
