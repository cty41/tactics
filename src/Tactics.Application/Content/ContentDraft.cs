using Tactics.Core.Content;

namespace Tactics.Application.Content;

/// <summary>
/// Engine-neutral input compiled from an editor/exporter DTO into a runtime snapshot.
/// </summary>
public sealed class ContentDraft
{
    public ContentDraft(
        ContentId contentId,
        string? resourceTypeId,
        int schemaVersion,
        IEnumerable<ContentId>? references = null,
        IReadOnlyDictionary<string, string>? properties = null)
    {
        ContentId = contentId;
        ResourceTypeId = resourceTypeId?.Trim() ?? string.Empty;
        SchemaVersion = schemaVersion;
        References = references?.ToArray() ?? Array.Empty<ContentId>();
        Properties = properties is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(properties, StringComparer.Ordinal);
    }

    public ContentId ContentId { get; }
    public string ResourceTypeId { get; }
    public int SchemaVersion { get; }
    public IReadOnlyList<ContentId> References { get; }
    public IReadOnlyDictionary<string, string> Properties { get; }
}
