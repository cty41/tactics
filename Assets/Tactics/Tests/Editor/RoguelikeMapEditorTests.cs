using NUnit.Framework;
using Tactics.RoguelikeMap;
using Tactics.Editor.RoguelikeMapEditor;
using UnityEngine;
using System.Collections.Generic;

namespace Tactics.Tests.Editor
{
    /// <summary>
    /// RoguelikeMap Editor 的最小回归测试。
    /// 覆盖：JSON round-trip、手工连接、Treasure/Store 配置、节点删除后边清理。
    /// </summary>
    public class RoguelikeMapEditorTests
    {
        // ═══════════════════════════════════════════
        //  JSON Serialize / Deserialize Round-Trip
        // ═══════════════════════════════════════════

        [Test]
        public void JsonRoundTrip_PreservesAllData()
        {
            var original = new SerializableMapData
            {
                maxReachableDistance = 200,
                visionRange = 3,
                nodes = new List<SerializableNodeData>
                {
                    new SerializableNodeData
                    {
                        nodeId = "start",
                        type = "Start",
                        position = new SerializableVector2(0, 0),
                        outgoing = new[] { "enemy1" }
                    },
                    new SerializableNodeData
                    {
                        nodeId = "enemy1",
                        type = "MinorEnemy",
                        position = new SerializableVector2(100, 0),
                        incoming = new[] { "start" },
                        outgoing = new[] { "boss" }
                    },
                    new SerializableNodeData
                    {
                        nodeId = "boss",
                        type = "Boss",
                        position = new SerializableVector2(200, 0),
                        incoming = new[] { "enemy1" }
                    }
                }
            };

            string json = MapDataSerializer.Serialize(original);
            var deserialized = MapDataSerializer.Deserialize(json);

            Assert.IsNotNull(deserialized);
            Assert.AreEqual(3, deserialized.nodes.Count);
            Assert.AreEqual(200, deserialized.maxReachableDistance);
            Assert.AreEqual(3, deserialized.visionRange);

            Assert.AreEqual("start", deserialized.nodes[0].nodeId);
            Assert.AreEqual("Start", deserialized.nodes[0].type);
            Assert.AreEqual(1, deserialized.nodes[0].outgoing.Length);
            Assert.AreEqual("enemy1", deserialized.nodes[0].outgoing[0]);

            Assert.AreEqual("enemy1", deserialized.nodes[1].nodeId);
            Assert.AreEqual(1, deserialized.nodes[1].outgoing.Length);
            Assert.AreEqual("boss", deserialized.nodes[1].outgoing[0]);

            Assert.AreEqual("boss", deserialized.nodes[2].nodeId);
            Assert.AreEqual(0, deserialized.nodes[2].outgoing.Length);
        }

        [Test]
        public void JsonRoundTrip_PreservesPositions()
        {
            var original = new SerializableMapData
            {
                nodes = new List<SerializableNodeData>
                {
                    new SerializableNodeData
                    {
                        nodeId = "n1",
                        type = "Start",
                        position = new SerializableVector2(42.5f, -17.3f)
                    }
                }
            };

            string json = MapDataSerializer.Serialize(original);
            var deserialized = MapDataSerializer.Deserialize(json);

            Assert.AreEqual(42.5f, deserialized.nodes[0].position.x, 0.001f);
            Assert.AreEqual(-17.3f, deserialized.nodes[0].position.y, 0.001f);
        }

        // ═══════════════════════════════════════════
        //  Document Round-Trip (手工连接保存/加载)
        // ═══════════════════════════════════════════

