using Godot;
using Tactics.Application.Presentation;
using Tactics.Core.Units;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>Consumes read-only cues and owns every transient Tween created for a battle page.</summary>
public partial class GodotBattlePresentationPlayer : Node
{
    private readonly List<Tween> _activeTweens = new();
    private StandardUnitPresentationResource _profile = new();
    private float _speed = 1f;
    private readonly Dictionary<string, SkillPresentationResource> _skillProfiles = new(StringComparer.Ordinal);
    public bool IsPlaying => _activeTweens.Any(GodotObject.IsInstanceValid);

    public void Configure(StandardUnitPresentationResource profile) => _profile = profile ?? throw new ArgumentNullException(nameof(profile));
    public void ConfigureSkills(IEnumerable<SkillPresentationResource> profiles)
    {
        _skillProfiles.Clear();
        foreach(SkillPresentationResource profile in profiles)_skillProfiles[profile.SkillBranch]=profile;
    }
    public void SetSpeed(float speed) => _speed = speed is >= 1f and <= 2f ? speed : throw new ArgumentOutOfRangeException(nameof(speed));

    public void Play(BattlePresentationFrame frame, IReadOnlyDictionary<UnitInstanceId, GodotUnitActor> actors)
    {
        ArgumentNullException.ThrowIfNull(frame);
        Clear();
        foreach (BattlePresentationCue cue in frame.Cues) PlayCue(cue, actors);
    }

    public void Clear()
    {
        foreach (Tween tween in _activeTweens)
            if (GodotObject.IsInstanceValid(tween)) tween.Kill();
        _activeTweens.Clear();
    }

    public override void _ExitTree() => Clear();

    private void PlayCue(BattlePresentationCue cue, IReadOnlyDictionary<UnitInstanceId, GodotUnitActor> actors)
    {
        if (!actors.TryGetValue(cue.ActorId, out GodotUnitActor? actor) || !GodotObject.IsInstanceValid(actor)) return;
        Tween tween = CreateTween().SetSpeedScale(_speed);
        _activeTweens.Add(tween);
        switch (cue.Kind)
        {
            case PresentationCueKind.Move:
                actor.Position = IsometricBattleBoardLayout.GridToScreen(cue.Origin);
                foreach (var cell in cue.Path)
                    tween.TweenProperty(actor, "position", IsometricBattleBoardLayout.GridToScreen(cell), _profile.MoveSegmentDuration).SetTrans(Tween.TransitionType.Sine);
                tween.TweenInterval(_profile.MoveSettleDuration);
                break;
            case PresentationCueKind.Melee:
                PlayLunge(tween, actor, cue, _profile.MeleeWindupDuration, _profile.MeleeLungeDuration, _profile.MeleeImpactHold, _profile.MeleeRecoverDuration, 18f);
                break;
            case PresentationCueKind.Ranged:
                PlayLunge(tween, actor, cue, _profile.RangedAimDuration, _profile.RangedReleaseDuration, 0f, _profile.RangedRecoverDuration, -8f);
                break;
            case PresentationCueKind.Cast:
                Vector2 baseScale = actor.Scale;
                tween.TweenProperty(actor, "scale", baseScale * 1.12f, _profile.CastChargeDuration).SetTrans(Tween.TransitionType.Sine);
                tween.TweenInterval(_profile.CastReleaseHold);
                tween.TweenProperty(actor, "scale", baseScale, _profile.CastRecoverDuration).SetTrans(Tween.TransitionType.Sine);
                break;
            case PresentationCueKind.Hit:
                if (actor.Body is not null)
                {
                    Color baseColor = actor.Body.Modulate;
                    tween.TweenProperty(actor.Body, "modulate", new Color(1f, 0.4f, 0.4f, baseColor.A), _profile.HitRecoilDuration);
                    tween.TweenProperty(actor.Body, "modulate", baseColor, _profile.HitRecoverDuration);
                }
                break;
            case PresentationCueKind.Defeat:
                tween.TweenInterval(_profile.HitRecoilDuration);
                tween.TweenCallback(Callable.From(() => { if (GodotObject.IsInstanceValid(actor)) actor.SetDeathVisual(true); }));
                tween.TweenInterval(_profile.CorpseDropDuration);
                break;
            case PresentationCueKind.CorpseRemoved:
                tween.TweenCallback(Callable.From(() => { if (GodotObject.IsInstanceValid(actor)) actor.QueueFree(); }));
                break;
        }
        if(cue.SkillId is not null)PlaySkillFx(cue);
        tween.Finished += () => _activeTweens.Remove(tween);
    }

    private void PlaySkillFx(BattlePresentationCue cue)
    {
        string branch=cue.SkillId!.Value.Value;
        SkillPresentationResource? profile=branch.Contains("fireball",StringComparison.Ordinal)?_skillProfiles.GetValueOrDefault("mage.fireball"):
            branch.Contains("bone-spear",StringComparison.Ordinal)?_skillProfiles.GetValueOrDefault("necromancer.bone-spear"):
            branch.Contains("thrust",StringComparison.Ordinal)?_skillProfiles.GetValueOrDefault("amazon.thrust"):null;
        if(profile is null)return;
        var fx=new GodotProgrammaticSkillFx{Kind=profile.ProgrammaticKind,Start=IsometricBattleBoardLayout.GridToScreen(cue.Origin),End=IsometricBattleBoardLayout.GridToScreen(cue.Destination),Primary=profile.PrimaryColor,Secondary=profile.SecondaryColor,ZIndex=900};
        GetParent().AddChild(fx);
        Tween tween=CreateTween().SetSpeedScale(_speed);_activeTweens.Add(tween);
        tween.TweenProperty(fx,"Progress",1f,profile.TravelDuration).SetTrans(Tween.TransitionType.Quad);
        tween.TweenInterval(profile.ImpactDuration);
        tween.TweenCallback(Callable.From(fx.QueueFree));
        tween.Finished+=()=>_activeTweens.Remove(tween);
    }

    private static void PlayLunge(Tween tween, GodotUnitActor actor, BattlePresentationCue cue,
        float windup, float release, float hold, float recover, float distance)
    {
        Vector2 origin = IsometricBattleBoardLayout.GridToScreen(cue.Origin);
        Vector2 target = IsometricBattleBoardLayout.GridToScreen(cue.Destination);
        Vector2 direction = origin.DirectionTo(target);
        tween.TweenInterval(windup);
        tween.TweenProperty(actor, "position", origin + direction * distance, release).SetTrans(Tween.TransitionType.Quad);
        if (hold > 0f) tween.TweenInterval(hold);
        tween.TweenProperty(actor, "position", origin, recover).SetTrans(Tween.TransitionType.Sine);
    }
}
