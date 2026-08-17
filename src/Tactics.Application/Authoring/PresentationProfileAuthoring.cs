using System.Text.Json;

namespace Tactics.Application.Authoring;

public enum PresentationAuthoringValueKind { String, Integer, Number, Boolean, Color, Vector2 }
public sealed record PresentationAuthoringValue(PresentationAuthoringValueKind Kind, string Value);
public enum PresentationGraphNodeKind { Root, Property, Marker, Delay, Parallel }
public sealed record PresentationGraphNode(string NodeId, PresentationGraphNodeKind Kind, string PropertyName, float X, float Y, bool Enabled = true);
public sealed record PresentationGraphEdge(string SourceNodeId, string TargetNodeId);

public sealed class PresentationGraphAuthoringDocument
{
    public PresentationGraphAuthoringDocument(IEnumerable<PresentationGraphNode> nodes, IEnumerable<PresentationGraphEdge> edges)
    {
        Nodes = Array.AsReadOnly((nodes ?? throw new ArgumentNullException(nameof(nodes))).ToArray());
        Edges = Array.AsReadOnly((edges ?? throw new ArgumentNullException(nameof(edges))).ToArray());
        Validate(Array.Empty<string>(), requireKnownProperties: false);
    }
    public IReadOnlyList<PresentationGraphNode> Nodes { get; }
    public IReadOnlyList<PresentationGraphEdge> Edges { get; }
    public void Validate(IEnumerable<string> properties, bool requireKnownProperties = true)
    {
        HashSet<string> ids = Nodes.Select(value => value.NodeId).ToHashSet(StringComparer.Ordinal);
        HashSet<string> known = properties.ToHashSet(StringComparer.Ordinal);
        if (Nodes.Count == 0 || Nodes.Count(value => value.Kind == PresentationGraphNodeKind.Root) != 1 || ids.Count != Nodes.Count)
            throw new ArgumentException("Presentation graph requires unique nodes and exactly one Root.");
        if (Nodes.Any(value => string.IsNullOrWhiteSpace(value.NodeId) || !float.IsFinite(value.X) || !float.IsFinite(value.Y)))
            throw new ArgumentException("Presentation graph nodes require identities and finite positions.");
        if (requireKnownProperties && Nodes.Where(value => value.Kind == PresentationGraphNodeKind.Property).Any(value => !known.Contains(value.PropertyName)))
            throw new ArgumentException("Presentation graph contains an unknown property leaf.");
        if (Edges.Any(value => value.SourceNodeId == value.TargetNodeId || !ids.Contains(value.SourceNodeId) || !ids.Contains(value.TargetNodeId)) || Edges.Distinct().Count() != Edges.Count)
            throw new ArgumentException("Presentation graph edges must be unique, non-self and reference known nodes.");
        var outgoing = Edges.GroupBy(value => value.SourceNodeId).ToDictionary(value => value.Key, value => value.Select(item => item.TargetNodeId).ToArray(), StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal); var visited = new HashSet<string>(StringComparer.Ordinal);
        bool Visit(string id) { if (!visiting.Add(id)) return false; if (!visited.Contains(id) && outgoing.GetValueOrDefault(id, Array.Empty<string>()).Any(next => !Visit(next))) return false; visiting.Remove(id); visited.Add(id); return true; }
        if (Nodes.Any(value => !visited.Contains(value.NodeId) && !Visit(value.NodeId))) throw new ArgumentException("Presentation graph cannot contain cycles.");
    }

    public void ValidateRuntimeCompatibility()
    {
        PresentationGraphNode[] unsupported = Nodes
            .Where(value => value.Enabled && value.Kind is PresentationGraphNodeKind.Delay or PresentationGraphNodeKind.Parallel)
            .ToArray();
        if (unsupported.Length > 0)
            throw new ArgumentException(
                "Enabled Delay/Parallel authoring nodes are not consumed by the current Presentation runtime. " +
                "Keep them disabled as layout notes or express timing through the supported profile duration fields.");
    }
    public static PresentationGraphAuthoringDocument CreateDefault(PresentationProfileAuthoringDocument profile)
    {
        PresentationGraphNode[] leaves = profile.Properties.Keys.Where(value => value != "AuthoringGraphJsonValue")
            .Order(StringComparer.Ordinal).Select((value, index) => new PresentationGraphNode("property-" + index, PresentationGraphNodeKind.Property, value, 350, 50 + index * 90)).ToArray();
        return new PresentationGraphAuthoringDocument(new[] { new PresentationGraphNode("root", PresentationGraphNodeKind.Root, string.Empty, 50, 50) }.Concat(leaves),
            leaves.Select(value => new PresentationGraphEdge("root", value.NodeId)));
    }
}

