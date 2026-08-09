namespace Tactics.Core.Units;

/// <summary>
/// Identifies one runtime unit inside a battle, replay, or save snapshot.
/// </summary>
/// <remarks>
/// This is deliberately separate from ContentId: multiple runtime instances may share one unit
/// definition. Adapters must assign deterministic IDs such as party.amazon.0 or enemy.zombie.1.
/// </remarks>
public readonly record struct UnitInstanceId
{
    public UnitInstanceId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("UnitInstanceId cannot be empty.", nameof(value));
        if (!IsCanonical(value))
        {
            throw new ArgumentException(
                "UnitInstanceId must contain lowercase letters or digits separated by '.', '-' or '_'.",
                nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;

    private static bool IsCanonical(string value)
    {
        bool previousWasSeparator = true;
        foreach (char character in value)
        {
            bool isSeparator = character is '.' or '-' or '_';
            if (isSeparator)
            {
                if (previousWasSeparator)
                    return false;
                previousWasSeparator = true;
                continue;
            }

            if (!char.IsAsciiLetterLower(character) && !char.IsAsciiDigit(character))
                return false;
            previousWasSeparator = false;
        }

        return !previousWasSeparator;
    }
}
