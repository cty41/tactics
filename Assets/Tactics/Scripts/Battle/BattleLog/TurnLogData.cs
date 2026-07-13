namespace Tactics.Runtime.BattleLog
{
    /// <summary>
    /// Battle log data for turn events.
    /// </summary>
    public class TurnLogData : BattleLogData
    {
        /// <summary>
        /// Gets the type of battle action.
        /// </summary>
        public override BattleActionType ActionType => IsStart
            ? BattleActionType.TurnStart
            : BattleActionType.TurnEnd;

        /// <summary>
        /// Gets or sets the player number.
        /// </summary>
        public int PlayerNumber { get; set; }

        /// <summary>
        /// Gets or sets the turn number.
        /// </summary>
        public int TurnNumber { get; set; }

        /// <summary>
        /// Gets or sets whether this is a turn start event (false = turn end).
        /// </summary>
        public bool IsStart { get; set; }

        /// <summary>
        /// Gets the display string for UI and console output.
        /// </summary>
        public override string GetDisplayString()
        {
            string state = IsStart ? "started" : "ended";
            return $"[TURN] Player {PlayerNumber} turn {state} (Turn {TurnNumber})";
        }
    }
}
