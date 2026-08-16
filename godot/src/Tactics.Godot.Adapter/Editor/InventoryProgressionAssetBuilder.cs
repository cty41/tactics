#if TOOLS
using Godot;

namespace Tactics.Godot.Adapter.Editor;

[Tool]
public partial class InventoryProgressionAssetBuilder : SceneTree
{
    public override void _Initialize()
    {
        try { InventoryProgressionAssetFactory.Build(); Quit(0); }
        catch (Exception error) { GD.PushError(error.ToString()); Quit(1); }
    }
}
#endif
