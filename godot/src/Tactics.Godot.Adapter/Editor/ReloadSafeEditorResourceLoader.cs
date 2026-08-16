#if TOOLS
using Godot;

namespace Tactics.Godot.Adapter.Editor;

internal enum EditorResourceLoadState
{
    Ready,
    ReloadPending,
    InvalidResource
}

internal readonly record struct EditorResourceLoadResult<T>(
    EditorResourceLoadState State,
    T? Resource,
    string Diagnostic) where T : Resource;

/// <summary>Distinguishes a transient C# reload gap from a malformed editor resource.</summary>
internal static class ReloadSafeEditorResourceLoader
{
    public const int MaximumDeferredAttempts = 120;

    public static EditorResourceLoadResult<T> Load<T>(
        string path,
        string expectedScriptPath,
        params string[] requiredProperties) where T : Resource
    {
        Resource? raw = ResourceLoader.Load(path, string.Empty, ResourceLoader.CacheMode.Ignore);
        if (raw is null)
            return Invalid<T>($"Resource is missing: {path}");

        HashSet<string> propertyNames = raw.GetPropertyList()
            .Select(property => property["name"].AsString())
            .ToHashSet(StringComparer.Ordinal);
        string[] missingProperties = requiredProperties.Where(value => !propertyNames.Contains(value)).ToArray();
        string scriptPath = GetScriptPath(raw);
        if (missingProperties.Length > 0)
            return Invalid<T>($"Resource '{path}' is missing serialized properties: {string.Join(", ", missingProperties)}. Script='{scriptPath}'.");
        if (!string.Equals(scriptPath, expectedScriptPath, StringComparison.Ordinal))
            return Invalid<T>($"Resource '{path}' uses script '{scriptPath}', expected '{expectedScriptPath}'.");
        if (raw is T typed)
            return new EditorResourceLoadResult<T>(EditorResourceLoadState.Ready, typed, string.Empty);

        return new EditorResourceLoadResult<T>(EditorResourceLoadState.ReloadPending, null,
            $"Resource '{path}' still has script '{scriptPath}' but instantiated as '{raw.GetClass()}', expected '{typeof(T).Name}'.");
    }

    public static bool RetryDeferred<T>(
        Node owner,
        StringName method,
        ref int attempts,
        EditorResourceLoadResult<T> result,
        string context) where T : Resource
    {
        if (result.State == EditorResourceLoadState.Ready)
        {
            attempts = 0;
            return false;
        }
        if (result.State == EditorResourceLoadState.InvalidResource)
            throw new InvalidOperationException(result.Diagnostic);

        attempts++;
        if (attempts == 1)
            GD.PushWarning($"[Tactics Tooling] {context} is waiting for C# Resource types after assembly reload.");
        if (attempts >= MaximumDeferredAttempts)
            throw new InvalidOperationException($"{context} did not recover after {MaximumDeferredAttempts} deferred frames. {result.Diagnostic}");
        owner.CallDeferred(method);
        return true;
    }

    private static EditorResourceLoadResult<T> Invalid<T>(string diagnostic) where T : Resource =>
        new(EditorResourceLoadState.InvalidResource, null, diagnostic);

    private static string GetScriptPath(Resource resource)
    {
        Variant scriptValue = resource.Get("script");
        return scriptValue.Obj is Script script ? script.ResourcePath : string.Empty;
    }
}
#endif
