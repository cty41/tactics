#if TOOLS
using Godot;

namespace Tactics.Godot.Adapter.Editor;

/// <summary>
/// Headless-safe ResourceSaver entry point for the complete Pure Run Unit batch.
/// </summary>
public partial class UnitAssetBuilder : SceneTree
{
    public override void _Initialize()
    {
        try
        {
            UnitAssetFactory.Build();
            GD.Print("Pure Run Unit Godot assets generated through ResourceSaver.");
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
