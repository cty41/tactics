#if TOOLS
using Godot;

namespace Tactics.Godot.Adapter.Editor;

/// <summary>
/// Upgrades committed Unit resources to the current attribute contract through ResourceSaver.
/// </summary>
public partial class UnitAttributeBalanceBuilder : SceneTree
{
    public override void _Initialize()
    {
        try
        {
            UnitAssetFactory.UpgradeExistingAttributeBalance();
            GD.Print("Pure Run Unit attribute balance upgraded through ResourceSaver.");
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
