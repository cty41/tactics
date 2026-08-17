#if TOOLS
using Godot;
using Tactics.Application.Presentation;
using Tactics.Application.Battle;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Units;
using Tactics.Core.Statuses;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

internal readonly record struct PresentationPreviewActorLoadResult(
    EditorResourceLoadState State,
    UnitDefinitionResource? Caster,
    UnitDefinitionResource? Target,
    string Diagnostic);

internal sealed partial class PresentationProfilePreviewStage : Control
{
    private const string CatalogPath = "res://content/ContentCatalog.tres";
    private const string CatalogScriptPath = "res://src/Tactics.Godot.Adapter/Runtime/GodotResourceCatalog.cs";
    private const string UnitScriptPath = "res://src/Tactics.Godot.Adapter/Runtime/UnitDefinitionResource.cs";
    private readonly Dictionary<UnitInstanceId, GodotUnitActor> _actors = new();
    private GodotBattlePresentationPlayer? _player;
    private GodotUnitActor? _caster;
    private GodotUnitActor? _target;
    private Label? _diagnostic;
    private Node? _actorParent;
    private Resource? _profile;
    private int _actorLoadAttempts;
    private bool _actorInitializationFailed;
    internal Func<PresentationPreviewActorLoadResult> ActorResourceProbe { get; set; } = ProbeActorResources;

