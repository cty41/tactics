#if TOOLS
using Godot;

namespace Tactics.Godot.Adapter.Editor;

public partial class OwnershipClosureAssetBuilder : SceneTree
{
    public override void _Initialize()
    {
        try
        {
            OwnershipClosureAssetFactory.Build();
            GD.Print("Godot ownership-closure Lv3 assets generated through ResourceSaver.");
            Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            Quit(1);
        }
    }
}
#endif
