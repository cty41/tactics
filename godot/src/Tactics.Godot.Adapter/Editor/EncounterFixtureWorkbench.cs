#if TOOLS
using Godot;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

[Tool]
public partial class EncounterFixtureWorkbench : VBoxContainer
{
    private const string CatalogPath = "res://content/ContentCatalog.tres";
    private const string CatalogScriptPath = "res://src/Tactics.Godot.Adapter/Runtime/GodotResourceCatalog.cs";
    private static readonly string[] ScenarioNames = ["N1", "N2", "N3", "Elite Charger", "Elite Poison Caster"];
    private GodotAiEncounterFixture? _fixture;
    private Label? _status;
    private int _catalogLoadAttempts;
    private bool _initialized;

    public override void _Ready() => CallDeferred(nameof(InitializeWorkbench));

    public void InitializeWorkbench()
    {
        if (_initialized || !IsInsideTree()) return;
        EditorResourceLoadResult<GodotResourceCatalog> result = ReloadSafeEditorResourceLoader.Load<GodotResourceCatalog>(
            CatalogPath, CatalogScriptPath, "Entries");
        if (ReloadSafeEditorResourceLoader.RetryDeferred(this, MethodName.InitializeWorkbench,
            ref _catalogLoadAttempts, result, "Encounter fixture workbench"))
            return;
        result.Resource!.Validate();
        _initialized = true;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        var toolbar = new HBoxContainer();
        var scenarios = new OptionButton { CustomMinimumSize = new Vector2(220, 0) };
        foreach (string name in ScenarioNames) scenarios.AddItem(name);
        scenarios.ItemSelected += index => RunSafely(() => _fixture!.SelectScenario((int)index), $"Loaded {ScenarioNames[index]}.");
        toolbar.AddChild(scenarios);
        Button step = new() { Text = "Step AI Turn" };
        step.Pressed += () => RunSafely(() => _fixture!.ExecuteSingleTurn(), "Executed one canonical AI turn.");
        toolbar.AddChild(step);
        Button round = new() { Text = "Run Round" };
        round.Pressed += () => RunSafely(() => _fixture!.ExecuteCurrentRound(), "Executed one canonical round.");
        toolbar.AddChild(round);
        Button reset = new() { Text = "Reset Seed" };
        reset.Pressed += () => RunSafely(() => _fixture!.ResetCurrentScenario(), "Restored deterministic seed.");
        toolbar.AddChild(reset);
        Button validate = new() { Text = "Validate Encounter Assets" };
        validate.Pressed += ValidateAssets;
        toolbar.AddChild(validate);
        AddChild(toolbar);

        _status = new Label { Text = "Fixture uses canonical Encounter/Layout/AI/Skill resources." };
        AddChild(_status);
        var frame = new SubViewportContainer
        {
            Stretch = true, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill
        };
        var viewport = new SubViewport
        {
            Size = new Vector2I(GodotAiEncounterFixture.CanvasWidth, GodotAiEncounterFixture.CanvasHeight),
            Size2DOverride = new Vector2I(GodotAiEncounterFixture.CanvasWidth, GodotAiEncounterFixture.CanvasHeight),
            Size2DOverrideStretch = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always
        };
        _fixture = new GodotAiEncounterFixture();
        viewport.AddChild(_fixture);
        frame.AddChild(viewport);
        AddChild(frame);
    }

    public override void _ExitTree()
    {
        _initialized = false;
        _catalogLoadAttempts = 0;
        _fixture = null;
        _status = null;
    }

    public static void ValidateCanonicalAssets()
    {
        GodotResourceCatalog catalog = ResourceLoader.Load<GodotResourceCatalog>("res://content/ContentCatalog.tres", string.Empty, ResourceLoader.CacheMode.Ignore)
            ?? throw new InvalidOperationException("Canonical content catalog is missing.");
        catalog.Validate();
        var entries = catalog.Entries.ToDictionary(entry => entry.ContentIdValue, StringComparer.Ordinal);
        foreach (string id in new[] { "encounter.pure-run.n1", "encounter.pure-run.n2", "encounter.pure-run.n3" })
        {
            if (!entries.TryGetValue(id, out GodotResourceEntry? entry)) throw new InvalidOperationException($"Missing encounter {id}.");
            EncounterDefinitionResource encounter = ResourceLoader.Load<EncounterDefinitionResource>(entry.DiagnosticPathValue, string.Empty, ResourceLoader.CacheMode.Ignore)
                ?? throw new InvalidOperationException($"Encounter cannot be loaded: {id}.");
            if (encounter.MonsterUnitContentIds.Length == 0 || encounter.MonsterUnitContentIds.Length != encounter.MonsterAiContentIds.Length)
                throw new InvalidOperationException($"Encounter has invalid unit/AI bindings: {id}.");
            if (!entries.ContainsKey(encounter.LayoutContentId)) throw new InvalidOperationException($"Encounter layout is missing: {encounter.LayoutContentId}.");
        }
    }

    private void ValidateAssets() => RunSafely(ValidateCanonicalAssets, "Encounter, layout, unit and AI references validated.");

    private void RunSafely(Action action, string success)
    {
        try { action(); SetStatus(success, false); }
        catch (Exception exception) { SetStatus(exception.Message, true); }
    }

    private void SetStatus(string text, bool error)
    {
        if (_status is null) return;
        _status.Text = text;
        _status.Modulate = error ? Colors.IndianRed : Colors.LightGreen;
    }
}
#endif
