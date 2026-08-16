#if TOOLS
using Godot;

namespace Tactics.Godot.Adapter.Editor;

/// <summary>
/// Headless-safe ResourceSaver entry point for the Pure Run Buff/Item batch.
/// </summary>
public partial class BuffItemAssetBuilder : SceneTree
{
    public override void _Initialize()
    {
        try
        {
            BuffItemAssetFactory.Build();
            GD.Print("Pure Run Buff/Item Godot assets generated through ResourceSaver.");
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
