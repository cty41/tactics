#if TOOLS
using Godot;

namespace Tactics.Godot.Adapter.Editor;

[Tool]
public partial class MapTreasureAssetBuilder : SceneTree
{
    public override void _Initialize()
    {
        try { MapTreasureAssetFactory.Build(); Quit(0); }
        catch (Exception exception) { GD.PushError(exception.ToString()); Quit(1); }
    }
}
#endif
