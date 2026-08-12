using Godot;
using Tactics.Application.Presentation;
namespace Tactics.Godot.Adapter.Runtime;
/// <summary>Bounded deterministic transform controller for the board only.</summary>
public partial class GodotBattleCameraFeedback : Node
{
    private Control? _root;private Tween? _tween;private BattleCameraPresentationResource _profile=new();public bool MotionEnabled{get;private set;}=true;
    public void Configure(Control root,BattleCameraPresentationResource? profile=null){_root=root;_profile=profile??new();Reset();}
    public void SetEnabled(bool enabled){MotionEnabled=enabled;if(!enabled)Reset();}
    public void Play(BattlePresentationFrame frame,float speed=1f){if(!MotionEnabled||_root is null)return;Clear();BattlePresentationCue? cue=frame.Cues.FirstOrDefault();if(cue is null)return;Vector2 a=IsometricBattleBoardLayout.GridToScreen(cue.Origin),b=IsometricBattleBoardLayout.GridToScreen(cue.Destination);Vector2 center=(a+b)*.5f;Vector2 boardCenter=IsometricBattleBoardLayout.GridToScreen(new(4,4));Vector2 offset=(boardCenter-center)*.08f;if(offset.Length()>_profile.MaximumTranslation)offset=offset.Normalized()*_profile.MaximumTranslation;_tween=CreateTween().SetSpeedScale(speed);_tween.TweenProperty(_root,"position",offset,_profile.FocusDuration);if(cue.Kind==PresentationCueKind.Hit){foreach(Vector2 shake in new[]{new Vector2(4,0),new Vector2(-4,2),new Vector2(2,-3),Vector2.Zero})_tween.TweenProperty(_root,"position",offset+shake,_profile.FocusDuration*.3f);}_tween.TweenProperty(_root,"position",Vector2.Zero,_profile.RecoverDuration);}
    public void Clear(){if(_tween is not null&&GodotObject.IsInstanceValid(_tween))_tween.Kill();_tween=null;}
    public void Reset(){Clear();if(_root is not null){_root.Position=Vector2.Zero;_root.Scale=Vector2.One;}}
    public override void _ExitTree()=>Reset();
}
