using Godot;
using Tactics.Application.Battle;
using Tactics.Core.Statuses;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>Actor-local programmatic status layer, isolated from Body transforms and gameplay.</summary>
public partial class GodotUnitStatusOverlay : Node2D
{
    private IReadOnlyList<BattleUiStatusSnapshot> _statuses=Array.Empty<BattleUiStatusSnapshot>();
    private Tween? _pulse;
    public int MaximumVisible { get; set; }=4;
    public float PulseDuration { get; set; }=.22f;
    public int StatusCount => _statuses.Count;
    public int ActiveTweenCount => _pulse is not null && GodotObject.IsInstanceValid(_pulse) && _pulse.IsRunning() ? 1 : 0;
    public void Apply(IReadOnlyList<BattleUiStatusSnapshot>? statuses)
    {
        _statuses=statuses??Array.Empty<BattleUiStatusSnapshot>(); QueueRedraw(); ClearAnimation();
        if (_statuses.Count == 0 || PulseDuration <= 0 || !IsInsideTree()) return;
        _pulse = CreateTween(); _pulse.TweenProperty(this, "scale", Vector2.One * 1.08f, PulseDuration * .5f)
            .SetTrans(Tween.TransitionType.Sine); _pulse.TweenProperty(this, "scale", Vector2.One, PulseDuration * .5f)
            .SetTrans(Tween.TransitionType.Sine);
    }
    public void ClearAnimation() { if (_pulse is not null && GodotObject.IsInstanceValid(_pulse)) _pulse.Kill(); _pulse = null; Scale = Vector2.One; }
    public override void _ExitTree() => ClearAnimation();
    public override void _Draw()
    {
        BattleUiStatusSnapshot[] ordered=_statuses.OrderBy(value=>value.StatusId.Value,StringComparer.Ordinal).ToArray();
        for(int index=0;index<Math.Min(MaximumVisible,ordered.Length);index++)DrawStatus(ordered[index],new Vector2(-27+index*18,-116));
        if(ordered.Length>MaximumVisible)DrawString(ThemeDB.FallbackFont,new Vector2(42,-111),$"+{ordered.Length-MaximumVisible}",HorizontalAlignment.Left,-1,11,Colors.White);
    }
    private void DrawStatus(BattleUiStatusSnapshot status,Vector2 center)
    {
        Color color=status.EffectKind switch{StatusEffectKind.Poison=>new(.35f,1f,.25f),StatusEffectKind.Burning=>new(1f,.35f,.08f),StatusEffectKind.Frozen=>new(.55f,.9f,1f),StatusEffectKind.Slow=>new(.25f,.8f,1f),StatusEffectKind.Stun=>new(1f,.9f,.15f),StatusEffectKind.Fear=>new(.75f,.25f,.95f),StatusEffectKind.CurseDamageAmplifier=>new(.5f,.12f,.68f),StatusEffectKind.DamageReduction=>new(.7f,.82f,1f),_=>new(.75f,.75f,.75f)};
        DrawCircle(center,7f,new Color(.08f,.1f,.12f,.9f));DrawArc(center,7f,0,Mathf.Tau,16,color,2f,true);
        string glyph=status.EffectKind.ToString()[..1];DrawString(ThemeDB.FallbackFont,center+new Vector2(-3,4),glyph,HorizontalAlignment.Left,-1,9,color);
        if(status.StackCount>1)DrawString(ThemeDB.FallbackFont,center+new Vector2(4,9),status.StackCount.ToString(),HorizontalAlignment.Left,-1,8,Colors.White);
    }
}
