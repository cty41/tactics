#if TOOLS
namespace Tactics.Godot.Adapter.Editor;

public sealed record AuthoringPreviewCleanupDiagnostic(
    string Page,
    string ContentId,
    int ActiveTweens,
    int TemporaryNodes,
    string State,
    DateTimeOffset TimestampUtc);

public static class AuthoringEditorDiagnostics
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, AuthoringPreviewCleanupDiagnostic> Cleanup = new(StringComparer.Ordinal);
    private static int _dirtyDocuments;
    private static int _queuedLifecycle;

    public static void RecordCleanup(string page, string contentId, int activeTweens, int temporaryNodes, string state)
    {
        lock (Gate) Cleanup[page] = new AuthoringPreviewCleanupDiagnostic(page, contentId, activeTweens,
            temporaryNodes, state, DateTimeOffset.UtcNow);
    }
    public static void RecordWorkspace(int dirtyDocuments, int queuedLifecycle)
    {
        lock (Gate) { _dirtyDocuments = dirtyDocuments; _queuedLifecycle = queuedLifecycle; }
    }
    public static (int DirtyDocuments, int QueuedLifecycle, IReadOnlyList<AuthoringPreviewCleanupDiagnostic> Cleanup) Snapshot()
    {
        lock (Gate) return (_dirtyDocuments, _queuedLifecycle,
            Array.AsReadOnly(Cleanup.Values.OrderBy(value => value.Page, StringComparer.Ordinal).ToArray()));
    }
}
#endif
