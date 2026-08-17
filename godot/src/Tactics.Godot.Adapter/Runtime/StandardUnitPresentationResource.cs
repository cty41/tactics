using Godot;

namespace Tactics.Godot.Adapter.Runtime;

[Tool]
[GlobalClass]
public partial class StandardUnitPresentationResource : Resource
{
    [Export] public string ContentIdValue { get; set; } = "presentation.unit.standard-v1";
    [Export] public float IdleDuration { get; set; } = 1.35f;
    [Export] public float IdleLiftPixels { get; set; } = 2.5f;
    [Export] public float IdleScaleAmount { get; set; } = 0.025f;
    [Export] public float MoveCycleDuration { get; set; } = 0.22f;
    [Export] public float MoveTiltDegrees { get; set; } = 5f;
    [Export] public float MoveLiftPixels { get; set; } = 3f;
    [Export] public float MoveSwayPixels { get; set; } = 3f;
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
    [Export] public float HitShakeDuration { get; set; } = 0.07f;
    [Export] public float HitRecoverDuration { get; set; } = 0.09f;
    [Export] public float HitRecoilPixels { get; set; } = 10f;
    [Export] public float HitRotationDegrees { get; set; } = 4f;
    [Export] public float LethalShakeDuration { get; set; } = 0.05f;
    [Export] public float LethalCollapseDuration { get; set; } = 0.08f;
    [Export] public Vector2 LethalCollapseScale { get; set; } = new(1.02f, 0.58f);
    [Export] public float CorpseDropDuration { get; set; } = 0.13f;
    [Export] public float CorpseImpactDuration { get; set; } = 0.07f;
    [Export] public float CorpseSettleDuration { get; set; } = 0.08f;
    [Export] public float CorpseStartHeightPixels { get; set; } = 8f;
    [Export] public float ShadowContactOffsetY { get; set; } = -10f;
    [Export] public string MarkerContract { get; set; } = "begin,release,impact,recover,complete";
    [Export(PropertyHint.MultilineText)] public string AuthoringGraphJsonValue { get; set; } = string.Empty;
}
