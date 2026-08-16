#if TOOLS
using Godot;
namespace Tactics.Godot.Adapter.Editor;
[Tool] public partial class LayerFourAssetBuilder : SceneTree
{
    public override void _Initialize(){try{LayerFourAssetFactory.Build();Quit();}catch(Exception e){GD.PushError(e.ToString());Quit(1);}}
}
#endif
