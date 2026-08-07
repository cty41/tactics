using UnityEngine;

namespace Tactics.Common.Units
{
    /// <summary>
    /// Shared formulas for unit statistics derived from authored attributes.
    /// </summary>
    public static class UnitDerivedStatRules
    {
        private const float MinimumMovement = 1f;
        private const float MaximumMovement = 4f;

        /// <summary>
        /// Converts speed into movement budget for the fixed 10x10 battlefield.
        /// </summary>
        public static float CalculateMovement(float speed)
        {
            return Mathf.Clamp(Mathf.Ceil(speed * 0.5f), MinimumMovement, MaximumMovement);
        }
    }
}
