namespace Tactics.RoguelikeMap
{
    /// <summary>
    /// Roguelike结算上下文
    /// 表达当前结算场景的完整信息，用于结算流程决策
    /// </summary>
    [System.Serializable]
    public class RoguelikeSettlementContext
    {
        /// <summary>
        /// 当前节点类型（如 "Battle", "Boss", "Event", "Treasure", "Store", "RestSite"）
        /// </summary>
        public string NodeType { get; private set; }

        /// <summary>
        /// 是否是Boss战
        /// </summary>
        public bool IsBoss { get; private set; }

        /// <summary>
        /// 是否是run-ending战斗（击败后Run结束）
        /// </summary>
        public bool IsRunEnding { get; private set; }

        /// <summary>
        /// 结算后目标去向（"Home" 或 "Map"）
        /// </summary>
        public string PostSettlementTarget { get; private set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="nodeType">当前节点类型</param>
        /// <param name="isBoss">是否是Boss战</param>
        /// <param name="isRunEnding">是否是run-ending战斗</param>
        /// <param name="postSettlementTarget">结算后目标去向</param>
        public RoguelikeSettlementContext(string nodeType, bool isBoss, bool isRunEnding, string postSettlementTarget)
        {
            NodeType = nodeType;
            IsBoss = isBoss;
            IsRunEnding = isRunEnding;
            PostSettlementTarget = postSettlementTarget;
        }

        /// <summary>
        /// 创建普通战斗结算上下文
        /// </summary>
        public static RoguelikeSettlementContext CreateBattleContext()
        {
            return new RoguelikeSettlementContext("Battle", false, false, "Map");
        }

        /// <summary>
        /// 创建Boss战结算上下文
        /// </summary>
        public static RoguelikeSettlementContext CreateBossContext()
        {
            return new RoguelikeSettlementContext("Boss", true, true, "Home");
        }

        /// <summary>
        /// 创建事件结算上下文
        /// </summary>
        public static RoguelikeSettlementContext CreateEventContext()
        {
            return new RoguelikeSettlementContext("Event", false, false, "Map");
        }

        /// <summary>
        /// 创建宝箱结算上下文
        /// </summary>
        public static RoguelikeSettlementContext CreateTreasureContext()
        {
            return new RoguelikeSettlementContext("Treasure", false, false, "Map");
        }

        /// <summary>
        /// 创建商店结算上下文
        /// </summary>
        public static RoguelikeSettlementContext CreateStoreContext()
        {
            return new RoguelikeSettlementContext("Store", false, false, "Map");
        }

        /// <summary>
        /// 创建休息点结算上下文
        /// </summary>
        public static RoguelikeSettlementContext CreateRestSiteContext()
        {
            return new RoguelikeSettlementContext("RestSite", false, false, "Map");
        }

        /// <summary>
        /// 获取结算上下文描述
        /// </summary>
        public string GetDescription()
        {
            return $"节点类型: {NodeType}, Boss: {IsBoss}, RunEnding: {IsRunEnding}, 目标: {PostSettlementTarget}";
        }
    }
}
