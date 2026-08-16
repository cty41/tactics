using System.Security.Cryptography;
using System.Globalization;
using System.Text;

namespace Tactics.Application.Presentation;

/// <summary>
/// Stores engine-neutral authoring coordinates for a presentation graph node.
/// </summary>
public readonly record struct PresentationNodePosition(float X, float Y)
{
    public bool IsFinite => float.IsFinite(X) && float.IsFinite(Y);
}

/// <summary>
/// Represents one engine-neutral presentation graph node at the editor mutation boundary.
/// </summary>
public sealed record PresentationNodeDocument(
    string NodeId,
    string NodeTypeId,
    string Kind,
    string Cue,
    bool Enabled,
    PresentationNodePosition Position);

/// <summary>
/// Represents one stable directed presentation graph edge.
/// </summary>
public sealed record PresentationEdgeDocument(string EdgeId, string SourceNodeId, string TargetNodeId);

/// <summary>
/// Immutable graph document whose normalized SHA-256 revision fences editor and agent writes.
/// </summary>
public sealed class PresentationGraphDocument
{
    public PresentationGraphDocument(
        int schemaVersion,
        IEnumerable<PresentationNodeDocument> nodes,
        IEnumerable<PresentationEdgeDocument> edges)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(edges);
        if (schemaVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(schemaVersion));

        PresentationNodeDocument[] nodeArray = nodes.ToArray();
        PresentationEdgeDocument[] edgeArray = edges.ToArray();
        Validate(nodeArray, edgeArray);
        SchemaVersion = schemaVersion;
        Nodes = Array.AsReadOnly(nodeArray);
        Edges = Array.AsReadOnly(edgeArray);
        Revision = ComputeRevision(schemaVersion, nodeArray, edgeArray);
    }

    public int SchemaVersion { get; }
    public IReadOnlyList<PresentationNodeDocument> Nodes { get; }
    public IReadOnlyList<PresentationEdgeDocument> Edges { get; }
    public string Revision { get; }

    internal PresentationGraphDocument WithNodes(IEnumerable<PresentationNodeDocument> nodes) =>
        new(SchemaVersion, nodes, Edges);

    private static void Validate(
        IReadOnlyList<PresentationNodeDocument> nodes,
        IReadOnlyList<PresentationEdgeDocument> edges)
    {
        if (nodes.Count == 0)
            throw new ArgumentException("Presentation graph must contain at least one node.", nameof(nodes));
        if (nodes.Any(node => string.IsNullOrWhiteSpace(node.NodeId) ||
                              string.IsNullOrWhiteSpace(node.NodeTypeId) ||
                              string.IsNullOrWhiteSpace(node.Kind) ||
                              !node.Position.IsFinite))
        {
            throw new ArgumentException(
                "Presentation nodes require stable IDs, types, kinds, and finite authoring positions.",
                nameof(nodes));
        }

        var nodeIds = nodes.Select(node => node.NodeId).ToHashSet(StringComparer.Ordinal);
        if (nodeIds.Count != nodes.Count)
            throw new ArgumentException("Presentation node IDs must be unique.", nameof(nodes));
        if (nodes.Select(node => node.Position).Distinct().Count() != nodes.Count)
            throw new ArgumentException("Presentation node authoring positions must not overlap.", nameof(nodes));
        if (edges.Any(edge => string.IsNullOrWhiteSpace(edge.EdgeId) ||
                              !nodeIds.Contains(edge.SourceNodeId) ||
                              !nodeIds.Contains(edge.TargetNodeId)))
        {
            throw new ArgumentException("Presentation edges require stable IDs and existing endpoints.", nameof(edges));
        }
        if (edges.Select(edge => edge.EdgeId).Distinct(StringComparer.Ordinal).Count() != edges.Count)
            throw new ArgumentException("Presentation edge IDs must be unique.", nameof(edges));
    }

    private static string ComputeRevision(
        int schemaVersion,
        IReadOnlyList<PresentationNodeDocument> nodes,
        IReadOnlyList<PresentationEdgeDocument> edges)
    {
        var canonical = new StringBuilder();
        Append(canonical, schemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));
        foreach (PresentationNodeDocument node in nodes)
        {
            Append(canonical, node.NodeId);
            Append(canonical, node.NodeTypeId);
            Append(canonical, node.Kind);
            Append(canonical, node.Cue);
            Append(canonical, node.Enabled ? "1" : "0");
            Append(canonical, node.Position.X.ToString("R", CultureInfo.InvariantCulture));
            Append(canonical, node.Position.Y.ToString("R", CultureInfo.InvariantCulture));
        }
        foreach (PresentationEdgeDocument edge in edges)
        {
            Append(canonical, edge.EdgeId);
            Append(canonical, edge.SourceNodeId);
            Append(canonical, edge.TargetNodeId);
        }

        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()));
        return "sha256:" + Convert.ToHexString(digest).ToLowerInvariant();
    }

    private static void Append(StringBuilder target, string? value)
    {
        string normalized = value ?? string.Empty;
        target.Append(normalized.Length)
            .Append(':')
            .Append(normalized)
            .Append(';');
    }
}
