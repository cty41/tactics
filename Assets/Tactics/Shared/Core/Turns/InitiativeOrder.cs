using Tactics.Core.Content;

namespace Tactics.Core.Turns;

public readonly record struct InitiativeEntry(ContentId UnitId, int Speed);

public static class InitiativeOrder
{
    public static IReadOnlyList<InitiativeEntry> Sort(IEnumerable<InitiativeEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return entries
            .OrderByDescending(entry => entry.Speed)
            .ThenBy(entry => entry.UnitId.Value, StringComparer.Ordinal)
            .ToArray();
    }
}
