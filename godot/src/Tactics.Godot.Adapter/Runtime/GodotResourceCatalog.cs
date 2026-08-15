using Godot;
using Tactics.Core.Content;

namespace Tactics.Godot.Adapter.Runtime;

[Tool]
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
            ContentId contentId = ToContentId(entry);
            if (string.IsNullOrWhiteSpace(entry.ResourceTypeIdValue))
                throw new InvalidOperationException($"Content '{contentId}' has an empty ResourceTypeId.");
            if (string.IsNullOrWhiteSpace(entry.ResourceUidValue) || !entry.ResourceUidValue.StartsWith("uid://", StringComparison.Ordinal))
                throw new InvalidOperationException($"Content '{contentId}' has an invalid Resource UID.");
            if (string.IsNullOrWhiteSpace(entry.DiagnosticPathValue))
                throw new InvalidOperationException($"Content '{contentId}' has an empty diagnostic path.");
            if (entry.SchemaVersion < 1)
                throw new InvalidOperationException($"Content '{contentId}' has an invalid SchemaVersion.");
            if (!ids.Add(contentId.Value))
                throw new InvalidOperationException($"Duplicate ContentId '{contentId}'.");

            long uid = ResourceUid.TextToId(entry.ResourceUidValue);
            if (uid == ResourceUid.InvalidId || !ResourceUid.HasId(uid))
                throw new InvalidOperationException($"Content '{contentId}' has an unregistered Resource UID '{entry.ResourceUidValue}'.");
            string resolvedPath = ResourceUid.GetIdPath(uid);
            if (!string.Equals(resolvedPath, entry.DiagnosticPathValue, StringComparison.Ordinal))
                throw new InvalidOperationException($"Content '{contentId}' UID resolves to '{resolvedPath}', expected '{entry.DiagnosticPathValue}'.");
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
            resource = ResourceLoader.Load(entry.ResourceLocator);
            return resource is not null;
        }

        return false;
    }

    public ContentId ToContentId(GodotResourceEntry entry) => new(entry.ContentIdValue);
}
