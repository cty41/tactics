using System.Collections.Generic;
using Newtonsoft.Json;

namespace Tactics.RoguelikeMap.Events
{
    /// <summary>
    /// Roguelike事件数据
    /// </summary>
    [System.Serializable]
    public class RoguelikeEvent
    {
        [JsonProperty("eventId")]
        public string eventId;

        [JsonProperty("title")]
        public string title;

        [JsonProperty("description")]
        public string description;

        [JsonProperty("options")]
        public List<EventOption> options;

        /// <summary>
        /// 从JSON加载事件
        /// </summary>
        public static RoguelikeEvent FromJson(string json)
        {
            return JsonConvert.DeserializeObject<RoguelikeEvent>(json);
        }

        /// <summary>
        /// 转换为JSON
        /// </summary>
        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        /// <summary>
        /// 获取选项数量
        /// </summary>
        public int GetOptionCount()
        {
            return options?.Count ?? 0;
        }

        /// <summary>
        /// 获取指定索引的选项
        /// </summary>
        public EventOption GetOption(int index)
        {
            if (options == null || index < 0 || index >= options.Count)
                return null;

            return options[index];
        }
    }
}
