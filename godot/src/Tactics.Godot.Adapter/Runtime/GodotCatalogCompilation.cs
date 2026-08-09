using Godot;
using Tactics.Application.Content;
using Tactics.Core.Content;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>
/// Godot objects remain in this adapter-only registry and never enter ContentSnapshot.
/// </summary>
public sealed class GodotResourceRegistry
{
    private readonly IReadOnlyDictionary<ContentId, Resource> _resources;

    internal GodotResourceRegistry(IReadOnlyDictionary<ContentId, Resource> resources)
    {
        _resources = resources;
    }

    public IReadOnlyDictionary<ContentId, Resource> Resources => _resources;

    public T Get<T>(ContentId contentId) where T : Resource
    {
        if (!_resources.TryGetValue(contentId, out Resource? resource))
            throw new KeyNotFoundException($"Missing content '{contentId}'.");
        return resource as T ?? throw new InvalidCastException($"Content '{contentId}' is not {typeof(T).Name}.");
    }

}

public sealed record GodotCatalogCompilation(
    ContentSnapshot Snapshot,
    GodotResourceRegistry Resources);

public static class GodotCatalogCompiler
{
    public static GodotCatalogCompilation Compile(GodotResourceCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        catalog.Validate();

        ContentCompileResult contentResult = new ContentCompiler().Compile(catalog.Entries.Select(entry =>
            new ContentDraft(
                catalog.ToContentId(entry),
                entry.ResourceTypeIdValue,
                entry.SchemaVersion,
                entry.ReferenceContentIds.Select(value => new ContentId(value)))));
        if (!contentResult.Succeeded)
        {
            string diagnostics = string.Join(System.Environment.NewLine, contentResult.Diagnostics.Select(item => $"{item.Code}: {item.Message}"));
            throw new InvalidOperationException($"Godot content catalog failed Application compilation:{System.Environment.NewLine}{diagnostics}");
        }

        var resources = new Dictionary<ContentId, Resource>();
        foreach (GodotResourceEntry entry in catalog.Entries)
        {
            ContentId contentId = catalog.ToContentId(entry);
            Resource? resource = ResourceLoader.Load(entry.ResourceLocator);
            if (resource is null)
                throw new InvalidOperationException($"Cannot load Resource for '{contentId}'.");
            if (!resources.TryAdd(contentId, resource))
                throw new InvalidOperationException($"Duplicate ContentId '{contentId}'.");
        }

        return new GodotCatalogCompilation(contentResult.Snapshot!, new GodotResourceRegistry(resources));
    }
}
