using System.Text.Json;
using Tactics.Core.AI;
using Tactics.Core.Content;

namespace Tactics.Application.Authoring;

public enum AiAuthoringNodeKind { Intent, Rule, Score }
public sealed record AiCurveKeyAuthoring(float Time, float Value, float InSlope, float OutSlope);
public sealed record AiAuthoringNode(string NodeId, AiAuthoringNodeKind Kind, string Type, bool Enabled,
    float Parameter, IReadOnlyList<AiCurveKeyAuthoring> Curve, float X = 0, float Y = 0);
public sealed record AiAuthoringEdge(string SourceNodeId, string TargetNodeId);

public sealed class AiAuthoringDocument : IAuthoringDocument
{
    private static readonly HashSet<string> RuntimeIntentTypes = new(StringComparer.Ordinal)
        { "FinishOff", "BasicAttack", "Engage", "Retreat", "HoldPosition" };
    private static readonly HashSet<string> RuntimeScoreTypes = new(StringComparer.Ordinal)
        { "KillPotential", "DistanceToTarget", "TargetHealth" };
    public AiAuthoringDocument(string contentId, AiArchetype archetype, IEnumerable<string> skillContentIds,
        IEnumerable<string> patternSkillContentIds, float distanceWeight, float damageWeight, float targetCountWeight,
        float harmfulStatusWeight, IEnumerable<AiAuthoringNode> nodes, IEnumerable<AiAuthoringEdge> edges,
        string sourceSha256, int maximumEngageCandidatesPerTarget, int preferredMinimumRange,
        int preferredMaximumRange, float preferredRangeRepositionBonus)
    {
        ContentId = Require(contentId); Archetype = archetype;
        SkillContentIds = Read(skillContentIds); PatternSkillContentIds = Read(patternSkillContentIds);
        DistanceWeight = distanceWeight; DamageWeight = damageWeight; TargetCountWeight = targetCountWeight; HarmfulStatusWeight = harmfulStatusWeight;
        Nodes = Array.AsReadOnly((nodes ?? throw new ArgumentNullException(nameof(nodes))).ToArray());
        Edges = Array.AsReadOnly((edges ?? throw new ArgumentNullException(nameof(edges))).ToArray());
        SourceSha256 = sourceSha256 ?? string.Empty; MaximumEngageCandidatesPerTarget = maximumEngageCandidatesPerTarget;
        PreferredMinimumRange = preferredMinimumRange; PreferredMaximumRange = preferredMaximumRange; PreferredRangeRepositionBonus = preferredRangeRepositionBonus;
        _ = ToCoreDefinition();
    }
    public string ContentId { get; }
    public int SchemaVersion => 1;
    public AiArchetype Archetype { get; }
    public IReadOnlyList<string> SkillContentIds { get; }
    public IReadOnlyList<string> PatternSkillContentIds { get; }
    public float DistanceWeight { get; }
    public float DamageWeight { get; }
    public float TargetCountWeight { get; }
    public float HarmfulStatusWeight { get; }
    public IReadOnlyList<AiAuthoringNode> Nodes { get; }
    public IReadOnlyList<AiAuthoringEdge> Edges { get; }
    public string SourceSha256 { get; }
    public int MaximumEngageCandidatesPerTarget { get; }
    public int PreferredMinimumRange { get; }
    public int PreferredMaximumRange { get; }
    public float PreferredRangeRepositionBonus { get; }
    public IReadOnlyList<string> Dependencies => Array.AsReadOnly(SkillContentIds.Concat(PatternSkillContentIds).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());

