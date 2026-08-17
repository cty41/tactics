#if TOOLS
using Tactics.Application.Authoring;
using Tactics.Core.AI;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

public static class AiAuthoringEditorService
{
    public static AiAuthoringDocument Read(AiDefinitionResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        (IReadOnlyList<AiAuthoringNode> nodes, IReadOnlyList<AiAuthoringEdge> edges) =
            string.IsNullOrWhiteSpace(resource.DecisionGraphJson)
                ? (Array.Empty<AiAuthoringNode>(), Array.Empty<AiAuthoringEdge>())
                : AiDecisionGraphAuthoringJson.Deserialize(resource.DecisionGraphJson);
        nodes = EnsureDeterministicPositions(nodes);
        return new AiAuthoringDocument(resource.ContentIdValue, ParseArchetype(resource.ArchetypeValue),
            resource.SkillContentIds, resource.PatternSkillContentIds, resource.DistanceWeight, resource.DamageWeight,
            resource.TargetCountWeight, resource.HarmfulStatusWeight, nodes, edges, resource.DecisionGraphHash,
            resource.MaximumEngageCandidatesPerTarget, resource.PreferredMinimumRange, resource.PreferredMaximumRange,
            resource.PreferredRangeRepositionBonus);
    }

    public static void Write(AiDefinitionResource resource, AiAuthoringDocument document)
    {
        if (!string.Equals(resource.ContentIdValue, document.ContentId, StringComparison.Ordinal))
            throw new InvalidOperationException("AI document identity differs from the Resource identity.");
        _ = document.ToCoreDefinition();
        resource.ArchetypeValue = document.Archetype.ToString();
        resource.SkillContentIds = document.SkillContentIds.ToArray();
        resource.PatternSkillContentIds = document.PatternSkillContentIds.ToArray();
        resource.DistanceWeight = document.DistanceWeight; resource.DamageWeight = document.DamageWeight;
        resource.TargetCountWeight = document.TargetCountWeight; resource.HarmfulStatusWeight = document.HarmfulStatusWeight;
        resource.MaximumEngageCandidatesPerTarget = document.MaximumEngageCandidatesPerTarget;
        resource.PreferredMinimumRange = document.PreferredMinimumRange; resource.PreferredMaximumRange = document.PreferredMaximumRange;
        resource.PreferredRangeRepositionBonus = document.PreferredRangeRepositionBonus;
        resource.DecisionGraphJson = AiDecisionGraphAuthoringJson.Serialize(document);
        // DecisionGraphHash is immutable frozen-source provenance, not the mutable authoring revision.
        // AuthoringRevision is computed from the normalized document and returned after typed reload.
        resource.DecisionGraphHash = document.SourceSha256;
    }

    private static AiArchetype ParseArchetype(string value) => value.ToLowerInvariant() switch
    {
        "aoe" => AiArchetype.Area,
        "elitecharger" or "elite-charger" => AiArchetype.EliteCharger,
        "elitepoisoncaster" or "elite-poison-caster" => AiArchetype.ElitePoisonCaster,
        _ => Enum.Parse<AiArchetype>(value, true)
    };

    private static IReadOnlyList<AiAuthoringNode> EnsureDeterministicPositions(IReadOnlyList<AiAuthoringNode> nodes)
    {
        if (nodes.Count <= 1 || nodes.Select(value => (value.X, value.Y)).Distinct().Count() == nodes.Count) return nodes;
        return Array.AsReadOnly(nodes.Select((value, index) => value with
        {
            X = 80 + index % 4 * 240,
            Y = 80 + index / 4 * 145
        }).ToArray());
    }
}
#endif
