#if TOOLS
using Godot;

namespace Tactics.Godot.Adapter.Editor;

public sealed record WorkbenchResourceSaveRequest(Resource Resource, string Path, Action<Resource> Validate, long? AssignedUid = null);
public sealed record WorkbenchFileMutationRequest(string Path, byte[]? Bytes);

internal sealed record WorkbenchPathSnapshot(string Path, string AbsolutePath, byte[]? Bytes, Resource? GodotResource, long Uid)
{
    public static WorkbenchPathSnapshot Capture(string path)
    {
        string absolute = ProjectSettings.GlobalizePath(path); string uidText = ResourceUid.PathToUid(path);
        long uid = uidText.StartsWith("uid://", StringComparison.Ordinal) ? ResourceUid.TextToId(uidText) : ResourceUid.InvalidId;
        byte[]? bytes = File.Exists(absolute) ? File.ReadAllBytes(absolute) : null;
        bool godotResourcePath = path.EndsWith(".tres", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".tscn", StringComparison.OrdinalIgnoreCase);
        Resource? resource = bytes is not null && godotResourcePath
            ? ResourceLoader.Load(path, string.Empty, ResourceLoader.CacheMode.Ignore)?.Duplicate(true)
            : null;
        if (bytes is not null && godotResourcePath && resource is null)
            throw new InvalidOperationException($"Cannot transactionally snapshot typed Godot Resource '{path}'.");
        return new WorkbenchPathSnapshot(path, absolute, bytes, resource, uid);
    }
    public void Restore()
    {
        if (Bytes is null)
        {
            if (File.Exists(AbsolutePath)) File.Delete(AbsolutePath);
        }
        else
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(AbsolutePath)!);
            if (GodotResource is not null)
            {
                Error error = ResourceSaver.Save(GodotResource, Path);
                if (error != Error.Ok) throw new InvalidOperationException($"Could not restore Resource snapshot '{Path}': {error}.");
            }
            else File.WriteAllBytes(AbsolutePath, Bytes);
        }
        if (Uid != ResourceUid.InvalidId)
        {
            if (ResourceUid.HasId(Uid)) ResourceUid.SetId(Uid, Path);
            else ResourceUid.AddId(Uid, Path);
            if (Bytes is not null) _ = ResourceSaver.SetUid(Path, Uid);
        }
        else
        {
            string uidText = ResourceUid.PathToUid(Path);
            if (uidText.StartsWith("uid://", StringComparison.Ordinal))
            {
                long currentUid = ResourceUid.TextToId(uidText);
                if (ResourceUid.HasId(currentUid)) ResourceUid.RemoveId(currentUid);
            }
        }
    }
}

public enum WorkbenchResourceSaveCheckpoint
{
    Validated,
    Saved,
    FileMutated,
    Reloaded
}

public sealed class WorkbenchTransactionReceipt
{
    private readonly WorkbenchPathSnapshot[] _before;
    private readonly WorkbenchPathSnapshot[] _after;

    internal WorkbenchTransactionReceipt(WorkbenchPathSnapshot[] before, WorkbenchPathSnapshot[] after)
    {
        _before = before;
        _after = after;
    }

    public void RestoreBefore() => Restore(_before);
    public void RestoreAfter() => Restore(_after);

    private static void Restore(IEnumerable<WorkbenchPathSnapshot> snapshots)
    {
        foreach (WorkbenchPathSnapshot snapshot in snapshots.Reverse()) snapshot.Restore();
    }
}

public static class WorkbenchResourceBatchSaveService
{
    public static void SaveWithRollback(
        IEnumerable<WorkbenchResourceSaveRequest> requests,
        Action<WorkbenchResourceSaveCheckpoint, int>? faultInjection = null)
        => SaveWithRollback(requests, Array.Empty<WorkbenchFileMutationRequest>(), faultInjection);

    public static void SaveWithRollback(
        IEnumerable<WorkbenchResourceSaveRequest> requests,
        IEnumerable<WorkbenchFileMutationRequest> fileMutations,
        Action<WorkbenchResourceSaveCheckpoint, int>? faultInjection = null)
        => _ = SaveWithRollbackAndReceipt(requests, fileMutations, faultInjection);

