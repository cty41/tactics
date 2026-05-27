using System.Collections.Generic;
using Tactics.Runtime.Utilities;

namespace Tactics.RoguelikeMap.Interaction
{
    /// <summary>
    /// 商店商品数据
    /// TODO: 对接物品系统
    /// </summary>
    [System.Serializable]
    public class ShopGood
    {
        public string EquipmentId;
        public string Name;      // 商品名称
        public int Price;        // 价格 3-15
        public string IconHint;  // 图标提示文字（emoji）
    }

    /// <summary>
    /// 商店管理器
    /// 负责生成占位商品列表
    /// TODO: 对接物品系统，替换占位商品
    /// </summary>
    public class ShopManager
    {
        private static readonly string[] GoodIds = { "sword_01", "leather_armor_01", "iron_helmet_01", "bow_01", "lucky_ring_01" };
        private static readonly string[] GoodNames = { "铁剑", "皮甲", "铁盔", "短弓", "幸运戒指" };
        private static readonly string[] GoodIcons = { "\u2694\uFE0F", "\U0001F6E1\uFE0F", "\u26D1", "\U0001F3F9", "\U0001F48D" };
        private static readonly int[] GoodPrices = { 12, 8, 7, 10, 15 };

        /// <summary>
        /// 生成指定数量的随机商品
        /// </summary>
        /// <param name="count">商品数量（2-3）</param>
        /// <returns>商品列表</returns>
        public List<ShopGood> GenerateGoods(int count)
        {
            if (count < 2) count = 2;
            if (count > 3) count = 3;

            var goods = new List<ShopGood>();
            var availableIndices = new List<int>();

            for (int i = 0; i < GoodNames.Length; i++)
                availableIndices.Add(i);

            // 随机选取不重复商品
            for (int i = 0; i < count; i++)
            {
                int pick = UnityEngine.Random.Range(0, availableIndices.Count);
                int idx = availableIndices[pick];
                availableIndices.RemoveAt(pick);

                goods.Add(new ShopGood
                {
                    EquipmentId = GoodIds[idx],
                    Name = GoodNames[idx],
                    Price = GoodPrices[idx],
                    IconHint = GoodIcons[idx]
                });
            }

            TLog.Info($"[ShopManager] 生成 {count} 件商品");
            return goods;
        }
    }
}