    public int TemporaryNodeCount => _player?.TransientNodeCount ?? 0;
    public int ActiveTweenCount => (_player?.ActiveTweenCount ?? 0) + (_caster?.StatusOverlay?.ActiveTweenCount ?? 0);
    public int ActorCount => _actors.Count;
    public bool IsPlaying => _player?.IsPlaying ?? false;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var background = new ColorRect { Color = new Color("26313b") };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);
        _diagnostic = new Label { Position = new Vector2(24, 18), Text = "Presentation preview idle." };
        background.AddChild(_diagnostic);
        _actorParent = background;
        _player = new GodotBattlePresentationPlayer();
        background.AddChild(_player);
        CallDeferred(nameof(InitializeActors));
    }

    public void Configure(Resource profile)
    {
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        Stop();
        _diagnostic!.Text = profile switch
        {
            SkillPresentationResource skill => $"Skill scenario: {skill.SkillBranch} / {skill.ProgrammaticKind}",
            StandardUnitPresentationResource => "Unit scenario: melee action + hit recovery",
            StatusPresentationResource => "Status scenario: runtime overlay visibility + pulse",
            _ => "Unsupported presentation profile."
        };
    }

    public void Play(float speed, string scope)
    {
        if (_profile is null || _player is null || _caster is null || _target is null)
            throw new InvalidOperationException("Presentation preview is not configured.");
        if (_profile is StatusPresentationResource statusProfile)
        {
            _caster.SetStatuses(
            [
                new BattleUiStatusSnapshot(new ContentId("buff.poison"), StatusEffectKind.Poison, StatusPolarity.Harmful, 3, 2),
                new BattleUiStatusSnapshot(new ContentId("buff.burning"), StatusEffectKind.Burning, StatusPolarity.Harmful, 2, 1),
                new BattleUiStatusSnapshot(new ContentId("buff.frozen"), StatusEffectKind.Frozen, StatusPolarity.Harmful, 1, 1),
                new BattleUiStatusSnapshot(new ContentId("buff.slow"), StatusEffectKind.Slow, StatusPolarity.Harmful, 2, 1),
                new BattleUiStatusSnapshot(new ContentId("buff.fear"), StatusEffectKind.Fear, StatusPolarity.Harmful, 1, 1)
            ], statusProfile.MaximumVisibleStatuses, statusProfile.PulseDuration / speed);
            _diagnostic!.Text = $"Playing status overlay: maximum {statusProfile.MaximumVisibleStatuses}, pulse {statusProfile.PulseDuration:0.###}s at {speed:0.#}×.";
            AuthoringEditorDiagnostics.RecordCleanup("Presentation", ContentId(), ActiveTweenCount, TemporaryNodeCount, "playing status");
            return;
        }
        StandardUnitPresentationResource unitProfile = _profile as StandardUnitPresentationResource
            ?? LoadPresentation<StandardUnitPresentationResource>("presentation.unit.standard-v1");
        _caster.ConfigurePresentation(unitProfile);
        _target.ConfigurePresentation(unitProfile);
        _player.Configure(unitProfile);
        _player.ConfigureSkills(_profile is SkillPresentationResource skill ? [skill] : Array.Empty<SkillPresentationResource>());
        _player.SetSpeed(speed);
        var casterId = new UnitInstanceId(_caster.GetMeta("preview_id").AsString());
        var targetId = new UnitInstanceId(_target.GetMeta("preview_id").AsString());
        ContentId? skillId = _profile is SkillPresentationResource skillProfile
            ? new ContentId("skill." + skillProfile.SkillBranch + ".lv1")
            : null;
        PresentationCueKind kind = _profile is SkillPresentationResource { SkillBranch: var branch } &&
            !branch.Contains("thrust", StringComparison.Ordinal) ? PresentationCueKind.Cast : PresentationCueKind.Melee;
        var cue = new BattlePresentationCue(kind, casterId, targetId, skillId,
            new GridPoint(1, 4), new GridPoint(7, 4), Array.Empty<GridPoint>(), [targetId],
            [
                new BattlePresentationMarker(PresentationMarkerKind.Begin, 0),
                new BattlePresentationMarker(PresentationMarkerKind.Release, 1),
                new BattlePresentationMarker(PresentationMarkerKind.Impact, 2),
                new BattlePresentationMarker(PresentationMarkerKind.Recover, 3),
                new BattlePresentationMarker(PresentationMarkerKind.Complete, 4)
            ]);
        var hit = cue with { Kind = PresentationCueKind.Hit, ActorId = targetId, SkillId = skillId,
            Origin = new GridPoint(7, 4), Destination = new GridPoint(7, 4) };
        BattlePresentationCue[] cues = scope switch
        {
            "Action" => [cue],
            "Impact" => [hit],
            _ => [cue, hit]
        };
        _player.Play(new BattlePresentationFrame("WorkbenchProfilePreview", null!, null!, cues, []), _actors);
        AuthoringEditorDiagnostics.RecordCleanup("Presentation", ContentId(), ActiveTweenCount, TemporaryNodeCount, "playing");
        _diagnostic!.Text = $"Playing {scope} current runtime semantics at {speed:0.#}×; active nodes are highlighted in the graph timeline.";
    }

    public void SetPaused(bool paused)
    {
        _player?.SetPaused(paused);
        if (_diagnostic is not null) _diagnostic.Text = paused ? "Paused; last valid frame retained." : "Playback resumed.";
    }

    public void Stop()
    {
        _player?.Clear();
        _caster?.StatusOverlay?.ClearAnimation();
        _caster?.SetStatuses(Array.Empty<BattleUiStatusSnapshot>());
        AuthoringEditorDiagnostics.RecordCleanup("Presentation", ContentId(), ActiveTweenCount, TemporaryNodeCount, "stopped");
        if (_diagnostic is not null)
            _diagnostic.Text = $"Stopped; cleanup tweens={ActiveTweenCount}, temporary nodes={TemporaryNodeCount}.";
    }

    public override void _ExitTree()
    {
        Stop();
        _actors.Clear();
        _caster = null;
        _target = null;
        _actorParent = null;
        _actorLoadAttempts = 0;
        _actorInitializationFailed = false;
    }

    private string ContentId() => _profile?.Get("ContentIdValue").AsString() ?? string.Empty;

    public void InitializeActors()
    {
        if (_actors.Count == 2 || _actorInitializationFailed || _actorParent is null) return;
        try
        {
            PresentationPreviewActorLoadResult result = ActorResourceProbe();
            if (result.State == EditorResourceLoadState.ReloadPending && _diagnostic is not null)
                _diagnostic.Text = "Waiting for C# Resource types used by presentation preview.";
            if (ReloadSafeEditorResourceLoader.RetryDeferred(this, MethodName.InitializeActors,
                    ref _actorLoadAttempts, result.State, result.Diagnostic, "Presentation preview")) return;
            BuildActors(_actorParent, result.Caster!, result.Target!);
            _actorLoadAttempts = 0;
            if (_diagnostic is not null) _diagnostic.Text = "Presentation preview actors ready.";
        }
        catch (Exception error)
        {
            _actorInitializationFailed = true;
            string diagnostic = "Presentation preview initialization failed: " + error.Message;
            if (_diagnostic is not null) _diagnostic.Text = diagnostic;
            GD.PushError("[Tactics Tooling] " + diagnostic);
        }
    }

    internal static PresentationPreviewActorLoadResult ProbeActorResources()
    {
        EditorResourceLoadResult<GodotResourceCatalog> catalogResult =
            ReloadSafeEditorResourceLoader.Load<GodotResourceCatalog>(CatalogPath, CatalogScriptPath, "Entries");
        if (catalogResult.State != EditorResourceLoadState.Ready)
            return new PresentationPreviewActorLoadResult(catalogResult.State, null, null, catalogResult.Diagnostic);
        GodotResourceCatalog catalog = catalogResult.Resource!;
        EditorResourceLoadResult<UnitDefinitionResource> casterResult = LoadUnit("unit.pure-run.amazon", catalog);
        if (casterResult.State != EditorResourceLoadState.Ready)
            return new PresentationPreviewActorLoadResult(casterResult.State, null, null, casterResult.Diagnostic);
        GodotResourceEntry? targetEntry = catalog.Entries.FirstOrDefault(value =>
            value.ResourceTypeIdValue == "unit" && value.ContentIdValue.Contains("goat", StringComparison.Ordinal));
        if (targetEntry is null)
            return new PresentationPreviewActorLoadResult(EditorResourceLoadState.InvalidResource, null, null,
                "Presentation preview Catalog contains no enemy Unit.");
        EditorResourceLoadResult<UnitDefinitionResource> targetResult = LoadUnit(targetEntry.ContentIdValue, catalog);
        return targetResult.State == EditorResourceLoadState.Ready
            ? new PresentationPreviewActorLoadResult(EditorResourceLoadState.Ready, casterResult.Resource,
                targetResult.Resource, string.Empty)
            : new PresentationPreviewActorLoadResult(targetResult.State, null, null, targetResult.Diagnostic);
    }

    private void BuildActors(Node parent, UnitDefinitionResource casterResource, UnitDefinitionResource targetResource)
    {
        var casterId = new UnitInstanceId("presentation.preview.caster");
        var targetId = new UnitInstanceId("presentation.preview.target");
        _caster = CreatePreviewActor(casterResource);
        _target = CreatePreviewActor(targetResource);
        _caster.SetMeta("preview_id", casterId.Value); _target.SetMeta("preview_id", targetId.Value);
        _caster.Position = IsometricBattleBoardLayout.GridToScreen(new GridPoint(1, 4));
        _target.Position = IsometricBattleBoardLayout.GridToScreen(new GridPoint(7, 4));
        _caster.Scale = Vector2.One * .34f; _target.Scale = Vector2.One * .34f;
        parent.AddChild(_caster); parent.AddChild(_target);
        _actors[casterId] = _caster; _actors[targetId] = _target;
    }

    internal static GodotUnitActor CreatePreviewActor(UnitDefinitionResource resource)
    {
        // Non-[Tool] scripts attached to PackedScene roots intentionally do not instantiate as
        // their runtime C# type inside the Editor. Build the preview shell explicitly while still
        // using the production GodotUnitActor.Configure and presentation methods.
        resource.ValidateVisualContract();
        var actor = new GodotUnitActor();
        var shadow = new Sprite2D { Name = "Shadow" };
        var body = new Sprite2D { Name = "Body", ZIndex = 1 };
        actor.Shadow = shadow;
        actor.Body = body;
        actor.AddChild(shadow);
        actor.AddChild(body);
        actor.Configure(resource);
        return actor;
    }

    private static EditorResourceLoadResult<UnitDefinitionResource> LoadUnit(
        string contentId,
        GodotResourceCatalog catalog)
    {
        GodotResourceEntry entry = catalog.Entries.FirstOrDefault(value => value.ContentIdValue == contentId)
            ?? throw new InvalidOperationException($"Presentation preview Catalog entry is missing: {contentId}.");
        return ReloadSafeEditorResourceLoader.Load<UnitDefinitionResource>(entry.DiagnosticPathValue,
            UnitScriptPath, "ContentIdValue", "ActorScene");
    }
    private static T LoadPresentation<T>(string contentId) where T : Resource => LoadByCatalog<T>(contentId);
    private static T LoadByCatalog<T>(string contentId, GodotResourceCatalog? supplied = null) where T : Resource
    {
        GodotResourceCatalog catalog = supplied ?? LoadCatalog();
        GodotResourceEntry entry = catalog.Entries.Single(value => value.ContentIdValue == contentId);
        return ResourceLoader.Load<T>(entry.DiagnosticPathValue, string.Empty, ResourceLoader.CacheMode.IgnoreDeep)
            ?? throw new InvalidOperationException($"Preview Resource '{contentId}' is not {typeof(T).Name}.");
    }
    private static GodotResourceCatalog LoadCatalog() =>
        ResourceLoader.Load<GodotResourceCatalog>(CatalogPath, string.Empty, ResourceLoader.CacheMode.Ignore)
        ?? throw new InvalidOperationException("Presentation preview Catalog is missing.");
}
#endif
