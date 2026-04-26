namespace Tactics.Runtime.BattleLog
{
    /// <summary>
    /// Battle log data for attack actions.
    /// </summary>
    public class AttackLogData : BattleLogData
    {
        /// <summary>
        /// Gets the type of battle action.
        /// </summary>
        public override BattleActionType ActionType => BattleActionType.Attack;

        /// <summary>
        /// Gets or sets the attacker name.
        /// </summary>
        public string Attacker { get; set; }

        /// <summary>
        /// Gets or sets the target name.
        /// </summary>
        public string Target { get; set; }

        /// <summary>
        /// Gets or sets the damage dealt.
        /// </summary>
        public float Damage { get; set; }

        /// <summary>
        /// Gets or sets whether this was a critical hit.
        /// </summary>
        public bool IsCritical { get; set; }

        /// <summary>
        /// Gets or sets whether the attack missed.
        /// </summary>
        public bool IsMissed { get; set; }

        /// <summary>
        /// Gets the display string for UI and console output.
        /// </summary>
        public override string GetDisplayString()
        {
            if (IsMissed)
            {
                return $"[MISS] {Attacker} -> {Target}";
            }
            string typeTag = IsCritical ? "[CRIT]" : "[ATK]";
            string critMark = IsCritical ? " (CRITICAL!)" : "";
            return $"{typeTag} {Attacker} -> {Target} : {Damage} dmg{critMark}";
        }
    }
}
