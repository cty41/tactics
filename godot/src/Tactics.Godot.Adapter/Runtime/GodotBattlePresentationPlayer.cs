using Godot;
using Tactics.Application.Presentation;
using Tactics.Core.Board;
using Tactics.Core.Units;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>Consumes read-only cues and owns every transient Tween created for a battle page.</summary>
public partial class GodotBattlePresentationPlayer : Node
{
    private readonly List<Tween> _activeTweens = new();
    private readonly List<Node> _transientNodes = new();
    private StandardUnitPresentationResource _profile = new();
    private float _speed = 1f;
    private readonly Dictionary<string, SkillPresentationResource> _skillProfiles = new(StringComparer.Ordinal);
    public bool IsPlaying => _activeTweens.Any(GodotObject.IsInstanceValid);
    public event Action? FrameFinished;
    public event Action<BattlePresentationNumber>? NumberRequested;

    public void Configure(StandardUnitPresentationResource profile) => _profile = profile ?? throw new ArgumentNullException(nameof(profile));
    public void ConfigureSkills(IEnumerable<SkillPresentationResource> profiles)
    {
        _skillProfiles.Clear();
        foreach(SkillPresentationResource profile in profiles)_skillProfiles[profile.SkillBranch]=profile;
    }
    public static bool IsSupportedSpeed(float speed) => speed is .5f or 1f or 2f or 4f;

    public void SetSpeed(float speed)
    {
        if (!IsSupportedSpeed(speed)) throw new ArgumentOutOfRangeException(nameof(speed));
        _speed = speed;
        foreach (Tween tween in _activeTweens.Where(GodotObject.IsInstanceValid)) tween.SetSpeedScale(speed);
    }

    public void Play(BattlePresentationFrame frame, IReadOnlyDictionary<UnitInstanceId, GodotUnitActor> actors)
    {
        ArgumentNullException.ThrowIfNull(frame);
        Clear();
        Tween sequence=CreateTween().SetSpeedScale(_speed);_activeTweens.Add(sequence);
        var pendingNumbers = new List<BattlePresentationNumber>(frame.Numbers.OrderBy(value => value.Sequence));
        for (int index = 0; index < frame.Cues.Count; index++)
        {
            BattlePresentationCue cue = frame.Cues[index];
            if (cue.Kind is PresentationCueKind.Melee or PresentationCueKind.Ranged or PresentationCueKind.Cast)
            {
                PlayCue(sequence, cue, actors, recoverAction: false);
                while (index + 1 < frame.Cues.Count && frame.Cues[index + 1].Kind == PresentationCueKind.Hit)
                {
                    BattlePresentationCue hit = frame.Cues[++index];
                    PlayCue(sequence, hit, actors);
                    QueueNextNumber(sequence, pendingNumbers, hit.ActorId);
                }
                RecoverAction(sequence, cue, actors);
            }
            else
            {
                PlayCue(sequence, cue, actors);
                if (cue.Kind == PresentationCueKind.Hit)
                    QueueNextNumber(sequence, pendingNumbers, cue.ActorId);
            }
        }
        foreach (BattlePresentationNumber number in pendingNumbers)
            sequence.TweenCallback(Callable.From(() => NumberRequested?.Invoke(number)));
        // Decision and EndTurn frames intentionally contain no visual cue. A
        // no-op callback keeps the Tween valid so Finished still advances the
        // authoritative automatic-frame queue.
        sequence.TweenCallback(Callable.From(() => { }));
        sequence.Finished += () =>
        {
            _activeTweens.Remove(sequence);
            FrameFinished?.Invoke();
        };
    }

    private void QueueNextNumber(Tween sequence, List<BattlePresentationNumber> pending, UnitInstanceId targetId)
    {
        int index = pending.FindIndex(value => value.TargetId == targetId);
        if (index < 0) return;
        BattlePresentationNumber number = pending[index];
        pending.RemoveAt(index);
        sequence.TweenCallback(Callable.From(() => NumberRequested?.Invoke(number)));
    }

    public static double EstimateMoveDuration(int segmentCount, double segmentDuration, double settleDuration) =>
        segmentCount * segmentDuration + settleDuration;

    public void SetPaused(bool paused){foreach(Tween tween in _activeTweens.Where(GodotObject.IsInstanceValid)){if(paused)tween.Pause();else tween.Play();}}

