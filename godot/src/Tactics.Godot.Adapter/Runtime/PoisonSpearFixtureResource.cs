using Godot;

namespace Tactics.Godot.Adapter.Runtime;

[GlobalClass]
public partial class PoisonSpearFixtureResource : Resource
{
    [Export] public string ContentIdValue { get; set; } = string.Empty;
    [Export] public int BoardWidth { get; set; }
    [Export] public int BoardHeight { get; set; }
    [Export] public Vector2I CasterCell { get; set; }
    [Export] public Vector2I TargetCell { get; set; }
}
