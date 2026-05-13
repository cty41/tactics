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
                throw new ArgumentException("JSON 字符串不能为空", nameof(json));

            var data = JsonConvert.DeserializeObject<SerializableEventData>(json);
            if (data == null)
                throw new InvalidOperationException("JSON 反序列化失败");

            Validate(data);
            return data;
        }

        /// <summary>
        /// 验证事件数据的完整性。
        /// </summary>
        public static void Validate(SerializableEventData data)
        {
            if (string.IsNullOrWhiteSpace(data.eventId))
                throw new InvalidOperationException("eventId 不能为空");

            if (string.IsNullOrWhiteSpace(data.title))
                throw new InvalidOperationException("title 不能为空");

            if (string.IsNullOrWhiteSpace(data.region))
                throw new InvalidOperationException("region 不能为空");

            if (data.nodes == null || data.nodes.Count == 0)
                throw new InvalidOperationException("至少需要一个节点");

            bool hasStart = false;
            bool hasEnd = false;
            var nodeIds = new HashSet<string>();

            foreach (var node in data.nodes)
            {
                if (string.IsNullOrWhiteSpace(node.nodeId))
                    throw new InvalidOperationException("节点 ID 不能为空");

                if (!nodeIds.Add(node.nodeId))
                    throw new InvalidOperationException($"节点 ID 重复: {node.nodeId}");

                if (node.type == EventNodeTypes.Start) hasStart = true;
                if (node.type == EventNodeTypes.End) hasEnd = true;
            }

            if (!hasStart)
                throw new InvalidOperationException("缺少 Start 节点");
            if (!hasEnd)
                throw new InvalidOperationException("缺少 End 节点");
        }
    }
}
