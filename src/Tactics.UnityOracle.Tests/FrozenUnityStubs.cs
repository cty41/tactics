namespace Tactics.Common.Cells
{
    /// <summary>
    /// Minimal compile-time surface required by the frozen pure C# pathfinding sources.
    /// </summary>
    public interface ICell
    {
    }
}

namespace Tactics.Common.Controllers
{
    /// <summary>
    /// Minimal reference-type key required by the frozen initiative registry.
    /// </summary>
    public interface IGridController
    {
    }
}

namespace Tactics.Common.Units
{
    /// <summary>
    /// Exact member subset read by the frozen initiative comparator and partition.
    /// </summary>
    public interface IUnit
    {
        float Health { get; }
        float Initiative { get; }
        int PlayerNumber { get; }
        int UnitID { get; }
    }
}

namespace Tactics.Common.Battle
{
    using Tactics.Common.Units;

    /// <summary>
    /// The tie-break Oracle does not include Amazon decoys, so the linked source only needs this seam.
    /// </summary>
    public static class AmazonBattleState
    {
        public static bool IsDecoy(IUnit unit) => false;
    }
}

namespace Tactics.Runtime.Utilities
{
    /// <summary>
    /// Test-only sink required by the frozen runtime scope timeout boundary.
    /// </summary>
    public static class TLog
    {
        private static readonly object Gate = new();
        private static readonly List<string> Errors = new();

        public static void Error(string message)
        {
            lock (Gate)
                Errors.Add(message);
        }

        public static string[] DrainErrors()
        {
            lock (Gate)
            {
                string[] result = Errors.ToArray();
                Errors.Clear();
                return result;
            }
        }
    }
}

namespace UnityEngine
{
    /// <summary>
    /// Minimal deterministic surface required to compile the frozen Unity movement formula.
    /// </summary>
    public static class Mathf
    {
        public static float Ceil(float value) => (float)Math.Ceiling(value);

        public static float Clamp(float value, float minimum, float maximum) =>
            Math.Min(maximum, Math.Max(minimum, value));
    }
}