        [Test]
        public void DocumentRoundTrip_PreservesManualConnections()
        {
            var doc = new MapEditorDocument();
            var startNode = doc.AddNode(RoguelikeNodeType.Start, Vector2.zero);
            var enemyNode = doc.AddNode(RoguelikeNodeType.MinorEnemy, new Vector2(100, 0));
            var bossNode = doc.AddNode(RoguelikeNodeType.Boss, new Vector2(200, 0));

            // 手工添加连接（跳过 enemy1，直接 start → boss）
            doc.AddConnection(startNode.nodeId, bossNode.nodeId);
            doc.AddConnection(startNode.nodeId, enemyNode.nodeId);
            doc.AddConnection(enemyNode.nodeId, bossNode.nodeId);

            // 序列化 → 反序列化 → 重新构建文档
            var data = doc.ToSerializable();
            string json = MapDataSerializer.Serialize(data);
            var loadedData = MapDataSerializer.Deserialize(json);
            var loadedDoc = MapEditorDocument.FromSerializable(loadedData);

            // 验证节点数量
            Assert.AreEqual(3, loadedDoc.nodes.Count);

            // 验证 start 节点的 outgoing
            var loadedStart = loadedDoc.GetNode(startNode.nodeId);
            Assert.IsNotNull(loadedStart);
            Assert.AreEqual(2, loadedStart.outgoing.Count);
            Assert.IsTrue(loadedStart.outgoing.Contains(bossNode.nodeId));
            Assert.IsTrue(loadedStart.outgoing.Contains(enemyNode.nodeId));

            // 验证 enemy 节点的 outgoing
            var loadedEnemy = loadedDoc.GetNode(enemyNode.nodeId);
            Assert.IsNotNull(loadedEnemy);
            Assert.AreEqual(1, loadedEnemy.outgoing.Count);
            Assert.IsTrue(loadedEnemy.outgoing.Contains(bossNode.nodeId));

            // 验证 incoming 重建正确
            var bossData = data.nodes.Find(n => n.nodeId == bossNode.nodeId);
            Assert.IsNotNull(bossData);
            Assert.AreEqual(2, bossData.incoming.Length);
            Assert.IsTrue(System.Array.Exists(bossData.incoming, id => id == startNode.nodeId));
            Assert.IsTrue(System.Array.Exists(bossData.incoming, id => id == enemyNode.nodeId));
        }

        [Test]
        public void DocumentRoundTrip_PreservesGlobalSettings()
        {
            var doc = new MapEditorDocument
            {
                maxReachableDistance = 500,
                visionRange = 7
            };
            doc.AddNode(RoguelikeNodeType.Start, Vector2.zero);

            var data = doc.ToSerializable();
            string json = MapDataSerializer.Serialize(data);
            var loadedData = MapDataSerializer.Deserialize(json);
            var loadedDoc = MapEditorDocument.FromSerializable(loadedData);

            Assert.AreEqual(500, loadedDoc.maxReachableDistance);
            Assert.AreEqual(7, loadedDoc.visionRange);
        }

        // ═══════════════════════════════════════════
        //  Treasure 配置 Round-Trip
        // ═══════════════════════════════════════════

        [Test]
        public void TreasureConfig_RoundTrip()
        {
            var data = new SerializableMapData
            {
                nodes = new List<SerializableNodeData>
                {
                    new SerializableNodeData
                    {
                        nodeId = "treasure1",
                        type = "Treasure",
                        position = new SerializableVector2(100, 100),
                        goldMin = 5,
                        goldMax = 10,
                        treasureEquipment = new List<SerializableNodeData.SerializableWeightedEquipmentData>
                        {
                            new SerializableNodeData.SerializableWeightedEquipmentData
                            {
                                equipmentId = "sword_01",
                                weight = 0.5f
                            },
                            new SerializableNodeData.SerializableWeightedEquipmentData
                            {
                                equipmentId = "shield_01",
                                weight = 0.3f
                            }
                        }
                    }
                }
            };

            string json = MapDataSerializer.Serialize(data);
            var deserialized = MapDataSerializer.Deserialize(json);

            var treasureNode = deserialized.nodes[0];
            Assert.AreEqual(5, treasureNode.goldMin);
            Assert.AreEqual(10, treasureNode.goldMax);
            Assert.AreEqual(2, treasureNode.treasureEquipment.Count);
            Assert.AreEqual("sword_01", treasureNode.treasureEquipment[0].equipmentId);
            Assert.AreEqual(0.5f, treasureNode.treasureEquipment[0].weight, 0.001f);
            Assert.AreEqual("shield_01", treasureNode.treasureEquipment[1].equipmentId);
            Assert.AreEqual(0.3f, treasureNode.treasureEquipment[1].weight, 0.001f);
        }

