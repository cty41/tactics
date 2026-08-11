#if TOOLS
using Godot;

namespace Tactics.Godot.Adapter.Editor;

public partial class AiEncounterAssetBuilder : SceneTree
{
    public override void _Initialize(){try{AiEncounterAssetFactory.Build();GD.Print("Pure Run AI/Encounter Godot assets generated through ResourceSaver.");Quit();}catch(Exception error){GD.PushError(error.ToString());Quit(1);}}
}
#endif
