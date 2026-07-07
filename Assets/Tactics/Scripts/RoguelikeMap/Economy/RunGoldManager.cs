using Tactics.Runtime.Utilities;
using Tactics.Roster;

namespace Tactics.RoguelikeMap.Economy
{
    /// <summary>
    /// Run金币管理器
    /// 负责管理单局游戏内的金币收支
    /// </summary>
    public class RunGoldManager
    {
        private static RunGoldManager _instance;
        public static RunGoldManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new RunGoldManager();
                return _instance;
            }
        }

        /// <summary>
        /// 当前金币数量
        /// </summary>
        public int CurrentGold { get; private set; }

        /// <summary>
        /// 单局金币上限
        /// </summary>
        public const int MaxGold = 50;

        /// <summary>
        /// 本次Run获得的总金币
        /// </summary>
        public int TotalGoldEarned { get; private set; }

        /// <summary>
        /// 本次Run花费的总金币
        /// </summary>
        public int TotalGoldSpent { get; private set; }

        private RunGoldManager() { }

        /// <summary>
        /// 初始化金币管理器
        /// </summary>
        public void Initialize()
        {
            CurrentGold = 0;
            TotalGoldEarned = 0;
            TotalGoldSpent = 0;
            TLog.Info($"[RunGoldManager] 初始化完成");
        }

        /// <summary>
        /// 增加金币
        /// </summary>
        /// <param name="amount">增加数量</param>
        /// <returns>实际增加数量</returns>
        public int AddGold(int amount)
        {
            if (amount <= 0)
            {
                TLog.Warning($"[RunGoldManager] 无效的金币增加数量: {amount}");
                return 0;
            }

            int actualAmount = amount;
            
            // 检查是否超过上限
            if (CurrentGold + amount > MaxGold)
            {
                actualAmount = MaxGold - CurrentGold;
                TLog.Info($"[RunGoldManager] 金币已达上限，实际增加: {actualAmount}");
            }

            CurrentGold += actualAmount;
            TotalGoldEarned += actualAmount;

            TLog.Info($"[RunGoldManager] 增加金币: {actualAmount}，当前: {CurrentGold}/{MaxGold}");
            return actualAmount;
        }

        /// <summary>
        /// 花费金币
        /// </summary>
        /// <param name="amount">花费数量</param>
        /// <returns>是否成功花费</returns>
        public bool SpendGold(int amount)
        {
            if (amount <= 0)
            {
                TLog.Warning($"[RunGoldManager] 无效的金币花费数量: {amount}");
                return false;
            }

            if (CurrentGold < amount)
            {
                TLog.Warning($"[RunGoldManager] 金币不足: 需要 {amount}，当前 {CurrentGold}");
                return false;
            }

            CurrentGold -= amount;
            TotalGoldSpent += amount;

            TLog.Info($"[RunGoldManager] 花费金币: {amount}，当前: {CurrentGold}");
            return true;
        }

        /// <summary>
        /// 检查是否有足够金币
        /// </summary>
        public bool HasEnoughGold(int amount)
        {
            return CurrentGold >= amount;
        }

        /// <summary>
        /// 从玩家冒险状态同步当前金币。
        /// </summary>
        public void SyncFromState(PlayerAdventureState state)
        {
            if (state == null)
                return;

            CurrentGold = System.Math.Clamp(state.Gold, 0, MaxGold);
            TLog.Info($"[RunGoldManager] 已从状态同步金币: {CurrentGold}/{MaxGold}");
        }

        /// <summary>
        /// 将当前金币同步回玩家冒险状态。
        /// </summary>
        public void SyncToState(PlayerAdventureState state)
        {
            if (state == null)
                return;

            state.Gold = CurrentGold;
            TLog.Info($"[RunGoldManager] 已写回状态金币: {state.Gold}");
        }

        /// <summary>
        /// 获取金币统计信息
        /// </summary>
        public string GetGoldStats()
        {
            return $"当前金币: {CurrentGold}/{MaxGold}，总收入: {TotalGoldEarned}，总支出: {TotalGoldSpent}";
        }

        /// <summary>
        /// 重置金币管理器
        /// </summary>
        public void Reset()
        {
            CurrentGold = 0;
            TotalGoldEarned = 0;
            TotalGoldSpent = 0;
            TLog.Info($"[RunGoldManager] 已重置");
        }
    }
}
