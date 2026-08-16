#if TOOLS
using Godot;

namespace Tactics.Godot.Adapter.Editor;

public partial class RunPersistenceAssetBuilder : SceneTree
{
    public override void _Initialize(){try{RunPersistenceAssetFactory.Build();GD.Print("Pure Run persistence assets generated through ResourceSaver.");Quit();}catch(Exception error){GD.PushError(error.ToString());Quit(1);}}
}
#endif
