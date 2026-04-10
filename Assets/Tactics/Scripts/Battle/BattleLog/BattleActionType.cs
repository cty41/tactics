namespace Tactics.Runtime.BattleLog
{
    /// <summary>
    /// Types of battle actions for logging.
    /// </summary>
    public enum BattleActionType
    {
        Attack,         // Normal attack
        Skill,          // Skill usage
        Item,           // Item usage
        TurnStart,      // Turn started
        TurnEnd,        // Turn ended
        Damage,         // Damage taken
        Destroy         // Unit destroyed
    }
}
