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
        public HashSet<string> visitedNodes;
        public string bossNodeName;
        public string configName;
        public float maxReachableDistance;
        public float visionRange;

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
    }
}
