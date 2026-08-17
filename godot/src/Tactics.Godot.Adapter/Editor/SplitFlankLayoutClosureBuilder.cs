#if TOOLS
using Godot;

namespace Tactics.Godot.Adapter.Editor;

[Tool]
public partial class SplitFlankLayoutClosureBuilder : SceneTree
{
    public override void _Initialize()
    {
        try { SplitFlankLayoutClosureFactory.Build(); Quit(); }
        catch (Exception exception) { GD.PushError(exception.ToString()); Quit(1); }
    }
}
#endif