        [Test]
        public void TreasureConfig_DocumentRoundTrip()
        {
            var doc = new MapEditorDocument();
            var treasureNode = doc.AddNode(RoguelikeNodeType.Treasure, new Vector2(100, 100));

            // 验证 AddNode 为 Treasure 设置了默认 goldMin/goldMax
            Assert.AreEqual(2, treasureNode.goldMin);
            Assert.AreEqual(5, treasureNode.goldMax);

            // 修改配置
            treasureNode.goldMin = 10;
            treasureNode.goldMax = 20;
            treasureNode.equipmentEntries.Add(new EquipmentEntry
            {
                equipmentId = "axe_01",
                weight = 0.8f
            });

            // 通过 Document round-trip
            var data = doc.ToSerializable();
            string json = MapDataSerializer.Serialize(data);
            var loadedData = MapDataSerializer.Deserialize(json);
            var loadedDoc = MapEditorDocument.FromSerializable(loadedData);

            var loaded = loadedDoc.GetNode(treasureNode.nodeId);
            Assert.IsNotNull(loaded);
            Assert.AreEqual(10, loaded.goldMin);
            Assert.AreEqual(20, loaded.goldMax);
            Assert.AreEqual(1, loaded.equipmentEntries.Count);
            Assert.AreEqual("axe_01", loaded.equipmentEntries[0].equipmentId);
            Assert.AreEqual(0.8f, loaded.equipmentEntries[0].weight, 0.001f);
        }

        // ═══════════════════════════════════════════
        //  Store 配置 Round-Trip
        // ═══════════════════════════════════════════

        [Test]
        public void StoreConfig_RoundTrip()
        {
            var data = new SerializableMapData
            {
                nodes = new List<SerializableNodeData>
                {
                    new SerializableNodeData
                    {
                        nodeId = "store1",
                        type = "Store",
                        position = new SerializableVector2(100, 100),
                        storeGoods = new List<SerializableNodeData.SerializableStoreGoodData>
                        {
                            new SerializableNodeData.SerializableStoreGoodData
                            {
                                equipmentId = "potion_01",
                                price = 50
                            },
                            new SerializableNodeData.SerializableStoreGoodData
                            {
                                equipmentId = "scroll_01",
                                price = 120
                            }
                        }
                    }
                }
            };

            string json = MapDataSerializer.Serialize(data);
            var deserialized = MapDataSerializer.Deserialize(json);

            var storeNode = deserialized.nodes[0];
            Assert.AreEqual(2, storeNode.storeGoods.Count);
            Assert.AreEqual("potion_01", storeNode.storeGoods[0].equipmentId);
            Assert.AreEqual(50, storeNode.storeGoods[0].price);
            Assert.AreEqual("scroll_01", storeNode.storeGoods[1].equipmentId);
            Assert.AreEqual(120, storeNode.storeGoods[1].price);
        }

        [Test]
        public void StoreConfig_DocumentRoundTrip()
        {
            var doc = new MapEditorDocument();
            var storeNode = doc.AddNode(RoguelikeNodeType.Store, new Vector2(100, 100));

            storeNode.storeGoods.Add(new StoreGoodEntry
            {
                equipmentId = "potion_01",
                price = 50
            });
            storeNode.storeGoods.Add(new StoreGoodEntry
            {
                equipmentId = "scroll_01",
                price = 120
            });

            var data = doc.ToSerializable();
            string json = MapDataSerializer.Serialize(data);
            var loadedData = MapDataSerializer.Deserialize(json);
            var loadedDoc = MapEditorDocument.FromSerializable(loadedData);

            var loaded = loadedDoc.GetNode(storeNode.nodeId);
            Assert.IsNotNull(loaded);
            Assert.AreEqual(2, loaded.storeGoods.Count);
            Assert.AreEqual("potion_01", loaded.storeGoods[0].equipmentId);
            Assert.AreEqual(50, loaded.storeGoods[0].price);
            Assert.AreEqual("scroll_01", loaded.storeGoods[1].equipmentId);
            Assert.AreEqual(120, loaded.storeGoods[1].price);
        }

