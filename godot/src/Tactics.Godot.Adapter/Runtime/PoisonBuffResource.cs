using Godot;
using Tactics.Core.Content;

namespace Tactics.Godot.Adapter.Runtime;

[GlobalClass]
public partial class PoisonBuffResource : Resource
{
    [Export] public string ContentIdValue { get; set; } = string.Empty;
    [Export] public int SchemaVersion { get; set; }
    [Export] public string DisplayName { get; set; } = string.Empty;
    [Export] public int DefaultDuration { get; set; }
    [Export] public int DamagePerTurn { get; set; }
    [Export] public string DamageCategory { get; set; } = string.Empty;
    [Export] public string EffectType { get; set; } = string.Empty;
    [Export] public string Polarity { get; set; } = string.Empty;
    [Export] public string RefreshStrategy { get; set; } = string.Empty;
    [Export] public string TriggerTiming { get; set; } = string.Empty;

    public ContentId ContentId => new(ContentIdValue);
}
