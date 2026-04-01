namespace Tactics.Runtime.BattleLog
{
    /// <summary>
    /// Battle log data for damage events.
    /// </summary>
    public class DamageLogData : BattleLogData
    {
        /// <summary>
        /// Gets the type of battle action.
        /// </summary>
        public override BattleActionType ActionType => BattleActionType.Damage;

        /// <summary>
        /// Gets or sets the damage source name.
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// Gets or sets the target name (the one taking damage).
        /// </summary>
        public string Target { get; set; }

        /// <summary>
        /// Gets or sets the damage amount.
        /// </summary>
        public float Damage { get; set; }

        /// <summary>
        /// Gets or sets the remaining health after damage.
        /// </summary>
        public float RemainingHealth { get; set; }

        /// <summary>
        /// Gets the display string for UI and console output.
        /// </summary>
        public override string GetDisplayString()
        {
            float previousHealth = RemainingHealth + Damage;
            return $"[DMG] {Target} : HP {previousHealth:F0} -> {RemainingHealth:F0}";
        }
    }
}
