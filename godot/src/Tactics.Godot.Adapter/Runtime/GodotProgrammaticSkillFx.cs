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
        }
    }
}
