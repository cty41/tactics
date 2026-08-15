using Godot;
using Tactics.Core.AI;
using Tactics.Core.Content;

namespace Tactics.Godot.Adapter.Runtime;

[Tool]
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
    [Export(PropertyHint.MultilineText)] public string DecisionGraphJson { get; set; }=string.Empty;
    [Export] public int MaximumEngageCandidatesPerTarget { get; set; }=3;
    [Export] public int PreferredMinimumRange { get; set; }=1;
    [Export] public int PreferredMaximumRange { get; set; }=1;
    [Export] public float PreferredRangeRepositionBonus { get; set; }

    public AiDefinition ToCoreDefinition() => new(
        new ContentId(ContentIdValue),
        ParseArchetype(ArchetypeValue),
        new AiProfileDefinition(DistanceWeight, DamageWeight, TargetCountWeight, HarmfulStatusWeight),
        SkillContentIds.Select(value => new ContentId(value)).ToArray(),
        PatternSkillContentIds.Select(value => new ContentId(value)).ToArray(),
        ParseGraph(), MaximumEngageCandidatesPerTarget, PreferredMinimumRange, PreferredMaximumRange, PreferredRangeRepositionBonus);

    private AiDecisionGraphDefinition? ParseGraph()
    {
        if (string.IsNullOrWhiteSpace(DecisionGraphJson)) return null;
        using System.Text.Json.JsonDocument document=System.Text.Json.JsonDocument.Parse(DecisionGraphJson);
        System.Text.Json.JsonElement root=document.RootElement;
        var intents=new List<AiIntentDefinition>();var rules=new List<AiRuleDefinition>();var scores=new List<AiScoreDefinition>();
        foreach(System.Text.Json.JsonElement node in root.GetProperty("nodes").EnumerateArray())
        {
            string id=node.GetProperty("nodeId").GetString()!;string kind=node.GetProperty("kind").GetString()!;string type=node.GetProperty("type").GetString()!;bool enabled=node.GetProperty("enabled").GetBoolean();
            if(kind=="intent")intents.Add(new AiIntentDefinition(id,type,node.GetProperty("basePriority").GetSingle(),enabled));
            else if(kind=="rule")rules.Add(new AiRuleDefinition(id,type,node.GetProperty("parameter").GetSingle(),enabled));
            else scores.Add(new AiScoreDefinition(id,type,node.GetProperty("weight").GetSingle(),enabled,node.GetProperty("curve").EnumerateArray().Select(key=>new AiCurveKey(key.GetProperty("time").GetSingle(),key.GetProperty("value").GetSingle(),key.GetProperty("inSlope").GetSingle(),key.GetProperty("outSlope").GetSingle())).ToArray()));
        }
        AiDecisionEdge[] edges=root.GetProperty("edges").EnumerateArray().Select(edge=>new AiDecisionEdge(edge.GetProperty("sourceNodeId").GetString()!,edge.GetProperty("targetNodeId").GetString()!)).ToArray();
        return new AiDecisionGraphDefinition(intents,rules,scores,edges,DecisionGraphHash);
    }

    private static AiArchetype ParseArchetype(string value) => value.ToLowerInvariant() switch
    {
        "aoe" => AiArchetype.Area,
        "elitecharger" or "elite-charger" => AiArchetype.EliteCharger,
        "elitepoisoncaster" or "elite-poison-caster" => AiArchetype.ElitePoisonCaster,
        _ => Enum.Parse<AiArchetype>(value, true)
    };
}
