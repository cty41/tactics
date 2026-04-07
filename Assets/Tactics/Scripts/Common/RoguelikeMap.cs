using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Tactics.RoguelikeMap
{
    public class RoguelikeMap
    {
        public List<RoguelikeMapNode> nodes;
        public List<Vector2Int> path;
        public string bossNodeName;
        public string configName;

        public RoguelikeMap(string configName, string bossNodeName, List<RoguelikeMapNode> nodes, List<Vector2Int> path)
        {
            this.configName = configName;
            this.bossNodeName = bossNodeName;
            this.nodes = nodes;
            this.path = path;
        }

        public RoguelikeMapNode GetBossNode()
        {
            return nodes.FirstOrDefault(n => n.nodeType == RoguelikeNodeType.Boss);
        }

        public float DistanceBetweenFirstAndLastLayers()
        {
            RoguelikeMapNode bossNode = GetBossNode();
            RoguelikeMapNode firstLayerNode = nodes.FirstOrDefault(n => n.point.y == 0);

            if (bossNode == null || firstLayerNode == null)
                return 0f;

            return bossNode.position.y - firstLayerNode.position.y;
        }

        public RoguelikeMapNode GetNode(Vector2Int point)
        {
            return nodes.FirstOrDefault(n => n.point.Equals(point));
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