    public AiDefinition ToCoreDefinition()
    {
        if (new[] { DistanceWeight, DamageWeight, TargetCountWeight, HarmfulStatusWeight }.Any(value => value < 0)) throw new ArgumentOutOfRangeException(nameof(DistanceWeight));
        if (MaximumEngageCandidatesPerTarget <= 0 || PreferredMinimumRange < 0 || PreferredMaximumRange < PreferredMinimumRange) throw new ArgumentOutOfRangeException(nameof(MaximumEngageCandidatesPerTarget));
        if (Nodes.Select(value => value.NodeId).Distinct(StringComparer.Ordinal).Count() != Nodes.Count) throw new ArgumentException("AI node identities must be unique.");
        if (Nodes.Any(value => string.IsNullOrWhiteSpace(value.NodeId) || string.IsNullOrWhiteSpace(value.Type) ||
                               !float.IsFinite(value.Parameter) || !float.IsFinite(value.X) || !float.IsFinite(value.Y)))
            throw new ArgumentException("AI nodes require identities, types, finite parameters and positions.");
        HashSet<string> ids = Nodes.Select(value => value.NodeId).ToHashSet(StringComparer.Ordinal);
        if (Edges.Any(value => !ids.Contains(value.SourceNodeId) || !ids.Contains(value.TargetNodeId) || value.SourceNodeId == value.TargetNodeId) || Edges.Distinct().Count() != Edges.Count) throw new ArgumentException("AI edges must be unique, non-self and reference known nodes.");
        if (Nodes.Where(value => value.Kind == AiAuthoringNodeKind.Intent && value.Enabled).All(value => !RuntimeIntentTypes.Contains(value.Type)))
            throw new ArgumentException("AI graphs require at least one enabled runtime-supported Intent.");
        if (Nodes.Any(value => value.Kind == AiAuthoringNodeKind.Intent && !RuntimeIntentTypes.Contains(value.Type)))
            throw new ArgumentException("AI graph contains an unknown Intent type.");
        if (Nodes.Any(value => value.Kind == AiAuthoringNodeKind.Score && !RuntimeScoreTypes.Contains(value.Type)))
            throw new ArgumentException("AI graph contains an unknown Score type.");
        foreach (AiAuthoringNode node in Nodes.Where(value => value.Kind == AiAuthoringNodeKind.Score))
            if (node.Curve.Count == 0 || node.Curve.Any(key => !float.IsFinite(key.Time) || !float.IsFinite(key.Value) || !float.IsFinite(key.InSlope) || !float.IsFinite(key.OutSlope)) ||
                node.Curve.Zip(node.Curve.Skip(1)).Any(pair => pair.First.Time >= pair.Second.Time))
                throw new ArgumentException("Score curves require finite, strictly increasing keys.");
        EnsureAcyclic(ids, Edges);
        var graph = new AiDecisionGraphDefinition(
            Nodes.Where(value => value.Kind == AiAuthoringNodeKind.Intent).Select(value => new AiIntentDefinition(value.NodeId, value.Type, value.Parameter, value.Enabled)).ToArray(),
            Nodes.Where(value => value.Kind == AiAuthoringNodeKind.Rule).Select(value => new AiRuleDefinition(value.NodeId, value.Type, value.Parameter, value.Enabled)).ToArray(),
            Nodes.Where(value => value.Kind == AiAuthoringNodeKind.Score).Select(value => new AiScoreDefinition(value.NodeId, value.Type, value.Parameter, value.Enabled, value.Curve.Select(key => new AiCurveKey(key.Time, key.Value, key.InSlope, key.OutSlope)).ToArray())).ToArray(),
            Edges.Select(value => new AiDecisionEdge(value.SourceNodeId, value.TargetNodeId)).ToArray(), SourceSha256);
        return new AiDefinition(new ContentId(ContentId), Archetype, new AiProfileDefinition(DistanceWeight, DamageWeight, TargetCountWeight, HarmfulStatusWeight), SkillContentIds.Select(value => new ContentId(value)).ToArray(), PatternSkillContentIds.Select(value => new ContentId(value)).ToArray(), graph, MaximumEngageCandidatesPerTarget, PreferredMinimumRange, PreferredMaximumRange, PreferredRangeRepositionBonus);
    }

