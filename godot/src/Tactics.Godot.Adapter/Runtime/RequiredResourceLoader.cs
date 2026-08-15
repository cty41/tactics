using Godot;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>Loads a required typed runtime Resource and preserves actionable type diagnostics.</summary>
internal static class RequiredResourceLoader
{
    public static T Load<T>(string path, string context) where T : Resource
    {
        Resource? raw = ResourceLoader.Load(path, string.Empty, ResourceLoader.CacheMode.Ignore);
        if (raw is T typed)
            return typed;

        string actualType = raw?.GetClass() ?? "<missing>";
        string scriptPath = string.Empty;
        if (raw is not null)
        {
            if (raw.Get("script").Obj is Script script)
                scriptPath = script.ResourcePath;
        }
        throw new InvalidOperationException(
            $"{context}: resource '{path}' expected '{typeof(T).Name}', actual '{actualType}', script '{scriptPath}'.");
    }
}
