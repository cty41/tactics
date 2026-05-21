using System;
using System.Collections.Generic;

namespace Tactics.Editor.RoguelikeEventEditor
{
    /// <summary>
    /// 可序列化的事件数据模型。这是编辑器和 JSON 之间的核心交换格式。
    /// </summary>
    [Serializable]
    public class SerializableEventData
    {
        public string eventId;
        public string title;
        public string description;
        public string region;
        public string version = "1.0";
        public List<EventNodeData> nodes = new List<EventNodeData>();
        public List<EventConnectionData> connections = new List<EventConnectionData>();
    }

    /// <summary>
    /// 单个节点的数据。
    /// </summary>
    [Serializable]
    public class EventNodeData
    {
        public string nodeId;
        public string type; // Start | Option | Check | Success | Failure | Branch | End
        public NodePosition position;
        public EventNodePayload data;
    }

    /// <summary>
    /// 节点位置（用于编辑器内恢复布局）。
    /// </summary>
    [Serializable]
    public class NodePosition
    {
        public float x;
        public float y;
    }

    /// <summary>
    /// 节点的具体属性数据。
    /// </summary>
    [Serializable]
    public class EventNodePayload
    {
        // Start 节点
        public string eventId;
        public string title;
        public string description;
        public string region;

        // Option 节点
        public string text;
        public string attribute;      // Strength | Dexterity | Constitution | Intelligence | Charisma
        public int? successRate;

        // Check 节点（自动从上游 Option 继承）
        public int? difficultyModifier;

        // Result 节点 (Success / Failure)
        public string resultType;     // gold | item | equip | buff | damage | damage_all | heal | battle | exp | nothing
        public int? amount;
        public string itemId;
        public string equipId;
        public string buffId;
        public string enemyGroupId;
        public string resultText;
        public string target;         // self | random | all

        // End 节点
        public string summaryText;
    }

    /// <summary>
    /// 节点间连接。
    /// </summary>
    [Serializable]
    public class EventConnectionData
    {
        public string from;
        public string to;
        public string port; // out | success | failure | branch_0 | branch_1 | ...
    }

    // ── 枚举常亮 ─────────────────────────────────
    public static class EventNodeTypes
    {
        public const string Start = "Start";
        public const string Option = "Option";
        public const string Check = "Check";
        public const string Success = "Success";
        public const string Failure = "Failure";
        public const string Branch = "Branch";
        public const string End = "End";
    }

    public static class EventAttributes
    {
        public const string Strength = "Strength";
        public const string Dexterity = "Dexterity";
        public const string Constitution = "Constitution";
        public const string Intelligence = "Intelligence";
        public const string Charisma = "Charisma";

        public static readonly string[] All =
            { Strength, Dexterity, Constitution, Intelligence, Charisma };

        public static readonly string[] DisplayNames =
            { "Strength", "Dexterity", "Constitution", "Intelligence", "Charisma" };
    }

    public static class EventResultTypes
    {
        public const string Gold = "gold";
        public const string Item = "item";
        public const string Equip = "equip";
        public const string Buff = "buff";
        public const string Damage = "damage";
        public const string DamageAll = "damage_all";
        public const string Heal = "heal";
        public const string Battle = "battle";
        public const string Exp = "exp";
        public const string Nothing = "nothing";

        public static readonly string[] All =
            { Gold, Item, Equip, Buff, Damage, DamageAll, Heal, Battle, Exp, Nothing };
    }

    public static class EventTargetTypes
    {
        public const string Self = "self";
        public const string RandomAlly = "random";
        public const string All = "all";

        public static readonly string[] AllValues =
            { Self, RandomAlly, All };

        public static readonly string[] DisplayNames =
            { "Self", "Random Ally", "All" };
    }

    public static class EventRegions
    {
        public const string DarkForest = "DarkForest";
        public const string BurialGrounds = "BurialGrounds";
        public const string Monastery = "Monastery";

        public static readonly string[] All = { DarkForest, BurialGrounds, Monastery };
        public static readonly string[] DisplayNames = { "Dark Forest", "Burial Grounds", "Monastery" };
    }
}
