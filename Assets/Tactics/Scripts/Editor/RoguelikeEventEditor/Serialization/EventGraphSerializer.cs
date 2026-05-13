using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Tactics.Editor.RoguelikeEventEditor
{
    /// <summary>
    /// 事件图的 JSON 序列化/反序列化器。
    /// </summary>
    public static class EventGraphSerializer
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };

        /// <summary>
        /// 将 SerializableEventData 序列化为 JSON 字符串。
        /// </summary>
        public static string Serialize(SerializableEventData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            Validate(data);
            return JsonConvert.SerializeObject(data, Settings);
        }

        /// <summary>
        /// 将 JSON 字符串反序列化为 SerializableEventData。
        /// </summary>
        public static SerializableEventData Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("JSON string cannot be empty", nameof(json));

            var data = JsonConvert.DeserializeObject<SerializableEventData>(json);
            if (data == null)
                throw new InvalidOperationException("JSON deserialization failed");

            Validate(data);
            return data;
        }

        /// <summary>
        /// 验证事件数据的完整性。
        /// </summary>
        public static void Validate(SerializableEventData data)
        {
            if (string.IsNullOrWhiteSpace(data.eventId))
                throw new InvalidOperationException("eventId cannot be empty");

            if (string.IsNullOrWhiteSpace(data.title))
                throw new InvalidOperationException("title cannot be empty");

            if (string.IsNullOrWhiteSpace(data.region))
                throw new InvalidOperationException("region cannot be empty");

            if (data.nodes == null || data.nodes.Count == 0)
                throw new InvalidOperationException("At least one node required");

            bool hasStart = false;
            bool hasEnd = false;
            var nodeIds = new HashSet<string>();

            foreach (var node in data.nodes)
            {
                if (string.IsNullOrWhiteSpace(node.nodeId))
                    throw new InvalidOperationException("Node ID cannot be empty");

                if (!nodeIds.Add(node.nodeId))
                    throw new InvalidOperationException($"Duplicate node ID: {node.nodeId}");

                if (node.type == EventNodeTypes.Start) hasStart = true;
                if (node.type == EventNodeTypes.End) hasEnd = true;
            }

            if (!hasStart)
                throw new InvalidOperationException("Missing Start node");
            if (!hasEnd)
                throw new InvalidOperationException("Missing End node");
        }
    }
}
