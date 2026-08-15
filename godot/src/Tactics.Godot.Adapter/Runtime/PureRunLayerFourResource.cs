using Godot;

namespace Tactics.Godot.Adapter.Runtime;

[Tool]
[GlobalClass]
public partial class PureRunLayerFourResource : Resource
{
    [Export] public string ContentIdValue { get; set; } = string.Empty;
    [Export] public string KindValue { get; set; } = string.Empty;
    [Export] public string PayloadJson { get; set; } = string.Empty;
}
