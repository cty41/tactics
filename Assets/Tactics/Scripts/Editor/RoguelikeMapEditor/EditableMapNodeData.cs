using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Tactics.RoguelikeMap;

namespace Tactics.Editor.RoguelikeMapEditor
{
    /// <summary>
    /// Editor-only 可编辑节点数据，从 SerializableNodeData / RoguelikeMapNode 转换而来。
    /// 所有编辑操作都通过此类进行，是 MapEditorDocument 的组成部分。
    /// </summary>
    [Serializable]
    public class EditableMapNodeData
    {
        public string nodeId;
        public RoguelikeNodeType nodeType;
        public Vector2 position;
        public List<string> outgoing = new();
        public string eventId;
        public string blueprintName;

        // Treasure 配置
        public int? goldMin;
        public int? goldMax;
        public List<BuffConfigEntry> buffEntries = new();
        public List<EquipmentEntry> equipmentEntries = new();

        // Store 配置
        public List<StoreGoodEntry> storeGoods = new();

        /// <summary>
        /// 从 SerializableNodeData 创建可编辑节点数据。
        /// </summary>
        public static EditableMapNodeData FromSerializableNode(SerializableNodeData nodeData)
        {
            var nodeType = Enum.Parse<RoguelikeNodeType>(nodeData.type, true);
            var editable = new EditableMapNodeData
            {
                nodeId = nodeData.nodeId,
                nodeType = nodeType,
                position = nodeData.position.ToVector2(),
                blueprintName = nodeData.blueprintName,
                eventId = nodeData.eventId,
                goldMin = nodeData.goldMin,
                goldMax = nodeData.goldMax,
                storeGoods = new List<StoreGoodEntry>()
            };

            // 复制 outgoing
            if (nodeData.outgoing != null)
                editable.outgoing = new List<string>(nodeData.outgoing);

            // 转换 treasureEquipment
            if (nodeData.treasureEquipment != null)
            {
                foreach (var e in nodeData.treasureEquipment)
                {
                    editable.equipmentEntries.Add(new EquipmentEntry
                    {
                        equipmentId = e.equipmentId,
                        weight = e.weight
                    });
                }
            }

            // 转换 treasureBuffs → buffEntries
            if (nodeData.treasureBuffs != null)
            {
                foreach (var b in nodeData.treasureBuffs)
                {
                    var buffConfig = !string.IsNullOrEmpty(b.buffConfigPath)
                        ? AssetDatabase.LoadAssetAtPath<Tactics.Common.Units.Buffs.BuffConfig>(b.buffConfigPath)
                        : null;
                    editable.buffEntries.Add(new BuffConfigEntry
                    {
                        buffConfig = buffConfig,
                        weight = b.weight
                    });
                }
            }

            // 转换 storeGoods
            if (nodeData.storeGoods != null)
            {
                foreach (var g in nodeData.storeGoods)
                {
                    editable.storeGoods.Add(new StoreGoodEntry
                    {
                        equipmentId = g.equipmentId,
                        price = g.price
                    });
                }
            }

            return editable;
        }

        /// <summary>
        /// 从 RoguelikeMapNode 创建可编辑节点数据。
        /// </summary>
        public static EditableMapNodeData FromRuntimeNode(RoguelikeMapNode node)
        {
            var editable = new EditableMapNodeData
            {
                nodeId = node.nodeId,
                nodeType = node.nodeType,
                position = node.position,
                blueprintName = node.blueprintName,
                eventId = node.eventId,
                goldMin = node.treasureConfig?.goldMin,
                goldMax = node.treasureConfig?.goldMax,
                outgoing = new List<string>(node.outgoing)
            };

            // 复制 treasureConfig
            if (node.treasureConfig != null)
            {
                editable.buffEntries = node.treasureConfig.buffEntries?.ConvertAll(e => new BuffConfigEntry
                {
                    buffConfig = e.buffConfig,
                    weight = e.weight
                }) ?? new List<BuffConfigEntry>();
                editable.equipmentEntries = node.treasureConfig.equipmentEntries?.ConvertAll(e => new EquipmentEntry
                {
                    equipmentId = e.equipmentId,
                    weight = e.weight
                }) ?? new List<EquipmentEntry>();
            }

            // 复制 storeConfig
            if (node.storeConfig != null)
            {
                editable.storeGoods = node.storeConfig.goods?.ConvertAll(g => new StoreGoodEntry
                {
                    equipmentId = g.equipmentId,
                    price = g.price
                }) ?? new List<StoreGoodEntry>();
            }

            return editable;
        }

