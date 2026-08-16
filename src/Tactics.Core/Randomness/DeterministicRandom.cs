namespace Tactics.Core.Randomness;

/// <summary>
/// Implements a stable SplitMix64 random stream for battle simulation.
/// </summary>
/// <remarks>
/// The algorithm and constants are part of the replay contract. Changing them requires a schema/version
/// migration because identical saved states and commands must continue to produce identical events.
/// </remarks>
public sealed class DeterministicRandom : IRandomSource
{
    /// <summary>
    /// Identifies the versioned algorithm stored by Golden vectors and save schemas.
    /// </summary>
    public const string AlgorithmId = "splitmix64-v1";

    private const ulong Increment = 0x9E3779B97F4A7C15UL;
    private ulong _state;

    /// <summary>
    /// Initializes a deterministic stream from an explicit seed or previously saved state.
    /// </summary>
    /// <param name="state">Initial state used for the next draw.</param>
    public DeterministicRandom(ulong state)
    {
        _state = state;
    }

    /// <inheritdoc />
    public ulong State => _state;

    /// <inheritdoc />
    public ulong NextUInt64()
    {
        _state = unchecked(_state + Increment);
        ulong value = _state;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }

    /// <inheritdoc />
    public int NextInt(int exclusiveUpperBound)
    {
        if (exclusiveUpperBound <= 0)
            throw new ArgumentOutOfRangeException(nameof(exclusiveUpperBound));

        ulong bound = (ulong)exclusiveUpperBound;
        ulong threshold = unchecked(0UL - bound) % bound;
        ulong value;
        do
        {
            value = NextUInt64();
        }
        while (value < threshold);

        return (int)(value % bound);
    }
}
