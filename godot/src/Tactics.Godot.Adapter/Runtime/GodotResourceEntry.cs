using Godot;

namespace Tactics.Godot.Adapter.Runtime;

[GlobalClass]
public partial class GodotResourceEntry : Resource
{
    [Export] public string ContentIdValue { get; set; } = string.Empty;
    [Export] public string ResourcePathValue { get; set; } = string.Empty;
}
