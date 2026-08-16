namespace Tactics.Core.Units;

/// <summary>
/// Stores the six immutable authored attributes used by the frozen Unity unit formulas.
/// </summary>
public readonly record struct UnitAttributes
{
    public UnitAttributes(
        int strength,
        int agility,
        int constitution,
        int intelligence,
        int charisma,
        int luck)
    {
        Strength = Validate(strength, nameof(strength));
        Agility = Validate(agility, nameof(agility));
        Constitution = Validate(constitution, nameof(constitution));
        Intelligence = Validate(intelligence, nameof(intelligence));
        Charisma = Validate(charisma, nameof(charisma));
        Luck = Validate(luck, nameof(luck));
    }

    public int Strength { get; }
    public int Agility { get; }
    public int Constitution { get; }
    public int Intelligence { get; }
    public int Charisma { get; }
    public int Luck { get; }

    private static int Validate(int value, string parameterName) =>
        value < 0 ? throw new ArgumentOutOfRangeException(parameterName) : value;
}
