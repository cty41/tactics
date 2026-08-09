namespace Tactics.Core.Randomness;

/// <summary>
/// Provides deterministic random values from explicit serializable state.
/// </summary>
/// <remarks>
/// Gameplay code must receive or reconstruct this source from battle state. Engine-global random APIs
/// are forbidden because replays, AI evaluation, Golden vectors, and save/load require identical draws.
/// </remarks>
public interface IRandomSource
{
    /// <summary>
    /// Gets the state that will be used to produce the next value.
    /// </summary>
    ulong State { get; }

    /// <summary>
    /// Advances the source and returns the next unsigned 64-bit value.
    /// </summary>
    /// <returns>The next deterministic value.</returns>
    ulong NextUInt64();

    /// <summary>
    /// Returns a value in the half-open interval from zero to the exclusive upper bound.
    /// </summary>
    /// <param name="exclusiveUpperBound">Exclusive upper bound. Must be positive.</param>
    /// <returns>A deterministic bounded integer.</returns>
    int NextInt(int exclusiveUpperBound);
}
