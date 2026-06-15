using System.Collections.Generic;
using System.Linq;
using Tactics.Equipment;
using Tactics.Runtime.Utilities;
using UnityEngine;

namespace Tactics.RoguelikeMap.Interaction
{
    [System.Serializable]
    public class ShopGood
    {
        public string EquipmentId;
        public string Name;
        public int Price;
        public string IconHint;
    }

    public class ShopManager
    {
        private const float RareChance = 0.3f;

        public List<ShopGood> GenerateGoods(int count)
        {
            count = Mathf.Clamp(count, 2, 4);

            EquipmentDatabase.Load();
            var allDefs = EquipmentDatabase.GetAll();

            var commonPool = allDefs.Where(d => d.Rarity == EquipmentRarity.Common).ToList();
            var rarePool = allDefs.Where(d => d.Rarity == EquipmentRarity.Rare).ToList();

            var selectedIds = new HashSet<string>();
            var goods = new List<ShopGood>();

            for (int i = 0; i < count; i++)
            {
                bool pickRare = rarePool.Count > 0
                    && Random.value < RareChance
                    && !goods.Any(g => EquipmentDatabase.GetById(g.EquipmentId)?.Rarity == EquipmentRarity.Rare);

                var pool = pickRare ? rarePool : commonPool;

                var available = pool.Where(d => !selectedIds.Contains(d.Id)).ToList();
                if (available.Count == 0)
                    available = pool;

                var picked = available[Random.Range(0, available.Count)];
                selectedIds.Add(picked.Id);

                goods.Add(new ShopGood
                {
                    EquipmentId = picked.Id,
                    Name = picked.DisplayName,
                    Price = picked.Price,
                    IconHint = string.Empty
                });
            }

            TLog.Info($"[ShopManager] 生成 {goods.Count} 件商品");
            return goods;
        }
    }
}
