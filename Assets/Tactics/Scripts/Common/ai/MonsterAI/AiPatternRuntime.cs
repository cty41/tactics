using System.Collections.Generic;
using Tactics.Common.Units;

namespace Tactics.Common.AI.MonsterAI
{
    /// <summary>
    /// Stores fixed-pattern cursors per unit rather than on shared ScriptableObjects.
    /// </summary>
    public static class AiPatternRuntime
    {
        private static readonly Dictionary<IUnit, int> _steps = new Dictionary<IUnit, int>();

        public static int GetStep(IUnit unit)
        {
            return unit != null && _steps.TryGetValue(unit, out int step) ? step : 0;
        }

        public static void Advance(IUnit unit, int stepCount)
        {
            if (unit == null || stepCount <= 0) return;
            _steps[unit] = (GetStep(unit) + 1) % stepCount;
        }

        public static void Reset(IUnit unit)
        {
            if (unit != null) _steps.Remove(unit);
        }

        public static void ResetAll()
        {
            _steps.Clear();
        }
    }
}