        // ═══════════════════════════════════════════
        //  节点删除后边清理
        // ═══════════════════════════════════════════

        [Test]
        public void NodeDeletion_CleansUpOutgoingReferences()
        {
            var doc = new MapEditorDocument();
            var startNode = doc.AddNode(RoguelikeNodeType.Start, Vector2.zero);
            var middleNode = doc.AddNode(RoguelikeNodeType.MinorEnemy, new Vector2(100, 0));
            var endNode = doc.AddNode(RoguelikeNodeType.Boss, new Vector2(200, 0));

            doc.AddConnection(startNode.nodeId, middleNode.nodeId);
            doc.AddConnection(middleNode.nodeId, endNode.nodeId);

            // 删除中间节点
            doc.RemoveNode(middleNode.nodeId);

            // 验证节点已移除
            Assert.IsFalse(doc.HasNode(middleNode.nodeId));
            Assert.AreEqual(2, doc.nodes.Count);

            // 验证 start 的 outgoing 不再引用已删除节点
            var start = doc.GetNode(startNode.nodeId);
            Assert.IsNotNull(start);
            Assert.IsFalse(start.outgoing.Contains(middleNode.nodeId));

            // 验证 end 节点未受影响
            var end = doc.GetNode(endNode.nodeId);
            Assert.IsNotNull(end);
            Assert.AreEqual(0, end.outgoing.Count);
        }

        [Test]
        public void NodeDeletion_CleansUpIncomingAfterRoundTrip()
        {
            var doc = new MapEditorDocument();
            var startNode = doc.AddNode(RoguelikeNodeType.Start, Vector2.zero);
            var middleNode = doc.AddNode(RoguelikeNodeType.MinorEnemy, new Vector2(100, 0));
            var endNode = doc.AddNode(RoguelikeNodeType.Boss, new Vector2(200, 0));

            doc.AddConnection(startNode.nodeId, middleNode.nodeId);
            doc.AddConnection(middleNode.nodeId, endNode.nodeId);

            // 删除中间节点
            doc.RemoveNode(middleNode.nodeId);

            // 序列化后验证 incoming 重建正确
            var data = doc.ToSerializable();
            var endData = data.nodes.Find(n => n.nodeId == endNode.nodeId);
            Assert.IsNotNull(endData);
            // end 的 incoming 应为空（middle 已删除，start 没有直接连 end）
            Assert.AreEqual(0, endData.incoming.Length);
        }

        [Test]
        public void RemoveConnection_WorksCorrectly()
        {
            var doc = new MapEditorDocument();
            var nodeA = doc.AddNode(RoguelikeNodeType.Start, Vector2.zero);
            var nodeB = doc.AddNode(RoguelikeNodeType.Boss, new Vector2(100, 0));

            doc.AddConnection(nodeA.nodeId, nodeB.nodeId);
            Assert.AreEqual(1, doc.GetAllConnections().Count);

            doc.RemoveConnection(nodeA.nodeId, nodeB.nodeId);
            Assert.AreEqual(0, doc.GetAllConnections().Count);

            var a = doc.GetNode(nodeA.nodeId);
            Assert.IsFalse(a.outgoing.Contains(nodeB.nodeId));
        }

        // ═══════════════════════════════════════════
        //  EditableMapNodeData.FromSerializableNode
        // ═══════════════════════════════════════════

