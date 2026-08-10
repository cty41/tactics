using Tactics.Core.Content;
using Tactics.Core.Units;

namespace Tactics.Application.Units;

/// <summary>
/// Disposable engine-neutral Unit DTO compiled before any Godot Resource is created.
/// </summary>
public sealed record UnitDefinitionDraft
{
    public int SchemaVersion { get; init; } = 1;
    public string ContentId { get; init; } = string.Empty;
    public string SourceId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string FamilyId { get; init; } = string.Empty;
    public string RoleId { get; init; } = string.Empty;
    public int Strength { get; init; }
    public int Agility { get; init; }
    public int Constitution { get; init; }
    public int Intelligence { get; init; }
    public int Charisma { get; init; }
    public int Luck { get; init; }
    public float Speed { get; init; }
    public int MaxHealth { get; init; }
    public int MaxMana { get; init; }
    public int StartingMana { get; init; }
    public int MoveRange { get; init; }
    public float Initiative { get; init; }
    public int AttackRange { get; init; }
    public float AttackFactor { get; init; }
    public float DefenceFactor { get; init; }
    public string MovementKind { get; init; } = string.Empty;
    public bool CanProduceCorpse { get; init; }
    public string ActorContentId { get; init; } = string.Empty;
    public string DownRightTexture { get; init; } = string.Empty;
    public string UpLeftTexture { get; init; } = string.Empty;
    public string? DeathTexture { get; init; }
    public string ShadowTexture { get; init; } = string.Empty;
    public float DownRightPivotX { get; init; } = 0.5f;
    public float DownRightPivotY { get; init; } = 0.078125f;
    public float UpLeftPivotX { get; init; } = 0.5f;
    public float UpLeftPivotY { get; init; } = 0.078125f;
    public float DeathPivotX { get; init; } = 0.5f;
    public float DeathPivotY { get; init; } = 0.5f;
    public int BodyPixelsPerUnit { get; init; } = 128;
    public int ShadowPixelsPerUnit { get; init; } = 64;
    public float ShadowOffsetX { get; init; }
    public float ShadowOffsetY { get; init; }
    public float ShadowScale { get; init; } = 0.8f;
    public float ShadowOpacity { get; init; } = 0.9f;
    public float BodyTintRed { get; init; } = 1f;
    public float BodyTintGreen { get; init; } = 1f;
    public float BodyTintBlue { get; init; } = 1f;
    public float BodyTintAlpha { get; init; } = 1f;
    public string BodyTintMode { get; init; } = UnitBodyTintModes.Multiply;
    public float BaseBodyColorRed { get; init; } = 1f;
    public float BaseBodyColorGreen { get; init; } = 1f;
    public float BaseBodyColorBlue { get; init; } = 1f;
    public float BaseBodyColorAlpha { get; init; } = 1f;
    public IReadOnlyList<string> DeferredDependencies { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Stable presentation modes for applying a Unit's audited body tint.
/// </summary>
public static class UnitBodyTintModes
{
    public const string Multiply = "multiply";
    public const string GoatBodyMaskV1 = "goat-body-mask-v1";

    public static bool IsSupported(string value) => value is Multiply or GoatBodyMaskV1;
}

/// <summary>
/// Stores an engine-neutral RGBA tint applied only by presentation adapters.
/// </summary>
public readonly record struct UnitBodyTint(float Red, float Green, float Blue, float Alpha);

/// <summary>
/// Stores a normalized Unity Sprite pivot without retaining engine objects.
/// </summary>
public readonly record struct UnitSpritePivot(float X, float Y);

/// <summary>
/// Stores validated Unit visual references without retaining Unity or Godot objects.
/// </summary>
public sealed record UnitVisualDefinition(
    string DownRightTexture,
    string UpLeftTexture,
    string? DeathTexture,
    string ShadowTexture,
    UnitSpritePivot DownRightPivot,
    UnitSpritePivot UpLeftPivot,
    UnitSpritePivot DeathPivot,
    int BodyPixelsPerUnit,
    int ShadowPixelsPerUnit,
    float ShadowOffsetX,
    float ShadowOffsetY,
    float ShadowScale,
    float ShadowOpacity,
    UnitBodyTint BodyTint,
    string BodyTintMode,
    UnitBodyTint BaseBodyColor);

/// <summary>
/// Combines the Core gameplay definition with adapter-facing visual and audit metadata.
/// </summary>
public sealed record CompiledUnitDefinition(
    UnitDefinition Definition,
    ContentId ActorContentId,
    UnitVisualDefinition Visual,
    IReadOnlyList<string> DeferredDependencies);