    public void Clear()
    {
        foreach (Tween tween in _activeTweens)
            if (GodotObject.IsInstanceValid(tween)) tween.Kill();
        _activeTweens.Clear();
        foreach(Node node in _transientNodes)
            if(GodotObject.IsInstanceValid(node))node.QueueFree();
        _transientNodes.Clear();
    }

    public override void _ExitTree() => Clear();

    private void PlayCue(Tween tween,BattlePresentationCue cue, IReadOnlyDictionary<UnitInstanceId, GodotUnitActor> actors,
        bool recoverAction = true)
    {
        if (!actors.TryGetValue(cue.ActorId, out GodotUnitActor? actor) || !GodotObject.IsInstanceValid(actor)) return;
        switch (cue.Kind)
        {
            case PresentationCueKind.Move:
                actor.Position = IsometricBattleBoardLayout.GridToScreen(cue.Origin);
                GridPoint stepOrigin=cue.Origin;
                foreach (var cell in cue.Path)
                {
                    GridPoint from=stepOrigin,to=cell;
                    tween.TweenCallback(Callable.From(()=>actor.SetFacing(GodotPresentationFacingResolver.Resolve(from,to,actor.PresentationFacing))));
                    tween.TweenProperty(actor, "position", IsometricBattleBoardLayout.GridToScreen(cell), _profile.MoveSegmentDuration)
                        .SetTrans(Tween.TransitionType.Linear).SetEase(Tween.EaseType.InOut);
                    stepOrigin=cell;
                }
                tween.TweenInterval(_profile.MoveSettleDuration);
                break;
            case PresentationCueKind.Melee:
                tween.TweenCallback(Callable.From(()=>actor.SetFacing(GodotPresentationFacingResolver.Resolve(cue.Origin,cue.Destination,actor.PresentationFacing))));
                PlayRelease(tween, actor, cue, _profile.MeleeWindupDuration, _profile.MeleeLungeDuration, _profile.MeleeImpactHold, 18f);
                if(cue.SkillId is not null)PlaySkillFx(tween,cue,actors);
                if (recoverAction) PlayRecover(tween,actor,cue,_profile.MeleeRecoverDuration);
                break;
            case PresentationCueKind.Ranged:
                tween.TweenCallback(Callable.From(()=>actor.SetFacing(GodotPresentationFacingResolver.Resolve(cue.Origin,cue.Destination,actor.PresentationFacing))));
                PlayRelease(tween, actor, cue, _profile.RangedAimDuration, _profile.RangedReleaseDuration, 0f, -8f);
                if(cue.SkillId is not null)PlaySkillFx(tween,cue,actors);
                if (recoverAction) PlayRecover(tween,actor,cue,_profile.RangedRecoverDuration);
                break;
            case PresentationCueKind.Cast:
                tween.TweenCallback(Callable.From(()=>actor.SetFacing(GodotPresentationFacingResolver.Resolve(cue.Origin,cue.Destination,actor.PresentationFacing))));
                Vector2 baseScale = actor.Scale;
                tween.TweenProperty(actor, "scale", baseScale * 1.12f, _profile.CastChargeDuration).SetTrans(Tween.TransitionType.Sine);
                tween.TweenInterval(_profile.CastReleaseHold);
                if(cue.SkillId is not null)PlaySkillFx(tween,cue,actors);
                if (recoverAction) tween.TweenProperty(actor, "scale", baseScale, _profile.CastRecoverDuration).SetTrans(Tween.TransitionType.Sine);
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
    }

    private void RecoverAction(Tween tween, BattlePresentationCue cue,
        IReadOnlyDictionary<UnitInstanceId, GodotUnitActor> actors)
    {
        if (!actors.TryGetValue(cue.ActorId, out GodotUnitActor? actor) || !GodotObject.IsInstanceValid(actor)) return;
        if (cue.Kind == PresentationCueKind.Cast)
            tween.TweenProperty(actor, "scale", actor.Scale, _profile.CastRecoverDuration).SetTrans(Tween.TransitionType.Sine);
        else
            PlayRecover(tween, actor, cue, cue.Kind == PresentationCueKind.Melee
                ? _profile.MeleeRecoverDuration : _profile.RangedRecoverDuration);
    }

    public static Vector2 VerticalLightningStart(Vector2 targetHead, float visibleBoardTop) =>
        new(targetHead.X, visibleBoardTop - 32f);

    private void PlaySkillFx(Tween tween, BattlePresentationCue cue, IReadOnlyDictionary<UnitInstanceId, GodotUnitActor> actors)
    {
        string branch=cue.SkillId!.Value.Value;
        SkillPresentationResource? profile=branch.Contains("fireball",StringComparison.Ordinal)?_skillProfiles.GetValueOrDefault("mage.fireball"):
            branch.Contains("bone-spear",StringComparison.Ordinal)?_skillProfiles.GetValueOrDefault("necromancer.bone-spear"):
            branch.Contains("ice-bolt",StringComparison.Ordinal)?_skillProfiles.GetValueOrDefault("mage.ice-bolt"):
            branch.Contains("lightning",StringComparison.Ordinal)?_skillProfiles.GetValueOrDefault("mage.lightning"):
            branch.Contains("poison-spear",StringComparison.Ordinal)?_skillProfiles.GetValueOrDefault("amazon.poison-spear"):
            branch.Contains("amplify-damage",StringComparison.Ordinal)?_skillProfiles.GetValueOrDefault("necromancer.amplify-damage"):
            branch.Contains("thrust",StringComparison.Ordinal)?_skillProfiles.GetValueOrDefault("amazon.thrust"):null;
        if(profile is null)return;
        GridPoint end=cue.Effects?.FirstOrDefault(effect=>effect.Kind==BattlePresentationEffectKind.SpearDropped)?.Cell??cue.Destination;
        Vector2[] impacts=(cue.Effects??Array.Empty<BattlePresentationEffect>()).Where(effect=>effect.TargetId is not null)
            .Select(effect=>cue.AffectedUnitIds.Contains(effect.TargetId!.Value)?effect.TargetId:null).Where(id=>id is not null)
            .Select(id=>cue.TargetId==id?cue.Destination:cue.Destination).Distinct().Select(IsometricBattleBoardLayout.GridToScreen).ToArray();
        if(impacts.Length==0)impacts=cue.AffectedUnitIds.Count>0?[IsometricBattleBoardLayout.GridToScreen(cue.Destination)]:[];
        Vector2 effectEnd = IsometricBattleBoardLayout.GridToScreen(end);
        if (profile.ProgrammaticKind == "lightning" && cue.TargetId is UnitInstanceId targetId &&
            actors.TryGetValue(targetId, out GodotUnitActor? targetActor) && GodotObject.IsInstanceValid(targetActor))
            effectEnd = targetActor.HeadAnchorInParent();
        Vector2 effectStart = profile.ProgrammaticKind == "lightning"
            ? VerticalLightningStart(effectEnd, IsometricBattleBoardLayout.TopCenter.Y)
            : IsometricBattleBoardLayout.GridToScreen(cue.Origin);
        var fx=new GodotProgrammaticSkillFx{Kind=profile.ProgrammaticKind,Start=effectStart,End=effectEnd,Impacts=impacts,Primary=profile.PrimaryColor,Secondary=profile.SecondaryColor,ZIndex=900,Visible=false};
        _transientNodes.Add(fx);
        GetParent().AddChild(fx);
        tween.TweenCallback(Callable.From(()=>{if(GodotObject.IsInstanceValid(fx))fx.Visible=true;}));
        tween.TweenProperty(fx,"Progress",1f,profile.TravelDuration).SetTrans(Tween.TransitionType.Quad);
        tween.TweenInterval(profile.ImpactDuration);
        tween.TweenCallback(Callable.From(() => { _transientNodes.Remove(fx); if (GodotObject.IsInstanceValid(fx)) fx.QueueFree(); }));
    }

    private static void PlayRelease(Tween tween, GodotUnitActor actor, BattlePresentationCue cue,
        float windup, float release, float hold, float distance)
    {
        Vector2 origin = IsometricBattleBoardLayout.GridToScreen(cue.Origin);
        Vector2 target = IsometricBattleBoardLayout.GridToScreen(cue.Destination);
        Vector2 direction = origin.DirectionTo(target);
        tween.TweenInterval(windup);
        tween.TweenProperty(actor, "position", origin + direction * distance, release).SetTrans(Tween.TransitionType.Quad);
        if (hold > 0f) tween.TweenInterval(hold);
    }

    private static void PlayRecover(Tween tween,GodotUnitActor actor,BattlePresentationCue cue,float recover)=>
        tween.TweenProperty(actor,"position",IsometricBattleBoardLayout.GridToScreen(cue.Origin),recover).SetTrans(Tween.TransitionType.Sine);
}
