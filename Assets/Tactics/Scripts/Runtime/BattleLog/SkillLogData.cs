namespace Tactics.Runtime.BattleLog
{
    /// <summary>
    /// Battle log data for skill usage.
    /// </summary>
    public class SkillLogData : BattleLogData
    {
        /// <summary>
        /// Gets the type of battle action.
        /// </summary>
        public override BattleActionType ActionType => BattleActionType.Skill;

        /// <summary>
        /// Gets or sets the source unit name (the one using the skill).
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// Gets or sets the skill name.
        /// </summary>
        public string SkillName { get; set; }

        /// <summary>
        /// Gets or sets the target name (optional).
        /// </summary>
        public string Target { get; set; }

        /// <summary>
        /// Gets the display string for UI and console output.
        /// </summary>
        public override string GetDisplayString()
        {
            string targetStr = Target != null ? $" -> {Target}" : "";
            return $"[SKILL] {Source} used {SkillName}{targetStr}";
        }
    }
}
