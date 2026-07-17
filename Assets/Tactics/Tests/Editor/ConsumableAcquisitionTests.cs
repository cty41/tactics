using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Tactics.Consumables;
using Tactics.RoguelikeMap;
using Tactics.RoguelikeMap.Events;
using Tactics.RoguelikeMap.Interaction;
using Tactics.Roster;

namespace Tactics.Tests.Editor
{
    public class ConsumableAcquisitionTests
    {
        [Test]
        public void GeneratedStores_AlwaysContainThreeGoodsAndUniqueConsumableGuarantee()
        {
            var manager = new ShopManager();
            for (int seed = 0; seed < 500; seed++)
            {
                var goods = manager.GenerateGoods(3, seed);
                var consumableIds = goods.Where(good => good.IsConsumable).Select(good => good.ConsumableId).ToList();

                Assert.That(goods, Has.Count.EqualTo(3), $"Seed {seed}");
                Assert.That(consumableIds, Is.Not.Empty, $"Seed {seed}");
                Assert.That(consumableIds.Distinct().Count(), Is.EqualTo(consumableIds.Count), $"Seed {seed}");
            }
        }

        [Test]
        public void ConfiguredLegacyEquipmentOnlyStore_ReplacesFinalSlotWithConsumable()
        {
            var configured = new[]
            {
                new StoreGoodEntry { equipmentId = "sword_01", price = 3 },
                new StoreGoodEntry { equipmentId = "staff_01", price = 4 },
                new StoreGoodEntry { equipmentId = "bow_01", price = 5 }
            };

            var goods = new ShopManager().GenerateGoods(3, 123, configured);

            Assert.That(goods, Has.Count.EqualTo(3));
            Assert.That(goods[0].EquipmentId, Is.EqualTo("sword_01"));
            Assert.That(goods[1].EquipmentId, Is.EqualTo("staff_01"));
            Assert.That(goods[2].IsConsumable, Is.True);
        }

        [Test]
        public void ConfiguredThreeConsumables_RemainUniqueAndOrdered()
        {
            var configured = new List<StoreGoodEntry>();
            foreach (string id in new[] { "life_potion", "mana_potion", "cleansing_potion" })
            {
                var entry = new StoreGoodEntry { price = 7 };
                entry.SetContent(StoreGoodKind.Consumable, id);
                configured.Add(entry);
            }

            var goods = new ShopManager().GenerateGoods(3, 456, configured);

            Assert.That(goods.Select(good => good.ConsumableId),
                Is.EqualTo(new[] { "life_potion", "mana_potion", "cleansing_potion" }));
        }

        [Test]
        public void SameSeedAndConfiguration_ProduceIdenticalStore()
        {
            var manager = new ShopManager();
            var first = manager.GenerateGoods(3, 9876).Select(ToStableKey).ToList();
            var repeated = manager.GenerateGoods(3, 9876).Select(ToStableKey).ToList();

            Assert.That(repeated, Is.EqualTo(first));
        }

        [Test]
        public void StoreGoodSerialization_RoundTripsNewFieldsAndReadsLegacyEquipmentId()
        {
            var data = new SerializableMapData
            {
                nodes = new List<SerializableNodeData>
                {
                    new SerializableNodeData
                    {
                        nodeId = "store",
                        type = "Store",
                        position = new SerializableVector2(0, 0),
                        storeGoods = new List<SerializableNodeData.SerializableStoreGoodData>
                        {
                            new SerializableNodeData.SerializableStoreGoodData
                            {
                                itemKind = StoreGoodKind.Consumable,
                                contentId = "life_potion",
                                price = 5
                            },
                            new SerializableNodeData.SerializableStoreGoodData
                            {
                                equipmentId = "sword_01",
                                price = 4
                            }
                        }
                    }
                }
            };

            string json = MapDataSerializer.Serialize(data);
            var runtimeMap = MapDataSerializer.ToRuntimeMap(MapDataSerializer.Deserialize(json));
            var goods = runtimeMap.GetNode("store").storeConfig.goods;

            Assert.That(goods[0].ResolvedKind, Is.EqualTo(StoreGoodKind.Consumable));
            Assert.That(goods[0].ResolvedContentId, Is.EqualTo("life_potion"));
            Assert.That(goods[1].ResolvedKind, Is.EqualTo(StoreGoodKind.Equipment));
            Assert.That(goods[1].ResolvedContentId, Is.EqualTo("sword_01"));
        }

        [Test]
        public void EventItemWithoutExplicitConfiguration_DoesNotInjectConsumable()
        {
            var noItem = new EventResult { type = EventResultType.Item };
            var explicitItem = new EventResult { type = EventResultType.Item, itemId = "life_potion" };

            Assert.That(noItem.ToRewardResult().ItemIds, Is.Empty);
            Assert.That(explicitItem.ToRewardResult().ItemIds, Is.EqualTo(new[] { "life_potion" }));
        }

        [Test]
        public void RewardDisplay_UsesConsumableNameAndChargeState()
        {
            var reward = RewardResult.Empty();
            reward.ItemIds.Add("life_potion");

            Assert.That(reward.GetDisplayText(), Does.Contain("生命药剂（1/1）"));
            Assert.That(reward.GetDisplayText(), Does.Not.Contain("life_potion"));
        }

        private static string ToStableKey(ShopGood good)
        {
            return $"{good.Kind}:{good.ContentId}:{good.Price}";
        }
    }
}
