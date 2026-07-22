namespace Tactics.Common.Units
{
    /// <summary>
    /// Result of one unified damage attempt. Attached effects use WasHit to avoid
    /// applying after a dodge, accuracy miss, or elemental immunity.
    /// </summary>
    public readonly struct DamageResolution
    {
        public bool WasHit { get; }
        public bool WasDodged { get; }
        public bool WasBlocked { get; }
        public bool WasCritical { get; }
        public float DamageApplied { get; }

        private DamageResolution(
            bool wasHit,
            bool wasDodged,
            bool wasBlocked,
            bool wasCritical,
            float damageApplied)
        {
            WasHit = wasHit;
            WasDodged = wasDodged;
            WasBlocked = wasBlocked;
            WasCritical = wasCritical;
            DamageApplied = damageApplied;
        }

        public static DamageResolution Hit(float damageApplied, bool wasCritical) =>
            new(true, false, false, wasCritical, damageApplied);

        public static DamageResolution Dodged() =>
            new(false, true, false, false, 0f);

        public static DamageResolution Blocked() =>
            new(false, false, true, false, 0f);

        public static DamageResolution Invalid() =>
            new(false, false, false, false, 0f);
    }
}
