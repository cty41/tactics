using Godot;

namespace Tactics.Godot.Adapter.Runtime;

[GlobalClass]
public partial class StandardUnitPresentationResource : Resource
{
    [Export] public string ContentIdValue { get; set; } = "presentation.unit.standard-v1";
    [Export] public float MoveSegmentDuration { get; set; } = 0.22f;
    [Export] public float MoveSettleDuration { get; set; } = 0.06f;
    [Export] public float MeleeWindupDuration { get; set; } = 0.07f;
    [Export] public float MeleeLungeDuration { get; set; } = 0.09f;
    [Export] public float MeleeImpactHold { get; set; } = 0.045f;
    [Export] public float MeleeRecoverDuration { get; set; } = 0.14f;
    [Export] public float RangedAimDuration { get; set; } = 0.1f;
    [Export] public float RangedReleaseDuration { get; set; } = 0.06f;
    [Export] public float RangedRecoverDuration { get; set; } = 0.16f;
    [Export] public float CastChargeDuration { get; set; } = 0.28f;
    [Export] public float CastReleaseHold { get; set; } = 0.06f;
    [Export] public float CastRecoverDuration { get; set; } = 0.2f;
    [Export] public float HitRecoilDuration { get; set; } = 0.07f;
    [Export] public float HitRecoverDuration { get; set; } = 0.09f;
    [Export] public float CorpseDropDuration { get; set; } = 0.13f;
    [Export] public string MarkerContract { get; set; } = "begin,release,impact,recover,complete";
}
