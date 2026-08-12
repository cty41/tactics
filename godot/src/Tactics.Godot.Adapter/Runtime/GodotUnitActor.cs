using Godot;
using Tactics.Application.Units;
using Tactics.Application.Battle;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>
/// Cardinal presentation directions supported by the shared Unit actor.
/// </summary>
public enum GodotUnitFacing
{
    South,
    North,
    East,
    West
}

/// <summary>
/// Shared presentation-only actor for generated Unit definitions.
/// </summary>
[GlobalClass]
public partial class GodotUnitActor : Node2D
{
    [Export] public Sprite2D? Shadow { get; set; }
    [Export] public Sprite2D? Body { get; set; }
    public GodotUnitStatusOverlay? StatusOverlay { get; private set; }

    private UnitDefinitionResource? _definition;

    public GodotUnitFacing Facing { get; private set; } = GodotUnitFacing.South;
    public bool IsShowingDeath { get; private set; }
    public bool IsBodyTintEnabled { get; private set; } = true;
    public bool UsesGoatBodyMaskTint =>
        _definition?.BodyTintModeValue == UnitBodyTintModes.GoatBodyMaskV1;

    /// <summary>
    /// Applies presentation data from a generated definition without touching Core state.
    /// </summary>
    public void Configure(UnitDefinitionResource definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.ValidateVisualContract();
        EnsureNodes();
        _definition = definition;
        Shadow!.Texture = definition.ShadowTexture;
        Shadow.Position = definition.ShadowOffset;
        Shadow.Scale = definition.ShadowScale;
        Shadow.Modulate = new Color(1f, 1f, 1f, definition.ShadowOpacity);
        Shadow.FlipH = false;
        IsBodyTintEnabled = true;
        ApplyTint();
        ApplyVisual();
    }

    public void SetFacing(GodotUnitFacing facing)
    {
        if (!Enum.IsDefined(facing))
            throw new ArgumentOutOfRangeException(nameof(facing));
        Facing = facing;
        ApplyVisual();
    }

    public void SetDeathVisual(bool enabled)
    {
        IsShowingDeath = enabled && _definition?.DeathTexture is not null;
        ApplyVisual();
    }

    /// <summary>
    /// Toggles presentation-only tinting for visual comparison without changing gameplay state.
    /// </summary>
    public void SetBodyTintEnabled(bool enabled)
    {
        IsBodyTintEnabled = enabled;
        ApplyTint();
    }

    public void SetStatuses(IReadOnlyList<BattleUiStatusSnapshot>? statuses,int maximumVisible=4)
    {
        if(StatusOverlay is null){StatusOverlay=new GodotUnitStatusOverlay{ZIndex=50};AddChild(StatusOverlay);}
        StatusOverlay.MaximumVisible=maximumVisible;StatusOverlay.Apply(statuses);
    }

    private void ApplyVisual()
    {
        if (_definition is null)
            return;
        EnsureNodes();
        bool usesUpLeft = Facing is GodotUnitFacing.North or GodotUnitFacing.East;
        Body!.Texture = IsShowingDeath
            ? _definition.DeathTexture
            : usesUpLeft
                ? _definition.UpLeftTexture
                : _definition.DownRightTexture;
        Body.FlipH = !IsShowingDeath &&
            (Facing == GodotUnitFacing.East || Facing == GodotUnitFacing.West);
        Vector2 offset = IsShowingDeath
            ? _definition.DeathBodyOffset
            : usesUpLeft
                ? _definition.UpLeftBodyOffset
                : _definition.DownRightBodyOffset;
        Body.Offset = Body.FlipH ? new Vector2(-offset.X, offset.Y) : offset;
        Shadow!.FlipH = false;
    }

    private void ApplyTint()
    {
        if (_definition is null)
            return;
        EnsureNodes();
        bool usesGoatBodyMask =
            _definition.BodyTintModeValue == UnitBodyTintModes.GoatBodyMaskV1;
        Body!.Material = usesGoatBodyMask && IsBodyTintEnabled
            ? _definition.BodyTintMaterial
            : null;
        Body.Modulate = usesGoatBodyMask || !IsBodyTintEnabled
            ? Colors.White
            : _definition.BodyTint;
    }

    private void EnsureNodes()
    {
        if (Body is null || Shadow is null)
            throw new InvalidOperationException("Godot Unit actor is missing its Body or Shadow node.");
    }
}
