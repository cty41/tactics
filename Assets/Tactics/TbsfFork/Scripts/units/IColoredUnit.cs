using UnityEngine;

namespace Tactics.Tbsf.Unity.Units
{
    /// <summary>
    /// Represents a unit with a color assigned to it.
    /// </summary>
    public interface IColoredUnit
    {
        public Color Color { get; set; }
    }
}
