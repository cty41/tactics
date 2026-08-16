#if TOOLS
using Godot;

namespace Tactics.Godot.Adapter.Editor;

public partial class StartingSkillAssetBuilder : SceneTree
{
    public override void _Initialize()
    {
        try { StartingSkillAssetFactory.Build(); GD.Print("Pure Run starting-skill Godot assets generated through ResourceSaver."); Quit(); }
        catch (Exception exception) { GD.PushError(exception.ToString()); Quit(1); }
    }
}
#endif
