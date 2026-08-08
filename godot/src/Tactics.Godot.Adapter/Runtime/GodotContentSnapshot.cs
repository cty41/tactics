using Godot;
using Tactics.Core.Content;

namespace Tactics.Godot.Adapter.Runtime;

public sealed class GodotContentSnapshot
{
    private readonly IReadOnlyDictionary<ContentId, Resource> _resources;

    private GodotContentSnapshot(IReadOnlyDictionary<ContentId, Resource> resources)
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

    public static GodotContentSnapshot Compile(GodotResourceCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        catalog.Validate();
        var resources = new Dictionary<ContentId, Resource>();
        foreach (GodotResourceEntry entry in catalog.Entries)
        {
            ContentId contentId = catalog.ToContentId(entry);
            Resource? resource = ResourceLoader.Load(entry.ResourcePathValue);
            if (resource is null)
                throw new InvalidOperationException($"Cannot load Resource for '{contentId}'.");
            if (!resources.TryAdd(contentId, resource))
                throw new InvalidOperationException($"Duplicate ContentId '{contentId}'.");
        }

        return new GodotContentSnapshot(resources);
    }
}
