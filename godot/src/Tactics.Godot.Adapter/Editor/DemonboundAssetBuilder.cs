#if TOOLS
using Godot;

namespace Tactics.Godot.Adapter.Editor;

public partial class DemonboundAssetBuilder : SceneTree
{
    public override void _Initialize()
    {
        try { DemonboundAssetFactory.Build(); GD.Print("Demonbound assets generated through ResourceSaver."); Quit(); }
        catch (Exception exception) { GD.PushError(exception.ToString()); Quit(1); }
    }
}
#endif
