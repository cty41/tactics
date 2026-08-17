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

internal sealed partial class PresentationProfilePreviewStage : Control
{
    private readonly Dictionary<UnitInstanceId, GodotUnitActor> _actors = new();
    private GodotBattlePresentationPlayer? _player;
    private GodotUnitActor? _caster;
    private GodotUnitActor? _target;
    private Label? _diagnostic;
    private Resource? _profile;

    public int TemporaryNodeCount => _player?.TransientNodeCount ?? 0;
    public int ActiveTweenCount => (_player?.ActiveTweenCount ?? 0) + (_caster?.StatusOverlay?.ActiveTweenCount ?? 0);
    public bool IsPlaying => _player?.IsPlaying ?? false;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var background = new ColorRect { Color = new Color("26313b") };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);
        _diagnostic = new Label { Position = new Vector2(24, 18), Text = "Presentation preview idle." };
        background.AddChild(_diagnostic);
        BuildActors(background);
        _player = new GodotBattlePresentationPlayer();
        background.AddChild(_player);
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

    public override void _ExitTree() => Stop();

    private string ContentId() => _profile?.Get("ContentIdValue").AsString() ?? string.Empty;

    private void BuildActors(Node parent)
    {
        UnitDefinitionResource casterResource = LoadUnit("unit.pure-run.amazon");
        UnitDefinitionResource targetResource = LoadFirstEnemyUnit();
        var casterId = new UnitInstanceId("presentation.preview.caster");
        var targetId = new UnitInstanceId("presentation.preview.target");
        _caster = GodotUnitFactory.InstantiateActor(casterResource);
        _target = GodotUnitFactory.InstantiateActor(targetResource);
        _caster.SetMeta("preview_id", casterId.Value); _target.SetMeta("preview_id", targetId.Value);
        _caster.Position = IsometricBattleBoardLayout.GridToScreen(new GridPoint(1, 4));
        _target.Position = IsometricBattleBoardLayout.GridToScreen(new GridPoint(7, 4));
        _caster.Scale = Vector2.One * .34f; _target.Scale = Vector2.One * .34f;
        parent.AddChild(_caster); parent.AddChild(_target);
        _actors[casterId] = _caster; _actors[targetId] = _target;
    }

    private static UnitDefinitionResource LoadUnit(string contentId) => LoadByCatalog<UnitDefinitionResource>(contentId);
    private static UnitDefinitionResource LoadFirstEnemyUnit()
    {
        GodotResourceCatalog catalog = LoadCatalog();
        string id = catalog.Entries.Where(value => value.ResourceTypeIdValue == "unit" &&
            value.ContentIdValue.Contains("goat", StringComparison.Ordinal)).Select(value => value.ContentIdValue).First();
        return LoadByCatalog<UnitDefinitionResource>(id, catalog);
    }
    private static T LoadPresentation<T>(string contentId) where T : Resource => LoadByCatalog<T>(contentId);
    private static T LoadByCatalog<T>(string contentId, GodotResourceCatalog? supplied = null) where T : Resource
    {
        GodotResourceCatalog catalog = supplied ?? LoadCatalog();
        GodotResourceEntry entry = catalog.Entries.Single(value => value.ContentIdValue == contentId);
        return ResourceLoader.Load<T>(entry.DiagnosticPathValue, string.Empty, ResourceLoader.CacheMode.Ignore)
            ?? throw new InvalidOperationException($"Preview Resource '{contentId}' is not {typeof(T).Name}.");
    }
    private static GodotResourceCatalog LoadCatalog() =>
        ResourceLoader.Load<GodotResourceCatalog>(TacticsAuthoringEditorService.CatalogPath, string.Empty,
            ResourceLoader.CacheMode.Ignore) ?? throw new InvalidOperationException("Presentation preview Catalog is missing.");
}
#endif
