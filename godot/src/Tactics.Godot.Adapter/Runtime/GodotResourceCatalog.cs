using Godot;
using Tactics.Core.Content;

namespace Tactics.Godot.Adapter.Runtime;

[GlobalClass]
public partial class GodotResourceCatalog : Resource
{
    [Export] public GodotResourceEntry[] Entries { get; set; } = Array.Empty<GodotResourceEntry>();

    public void Validate()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (GodotResourceEntry entry in Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.ContentIdValue))
                throw new InvalidOperationException("Content catalog contains an empty ContentId.");
            if (string.IsNullOrWhiteSpace(entry.ResourcePathValue))
                throw new InvalidOperationException($"Content '{entry.ContentIdValue}' has an empty Resource path.");
            if (!ids.Add(entry.ContentIdValue))
                throw new InvalidOperationException($"Duplicate ContentId '{entry.ContentIdValue}'.");
        }
    }

    public bool TryGet(string contentId, out Resource? resource)
    {
        resource = null;
        if (string.IsNullOrWhiteSpace(contentId))
            return false;

        foreach (GodotResourceEntry entry in Entries)
        {
            if (!string.Equals(entry.ContentIdValue, contentId, StringComparison.Ordinal))
                continue;
            resource = ResourceLoader.Load(entry.ResourcePathValue);
            return resource is not null;
        }

        return false;
    }

    public ContentId ToContentId(GodotResourceEntry entry) => new(entry.ContentIdValue);
}
