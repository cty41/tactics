namespace Tactics.Core.Battle;

/// <summary>
/// Engine-neutral result of one damage attempt.
/// </summary>
public readonly record struct DamageResolution(
    bool WasHit,
    bool WasDodged,
    bool WasBlocked,
    bool WasCritical,
    float DamageApplied)
{
    public static DamageResolution Hit(float damageApplied, bool wasCritical) =>
        new(true, false, false, wasCritical, damageApplied);

    public static DamageResolution Dodged() =>
        new(false, true, false, false, 0f);

    public static DamageResolution Blocked() =>
        new(false, false, true, false, 0f);

    public static DamageResolution Invalid() =>
        new(false, false, false, false, 0f);
}
