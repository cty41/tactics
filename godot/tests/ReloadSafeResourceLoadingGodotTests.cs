using GdUnit4;
using Godot;
using Tactics.Godot.Adapter.Editor;
using Tactics.Godot.Adapter.Runtime;
using static GdUnit4.Assertions;

namespace Tactics.Godot.Tests;

[TestSuite]
public class ReloadSafeResourceLoadingGodotTests
{
    private const string CatalogPath = "res://content/ContentCatalog.tres";
    private const string CatalogScriptPath = "res://src/Tactics.Godot.Adapter/Runtime/GodotResourceCatalog.cs";

    [TestCase]
    [RequireGodotRuntime]
    public void EditorLoaderAcceptsReadyTypedCatalog()
    {
        EditorResourceLoadResult<GodotResourceCatalog> result = ReloadSafeEditorResourceLoader.Load<GodotResourceCatalog>(
            CatalogPath, CatalogScriptPath, "Entries");

        AssertThat((int)result.State).IsEqual((int)EditorResourceLoadState.Ready);
        AssertThat(result.Resource).IsNotNull();
        AssertThat(result.Diagnostic).IsEmpty();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void EditorLoaderRejectsScriptAndSchemaDrift()
    {
        EditorResourceLoadResult<GodotResourceCatalog> wrongScript = ReloadSafeEditorResourceLoader.Load<GodotResourceCatalog>(
            CatalogPath, "res://wrong.cs", "Entries");
        EditorResourceLoadResult<GodotResourceCatalog> missingProperty = ReloadSafeEditorResourceLoader.Load<GodotResourceCatalog>(
            CatalogPath, CatalogScriptPath, "MissingProperty");

        AssertThat((int)wrongScript.State).IsEqual((int)EditorResourceLoadState.InvalidResource);
        AssertThat(wrongScript.Diagnostic).Contains("expected 'res://wrong.cs'");
        AssertThat((int)missingProperty.State).IsEqual((int)EditorResourceLoadState.InvalidResource);
        AssertThat(missingProperty.Diagnostic).Contains("MissingProperty");
    }

    [TestCase]
    [RequireGodotRuntime]
    public void EditorLoaderClassifiesMatchingBaseResourceAsReloadPending()
    {
        const string path = "res://content/units/PureRunAmazon.tres";
        const string scriptPath = "res://src/Tactics.Godot.Adapter/Runtime/UnitDefinitionResource.cs";
        EditorResourceLoadResult<UnitDefinitionResource> result =
            ReloadSafeEditorResourceLoader.Inspect<UnitDefinitionResource>(new Resource(), path, scriptPath,
                new HashSet<string>(StringComparer.Ordinal) { "ContentIdValue", "ActorScene" }, scriptPath,
                "ContentIdValue", "ActorScene");

        AssertThat((int)result.State).IsEqual((int)EditorResourceLoadState.ReloadPending);
        AssertThat(result.Resource).IsNull();
        AssertThat(result.Diagnostic).Contains("instantiated as 'Resource'");
    }

    [TestCase]
    [RequireGodotRuntime]
    public void AuthoringReadinessProbeAcceptsEveryImmediateEditorResourceType()
    {
        AuthoringEditorReadinessResult result = AuthoringEditorReadinessProbe.Probe();

        AssertThat((int)result.State).IsEqual((int)EditorResourceLoadState.Ready);
        AssertThat(result.Diagnostic).IsEmpty();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void PresentationPreviewBuildsToolSafeRuntimeActorShell()
    {
        EditorResourceLoadResult<UnitDefinitionResource> unit = ReloadSafeEditorResourceLoader.Load<UnitDefinitionResource>(
            "res://content/units/PureRunAmazon.tres",
            "res://src/Tactics.Godot.Adapter/Runtime/UnitDefinitionResource.cs", "ContentIdValue", "ActorScene");

        GodotUnitActor actor = PresentationProfilePreviewStage.CreatePreviewActor(unit.Resource!);

        AssertThat(actor.Body).IsNotNull();
        AssertThat(actor.Shadow).IsNotNull();
        actor.Free();
    }

    [TestCase]
    public void DirectAuthoringResourcesAreToolEnabled()
    {
        Type[] types =
        [
            typeof(UnitDefinitionResource),
            typeof(SkillPresentationResource),
            typeof(StatusPresentationResource),
            typeof(StandardUnitPresentationResource)
        ];

        foreach (Type type in types)
            AssertThat(type.IsDefined(typeof(ToolAttribute), inherit: false))
                .OverrideFailureMessage(type.Name)
                .IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void RuntimeLoaderKeepsTypedFailFastBoundary()
    {
        GodotResourceCatalog catalog = RequiredResourceLoader.Load<GodotResourceCatalog>(CatalogPath, "test catalog");
        AssertThat(catalog.Entries.Length).IsGreater(0);

        try
        {
            _ = RequiredResourceLoader.Load<PureRunMapResource>(CatalogPath, "wrong runtime type");
            AssertThat(false).IsTrue();
        }
        catch (InvalidOperationException exception)
        {
            AssertThat(exception.Message).Contains("expected 'PureRunMapResource'");
            AssertThat(exception.Message).Contains("GodotResourceCatalog");
        }
    }
}