        [Test]
        public void EditableMapNodeData_FromSerializable_Treasure()
        {
            var serializable = new SerializableNodeData
            {
                nodeId = "test_treasure",
                type = "Treasure",
                position = new SerializableVector2(50, 75),
                goldMin = 3,
                goldMax = 8,
                treasureEquipment = new List<SerializableNodeData.SerializableWeightedEquipmentData>
                {
                    new SerializableNodeData.SerializableWeightedEquipmentData
                    {
                        equipmentId = "shield_01",
                        weight = 0.3f
                    }
                }
            };

            var editable = EditableMapNodeData.FromSerializableNode(serializable);

            Assert.AreEqual("test_treasure", editable.nodeId);
            Assert.AreEqual(RoguelikeNodeType.Treasure, editable.nodeType);
            Assert.AreEqual(50f, editable.position.x, 0.001f);
            Assert.AreEqual(75f, editable.position.y, 0.001f);
            Assert.AreEqual(3, editable.goldMin);
            Assert.AreEqual(8, editable.goldMax);
            Assert.AreEqual(1, editable.equipmentEntries.Count);
            Assert.AreEqual("shield_01", editable.equipmentEntries[0].equipmentId);
            Assert.AreEqual(0.3f, editable.equipmentEntries[0].weight, 0.001f);
        }

        [Test]
        public void EditableMapNodeData_FromSerializable_Store()
        {
            var serializable = new SerializableNodeData
            {
                nodeId = "test_store",
                type = "Store",
                position = new SerializableVector2(200, 300),
                storeGoods = new List<SerializableNodeData.SerializableStoreGoodData>
                {
                    new SerializableNodeData.SerializableStoreGoodData
                    {
                        equipmentId = "potion_01",
                        price = 50
                    }
                }
            };

            var editable = EditableMapNodeData.FromSerializableNode(serializable);

            Assert.AreEqual("test_store", editable.nodeId);
            Assert.AreEqual(RoguelikeNodeType.Store, editable.nodeType);
            Assert.AreEqual(1, editable.storeGoods.Count);
            Assert.AreEqual("potion_01", editable.storeGoods[0].equipmentId);
            Assert.AreEqual(50, editable.storeGoods[0].price);
        }

        [Test]
        public void EditableMapNodeData_ToSerializable_PreservesOutgoing()
        {
            var editable = new EditableMapNodeData
            {
                nodeId = "node_a",
                nodeType = RoguelikeNodeType.MinorEnemy,
                position = new Vector2(100, 0)
            };
            editable.outgoing.Add("node_b");
            editable.outgoing.Add("node_c");

            var serializable = editable.ToSerializableNode();

            Assert.AreEqual(2, serializable.outgoing.Length);
            Assert.AreEqual("node_b", serializable.outgoing[0]);
            Assert.AreEqual("node_c", serializable.outgoing[1]);
        }

        // ═══════════════════════════════════════════
        //  Document 边界情况
        // ═══════════════════════════════════════════

        [Test]
        public void AddConnection_RejectsSelfLoop()
        {
            var doc = new MapEditorDocument();
            var node = doc.AddNode(RoguelikeNodeType.Start, Vector2.zero);

            doc.AddConnection(node.nodeId, node.nodeId);

            Assert.AreEqual(0, doc.GetAllConnections().Count);
        }

        [Test]
        public void AddConnection_RejectsDuplicate()
        {
            var doc = new MapEditorDocument();
            var a = doc.AddNode(RoguelikeNodeType.Start, Vector2.zero);
            var b = doc.AddNode(RoguelikeNodeType.Boss, new Vector2(100, 0));

            doc.AddConnection(a.nodeId, b.nodeId);
            doc.AddConnection(a.nodeId, b.nodeId); // 重复添加

            Assert.AreEqual(1, doc.GetAllConnections().Count);
            Assert.AreEqual(1, doc.GetNode(a.nodeId).outgoing.Count);
        }

        [Test]
        public void Clear_RemovesAllNodes()
        {
            var doc = new MapEditorDocument();
            doc.AddNode(RoguelikeNodeType.Start, Vector2.zero);
            doc.AddNode(RoguelikeNodeType.Boss, new Vector2(100, 0));

            doc.Clear();

            Assert.AreEqual(0, doc.nodes.Count);
            Assert.IsTrue(doc.IsDirty);
        }
    }
}
