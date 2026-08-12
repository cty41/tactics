using Godot;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>Short-lived vector-only skill visual. It never performs gameplay queries.</summary>
public partial class GodotProgrammaticSkillFx : Node2D
{
    public string Kind { get; init; } = string.Empty;
    public Vector2 Start { get; init; }
    public Vector2 End { get; init; }
    public Color Primary { get; init; } = Colors.White;
    public Color Secondary { get; init; } = Colors.White;
    public IReadOnlyList<Vector2> Impacts { get; init; } = Array.Empty<Vector2>();
    private float _progress;
    public float Progress { get => _progress; set { _progress = value; QueueRedraw(); } }

    public override void _Draw()
    {
        Vector2 current=Start.Lerp(End,Math.Clamp(Progress,0f,1f));
        Vector2 direction=Start.DirectionTo(End);
        Vector2 normal=new(-direction.Y,direction.X);
        switch(Kind)
        {
            case "fireball":
                DrawLine(Start,current,Secondary with { A=.42f },8f,true);
                DrawCircle(current,10f,Primary);
                DrawCircle(current,4f,Colors.White);
                if(Progress>.85f)DrawArc(End,24f,0,Mathf.Tau,32,Secondary with { A=(Progress-.85f)/.15f },4f,true);
                break;
            case "bone-spear":
                DrawLine(Start,current,Secondary with { A=.35f },3f,true);
                Vector2 tip=current+direction*18f;
                DrawColoredPolygon([tip,current+normal*6f,current-direction*16f,current-normal*6f],Primary);
                break;
            case "thrust":
                DrawLine(Start,Start.Lerp(End,Progress),Primary,7f,true);
                DrawLine(Start+normal*5f,Start.Lerp(End,Progress)+normal*5f,Secondary,2f,true);
                if(Progress>.7f){DrawLine(End-normal*9f,End+normal*9f,Colors.White,3f,true);DrawLine(End-direction*9f,End+direction*9f,Colors.White,3f,true);}
                break;
            case "ice-bolt":
                DrawLine(Start,current,Secondary with { A=.45f },5f,true);
                DrawColoredPolygon([current+direction*13f,current+normal*6f,current-direction*10f,current-normal*6f],Primary);
                if(Progress>.82f)foreach(float angle in new[]{0f,Mathf.Pi*.5f,Mathf.Pi,Mathf.Pi*1.5f})DrawLine(End,End+Vector2.FromAngle(angle)*18f,Colors.White,2f,true);
                break;
            case "lightning":
                if(Progress>.25f){Vector2 last=Start;for(int index=1;index<=7;index++){float ratio=index/7f;Vector2 point=Start.Lerp(End,ratio)+normal*((index%2==0?1:-1)*7f*(1f-ratio*.45f));DrawLine(last,point,index%2==0?Primary:Secondary,5f,true);last=point;}DrawCircle(End,18f,Secondary with{A=.65f});}
                break;
            case "poison-spear":
                DrawLine(Start,current,Secondary with{A=.35f},3f,true);Vector2 spearTip=current+direction*20f;DrawColoredPolygon([spearTip,current+normal*5f,current-direction*18f,current-normal*5f],Primary);
                if(Progress>.84f)foreach(Vector2 impact in Impacts)DrawArc(impact,17f,0,Mathf.Tau,18,Secondary with{A=.8f},3f,true);
                break;
            case "amplify-damage":
                if(Progress>.2f)foreach(Vector2 impact in Impacts){float radius=20f+Progress*8f;DrawArc(impact,radius,0,Mathf.Tau,28,Primary with{A=.75f},4f,true);DrawArc(impact,radius*.62f,0,Mathf.Tau,20,Secondary with{A=.7f},2f,true);for(int index=0;index<6;index++){Vector2 p=impact+Vector2.FromAngle(index*Mathf.Tau/6f)*radius;DrawCircle(p,2.5f,Secondary);}}
                break;
        }
    }
}
