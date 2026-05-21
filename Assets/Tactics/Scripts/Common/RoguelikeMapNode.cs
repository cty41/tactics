using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;

namespace Tactics.RoguelikeMap
{
    /// <summary>
    /// 节点状态枚举
    /// </summary>
    public enum NodeState
    {
        Unrevealed,  // 未揭示：灰色问号，不可点击
        Revealed,    // 已揭示：真实图标，半透明，不可点击
        Reachable,   // 可到达：真实图标，高亮边框，可点击
        Visited      // 已访问：半透明，不可点击
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
        [JsonConverter(typeof(StringEnumConverter))]
        public NodeState state = NodeState.Unrevealed;

        /// <summary>
        /// 事件 ID（用于 Mystery / 自定义事件节点）。
        /// </summary>
        public string eventId = "";

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
