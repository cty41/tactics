#if TOOLS
using Godot;

namespace Tactics.Godot.Adapter.Editor;

[Tool]
public partial class IsometricPresentationAssetBuilder : SceneTree
{
    public override void _Initialize()
    {
        try { IsometricPresentationAssetFactory.BuildBoard(); Quit(); }
        catch (Exception error) { GD.PushError(error.ToString()); Quit(1); }
    }
}
#endif
