namespace Tactics.Runtime.BattleLog
{
    /// <summary>
    /// Types of battle actions for logging.
    /// </summary>
    public enum BattleActionType
    {
        Attack,         // 普通攻击
        Skill,          // 技能使用
        Item,           // 物品使用
        TurnStart,      // 回合开始
        TurnEnd,        // 回合结束
        Damage,         // 受到伤害
        Destroy,        // 单位被消灭
        Heal,           // 治疗
        Buff            // Buff施加/效果
    }
}
