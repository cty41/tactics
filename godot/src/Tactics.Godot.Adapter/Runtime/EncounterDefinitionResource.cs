using Godot;

namespace Tactics.Godot.Adapter.Runtime;

[Tool]
[GlobalClass]
public partial class EncounterDefinitionResource : Resource
{
    [Export] public int SchemaVersion { get; set; }=1;
    [Export] public string ContentIdValue { get; set; }=string.Empty;
    [Export] public string LayoutContentId { get; set; }=string.Empty;
    [Export] public string[] MonsterUnitContentIds { get; set; }=Array.Empty<string>();
    [Export] public string[] MonsterAiContentIds { get; set; }=Array.Empty<string>();
    [Export] public float HealthMultiplier { get; set; } = 1f;
    [Export] public float OutputMultiplier { get; set; } = 1f;
    [Export] public int MinimumStartingMana { get; set; }
    [Export] public string EncounterClassValue { get; set; } = "Normal";
}
