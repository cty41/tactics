using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;

namespace Tactics.RoguelikeMap
{
    /// <summary>
    /// 节点可见性状态
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum NodeVisibility
    {
        Hidden,   // 完全隐藏：不可见
        Fogged,   // 迷雾状态：显示轮廓但不可交互
        Revealed  // 已揭示：完全可见
    }

    /// <summary>
    /// 节点访问状态
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum NodeVisitState
    {
        Unvisited,  // 未访问
        Visited     // 已访问
    }

    /// <summary>
    /// 节点状态枚举
    /// </summary>
    [Obsolete("Use NodeVisibility and NodeVisitState instead.")]
    public enum NodeState
    {
        Unrevealed,  // 未揭示：灰色问号，不可点击
        Revealed,    // 已揭示：真实图标，半透明，不可点击
        Reachable,   // 可到达：真实图标，高亮边框，可点击
        Visited      // 已访问：半透明，不可点击
    }

    /// <summary>
    /// Buff配置项（BuffConfig 引用 + 可选概率权重）
    /// </summary>
    [System.Serializable]
    public class BuffConfigEntry
    {
        public Tactics.Common.Units.Buffs.BuffConfig buffConfig;
        public float weight = 1f;
    }

    /// <summary>
    /// 装备奖励配置项
    /// </summary>
    [System.Serializable]
    public class EquipmentEntry
    {
        public string equipmentId;
        public float weight = 1f;
    }

    /// <summary>
    /// 商店商品配置项
    /// </summary>
    [System.Serializable]
    public class StoreGoodEntry
    {
        public string equipmentId;
        public int price = 5;
    }

    /// <summary>
    /// 宝藏节点奖励配置
    /// </summary>
    [System.Serializable]
    public class TreasureNodeConfig
    {
        public int goldMin = 2;
        public int goldMax = 5;
        public System.Collections.Generic.List<BuffConfigEntry> buffEntries = new System.Collections.Generic.List<BuffConfigEntry>();
        public System.Collections.Generic.List<EquipmentEntry> equipmentEntries = new System.Collections.Generic.List<EquipmentEntry>();

        public TreasureNodeConfig Clone()
        {
            return new TreasureNodeConfig
            {
                goldMin = goldMin,
                goldMax = goldMax,
                buffEntries = buffEntries?.ConvertAll(e => new BuffConfigEntry
                {
                    buffConfig = e.buffConfig,
                    weight = e.weight
                }) ?? new System.Collections.Generic.List<BuffConfigEntry>(),
                equipmentEntries = equipmentEntries?.ConvertAll(e => new EquipmentEntry
                {
                    equipmentId = e.equipmentId,
                    weight = e.weight
                }) ?? new System.Collections.Generic.List<EquipmentEntry>()
            };
        }
    }

    /// <summary>
    /// 商店节点商品配置
    /// </summary>
    [System.Serializable]
    public class StoreNodeConfig
    {
        public System.Collections.Generic.List<StoreGoodEntry> goods = new System.Collections.Generic.List<StoreGoodEntry>();

        public StoreNodeConfig Clone()
        {
            return new StoreNodeConfig
            {
                goods = goods?.ConvertAll(g => new StoreGoodEntry
                {
                    equipmentId = g.equipmentId,
                    price = g.price
                }) ?? new System.Collections.Generic.List<StoreGoodEntry>()
            };
        }
    }

    public class RoguelikeMapNode
    {
        public readonly string nodeId;
        public readonly List<string> incoming = new List<string>();
        public readonly List<string> outgoing = new List<string>();
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty("type")]
                public readonly RoguelikeNodeType nodeType;
        public readonly string blueprintName;
        public Vector2 position;
        [Obsolete("Use Visibility and VisitState instead.")]
        [JsonConverter(typeof(StringEnumConverter))]
        public NodeState state = NodeState.Unrevealed;

        [JsonConverter(typeof(StringEnumConverter))]
        public NodeVisibility Visibility = NodeVisibility.Hidden;

        [JsonConverter(typeof(StringEnumConverter))]
        public NodeVisitState VisitState = NodeVisitState.Unvisited;

        /// <summary>
        /// 是否可到达（用于 UI 交互判定）
        /// </summary>
        public bool IsReachable = false;

        /// <summary>
        /// 是否已消耗（用于 Mystery/Treasure/RestSite 节点，首次访问后标记，重访不再触发事件）
        /// </summary>
        public bool IsConsumed = false;

        /// <summary>
        /// 事件 ID（用于 Mystery / 自定义事件节点）。
        /// </summary>
        public string eventId = "";

        /// <summary>
        /// 宝藏节点奖励配置（仅 Treasure 类型节点使用）。
        /// </summary>
        public TreasureNodeConfig treasureConfig;

        /// <summary>
        /// 商店节点商品配置（仅 Store 类型节点使用）。
        /// </summary>
        public StoreNodeConfig storeConfig;

        [Obsolete("Use RoguelikeMapNode(string nodeId, RoguelikeNodeType, string, Vector2) instead.")]
        public RoguelikeMapNode(RoguelikeNodeType nodeType, string blueprintName, Vector2Int point)
        {
            this.nodeType = nodeType;
            this.blueprintName = blueprintName;
            this.nodeId = $"{point.x},{point.y}";
        }

        [JsonConstructor]
                public RoguelikeMapNode(string nodeId, RoguelikeNodeType nodeType, string blueprintName, Vector2 position)
        {
            this.nodeId = nodeId;
            this.nodeType = nodeType;
            this.blueprintName = blueprintName;
            this.position = position;
        }

        public void AddIncoming(string id)
        {
            if (incoming.Contains(id))
                return;

            incoming.Add(id);
        }

        public void AddOutgoing(string id)
        {
            if (outgoing.Contains(id))
                return;

            outgoing.Add(id);
        }

        public void RemoveIncoming(string id)
        {
            incoming.Remove(id);
        }

        public void RemoveOutgoing(string id)
        {
            outgoing.Remove(id);
        }

        public bool HasNoConnections()
        {
            return incoming.Count == 0 && outgoing.Count == 0;
        }
    }
}
