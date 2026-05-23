using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Tactics.RoguelikeMap
{
    public class RoguelikeMap
    {
        public List<RoguelikeMapNode> nodes;
                public HashSet<string> visitedNodes = new HashSet<string>();
        public string bossNodeName;
        public string configName;
        public float maxReachableDistance;
        public float visionRange;

        /// <summary>
        /// 商店购买状态存储。Key = nodeId，Value = 已购买商品名称列表。
        /// </summary>
        public Dictionary<string, List<string>> StorePurchases = new Dictionary<string, List<string>>();

        public RoguelikeMap() { }


        public RoguelikeMap(string configName, string bossNodeName, List<RoguelikeMapNode> nodes,
            HashSet<string> visitedNodes, float maxReachableDistance = 0f, float visionRange = 0f)
        {
            this.configName = configName;
            this.bossNodeName = bossNodeName;
            this.nodes = nodes;
            this.visitedNodes = visitedNodes ?? new HashSet<string>();
            this.maxReachableDistance = maxReachableDistance;
            this.visionRange = visionRange;
        }

        public RoguelikeMapNode GetBossNode()
        {
            return nodes.FirstOrDefault(n => n.nodeType == RoguelikeNodeType.Boss);
        }

        public RoguelikeMapNode GetNode(string nodeId)
        {
            return nodes.FirstOrDefault(n => n.nodeId == nodeId);
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented, new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            });
        }

        /// <summary>
        /// 记录商店购买。
        /// </summary>
        public void AddStorePurchase(string nodeId, string goodName)
        {
            if (!StorePurchases.ContainsKey(nodeId))
                StorePurchases[nodeId] = new List<string>();
            if (!StorePurchases[nodeId].Contains(goodName))
                StorePurchases[nodeId].Add(goodName);
        }

        /// <summary>
        /// 获取指定商店的已购买商品列表。
        /// </summary>
        public List<string> GetStorePurchases(string nodeId)
        {
            return StorePurchases.TryGetValue(nodeId, out var list) ? list : new List<string>();
        }

        /// <summary>
        /// 检查指定商店的某商品是否已购买。
        /// </summary>
        public bool IsStoreGoodPurchased(string nodeId, string goodName)
        {
            return StorePurchases.TryGetValue(nodeId, out var list) && list.Contains(goodName);
        }
    }
}
