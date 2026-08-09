using Tactics.Core.Units;

namespace Tactics.Core.Turns;

public readonly record struct InitiativeEntry(
    UnitInstanceId UnitId,
    float Initiative,
    int PlayerNumber,
    int SpawnOrdinal);

public static class InitiativeOrder
{
    public static IReadOnlyList<InitiativeEntry> Sort(IEnumerable<InitiativeEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return entries
            .OrderByDescending(entry => entry.Initiative)
            .ThenBy(entry => entry.PlayerNumber)
            .ThenBy(entry => entry.SpawnOrdinal)
            .ThenBy(entry => entry.UnitId.Value, StringComparer.Ordinal)
            .ToArray();
    }
}
