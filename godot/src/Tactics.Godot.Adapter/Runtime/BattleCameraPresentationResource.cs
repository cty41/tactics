using Godot;
namespace Tactics.Godot.Adapter.Runtime;
[GlobalClass]
public partial class BattleCameraPresentationResource : Resource
{
    [Export] public string ContentIdValue { get; set; }="presentation.camera.battle-focus-v1";
    [Export] public float MaximumTranslation { get; set; }=28f;
    [Export] public float MaximumScale { get; set; }=1.04f;
    [Export] public float FocusDuration { get; set; }=.12f;
    [Export] public float RecoverDuration { get; set; }=.18f;
    [Export] public float HitShakePixels { get; set; }=4f;
}