        /// <summary>
        /// 转换为 SerializableNodeData 用于 JSON 序列化。
        /// </summary>
        public SerializableNodeData ToSerializableNode()
        {
            var nodeData = new SerializableNodeData
            {
                nodeId = nodeId,
                type = nodeType.ToString(),
                position = SerializableVector2.FromVector2(position),
                blueprintName = blueprintName,
                outgoing = outgoing.ToArray(),
                incoming = Array.Empty<string>(), // 由文档模型在导出时重建
                eventId = string.IsNullOrEmpty(eventId) ? null : eventId,
                goldMin = goldMin,
                goldMax = goldMax,
                treasureEquipment = new List<SerializableNodeData.SerializableWeightedEquipmentData>(),
                storeGoods = new List<SerializableNodeData.SerializableStoreGoodData>(),
                treasureBuffs = new List<SerializableNodeData.SerializableBuffEntryData>()
            };

            // 转换 equipmentEntries → treasureEquipment
            foreach (var e in equipmentEntries)
            {
                nodeData.treasureEquipment.Add(new SerializableNodeData.SerializableWeightedEquipmentData
                {
                    equipmentId = e.equipmentId,
                    weight = e.weight
                });
            }

            // 转换 buffEntries → treasureBuffs
            foreach (var b in buffEntries)
            {
                var buffPath = b.buffConfig != null
                    ? AssetDatabase.GetAssetPath(b.buffConfig)
                    : null;
                nodeData.treasureBuffs.Add(new SerializableNodeData.SerializableBuffEntryData
                {
                    buffConfigPath = buffPath,
                    weight = b.weight
                });
            }

            // 转换 storeGoods
            foreach (var g in storeGoods)
            {
                nodeData.storeGoods.Add(new SerializableNodeData.SerializableStoreGoodData
                {
                    equipmentId = g.equipmentId,
                    price = g.price
                });
            }

            return nodeData;
        }

        /// <summary>
        /// 创建 TreasureNodeConfig 副本（用于运行时兼容）。
        /// </summary>
        public TreasureNodeConfig ToTreasureConfig()
        {
            if (nodeType != RoguelikeNodeType.Treasure) return null;
            return new TreasureNodeConfig
            {
                goldMin = goldMin ?? 2,
                goldMax = goldMax ?? 5,
                buffEntries = buffEntries?.ConvertAll(e => new BuffConfigEntry
                {
                    buffConfig = e.buffConfig,
                    weight = e.weight
                }) ?? new List<BuffConfigEntry>(),
                equipmentEntries = equipmentEntries?.ConvertAll(e => new EquipmentEntry
                {
                    equipmentId = e.equipmentId,
                    weight = e.weight
                }) ?? new List<EquipmentEntry>()
            };
        }

        /// <summary>
        /// 创建 StoreNodeConfig 副本（用于运行时兼容）。
        /// </summary>
        public StoreNodeConfig ToStoreConfig()
        {
            if (nodeType != RoguelikeNodeType.Store) return null;
            return new StoreNodeConfig
            {
                goods = storeGoods?.ConvertAll(g => new StoreGoodEntry
                {
                    equipmentId = g.equipmentId,
                    price = g.price
                }) ?? new List<StoreGoodEntry>()
            };
        }

        /// <summary>
        /// 添加出边连接（去重）。
        /// </summary>
        public void AddOutgoing(string targetId)
        {
            if (!outgoing.Contains(targetId))
                outgoing.Add(targetId);
        }

        /// <summary>
        /// 移除出边连接。
        /// </summary>
        public void RemoveOutgoing(string targetId)
        {
            outgoing.Remove(targetId);
        }

        /// <summary>
        /// 深拷贝此节点数据。
        /// </summary>
        public EditableMapNodeData Clone()
        {
            var clone = new EditableMapNodeData
            {
                nodeId = nodeId,
                nodeType = nodeType,
                position = position,
                blueprintName = blueprintName,
                eventId = eventId,
                goldMin = goldMin,
                goldMax = goldMax,
                outgoing = new List<string>(outgoing),
                buffEntries = buffEntries?.ConvertAll(e => new BuffConfigEntry
                {
                    buffConfig = e.buffConfig,
                    weight = e.weight
                }) ?? new List<BuffConfigEntry>(),
                equipmentEntries = equipmentEntries?.ConvertAll(e => new EquipmentEntry
                {
                    equipmentId = e.equipmentId,
                    weight = e.weight
                }) ?? new List<EquipmentEntry>(),
                storeGoods = storeGoods?.ConvertAll(g => new StoreGoodEntry
                {
                    equipmentId = g.equipmentId,
                    price = g.price
                }) ?? new List<StoreGoodEntry>()
            };
            return clone;
        }
    }
}
