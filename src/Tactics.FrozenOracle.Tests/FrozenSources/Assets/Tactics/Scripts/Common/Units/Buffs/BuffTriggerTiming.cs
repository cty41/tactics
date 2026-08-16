namespace Tactics.Common.Units.Buffs
{
    public enum BuffTriggerTiming
    {
        None,           // 无触发时机（如 Frozen）
        TurnStart,      // 回合开始时（DoT）
        DamageTaken,    // 受到伤害时（Counter）
        BeforeAttacked  // 被攻击前（Mark）
    }
}
