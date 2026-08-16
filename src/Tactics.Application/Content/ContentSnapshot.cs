using Tactics.Core.Content;

namespace Tactics.Application.Content;

public sealed record ContentSnapshotEntry(
    ContentId ContentId,
    string ResourceTypeId,
    int SchemaVersion,
    IReadOnlyList<ContentId> References,
    IReadOnlyDictionary<string, string> Properties);

/// <summary>
/// Immutable, engine-neutral runtime content. It must never retain Unity or Godot objects.
/// </summary>
public sealed class ContentSnapshot
{
    private readonly IReadOnlyDictionary<ContentId, ContentSnapshotEntry> _entries;

    internal ContentSnapshot(IEnumerable<ContentSnapshotEntry> entries)
    {
        _entries = entries.ToDictionary(entry => entry.ContentId);
    }

    public IReadOnlyDictionary<ContentId, ContentSnapshotEntry> Entries => _entries;

    public ContentSnapshotEntry Get(ContentId contentId) =>
        _entries.TryGetValue(contentId, out ContentSnapshotEntry? entry)
            ? entry
            : throw new KeyNotFoundException($"Content '{contentId}' is not present in this snapshot.");
}
