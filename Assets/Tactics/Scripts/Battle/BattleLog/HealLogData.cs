namespace Tactics.Runtime.BattleLog
{
    /// <summary>
    /// Battle log data for healing actions.
    /// </summary>
    public class HealLogData : BattleLogData
    {
        /// <summary>
        /// Gets the type of battle action.
        /// </summary>
        public override BattleActionType ActionType => BattleActionType.Heal;

        /// <summary>
        /// Gets or sets the healer name.
        /// </summary>
        public string Healer { get; set; }

        /// <summary>
        /// Gets or sets the target name.
        /// </summary>
        public string Target { get; set; }

        /// <summary>
        /// Gets or sets the amount healed.
        /// </summary>
        public float HealAmount { get; set; }

        /// <summary>
        /// Gets or sets the remaining health after healing.
        /// </summary>
        public float RemainingHealth { get; set; }

        /// <summary>
        /// Gets the display string for UI and console output.
        /// </summary>
        public override string GetDisplayString()
        {
            float healthBefore = RemainingHealth - HealAmount;
            return $"[HEAL] {Target} : HP {healthBefore:F0} -> {RemainingHealth:F0} (+{HealAmount:F0})";
        }
    }
}
