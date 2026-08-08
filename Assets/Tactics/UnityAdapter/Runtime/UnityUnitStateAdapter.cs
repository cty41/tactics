using Tactics.Core.Board;
using Tactics.Core.Content;
using UnityEngine;

namespace Tactics.Unity.Adapter.Runtime;

public sealed class UnityUnitStateAdapter
{
    public UnitState Capture(
        string contentId,
        Vector3 worldPosition,
        Grid grid,
        int moveRange,
        int initiative,
        bool isAlive = true)
    {
        if (string.IsNullOrWhiteSpace(contentId))
            throw new System.ArgumentException("ContentId is required.", nameof(contentId));
        if (grid == null)
            throw new System.ArgumentNullException(nameof(grid));

        Vector3Int cell = grid.WorldToCell(worldPosition);
        return new UnitState(
            new ContentId(contentId),
            new GridPoint(cell.x, cell.y),
            moveRange,
            initiative,
            isAlive);
    }
}