    public void WriteCanonical(Utf8JsonWriter writer)
    {
        writer.WriteStartObject(); writer.WriteString("contentId", ContentId); writer.WriteNumber("schemaVersion", SchemaVersion); writer.WriteString("archetype", Archetype.ToString());
        WriteStrings(writer, "skillContentIds", SkillContentIds); WriteStrings(writer, "patternSkillContentIds", PatternSkillContentIds);
        writer.WriteNumber("distanceWeight", DistanceWeight); writer.WriteNumber("damageWeight", DamageWeight); writer.WriteNumber("targetCountWeight", TargetCountWeight); writer.WriteNumber("harmfulStatusWeight", HarmfulStatusWeight);
        writer.WriteNumber("maximumEngageCandidatesPerTarget", MaximumEngageCandidatesPerTarget); writer.WriteNumber("preferredMinimumRange", PreferredMinimumRange); writer.WriteNumber("preferredMaximumRange", PreferredMaximumRange); writer.WriteNumber("preferredRangeRepositionBonus", PreferredRangeRepositionBonus); writer.WriteString("sourceSha256", SourceSha256);
        writer.WriteStartArray("nodes"); foreach (AiAuthoringNode node in Nodes.OrderBy(value => value.NodeId, StringComparer.Ordinal)) { writer.WriteStartObject(); writer.WriteString("nodeId", node.NodeId); writer.WriteString("kind", node.Kind.ToString()); writer.WriteString("type", node.Type); writer.WriteBoolean("enabled", node.Enabled); writer.WriteNumber("parameter", node.Parameter); writer.WriteNumber("x", node.X); writer.WriteNumber("y", node.Y); writer.WriteStartArray("curve"); foreach (AiCurveKeyAuthoring key in node.Curve) { writer.WriteStartObject(); writer.WriteNumber("time", key.Time); writer.WriteNumber("value", key.Value); writer.WriteNumber("inSlope", key.InSlope); writer.WriteNumber("outSlope", key.OutSlope); writer.WriteEndObject(); } writer.WriteEndArray(); writer.WriteEndObject(); } writer.WriteEndArray();
        writer.WriteStartArray("edges"); foreach (AiAuthoringEdge edge in Edges.OrderBy(value => value.SourceNodeId, StringComparer.Ordinal).ThenBy(value => value.TargetNodeId, StringComparer.Ordinal)) { writer.WriteStartObject(); writer.WriteString("sourceNodeId", edge.SourceNodeId); writer.WriteString("targetNodeId", edge.TargetNodeId); writer.WriteEndObject(); } writer.WriteEndArray(); writer.WriteEndObject();
    }
    private static IReadOnlyList<string> Read(IEnumerable<string> values) => Array.AsReadOnly((values ?? throw new ArgumentNullException(nameof(values))).Select(Require).ToArray());
    private static void WriteStrings(Utf8JsonWriter writer, string name, IEnumerable<string> values) { writer.WriteStartArray(name); foreach (string value in values) writer.WriteStringValue(value); writer.WriteEndArray(); }
    private static string Require(string value) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Content identity is required.") : value;

    private static void EnsureAcyclic(IEnumerable<string> nodeIds, IEnumerable<AiAuthoringEdge> edges)
    {
        Dictionary<string, List<string>> outgoing = nodeIds.ToDictionary(value => value, _ => new List<string>(), StringComparer.Ordinal);
        foreach (AiAuthoringEdge edge in edges) outgoing[edge.SourceNodeId].Add(edge.TargetNodeId);
        var visiting = new HashSet<string>(StringComparer.Ordinal); var visited = new HashSet<string>(StringComparer.Ordinal);
        bool Visit(string id)
        {
            if (visiting.Contains(id)) return false;
            if (!visited.Add(id)) return true;
            visiting.Add(id);
            foreach (string target in outgoing[id]) if (!Visit(target)) return false;
            visiting.Remove(id); return true;
        }
        if (outgoing.Keys.Any(id => !Visit(id))) throw new ArgumentException("AI graph cannot contain cycles.");
    }
}

public static class AiDecisionGraphAuthoringJson
{
    public static (IReadOnlyList<AiAuthoringNode> Nodes, IReadOnlyList<AiAuthoringEdge> Edges) Deserialize(string json)
    {
        using JsonDocument payload = JsonDocument.Parse(json); JsonElement root = payload.RootElement;
        AiAuthoringNode[] nodes = root.GetProperty("nodes").EnumerateArray().Select(node =>
        {
            AiAuthoringNodeKind kind = Enum.Parse<AiAuthoringNodeKind>(node.GetProperty("kind").GetString()!, true);
            float parameter = kind switch { AiAuthoringNodeKind.Intent => node.GetProperty("basePriority").GetSingle(), AiAuthoringNodeKind.Rule => node.GetProperty("parameter").GetSingle(), _ => node.GetProperty("weight").GetSingle() };
            AiCurveKeyAuthoring[] curve = node.TryGetProperty("curve", out JsonElement keys) ? keys.EnumerateArray().Select(key => new AiCurveKeyAuthoring(key.GetProperty("time").GetSingle(), key.GetProperty("value").GetSingle(), key.GetProperty("inSlope").GetSingle(), key.GetProperty("outSlope").GetSingle())).ToArray() : Array.Empty<AiCurveKeyAuthoring>();
            return new AiAuthoringNode(node.GetProperty("nodeId").GetString()!, kind, node.GetProperty("type").GetString()!, node.GetProperty("enabled").GetBoolean(), parameter, curve, node.TryGetProperty("x", out JsonElement x) ? x.GetSingle() : 0, node.TryGetProperty("y", out JsonElement y) ? y.GetSingle() : 0);
        }).ToArray();
        AiAuthoringEdge[] edges = root.GetProperty("edges").EnumerateArray().Select(edge => new AiAuthoringEdge(edge.GetProperty("sourceNodeId").GetString()!, edge.GetProperty("targetNodeId").GetString()!)).ToArray(); return (nodes, edges);
    }

