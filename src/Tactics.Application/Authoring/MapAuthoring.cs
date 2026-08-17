using System.Text.Json;
using Tactics.Core.Content;
using Tactics.Core.Runs;

namespace Tactics.Application.Authoring;

public sealed record MapAuthoringNode(
    string NodeId,
    int Layer,
    PureRunNodeKind Kind,
    string ContentId,
    string Title,
    float Lane);

public sealed record MapAuthoringConnection(string FromNodeId, string ToNodeId);

public sealed class MapAuthoringDocument : IAuthoringDocument
{
    public MapAuthoringDocument(
        string contentId,
        int layoutVersion,
        IEnumerable<MapAuthoringNode> nodes,
        IEnumerable<MapAuthoringConnection> connections)
    {
        if (string.IsNullOrWhiteSpace(contentId)) throw new ArgumentException("ContentId is required.", nameof(contentId));
        if (layoutVersion < 2) throw new ArgumentOutOfRangeException(nameof(layoutVersion));
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(connections);
        ContentId = contentId;
        LayoutVersion = layoutVersion;
        Nodes = Array.AsReadOnly(nodes.ToArray());
        Connections = Array.AsReadOnly(connections.ToArray());
        _ = ToCoreDefinition();
    }

    public string ContentId { get; }
    public int SchemaVersion => 1;
    public int LayoutVersion { get; }
    public IReadOnlyList<MapAuthoringNode> Nodes { get; }
    public IReadOnlyList<MapAuthoringConnection> Connections { get; }
    public IReadOnlyList<string> Dependencies => Array.AsReadOnly(
        Nodes.Select(value => value.ContentId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());

    public PureRunMapDefinition ToCoreDefinition() => new(
        new ContentId(ContentId),
        LayoutVersion,
        Nodes.Select(value => new PureRunMapNodeDefinition(
            value.NodeId, value.Layer, value.Kind, new ContentId(value.ContentId), value.Title, value.Lane)),
        Connections.Select(value => new PureRunMapConnectionDefinition(value.FromNodeId, value.ToNodeId)));

    public void WriteCanonical(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("contentId", ContentId);
        writer.WriteNumber("schemaVersion", SchemaVersion);
        writer.WriteNumber("layoutVersion", LayoutVersion);
        writer.WriteStartArray("nodes");
        foreach (MapAuthoringNode node in Nodes.OrderBy(value => value.NodeId, StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("nodeId", node.NodeId);
            writer.WriteNumber("layer", node.Layer);
            writer.WriteString("kind", node.Kind.ToString());
            writer.WriteString("contentId", node.ContentId);
            writer.WriteString("title", node.Title);
            writer.WriteNumber("lane", node.Lane);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteStartArray("connections");
        foreach (MapAuthoringConnection edge in Connections
                     .OrderBy(value => value.FromNodeId, StringComparer.Ordinal)
                     .ThenBy(value => value.ToNodeId, StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("from", edge.FromNodeId);
            writer.WriteString("to", edge.ToNodeId);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}

public sealed record AddMapNodeOperation(MapAuthoringNode Node) : AuthoringOperation;
public sealed record UpdateMapNodeOperation(MapAuthoringNode Node) : AuthoringOperation;
public sealed record RemoveMapNodeOperation(string NodeId) : AuthoringOperation;
public sealed record AddMapConnectionOperation(MapAuthoringConnection Connection) : AuthoringOperation;
public sealed record RemoveMapConnectionOperation(MapAuthoringConnection Connection) : AuthoringOperation;

public sealed record MapAuthoringMutationResult(
    MapAuthoringDocument Document,
    bool Succeeded,
    bool Changed,
    IReadOnlyList<AuthoringDiagnostic> Diagnostics);

public static class MapAuthoringValidator
{
    public static IReadOnlyList<AuthoringDiagnostic> Validate(MapAuthoringDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var diagnostics = new List<AuthoringDiagnostic>();
        MapAuthoringNode[] starts = document.Nodes.Where(value => value.Layer == 0).ToArray();
        MapAuthoringNode[] bosses = document.Nodes.Where(value => value.Kind == PureRunNodeKind.Boss).ToArray();
        if (starts.Length != 1)
            diagnostics.Add(Error("map.start_count", $"Map requires exactly one layer-zero start node; found {starts.Length}."));
        if (bosses.Length != 1)
            diagnostics.Add(Error("map.boss_count", $"Map requires exactly one Boss node; found {bosses.Length}."));
        foreach (MapAuthoringNode node in document.Nodes)
        {
            if (node.Layer < 0) diagnostics.Add(Error("map.negative_layer", $"Node '{node.NodeId}' has a negative layer.", node.NodeId));
            if (!float.IsFinite(node.Lane)) diagnostics.Add(Error("map.invalid_lane", $"Node '{node.NodeId}' has a non-finite lane.", node.NodeId));
            if (string.IsNullOrWhiteSpace(node.NodeId) || string.IsNullOrWhiteSpace(node.ContentId) || string.IsNullOrWhiteSpace(node.Title))
                diagnostics.Add(Error("map.node_field_missing", "Map nodes require NodeId, ContentId and title.", node.NodeId));
        }

        Dictionary<string, MapAuthoringNode> byId = document.Nodes.ToDictionary(value => value.NodeId, StringComparer.Ordinal);
        foreach (MapAuthoringConnection edge in document.Connections)
        {
            if (edge.FromNodeId == edge.ToNodeId)
                diagnostics.Add(Error("map.self_loop", $"Node '{edge.FromNodeId}' connects to itself.", edge.FromNodeId));
            if (byId.TryGetValue(edge.FromNodeId, out MapAuthoringNode? from) &&
                byId.TryGetValue(edge.ToNodeId, out MapAuthoringNode? to) && to.Layer <= from.Layer)
                diagnostics.Add(Error("map.non_forward_edge", $"Connection '{edge.FromNodeId}' -> '{edge.ToNodeId}' must advance to a later layer.", edge.FromNodeId));
        }

        if (starts.Length == 1)
        {
            string startId = starts[0].NodeId;
            if (document.Connections.Any(value => value.ToNodeId == startId))
                diagnostics.Add(Error("map.start_has_incoming", "The start node cannot have incoming connections.", startId));
            var reachable = new HashSet<string>(StringComparer.Ordinal) { startId };
            var queue = new Queue<string>();
            queue.Enqueue(startId);
            while (queue.TryDequeue(out string? current))
            {
                foreach (string next in document.Connections.Where(value => value.FromNodeId == current).Select(value => value.ToNodeId))
                    if (reachable.Add(next)) queue.Enqueue(next);
            }
            foreach (MapAuthoringNode node in document.Nodes.Where(value => !reachable.Contains(value.NodeId)))
                diagnostics.Add(Error("map.unreachable_node", $"Node '{node.NodeId}' is unreachable from the start.", node.NodeId));
        }
        return Array.AsReadOnly(diagnostics.ToArray());
    }

    public static void ValidateOrThrow(MapAuthoringDocument document)
    {
        IReadOnlyList<AuthoringDiagnostic> diagnostics = Validate(document);
        AuthoringDiagnostic[] errors = diagnostics.Where(value => value.Severity == AuthoringDiagnosticSeverity.Error).ToArray();
        if (errors.Length > 0) throw new InvalidOperationException(string.Join("; ", errors.Select(value => value.Message)));
    }

    private static AuthoringDiagnostic Error(string code, string message, string? path = null) =>
        new(code, AuthoringDiagnosticSeverity.Error, message, path);
}

public sealed class MapAuthoringMutationService
{
    public MapAuthoringMutationResult Apply(MapAuthoringDocument source, AuthoringChangeSet changeSet)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(changeSet);
        string sourceRevision = AuthoringRevision.Compute(source);
        if (changeSet.Kind != AuthoringDocumentKind.Map ||
            !string.Equals(source.ContentId, changeSet.ContentId, StringComparison.Ordinal))
            return Failed(source, "map.identity_mismatch", "ChangeSet does not target this map.");
        if (!string.Equals(sourceRevision, changeSet.ExpectedRevision, StringComparison.Ordinal))
            return Failed(source, "map.revision_conflict", $"Expected '{changeSet.ExpectedRevision}', actual '{sourceRevision}'.");

        var nodes = source.Nodes.ToList();
        var edges = source.Connections.ToList();
        foreach (AuthoringOperation operation in changeSet.Operations)
        {
            switch (operation)
            {
                case AddMapNodeOperation add when nodes.Any(value => value.NodeId == add.Node.NodeId):
                    return Failed(source, "map.duplicate_node", $"Node '{add.Node.NodeId}' already exists.");
                case AddMapNodeOperation add:
                    nodes.Add(add.Node);
                    break;
                case UpdateMapNodeOperation update:
                {
                    int index = nodes.FindIndex(value => value.NodeId == update.Node.NodeId);
                    if (index < 0) return Failed(source, "map.node_not_found", $"Node '{update.Node.NodeId}' does not exist.");
                    nodes[index] = update.Node;
                    break;
                }
                case RemoveMapNodeOperation remove:
                {
                    int removed = nodes.RemoveAll(value => value.NodeId == remove.NodeId);
                    if (removed == 0) return Failed(source, "map.node_not_found", $"Node '{remove.NodeId}' does not exist.");
                    edges.RemoveAll(value => value.FromNodeId == remove.NodeId || value.ToNodeId == remove.NodeId);
                    break;
                }
                case AddMapConnectionOperation add when add.Connection.FromNodeId == add.Connection.ToNodeId:
                    return Failed(source, "map.self_loop", "Map connections cannot be self loops.");
                case AddMapConnectionOperation add when edges.Contains(add.Connection):
                    return Failed(source, "map.duplicate_connection", "Map connection already exists.");
                case AddMapConnectionOperation add:
                    edges.Add(add.Connection);
                    break;
                case RemoveMapConnectionOperation remove when !edges.Remove(remove.Connection):
                    return Failed(source, "map.connection_not_found", "Map connection does not exist.");
                case RemoveMapConnectionOperation:
                    break;
                default:
                    return Failed(source, "map.unsupported_operation", $"Unsupported operation '{operation.GetType().Name}'.");
            }
        }

        try
        {
            var candidate = new MapAuthoringDocument(source.ContentId, source.LayoutVersion, nodes, edges);
            bool changed = !string.Equals(sourceRevision, AuthoringRevision.Compute(candidate), StringComparison.Ordinal);
            return new MapAuthoringMutationResult(candidate, true, changed, Array.Empty<AuthoringDiagnostic>());
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or FormatException)
        {
            return Failed(source, "map.invalid_candidate", exception.Message);
        }
    }

    private static MapAuthoringMutationResult Failed(MapAuthoringDocument source, string code, string message) =>
        new(source, false, false, new[]
        {
            new AuthoringDiagnostic(code, AuthoringDiagnosticSeverity.Error, message)
        });
}

public static class MapAuthoringJson
{
    public static string Serialize(MapAuthoringDocument document, bool indented = true)
    {
        ArgumentNullException.ThrowIfNull(document);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = indented }))
            document.WriteCanonical(writer);
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    public static MapAuthoringDocument Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Map authoring JSON is required.", nameof(json));
        using JsonDocument parsed = JsonDocument.Parse(json);
        JsonElement root = parsed.RootElement;
        string contentId = root.GetProperty("contentId").GetString()
            ?? throw new InvalidOperationException("Map JSON ContentId is missing.");
        int layoutVersion = root.GetProperty("layoutVersion").GetInt32();
        MapAuthoringNode[] nodes = root.GetProperty("nodes").EnumerateArray().Select(value =>
            new MapAuthoringNode(
                value.GetProperty("nodeId").GetString() ?? string.Empty,
                value.GetProperty("layer").GetInt32(),
                Enum.Parse<PureRunNodeKind>(value.GetProperty("kind").GetString() ?? string.Empty, false),
                value.GetProperty("contentId").GetString() ?? string.Empty,
                value.GetProperty("title").GetString() ?? string.Empty,
                value.GetProperty("lane").GetSingle())).ToArray();
        MapAuthoringConnection[] connections = root.GetProperty("connections").EnumerateArray().Select(value =>
            new MapAuthoringConnection(
                value.GetProperty("from").GetString() ?? string.Empty,
                value.GetProperty("to").GetString() ?? string.Empty)).ToArray();
        return new MapAuthoringDocument(contentId, layoutVersion, nodes, connections);
    }
}
