using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Utilities;

namespace Tactics.Common.Cells
{
    /// <summary>
    /// A concrete implementation of <see cref="UnityCellManager"/> for managing regular cells in the grid.
    /// This manager automatically loads cells that are its children in the scene tree.
    /// </summary>
    public class RegularCellManager : UnityCellManager
    {
        public override event Action<ICell> CellAdded;
#pragma warning disable CS0067 // No dynamic cell removal in this manager
        public override event Action<ICell> CellRemoved;
#pragma warning restore CS0067

        Dictionary<Vector2IntImpl, Cell> _cellCache;

        public override void Initialize(IGridController gridController)
        {
            _cellCache = new Dictionary<Vector2IntImpl, Cell> ();
            foreach (var cell in GetComponentsInChildren<Cell>())
            {
                _cellCache.Add(cell.GridCoordinates, cell);
                CellAdded?.Invoke(cell);
            }
        }

        public override ICell GetCellAt(Vector2IntImpl coords)
        {
            return _cellCache.TryGetValue(coords, out Cell cell) ? cell : null;
        }

        public override IEnumerable<ICell> GetCells()
        {
            return _cellCache.Values;
        }

        /// <summary>
        /// Marks the specified cell as highlighted.
        /// </summary>
        /// <param name="cell">The cell to highlight.</param>
        public override Task MarkAsHighlighted(ICell cell)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Unmarks the specified cell.
        /// </summary>
        /// <param name="cell">The cell to unmark.</param>
        public override Task UnMarkAsHighlighted(ICell cell)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Marks the specified cells as part of a movement path.
        /// </summary>
        /// <param name="cells">The cells forming the path.</param>
        /// <param name="originCell">The origin cell of the path.</param>
        public override Task MarkAsPath(IEnumerable<ICell> cells, ICell originCell)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Unmarks the specified cells.
        /// </summary>
        /// <param name="cells">The cells to unmark.</param>
        public override Task UnMark(IEnumerable<ICell> cells)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Unmarks the specified cell.
        /// </summary>
        /// <param name="cell">The cell to unmark.</param>
        public override Task UnMark(ICell cell)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Marks the specified cells as reachable.
        /// </summary>
        /// <param name="cells">The cells to mark as reachable.</param>
        public override async Task MarkAsReachable(IEnumerable<ICell> cells)
        {
            foreach (var cell in cells)
            {
                await MarkAsReachable(cell);
            }
        }

        /// <summary>
        /// Marks the specified cell as reachable.
        /// </summary>
        /// <param name="cell">The cell to mark as reachable.</param>
        public override Task MarkAsReachable(ICell cell)
        {
            return Task.CompletedTask;
        }

        public override Task MarkAsAoE(IEnumerable<ICell> cells)
        {
            return Task.CompletedTask;
        }

        public override void SetColor(ICell cell, float r, float g, float b, float a)
        {
            (cell as Cell).SetColor(r, g, b, a);
        }
    }
}