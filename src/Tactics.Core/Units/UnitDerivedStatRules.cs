namespace Tactics.Core.Units;

/// <summary>
/// Implements the frozen <c>unity-unit-derived-v1</c> unit formula without engine dependencies.
/// </summary>
public static class UnitDerivedStatRules
{
    public const string ContractId = "ATTR-DERIVED-STATS-002";

    /// <summary>
    /// Calculates every derived value that must also be serialized explicitly by adapters.
    /// </summary>
    public static UnitDerivedStats Calculate(UnitAttributes attributes, int movementTraitModifier = 0)
    {
        int maxHealth = Math.Max(1, checked(attributes.Constitution * 4));
        int maxMana = Math.Max(0, checked(attributes.Charisma * 3));
        int moveRange = Math.Clamp(checked(2 + attributes.Constitution / 2 + movementTraitModifier), 2, 5);
        float initiative = checked(attributes.Agility * 2f);

        return new UnitDerivedStats(
            maxHealth,
            maxMana,
            attributes.Charisma,
            moveRange,
            initiative);
    }

    public static UnitDerivedStats Calculate(UnitAttributes attributes, float legacySpeed)
    {
        if (!float.IsFinite(legacySpeed) || legacySpeed < 0f)
            throw new ArgumentOutOfRangeException(nameof(legacySpeed));
        return Calculate(attributes, 0);
    }
}
