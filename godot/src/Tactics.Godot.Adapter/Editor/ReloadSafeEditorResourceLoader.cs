#if TOOLS
using Godot;
using Tactics.Godot.Adapter.Runtime;

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
        // IgnoreDeep is required across a C# assembly reload: Ignore refreshes only the outer
        // .tres while nested PackedScene/Script dependencies may still point at the old assembly.
        Resource? raw = ResourceLoader.Load(path, string.Empty, ResourceLoader.CacheMode.IgnoreDeep);
        if (raw is null)
            return Invalid<T>($"Resource is missing: {path}");

        HashSet<string> propertyNames = raw.GetPropertyList()
            .Select(property => property["name"].AsString())
            .ToHashSet(StringComparer.Ordinal);
        string scriptPath = GetScriptPath(raw);
        return Inspect<T>(raw, path, expectedScriptPath, propertyNames, scriptPath, requiredProperties);
    }

    internal static EditorResourceLoadResult<T> Inspect<T>(
        Resource raw,
        string path,
        string expectedScriptPath,
        IReadOnlySet<string> propertyNames,
        string scriptPath,
        params string[] requiredProperties) where T : Resource
    {
        string[] missingProperties = requiredProperties.Where(value => !propertyNames.Contains(value)).ToArray();
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
        string context) where T : Resource =>
        RetryDeferred(owner, method, ref attempts, result.State, result.Diagnostic, context);

    internal static bool RetryDeferred(
        Node owner,
        StringName method,
        ref int attempts,
        EditorResourceLoadState state,
        string diagnostic,
        string context)
    {
        if (state == EditorResourceLoadState.Ready)
        {
            attempts = 0;
            return false;
        }
        if (state == EditorResourceLoadState.InvalidResource)
            throw new InvalidOperationException(diagnostic);

        attempts++;
        if (attempts == 1)
            GD.PushWarning($"[Tactics Tooling] {context} is waiting for C# Resource types after assembly reload.");
        if (attempts >= MaximumDeferredAttempts)
            throw new InvalidOperationException($"{context} did not recover after {MaximumDeferredAttempts} deferred frames. {diagnostic}");
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

internal readonly record struct AuthoringEditorReadinessResult(
    EditorResourceLoadState State,
    string Diagnostic);

/// <summary>Guards Main Screen and bridge readiness on the C# Resources used immediately by authoring UI.</summary>
internal static class AuthoringEditorReadinessProbe
{
    private const string CatalogPath = "res://content/ContentCatalog.tres";
    private const string CatalogScriptPath = "res://src/Tactics.Godot.Adapter/Runtime/GodotResourceCatalog.cs";
    private const string UnitScriptPath = "res://src/Tactics.Godot.Adapter/Runtime/UnitDefinitionResource.cs";
    private const string SkillPresentationScriptPath = "res://src/Tactics.Godot.Adapter/Runtime/SkillPresentationResource.cs";
    private const string StatusPresentationScriptPath = "res://src/Tactics.Godot.Adapter/Runtime/StatusPresentationResource.cs";
    private const string UnitPresentationScriptPath = "res://src/Tactics.Godot.Adapter/Runtime/StandardUnitPresentationResource.cs";

    public static AuthoringEditorReadinessResult Probe()
    {
        EditorResourceLoadResult<GodotResourceCatalog> catalog = ReloadSafeEditorResourceLoader.Load<GodotResourceCatalog>(
            CatalogPath, CatalogScriptPath, "Entries");
        if (catalog.State != EditorResourceLoadState.Ready)
            return From(catalog);

        AuthoringEditorReadinessResult[] resources =
        [
            ProbeEntry<UnitDefinitionResource>(catalog.Resource!, "unit.pure-run.amazon", UnitScriptPath,
                "ContentIdValue", "ActorScene"),
            ProbeEntry<UnitDefinitionResource>(catalog.Resource!, "unit.pure-run.goat-aoe", UnitScriptPath,
                "ContentIdValue", "ActorScene"),
            ProbeEntry<SkillPresentationResource>(catalog.Resource!, "presentation.skill.mage.fireball",
                SkillPresentationScriptPath, "ContentIdValue", "ProgrammaticKind"),
            ProbeEntry<StatusPresentationResource>(catalog.Resource!, "presentation.status.standard-v1",
                StatusPresentationScriptPath, "ContentIdValue", "MaximumVisibleStatuses"),
            ProbeEntry<StandardUnitPresentationResource>(catalog.Resource!, "presentation.unit.standard-v1",
                UnitPresentationScriptPath, "ContentIdValue", "IdleDuration")
        ];
        return resources.FirstOrDefault(value => value.State != EditorResourceLoadState.Ready,
            new AuthoringEditorReadinessResult(EditorResourceLoadState.Ready, string.Empty));
    }

    private static AuthoringEditorReadinessResult ProbeEntry<T>(
        GodotResourceCatalog catalog,
        string contentId,
        string scriptPath,
        params string[] requiredProperties) where T : Resource
    {
        GodotResourceEntry? entry = catalog.Entries.FirstOrDefault(value => value.ContentIdValue == contentId);
        if (entry is null)
            return new AuthoringEditorReadinessResult(EditorResourceLoadState.InvalidResource,
                $"Authoring readiness Catalog entry is missing: {contentId}.");
        return From(ReloadSafeEditorResourceLoader.Load<T>(entry.DiagnosticPathValue, scriptPath, requiredProperties));
    }

    private static AuthoringEditorReadinessResult From<T>(EditorResourceLoadResult<T> result) where T : Resource =>
        new(result.State, result.Diagnostic);
}
#endif
