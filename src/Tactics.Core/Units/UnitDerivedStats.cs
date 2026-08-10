namespace Tactics.Core.Units;

/// <summary>
/// Stores explicit unit values produced by a versioned derived-stat contract.
/// </summary>
public readonly record struct UnitDerivedStats
{
    public UnitDerivedStats(
        int maxHealth,
        int maxMana,
        int startingMana,
        int moveRange,
        float initiative)
    {
        if (maxHealth <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxHealth));
        if (maxMana < 0)
            throw new ArgumentOutOfRangeException(nameof(maxMana));
        if (startingMana < 0 || startingMana > maxMana)
            throw new ArgumentOutOfRangeException(nameof(startingMana));
        if (moveRange < 0)
            throw new ArgumentOutOfRangeException(nameof(moveRange));
        if (!float.IsFinite(initiative) || initiative < 0)
            throw new ArgumentOutOfRangeException(nameof(initiative));

        MaxHealth = maxHealth;
        MaxMana = maxMana;
        StartingMana = startingMana;
        MoveRange = moveRange;
        Initiative = initiative;
    }

    public int MaxHealth { get; }
    public int MaxMana { get; }
    public int StartingMana { get; }
    public int MoveRange { get; }
    public float Initiative { get; }
}
