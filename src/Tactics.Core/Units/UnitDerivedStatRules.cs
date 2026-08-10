namespace Tactics.Core.Units;

/// <summary>
/// Implements the frozen <c>unity-unit-derived-v1</c> unit formula without engine dependencies.
/// </summary>
public static class UnitDerivedStatRules
{
    public const string ContractId = "unity-unit-derived-v1";

    /// <summary>
    /// Calculates every derived value that must also be serialized explicitly by adapters.
    /// </summary>
    public static UnitDerivedStats Calculate(UnitAttributes attributes, float speed)
    {
        if (!float.IsFinite(speed) || speed < 0)
            throw new ArgumentOutOfRangeException(nameof(speed));

        int maxHealth = Math.Max(1, checked(attributes.Constitution * 4));
        int maxMana = Math.Max(0, checked(attributes.Charisma * 3));
        int moveRange = (int)Math.Clamp(Math.Ceiling(speed * 0.5d), 1d, 4d);
        float initiative = speed * 2f;
        if (!float.IsFinite(initiative))
            throw new ArgumentOutOfRangeException(nameof(speed), "Derived initiative must remain finite.");

        return new UnitDerivedStats(
            maxHealth,
            maxMana,
            attributes.Charisma,
            moveRange,
            initiative);
    }
}
