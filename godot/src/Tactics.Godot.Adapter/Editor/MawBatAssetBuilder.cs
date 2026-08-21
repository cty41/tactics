#if TOOLS
using Godot;

namespace Tactics.Godot.Adapter.Editor;

[Tool]
public partial class MawBatAssetBuilder : SceneTree
{
    public override void _Initialize()
    {
        try { MawBatAssetFactory.Build(); GD.Print("Maw Bat assets generated through ResourceSaver."); Quit(); }
        catch (Exception exception) { GD.PushError(exception.ToString()); Quit(1); }
    }
}
#endif
