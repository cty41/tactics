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
        public StoreGoodKind Kind => IsConsumable ? StoreGoodKind.Consumable : StoreGoodKind.Equipment;
        public string ContentId => IsConsumable ? ConsumableId : EquipmentId;
    }

    public class ShopManager
    {
        /// <summary>
        /// Builds a deterministic shop while preserving valid configured goods first.
        /// Every generated set contains at least one consumable, and consumables never repeat.
        /// </summary>
        public List<ShopGood> GenerateGoods(
            int count,
            int seed = 0,
            IEnumerable<StoreGoodEntry> configuredGoods = null)
        {
            count = Mathf.Clamp(count, 2, 4);

            EquipmentDatabase.Load();
            var allDefs = EquipmentDatabase.GetAll().Where(definition => definition != null).ToList();

            var commonPool = allDefs.Where(d => d.Rarity == EquipmentRarity.Common).ToList();
            var rarePool = allDefs.Where(d => d.Rarity == EquipmentRarity.Rare).ToList();
            var consumablePool = ConsumableDatabase.GetAll()
                .Where(definition => definition != null)
                .OrderBy(definition => definition.Id, System.StringComparer.Ordinal)
                .ToList();
            var random = new System.Random(seed);

            var selectedIds = new HashSet<string>(System.StringComparer.Ordinal);
            var goods = new List<ShopGood>();

            foreach (var entry in configuredGoods ?? Enumerable.Empty<StoreGoodEntry>())
            {
                if (goods.Count >= count)
                    break;

                var configuredGood = ResolveConfiguredGood(entry);
                if (configuredGood == null || !selectedIds.Add(GetGoodKey(configuredGood)))
                    continue;

                goods.Add(configuredGood);
            }

            if (!goods.Any(good => good.IsConsumable))
            {
                // A full legacy equipment list keeps its first entries; the final slot
                // becomes the mandatory consumable slot.
                if (goods.Count >= count)
                {
                    selectedIds.Remove(GetGoodKey(goods[goods.Count - 1]));
                    goods.RemoveAt(goods.Count - 1);
                }

                var guaranteedConsumable = PickConsumable(consumablePool, random, selectedIds);
                if (guaranteedConsumable != null)
                    goods.Add(guaranteedConsumable);
            }

            while (goods.Count < count)
            {
                int categoryRoll = random.Next(111);
                bool preferConsumable = categoryRoll < 60;
                var nextGood = preferConsumable
                    ? PickConsumable(consumablePool, random, selectedIds)
                    : PickEquipment(allDefs, commonPool, rarePool, categoryRoll, random, selectedIds);

                nextGood ??= preferConsumable
                    ? PickEquipment(allDefs, commonPool, rarePool, categoryRoll, random, selectedIds)
                    : PickConsumable(consumablePool, random, selectedIds);

                if (nextGood == null)
                    break;

                goods.Add(nextGood);
            }

            TLog.Info($"[ShopManager] 生成 {goods.Count} 件商品");
            return goods;
        }

        private static ShopGood ResolveConfiguredGood(StoreGoodEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.ResolvedContentId))
                return null;

            string contentId = entry.ResolvedContentId;
            if (entry.ResolvedKind == StoreGoodKind.Consumable)
            {
                var consumable = ConsumableDatabase.GetById(contentId);
                return consumable == null
                    ? null
                    : new ShopGood
                    {
                        ConsumableId = consumable.Id,
                        Name = consumable.DisplayName,
                        Price = Mathf.Max(0, entry.price),
                        IconHint = string.Empty
                    };
            }

            var equipment = EquipmentDatabase.GetById(contentId);
            return equipment == null
                ? null
                : new ShopGood
                {
                    EquipmentId = equipment.Id,
                    Name = equipment.DisplayName,
                    Price = Mathf.Max(0, entry.price),
                    IconHint = string.Empty
                };
        }

        private static ShopGood PickConsumable(
            IReadOnlyList<ConsumableDefinition> pool,
            System.Random random,
            ISet<string> selectedIds)
        {
            var available = pool
                .Where(definition => !selectedIds.Contains($"item:{definition.Id}"))
                .ToList();
            if (available.Count == 0)
                return null;

            var picked = available[random.Next(available.Count)];
            selectedIds.Add($"item:{picked.Id}");
            return new ShopGood
            {
                ConsumableId = picked.Id,
                Name = picked.DisplayName,
                Price = picked.Price,
                IconHint = string.Empty
            };
        }

        private static ShopGood PickEquipment(
            IReadOnlyList<EquipmentDefinition> allDefinitions,
            IReadOnlyList<EquipmentDefinition> commonPool,
            IReadOnlyList<EquipmentDefinition> rarePool,
            int categoryRoll,
            System.Random random,
            ISet<string> selectedIds)
        {
            IReadOnlyList<EquipmentDefinition> preferredPool = categoryRoll >= 110 && rarePool.Count > 0
                ? rarePool
                : commonPool;
            if (preferredPool.Count == 0)
                preferredPool = allDefinitions;

            var available = preferredPool
                .Where(definition => !selectedIds.Contains($"equipment:{definition.Id}"))
                .ToList();
            if (available.Count == 0)
            {
                available = allDefinitions
                    .Where(definition => !selectedIds.Contains($"equipment:{definition.Id}"))
                    .ToList();
            }

            if (available.Count == 0)
                return null;

            var picked = available[random.Next(available.Count)];
            selectedIds.Add($"equipment:{picked.Id}");
            return new ShopGood
            {
                EquipmentId = picked.Id,
                Name = picked.DisplayName,
                Price = picked.Price,
                IconHint = string.Empty
            };
        }

        private static string GetGoodKey(ShopGood good)
        {
            return good.IsConsumable
                ? $"item:{good.ConsumableId}"
                : $"equipment:{good.EquipmentId}";
        }
    }
}