public static class PresentationGraphAuthoringJson
{
    public static string Serialize(PresentationGraphAuthoringDocument document)
    {
        using var stream = new MemoryStream(); using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject(); writer.WriteStartArray("nodes");
            foreach (PresentationGraphNode node in document.Nodes.OrderBy(value => value.NodeId, StringComparer.Ordinal)) { writer.WriteStartObject(); writer.WriteString("id", node.NodeId); writer.WriteString("kind", node.Kind.ToString()); writer.WriteString("property", node.PropertyName); writer.WriteNumber("x", node.X); writer.WriteNumber("y", node.Y); writer.WriteBoolean("enabled", node.Enabled); writer.WriteEndObject(); }
            writer.WriteEndArray(); writer.WriteStartArray("edges"); foreach (PresentationGraphEdge edge in document.Edges.OrderBy(value => value.SourceNodeId, StringComparer.Ordinal).ThenBy(value => value.TargetNodeId, StringComparer.Ordinal)) { writer.WriteStartObject(); writer.WriteString("source", edge.SourceNodeId); writer.WriteString("target", edge.TargetNodeId); writer.WriteEndObject(); } writer.WriteEndArray(); writer.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }
    public static PresentationGraphAuthoringDocument Deserialize(string json)
    {
        using JsonDocument payload = JsonDocument.Parse(json); JsonElement root = payload.RootElement;
        return new PresentationGraphAuthoringDocument(root.GetProperty("nodes").EnumerateArray().Select(value => new PresentationGraphNode(value.GetProperty("id").GetString()!, Enum.Parse<PresentationGraphNodeKind>(value.GetProperty("kind").GetString()!), value.GetProperty("property").GetString() ?? string.Empty, value.GetProperty("x").GetSingle(), value.GetProperty("y").GetSingle(), value.GetProperty("enabled").GetBoolean())),
            root.GetProperty("edges").EnumerateArray().Select(value => new PresentationGraphEdge(value.GetProperty("source").GetString()!, value.GetProperty("target").GetString()!)));
    }
}

public sealed class PresentationProfileAuthoringDocument : IAuthoringDocument
{
    public PresentationProfileAuthoringDocument(string contentId, string resourceClass,
        IReadOnlyDictionary<string, PresentationAuthoringValue> properties)
    {
        ContentId = string.IsNullOrWhiteSpace(contentId) ? throw new ArgumentException("ContentId is required.") : contentId;
        ResourceClass = string.IsNullOrWhiteSpace(resourceClass) ? throw new ArgumentException("Resource class is required.") : resourceClass;
        Properties = new Dictionary<string, PresentationAuthoringValue>(properties ?? throw new ArgumentNullException(nameof(properties)), StringComparer.Ordinal);
        if (Properties.Keys.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Presentation property names cannot be empty.");
    }
    public string ContentId { get; }
    public int SchemaVersion => 1;
    public string ResourceClass { get; }
    public IReadOnlyDictionary<string, PresentationAuthoringValue> Properties { get; }
    public IReadOnlyList<string> Dependencies => Array.Empty<string>();
    public void WriteCanonical(Utf8JsonWriter writer)
    {
        writer.WriteStartObject(); writer.WriteString("contentId", ContentId); writer.WriteNumber("schemaVersion", SchemaVersion); writer.WriteString("resourceClass", ResourceClass); writer.WriteStartObject("properties");
        foreach ((string name, PresentationAuthoringValue value) in Properties.OrderBy(value => value.Key, StringComparer.Ordinal)) { writer.WriteStartObject(name); writer.WriteString("kind", value.Kind.ToString()); writer.WriteString("value", value.Value); writer.WriteEndObject(); }
        writer.WriteEndObject(); writer.WriteEndObject();
    }
}

public static class PresentationProfileAuthoringJson
{
    public static string Serialize(PresentationProfileAuthoringDocument document) { using var stream = new MemoryStream(); using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true })) document.WriteCanonical(writer); return System.Text.Encoding.UTF8.GetString(stream.ToArray()); }
    public static PresentationProfileAuthoringDocument Deserialize(string json)
    {
        using JsonDocument payload = JsonDocument.Parse(json); JsonElement root = payload.RootElement;
        Dictionary<string, PresentationAuthoringValue> properties = root.GetProperty("properties").EnumerateObject().ToDictionary(value => value.Name, value => new PresentationAuthoringValue(Enum.Parse<PresentationAuthoringValueKind>(value.Value.GetProperty("kind").GetString()!), value.Value.GetProperty("value").GetString() ?? string.Empty), StringComparer.Ordinal);
        return new PresentationProfileAuthoringDocument(root.GetProperty("contentId").GetString()!, root.GetProperty("resourceClass").GetString()!, properties);
    }
}