    public static WorkbenchTransactionReceipt SaveWithRollbackAndReceipt(
        IEnumerable<WorkbenchResourceSaveRequest> requests,
        IEnumerable<WorkbenchFileMutationRequest> fileMutations,
        Action<WorkbenchResourceSaveCheckpoint, int>? faultInjection = null)
    {
        WorkbenchResourceSaveRequest[] values = (requests ?? throw new ArgumentNullException(nameof(requests))).ToArray();
        WorkbenchFileMutationRequest[] mutations = (fileMutations ?? throw new ArgumentNullException(nameof(fileMutations))).ToArray();
        if (values.Length == 0 && mutations.Length == 0)
            throw new ArgumentException("A transaction requires Resource saves or file mutations.", nameof(requests));
        string[] paths = values.Select(value => value.Path).Concat(mutations.Select(value => value.Path)).ToArray();
        if (paths.Distinct(StringComparer.Ordinal).Count() != paths.Length)
            throw new ArgumentException("A Resource batch requires unique paths.", nameof(requests));
        if (mutations.Any(value => value.Bytes is not null &&
            (value.Path.EndsWith(".tres", StringComparison.OrdinalIgnoreCase) ||
             value.Path.EndsWith(".tscn", StringComparison.OrdinalIgnoreCase))))
            throw new ArgumentException("Typed Godot Resources must be written through ResourceSaver requests.", nameof(fileMutations));
        WorkbenchPathSnapshot[] snapshots = paths.Select(WorkbenchPathSnapshot.Capture).ToArray();
        try
        {
            for (int index = 0; index < values.Length; index++)
            {
                WorkbenchResourceSaveRequest request = values[index];
                if (snapshots[index].Uid == ResourceUid.InvalidId && request.AssignedUid is long assignedUid)
                {
                    if (ResourceUid.HasId(assignedUid)) ResourceUid.SetId(assignedUid, request.Path);
                    else ResourceUid.AddId(assignedUid, request.Path);
                }
            }
            for (int index = 0; index < values.Length; index++)
            {
                values[index].Validate(values[index].Resource);
                faultInjection?.Invoke(WorkbenchResourceSaveCheckpoint.Validated, index);
            }
            for (int index = 0; index < values.Length; index++)
            {
                WorkbenchResourceSaveRequest request = values[index];
                WorkbenchPathSnapshot snapshot = snapshots[index];
                Error error = ResourceSaver.Save(request.Resource, request.Path); if (error != Error.Ok) throw new InvalidOperationException($"ResourceSaver failed for '{request.Path}': {error}.");
                long uid = snapshot.Uid != ResourceUid.InvalidId ? snapshot.Uid : request.AssignedUid ?? ResourceUid.InvalidId;
                if (uid != ResourceUid.InvalidId)
                {
                    if (ResourceUid.HasId(uid)) ResourceUid.SetId(uid, request.Path);
                    else ResourceUid.AddId(uid, request.Path);
                    if (ResourceSaver.SetUid(request.Path, uid) != Error.Ok) throw new InvalidOperationException($"Could not assign UID for '{request.Path}'.");
                }
                faultInjection?.Invoke(WorkbenchResourceSaveCheckpoint.Saved, index);
            }
            for (int index = 0; index < mutations.Length; index++)
            {
                WorkbenchFileMutationRequest mutation = mutations[index];
                string absolutePath = ProjectSettings.GlobalizePath(mutation.Path);
                if (mutation.Bytes is null)
                {
                    if (File.Exists(absolutePath)) File.Delete(absolutePath);
                    string uidText = ResourceUid.PathToUid(mutation.Path);
                    if (uidText.StartsWith("uid://", StringComparison.Ordinal))
                    {
                        long uid = ResourceUid.TextToId(uidText);
                        if (ResourceUid.HasId(uid)) ResourceUid.RemoveId(uid);
                    }
                }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
                    File.WriteAllBytes(absolutePath, mutation.Bytes);
                }
                faultInjection?.Invoke(WorkbenchResourceSaveCheckpoint.FileMutated, index);
            }
            for (int index = 0; index < values.Length; index++)
            {
                WorkbenchResourceSaveRequest request = values[index];
                Resource? reloaded = ResourceLoader.Load(request.Path, string.Empty, ResourceLoader.CacheMode.Ignore); if (reloaded is null) throw new InvalidOperationException($"Saved Resource cannot be reloaded: {request.Path}."); request.Validate(reloaded);
                faultInjection?.Invoke(WorkbenchResourceSaveCheckpoint.Reloaded, index);
            }
            return new WorkbenchTransactionReceipt(snapshots, paths.Select(WorkbenchPathSnapshot.Capture).ToArray());
        }
        catch
        {
            foreach (WorkbenchPathSnapshot snapshot in snapshots.Reverse()) snapshot.Restore();
            throw;
        }
    }

}
#endif
