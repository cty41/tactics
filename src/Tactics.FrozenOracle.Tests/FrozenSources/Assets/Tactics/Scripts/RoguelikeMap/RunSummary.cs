using System.Collections.Generic;
using Newtonsoft.Json;
using Tactics.Runtime.Utilities;

namespace Tactics.RoguelikeMap
{
    /// <summary>
    /// Run 结局类型
    /// </summary>
    public enum RunOutcome
    {
        /// <summary>
        /// 胜利（击败Boss）
        /// </summary>
        Victory,

        /// <summary>
        /// 失败（角色死亡）
        /// </summary>
        Defeat,

        /// <summary>
        /// 放弃（玩家主动退出）
        /// </summary>
        Abandoned
    }

    /// <summary>
    /// Run结算数据
    /// 记录本次Run的所有奖励和统计
    /// </summary>
    [System.Serializable]
    public class RunSummary
    {
        /// <summary>
        /// 获得的总金币
        /// </summary>
        public int totalGold;

        /// <summary>
        /// 获得的装备列表
        /// </summary>
        public List<string> acquiredEquipment = new List<string>();

        /// <summary>
        /// 获得的物品列表
        /// </summary>
        public List<string> acquiredItems = new List<string>();

        /// <summary>
        /// 击败的敌人数量
        /// </summary>
        public int enemiesDefeated;

        /// <summary>
        /// 访问的节点数量
        /// </summary>
        public int nodesVisited;

        /// <summary>
        /// 完成的事件数量
        /// </summary>
        public int eventsCompleted;

        /// <summary>
        /// 是否击败了Boss
        /// </summary>
        public bool bossDefeated;

        /// <summary>
        /// Run结局
        /// </summary>
        [JsonProperty]
        private RunOutcome _outcome = RunOutcome.Abandoned;

        public RunSummary Clone()
        {
            return new RunSummary
            {
                totalGold = totalGold,
                acquiredEquipment = new List<string>(acquiredEquipment ?? new List<string>()),
                acquiredItems = new List<string>(acquiredItems ?? new List<string>()),
                enemiesDefeated = enemiesDefeated,
                nodesVisited = nodesVisited,
                eventsCompleted = eventsCompleted,
                bossDefeated = bossDefeated,
                _outcome = _outcome
            };
        }

        /// <summary>
        /// 添加金币
        /// </summary>
        public void AddGold(int amount)
        {
            totalGold += amount;
        }

        /// <summary>
        /// 添加装备
        /// </summary>
        public void AddEquipment(string equipmentId)
        {
            if (!string.IsNullOrEmpty(equipmentId))
            {
                acquiredEquipment.Add(equipmentId);
            }
        }

        /// <summary>
        /// 添加物品
        /// </summary>
        public void AddItem(string itemId)
        {
            if (!string.IsNullOrEmpty(itemId))
            {
                acquiredItems.Add(itemId);
            }
        }

        /// <summary>
        /// 增加击败敌人计数
        /// </summary>
        public void IncrementEnemiesDefeated()
        {
            enemiesDefeated++;
        }

        /// <summary>
        /// 增加访问节点计数
        /// </summary>
        public void IncrementNodesVisited()
        {
            nodesVisited++;
        }

        /// <summary>
        /// 增加完成事件计数
        /// </summary>
        public void IncrementEventsCompleted()
        {
            eventsCompleted++;
        }

        /// <summary>
        /// 标记Boss已击败
        /// </summary>
        public void MarkBossDefeated()
        {
            bossDefeated = true;
        }

        /// <summary>
        /// 设置Run结局
        /// </summary>
        public void SetRunOutcome(RunOutcome outcome)
        {
            _outcome = outcome;
            TLog.Info($"[RunSummary] Run结局设置为: {outcome}");
        }

        /// <summary>
        /// 获取Run结局
        /// </summary>
        public RunOutcome GetRunOutcome()
        {
            return _outcome;
        }

        /// <summary>
        /// 获取结算摘要
        /// </summary>
        public string GetSummaryText()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Run结局: {GetOutcomeDisplayName(_outcome)}");
            sb.AppendLine($"总金币: {totalGold}");
            sb.AppendLine($"击败敌人: {enemiesDefeated}");
            sb.AppendLine($"访问节点: {nodesVisited}");
            sb.AppendLine($"完成事件: {eventsCompleted}");
            
            if (acquiredEquipment.Count > 0)
            {
                sb.AppendLine($"获得装备: {string.Join(", ", acquiredEquipment)}");
            }
            
            if (acquiredItems.Count > 0)
            {
                sb.AppendLine($"获得物品: {string.Join(", ", acquiredItems)}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// 获取结局显示名称
        /// </summary>
        private string GetOutcomeDisplayName(RunOutcome outcome)
        {
            switch (outcome)
            {
                case RunOutcome.Victory:
                    return "胜利";
                case RunOutcome.Defeat:
                    return "失败";
                case RunOutcome.Abandoned:
                    return "放弃";
                default:
                    return outcome.ToString();
            }
        }

        /// <summary>
        /// 重置结算数据
        /// </summary>
        public void Reset()
        {
            totalGold = 0;
            acquiredEquipment.Clear();
            acquiredItems.Clear();
            enemiesDefeated = 0;
            nodesVisited = 0;
            eventsCompleted = 0;
            bossDefeated = false;
            _outcome = RunOutcome.Abandoned;
        }
    }
}