    public static string Serialize(AiAuthoringDocument document)
    {
        using var stream = new MemoryStream(); using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject(); writer.WriteString("dependencyHash", document.SourceSha256); writer.WriteStartArray("nodes"); foreach (AiAuthoringNode node in document.Nodes) { writer.WriteStartObject(); writer.WriteString("nodeId", node.NodeId); writer.WriteString("kind", node.Kind.ToString().ToLowerInvariant()); writer.WriteString("type", node.Type); writer.WriteBoolean("enabled", node.Enabled); writer.WriteNumber(node.Kind switch { AiAuthoringNodeKind.Intent => "basePriority", AiAuthoringNodeKind.Rule => "parameter", _ => "weight" }, node.Parameter); writer.WriteNumber("x", node.X); writer.WriteNumber("y", node.Y); writer.WriteStartArray("curve"); foreach (AiCurveKeyAuthoring key in node.Curve) { writer.WriteStartObject(); writer.WriteNumber("time", key.Time); writer.WriteNumber("value", key.Value); writer.WriteNumber("inSlope", key.InSlope); writer.WriteNumber("outSlope", key.OutSlope); writer.WriteEndObject(); } writer.WriteEndArray(); writer.WriteEndObject(); } writer.WriteEndArray(); writer.WriteStartArray("edges"); foreach (AiAuthoringEdge edge in document.Edges) { writer.WriteStartObject(); writer.WriteString("sourceNodeId", edge.SourceNodeId); writer.WriteString("targetNodeId", edge.TargetNodeId); writer.WriteEndObject(); } writer.WriteEndArray(); writer.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }
}

public static class AiAuthoringJson
{
    public static string Serialize(AiAuthoringDocument document)
    {
        using var stream = new MemoryStream(); using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true })) document.WriteCanonical(writer);
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    public static AiAuthoringDocument Deserialize(string json)
    {
        using JsonDocument payload = JsonDocument.Parse(json); JsonElement root = payload.RootElement;
        AiAuthoringNode[] nodes = root.GetProperty("nodes").EnumerateArray().Select(node => new AiAuthoringNode(
            node.GetProperty("nodeId").GetString()!, Enum.Parse<AiAuthoringNodeKind>(node.GetProperty("kind").GetString()!),
            node.GetProperty("type").GetString()!, node.GetProperty("enabled").GetBoolean(), node.GetProperty("parameter").GetSingle(),
            node.GetProperty("curve").EnumerateArray().Select(key => new AiCurveKeyAuthoring(key.GetProperty("time").GetSingle(), key.GetProperty("value").GetSingle(), key.GetProperty("inSlope").GetSingle(), key.GetProperty("outSlope").GetSingle())).ToArray(), node.GetProperty("x").GetSingle(), node.GetProperty("y").GetSingle())).ToArray();
        AiAuthoringEdge[] edges = root.GetProperty("edges").EnumerateArray().Select(edge => new AiAuthoringEdge(edge.GetProperty("sourceNodeId").GetString()!, edge.GetProperty("targetNodeId").GetString()!)).ToArray();
        return new AiAuthoringDocument(root.GetProperty("contentId").GetString()!, Enum.Parse<AiArchetype>(root.GetProperty("archetype").GetString()!),
            root.GetProperty("skillContentIds").EnumerateArray().Select(value => value.GetString()!), root.GetProperty("patternSkillContentIds").EnumerateArray().Select(value => value.GetString()!),
            root.GetProperty("distanceWeight").GetSingle(), root.GetProperty("damageWeight").GetSingle(), root.GetProperty("targetCountWeight").GetSingle(), root.GetProperty("harmfulStatusWeight").GetSingle(), nodes, edges,
            root.GetProperty("sourceSha256").GetString() ?? string.Empty, root.GetProperty("maximumEngageCandidatesPerTarget").GetInt32(), root.GetProperty("preferredMinimumRange").GetInt32(), root.GetProperty("preferredMaximumRange").GetInt32(), root.GetProperty("preferredRangeRepositionBonus").GetSingle());
    }
}
