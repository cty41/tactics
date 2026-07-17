namespace Tactics.Runtime.BattleLog
{
    /// <summary>
    /// Battle log data for a completed cleansing consumable.
    /// </summary>
    public sealed class CleanseLogData : BattleLogData
    {
        public override BattleActionType ActionType => BattleActionType.Skill;

        public string Source { get; set; }
        public string ItemName { get; set; }
        public int RemovedCount { get; set; }

        public override string GetDisplayString()
        {
            return RemovedCount > 0
                ? $"{Source}使用{ItemName}，清除了 {RemovedCount} 个减益效果"
                : $"{Source}使用{ItemName}，但未清除任何减益效果";
        }
    }
}
