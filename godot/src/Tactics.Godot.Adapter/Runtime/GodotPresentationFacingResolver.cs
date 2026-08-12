using Tactics.Core.Board;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>Presentation equivalent of the frozen Unity FacingResolver contract.</summary>
public static class GodotPresentationFacingResolver
{
    public static GodotUnitFacing Initial(int playerNumber) => playerNumber == 0 ? GodotUnitFacing.East : GodotUnitFacing.West;

    public static GodotUnitFacing Resolve(GridPoint from,GridPoint to,GodotUnitFacing current)
    {
        int dx=to.X-from.X,dy=to.Y-from.Y;if(dx==0&&dy==0)return current;
        int ax=Math.Abs(dx),ay=Math.Abs(dy);
        if(ax>ay)return dx>0?GodotUnitFacing.East:GodotUnitFacing.West;
        if(ay>ax)return dy>0?GodotUnitFacing.North:GodotUnitFacing.South;
        bool horizontal=current is GodotUnitFacing.East or GodotUnitFacing.West;
        bool vertical=current is GodotUnitFacing.North or GodotUnitFacing.South;
        if(horizontal&&((current==GodotUnitFacing.East&&dx>0)||(current==GodotUnitFacing.West&&dx<0)))return current;
        if(vertical&&((current==GodotUnitFacing.North&&dy>0)||(current==GodotUnitFacing.South&&dy<0)))return current;
        return dx>0?GodotUnitFacing.East:GodotUnitFacing.West;
    }
}
