using System.Text.Json;

namespace Tactics.Application.Authoring;

/// <summary>
/// Stores editor-only node positions without duplicating semantic graph edges.
/// </summary>
/// <remarks>
/// Runtime compilers ignore this metadata. Stable node identities are derived by each
/// authoring document so layout can participate in revisions and Undo without becoming
/// a second source of gameplay truth.
/// </remarks>
public sealed class AuthoringGraphLayout
{
    public AuthoringGraphLayout(IEnumerable<AuthoringGraphNodeLayout>? nodes = null, int layoutSchemaVersion = 1)
    {
        if (layoutSchemaVersion < 1) throw new ArgumentOutOfRangeException(nameof(layoutSchemaVersion));
        AuthoringGraphNodeLayout[] values = (nodes ?? Array.Empty<AuthoringGraphNodeLayout>()).ToArray();
        if (values.Select(value => value.NodeId).Distinct(StringComparer.Ordinal).Count() != values.Length)
            throw new ArgumentException("Authoring graph layout node identities must be unique.", nameof(nodes));
        LayoutSchemaVersion = layoutSchemaVersion;
        Nodes = Array.AsReadOnly(values.OrderBy(value => value.NodeId, StringComparer.Ordinal).ToArray());
    }

    public int LayoutSchemaVersion { get; }
    public IReadOnlyList<AuthoringGraphNodeLayout> Nodes { get; }

    internal void WriteCanonical(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteNumber("layoutSchemaVersion", LayoutSchemaVersion);
        writer.WriteStartArray("nodes");
        foreach (AuthoringGraphNodeLayout node in Nodes)
        {
            writer.WriteStartObject();
            writer.WriteString("nodeId", node.NodeId);
            writer.WriteNumber("x", node.X);
            writer.WriteNumber("y", node.Y);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    internal static AuthoringGraphLayout Read(JsonElement value) => new(
        value.TryGetProperty("nodes", out JsonElement nodes)
            ? nodes.EnumerateArray().Select(node => new AuthoringGraphNodeLayout(
                node.GetProperty("nodeId").GetString()!, node.GetProperty("x").GetDouble(),
                node.GetProperty("y").GetDouble()))
            : Array.Empty<AuthoringGraphNodeLayout>(),
        value.TryGetProperty("layoutSchemaVersion", out JsonElement version) ? version.GetInt32() : 1);
}

/// <summary>Defines one editor graph node position in logical pixels.</summary>
public sealed record AuthoringGraphNodeLayout
{
    public AuthoringGraphNodeLayout(string nodeId, double x, double y)
    {
        if (string.IsNullOrWhiteSpace(nodeId)) throw new ArgumentException("NodeId is required.", nameof(nodeId));
        if (!double.IsFinite(x) || !double.IsFinite(y)) throw new ArgumentOutOfRangeException(nameof(x), "Graph coordinates must be finite.");
        NodeId = nodeId;
        X = x;
        Y = y;
    }

    public string NodeId { get; }
    public double X { get; }
    public double Y { get; }
}

/// <summary>Serializes editor graph layout metadata for Godot Resource adapters.</summary>
public static class AuthoringGraphLayoutJson
{
    public static string Serialize(AuthoringGraphLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
            layout.WriteCanonical(writer);
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    public static AuthoringGraphLayout Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new AuthoringGraphLayout();
        using JsonDocument document = JsonDocument.Parse(json);
        return AuthoringGraphLayout.Read(document.RootElement);
    }
}
