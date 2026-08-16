namespace Tactics.Application.Content;

public sealed record ContentSchemaDefinition(
    string ResourceTypeId,
    int MinimumSupportedVersion,
    int CurrentVersion);

/// <summary>
/// Defines the engine-neutral content types and schema versions accepted by the runtime compiler.
/// </summary>
public sealed class ContentSchemaCatalog
{
    private readonly IReadOnlyDictionary<string, ContentSchemaDefinition> _definitions;

    public ContentSchemaCatalog(IEnumerable<ContentSchemaDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        var indexed = new Dictionary<string, ContentSchemaDefinition>(StringComparer.Ordinal);
        foreach (ContentSchemaDefinition definition in definitions)
        {
            string resourceTypeId = definition.ResourceTypeId?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(resourceTypeId))
                throw new ArgumentException("ResourceTypeId cannot be empty.", nameof(definitions));
            if (definition.MinimumSupportedVersion < 1 || definition.CurrentVersion < definition.MinimumSupportedVersion)
                throw new ArgumentException($"Invalid schema range for '{resourceTypeId}'.", nameof(definitions));
            if (!indexed.TryAdd(resourceTypeId, definition with { ResourceTypeId = resourceTypeId }))
                throw new ArgumentException($"Duplicate ResourceTypeId '{resourceTypeId}'.", nameof(definitions));
        }

        _definitions = indexed;
    }

    public static ContentSchemaCatalog RuntimeV1 { get; } = new(new[]
    {
        new ContentSchemaDefinition("ai-profile", 1, 1),
        new ContentSchemaDefinition("ai", 1, 1),
        new ContentSchemaDefinition("battle-board", 1, 1),
        new ContentSchemaDefinition("battle-layout", 1, 1),
        new ContentSchemaDefinition("buff", 1, 1),
        new ContentSchemaDefinition("encounter", 1, 1),
        new ContentSchemaDefinition("event", 1, 1),
        new ContentSchemaDefinition("rest", 1, 1),
        new ContentSchemaDefinition("run-map", 1, 1),
        new ContentSchemaDefinition("store", 1, 1),
        new ContentSchemaDefinition("treasure", 1, 1),
        new ContentSchemaDefinition("run", 1, 1),
        new ContentSchemaDefinition("item", 1, 1),
        new ContentSchemaDefinition("packed-scene", 1, 1),
        new ContentSchemaDefinition("presentation", 1, 1),
        new ContentSchemaDefinition("skill", 1, 1),
        new ContentSchemaDefinition("unit", 1, 1)
    });

    public bool TryGet(string resourceTypeId, out ContentSchemaDefinition definition) =>
        _definitions.TryGetValue(resourceTypeId, out definition!);
}
