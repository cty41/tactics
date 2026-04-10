namespace Tactics.Runtime.BattleLog
{
    /// <summary>
    /// Battle log data for buff effects.
    /// </summary>
    public class BuffLogData : BattleLogData
    {
        /// <summary>
        /// Gets the type of battle action.
        /// </summary>
        public override BattleActionType ActionType => BattleActionType.Buff;

        /// <summary>
        /// Gets or sets the source name (unit applying the buff).
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// Gets or sets the target name (unit receiving the buff).
        /// </summary>
        public string Target { get; set; }

        /// <summary>
        /// Gets or sets the buff name.
        /// </summary>
        public string BuffName { get; set; }

        /// <summary>
        /// Gets or sets the duration in turns.
        /// </summary>
        public int Duration { get; set; }

        /// <summary>
        /// Gets the display string for UI and console output.
        /// </summary>
        public override string GetDisplayString()
        {
            return $"[BUFF] {Source} applied {BuffName} on {Target} ({Duration} turns)";
        }
    }
}
