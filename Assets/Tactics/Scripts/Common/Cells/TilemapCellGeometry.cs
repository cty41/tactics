using System;
using UnityEngine;

namespace Tactics.Cells
{
    /// <summary>
    /// Defines the single world-space anchor contract for tilemap-backed battle cells.
    /// </summary>
    internal static class TilemapCellGeometry
    {
        /// <summary>
        /// Returns the ground anchor used by cells, units, corpses, highlights, and pointer targets.
        /// </summary>
        internal static Vector3 GetGroundCenterWorld(GridLayout grid, Vector3Int coordinates)
        {
            if (grid == null)
                throw new ArgumentNullException(nameof(grid));

            Vector3 layoutCenter = grid.GetLayoutCellCenter();
            layoutCenter.z = 0f;
            Vector3 interpolatedCoordinates = (Vector3)coordinates + layoutCenter;
            return grid.LocalToWorld(grid.CellToLocalInterpolated(interpolatedCoordinates));
        }

        /// <summary>
        /// Resolves a raw world-space point without applying an additional cell-center offset.
        /// </summary>
        internal static Vector3Int WorldToCell(GridLayout grid, Vector3 worldPosition)
        {
            if (grid == null)
                throw new ArgumentNullException(nameof(grid));

            return grid.WorldToCell(worldPosition);
        }

        /// <summary>
        /// Returns the two full world-space axes spanning one cell diamond.
        /// </summary>
        internal static void GetCellBasisWorld(
            GridLayout grid,
            Vector3Int coordinates,
            out Vector3 xAxis,
            out Vector3 yAxis)
        {
            Vector3 center = GetGroundCenterWorld(grid, coordinates);
            xAxis = GetGroundCenterWorld(grid, coordinates + Vector3Int.right) - center;
            yAxis = GetGroundCenterWorld(grid, coordinates + Vector3Int.up) - center;
        }

        /// <summary>
        /// Returns the four world-space vertices of one cell diamond in clockwise order.
        /// </summary>
        internal static void GetDiamondVerticesWorld(
            GridLayout grid,
            Vector3Int coordinates,
            out Vector3 top,
            out Vector3 right,
            out Vector3 bottom,
            out Vector3 left)
        {
            Vector3 center = GetGroundCenterWorld(grid, coordinates);
            GetCellBasisWorld(grid, coordinates, out Vector3 xAxis, out Vector3 yAxis);
            Vector3 halfX = xAxis * 0.5f;
            Vector3 halfY = yAxis * 0.5f;

            top = center + halfX + halfY;
            right = center + halfX - halfY;
            bottom = center - halfX - halfY;
            left = center - halfX + halfY;
        }
    }
}
