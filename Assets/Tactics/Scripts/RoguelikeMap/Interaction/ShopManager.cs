using System.Collections.Generic;
using System.Linq;
using Tactics.Equipment;
using Tactics.Consumables;
using Tactics.Runtime.Utilities;
using UnityEngine;

namespace Tactics.RoguelikeMap.Interaction
{
    [System.Serializable]
    public class ShopGood
    {
        public string EquipmentId;
        public string ConsumableId;
        public string Name;
        public int Price;
        public string IconHint;

        public bool IsConsumable => !string.IsNullOrWhiteSpace(ConsumableId);
    }

    public class ShopManager
    {
        public List<ShopGood> GenerateGoods(int count, int seed = 0)
        {
            count = Mathf.Clamp(count, 2, 4);

            EquipmentDatabase.Load();
            var allDefs = EquipmentDatabase.GetAll();

            var commonPool = allDefs.Where(d => d.Rarity == EquipmentRarity.Common).ToList();
            var rarePool = allDefs.Where(d => d.Rarity == EquipmentRarity.Rare).ToList();
            var consumablePool = ConsumableDatabase.GetAll().ToList();
            var random = new System.Random(seed);

            var selectedIds = new HashSet<string>();
            var goods = new List<ShopGood>();

            for (int i = 0; i < count; i++)
            {
                int categoryRoll = random.Next(111);
                bool pickConsumable = categoryRoll < 60 && consumablePool.Count > 0;
                bool pickRare = !pickConsumable && categoryRoll >= 110 && rarePool.Count > 0;

                if (pickConsumable)
                {
                    var availableConsumables = consumablePool.Where(d => !selectedIds.Contains($"item:{d.Id}")).ToList();
                    if (availableConsumables.Count == 0) availableConsumables = consumablePool;
                    var picked = availableConsumables[random.Next(availableConsumables.Count)];
                    selectedIds.Add($"item:{picked.Id}");
                    goods.Add(new ShopGood
                    {
                        ConsumableId = picked.Id,
                        Name = picked.DisplayName,
                        Price = picked.Price,
                        IconHint = string.Empty
                    });
                    continue;
                }

                var pool = pickRare ? rarePool : commonPool;
                if (pool.Count == 0) pool = allDefs.ToList();
                if (pool.Count == 0) continue;
                var available = pool.Where(d => !selectedIds.Contains($"equipment:{d.Id}")).ToList();
                if (available.Count == 0) available = pool;
                var equipment = available[random.Next(available.Count)];
                selectedIds.Add($"equipment:{equipment.Id}");
                goods.Add(new ShopGood
                {
                    EquipmentId = equipment.Id,
                    Name = equipment.DisplayName,
                    Price = equipment.Price,
                    IconHint = string.Empty
                });
            }

            TLog.Info($"[ShopManager] 生成 {goods.Count} 件商品");
            return goods;
        }
    }
}
