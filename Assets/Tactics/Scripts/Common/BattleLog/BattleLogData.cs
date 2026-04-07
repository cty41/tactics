namespace Tactics.Runtime.BattleLog
{
    /// <summary>
    /// Abstract base class for battle log data.
    /// </summary>
    public abstract class BattleLogData
    {
        /// <summary>
        /// Gets the type of battle action.
        /// </summary>
        public abstract BattleActionType ActionType { get; }

        /// <summary>
        /// Gets or sets the custom message (optional).
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets the display string for UI and console output.
        /// </summary>
        /// <returns>The formatted display string.</returns>
        public abstract string GetDisplayString();
    }
}
