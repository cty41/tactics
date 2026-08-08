using Godot;

namespace Tactics.Godot.Adapter.Runtime;

[GlobalClass]
public partial class PoisonSpearFixtureResource : Resource
{
    [Export] public string ContentIdValue { get; set; } = "encounter.poison-spear.10x10";
    [Export] public int BoardWidth { get; set; } = 10;
    [Export] public int BoardHeight { get; set; } = 10;
    [Export] public Vector2I CasterCell { get; set; } = new(1, 1);
    [Export] public Vector2I TargetCell { get; set; } = new(3, 2);
}
