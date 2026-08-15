#if TOOLS
using Godot;

namespace Tactics.Godot.Adapter.Editor;

/// <summary>Persists an authored resource without losing the last reloadable artifact.</summary>
public static class WorkbenchResourceSaveService
{
    public static void SaveWithRollback<TResource>(TResource resource, string path, Action<TResource> validate)
        where TResource : Resource
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(validate);
        validate(resource);

        string absolutePath = ProjectSettings.GlobalizePath(path);
        byte[]? previousBytes = File.Exists(absolutePath) ? File.ReadAllBytes(absolutePath) : null;
        string uidText = ResourceUid.PathToUid(path);
        long uid = uidText.StartsWith("uid://", StringComparison.Ordinal)
            ? ResourceUid.TextToId(uidText)
            : ResourceUid.InvalidId;
        try
        {
            Error saveError = ResourceSaver.Save(resource, path);
            if (saveError != Error.Ok)
                throw new InvalidOperationException($"ResourceSaver failed for '{path}': {saveError}.");
            if (uid != ResourceUid.InvalidId)
            {
                Error uidError = ResourceSaver.SetUid(path, uid);
                if (uidError != Error.Ok)
                    throw new InvalidOperationException($"Could not preserve UID for '{path}': {uidError}.");
            }

            TResource? reloaded = ResourceLoader.Load<TResource>(path, string.Empty, ResourceLoader.CacheMode.Ignore);
            if (reloaded is null)
                throw new InvalidOperationException($"Saved resource cannot be reloaded: {path}.");
            validate(reloaded);
        }
        catch
        {
            if (previousBytes is null)
            {
                if (File.Exists(absolutePath)) File.Delete(absolutePath);
            }
            else File.WriteAllBytes(absolutePath, previousBytes);
            throw;
        }
    }
}
#endif
