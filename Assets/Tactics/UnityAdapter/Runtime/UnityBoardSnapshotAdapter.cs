using System.Collections.Generic;
using Tactics.Core.Board;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Tactics.Unity.Adapter.Runtime;

/// <summary>
/// Temporary Unity bridge for the 10x10 migration fixture. It is deleted with the Unity project.
/// </summary>
public sealed class UnityBoardSnapshotAdapter
{
    public BoardSnapshot Capture(Tilemap tilemap)
    {
        if (tilemap == null)
            throw new System.ArgumentNullException(nameof(tilemap));

        var cells = new Dictionary<GridPoint, CellState>(BoardSpec.CellCount);
        for (int x = 0; x < BoardSpec.Width; x++)
        {
            for (int y = 0; y < BoardSpec.Height; y++)
            {
                var cell = new Vector3Int(x, y, 0);
                bool hasTile = tilemap.HasTile(cell);
                cells[new GridPoint(x, y)] = new CellState(
                    blocksMovement: !hasTile,
                    blocksLineOfSight: !hasTile);
            }
        }

        return new BoardSnapshot(cells);
    }
}
