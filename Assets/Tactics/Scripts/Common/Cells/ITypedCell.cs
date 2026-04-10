using UnityEngine;

namespace Tactics.Common.Cells
{
    /// <summary>
    /// Represents a cell that has an associated type, defined by a <see cref="ScriptableObject"/>.
    /// </summary>
    public interface ITypedCell
    {
        
        /// <summary>
        /// Gets the resource representing the cell's type.
        /// </summary>
        ScriptableObject CellType { get; }
    }
}