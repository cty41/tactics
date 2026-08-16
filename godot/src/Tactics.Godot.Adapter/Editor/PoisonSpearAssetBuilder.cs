#if TOOLS
using Godot;

namespace Tactics.Godot.Adapter.Editor;

/// <summary>
/// Headless-safe entry point. A Godot --script entry must inherit MainLoop/SceneTree;
/// it cannot be an EditorScript because EditorScript is only hosted by the editor UI.
/// </summary>
public partial class PoisonSpearAssetBuilder : SceneTree
{
    public override void _Initialize()
    {
        try
        {
            PoisonSpearAssetFactory.BuildLv1();
            GD.Print("Poison Spear Lv1 Godot assets generated through ResourceSaver.");
            Quit();
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            Quit(1);
        }
    }
}
#endif
