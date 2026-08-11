using Godot;
using Tactics.Core.AI;
using Tactics.Core.Content;

namespace Tactics.Godot.Adapter.Runtime;

[GlobalClass]
public partial class AiDefinitionResource : Resource
{
    [Export] public int SchemaVersion { get; set; }=1;
    [Export] public string ContentIdValue { get; set; }=string.Empty;
    [Export] public string ArchetypeValue { get; set; }=string.Empty;
    [Export] public string[] SkillContentIds { get; set; }=Array.Empty<string>();
    [Export] public string[] PatternSkillContentIds { get; set; }=Array.Empty<string>();
    [Export] public float DistanceWeight { get; set; }=1;
    [Export] public float DamageWeight { get; set; }=1;
    [Export] public float TargetCountWeight { get; set; }
    [Export] public float HarmfulStatusWeight { get; set; }
    [Export] public string BrainPath { get; set; }=string.Empty;
    [Export] public string BrainGuid { get; set; }=string.Empty;
    [Export] public long BrainLocalFileId { get; set; }
    [Export] public string ProfilePath { get; set; }=string.Empty;
    [Export] public string ProfileGuid { get; set; }=string.Empty;
    [Export] public string DecisionGraphPath { get; set; }=string.Empty;
    [Export] public string DecisionGraphHash { get; set; }=string.Empty;

    public AiDefinition ToCoreDefinition() => new(
        new ContentId(ContentIdValue),
        ParseArchetype(ArchetypeValue),
        new AiProfileDefinition(DistanceWeight, DamageWeight, TargetCountWeight, HarmfulStatusWeight),
        SkillContentIds.Select(value => new ContentId(value)).ToArray(),
        PatternSkillContentIds.Select(value => new ContentId(value)).ToArray());

    private static AiArchetype ParseArchetype(string value) => value.ToLowerInvariant() switch
    {
        "aoe" => AiArchetype.Area,
        "elitecharger" or "elite-charger" => AiArchetype.EliteCharger,
        "elitepoisoncaster" or "elite-poison-caster" => AiArchetype.ElitePoisonCaster,
        _ => Enum.Parse<AiArchetype>(value, true)
    };
}
