using System;
using System.Collections.Generic;
using System.Linq;
using Tactics.Runtime.Utilities;
using Tactics.Roster;
using UnityEngine;

namespace Tactics.RoguelikeMap.Events
{
    /// <summary>
    /// 事件效果上下文
    /// 管理队伍信息、目标选取和属性读取
    /// 用于在事件选项执行时提供BG3式属性判定上下文
    /// </summary>
    public class EventEffectContext
    {
        /// <summary>队伍成员列表</summary>
        public List<CharacterDefinition> Party { get; private set; }

        /// <summary>自身角色ID（默认为队伍第一个角色）</summary>
        public string SelfCharacterId { get; set; }

        public EventEffectContext(List<CharacterDefinition> party, string selfCharacterId = null)
        {
            Party = party ?? new List<CharacterDefinition>();
            SelfCharacterId = selfCharacterId;
        }

        /// <summary>
        /// 获取自身角色定义
        /// </summary>
        public CharacterDefinition GetSelfCharacter()
        {
            if (Party.Count == 0)
                return null;

            // 优先按SelfCharacterId查找
            if (!string.IsNullOrEmpty(SelfCharacterId))
            {
                var character = Party.FirstOrDefault(c => c.Id == SelfCharacterId);
                if (character != null)
                    return character;
            }

            // 降级：返回第一个角色
            return Party[0];
        }

        /// <summary>
        /// 根据目标类型选取角色
        /// </summary>
        /// <param name="target">目标类型</param>
        /// <returns>选取的角色定义</returns>
        public CharacterDefinition PickTarget(EventTargetType target)
        {
            if (Party.Count == 0)
            {
                TLog.Warning("[EventEffectContext] 队伍为空，无法选取目标");
                return null;
            }

            switch (target)
            {
                case EventTargetType.Self:
                    return GetSelfCharacter();

                case EventTargetType.RandomAlly:
                    int randomIndex = UnityEngine.Random.Range(0, Party.Count);
                    TLog.Info($"[EventEffectContext] 随机选取队友: {Party[randomIndex].DisplayName}");
                    return Party[randomIndex];

                case EventTargetType.All:
                    return GetSelfCharacter();

                default:
                    TLog.Warning($"[EventEffectContext] 未知目标类型: {target}");
                    return GetSelfCharacter();
            }
        }

        /// <summary>
        /// 根据目标类型和属性类型选取角色（属性最高者）
        /// </summary>
        /// <param name="target">目标类型</param>
        /// <param name="attribute">属性类型</param>
        /// <returns>选取的角色定义</returns>
        public CharacterDefinition PickTarget(EventTargetType target, AttributeType attribute)
        {
            if (Party.Count == 0)
            {
                TLog.Warning("[EventEffectContext] 队伍为空，无法选取目标");
                return null;
            }

            switch (target)
            {
                case EventTargetType.Self:
                    return GetSelfCharacter();

                case EventTargetType.RandomAlly:
                    int randomIndex = UnityEngine.Random.Range(0, Party.Count);
                    var randomChar = Party[randomIndex];
                    TLog.Info($"[EventEffectContext] 随机选取队友: {randomChar.DisplayName}");
                    return randomChar;

                case EventTargetType.All:
                    // 选取相关属性最高的角色（用于UI展示）
                    var bestCharacter = Party
                        .OrderByDescending(c => GetCharacterAttribute(c, attribute))
                        .FirstOrDefault();
                    if (bestCharacter != null)
                    {
                        TLog.Info($"[EventEffectContext] 属性最高角色: {bestCharacter.DisplayName}, " +
                                  $"属性: {attribute}, 值: {GetCharacterAttribute(bestCharacter, attribute)}");
                    }
                    return bestCharacter;

                default:
                    return GetSelfCharacter();
            }
        }

        /// <summary>
        /// 读取角色的指定属性值
        /// 映射 EventOption.AttributeType → CharacterDefinition 属性
        /// </summary>
        /// <param name="character">角色定义</param>
        /// <param name="attributeType">属性类型</param>
        /// <returns>属性值</returns>
        public static int GetCharacterAttribute(CharacterDefinition character, AttributeType attributeType)
        {
            if (character == null)
                return 0;

            switch (attributeType)
            {
                case AttributeType.None:
                    return 0;
                case AttributeType.Strength:
                    return character.Strength;
                case AttributeType.Dexterity:
                    // CharacterDefinition 使用 Agility 对应 Dexterity
                    return character.Agility;
                case AttributeType.Constitution:
                    return character.Constitution;
                case AttributeType.Intelligence:
                    return character.Intelligence;
                case AttributeType.Charisma:
                    return character.Charisma;
                default:
                    TLog.Warning($"[EventEffectContext] 未知属性类型: {attributeType}");
                    return 0;
            }
        }

        /// <summary>
        /// 获取属性名称（中文）
        /// </summary>
        public static string GetAttributeName(AttributeType attributeType)
        {
            switch (attributeType)
            {
                case AttributeType.Strength: return "力量";
                case AttributeType.Dexterity: return "敏捷";
                case AttributeType.Constitution: return "体质";
                case AttributeType.Intelligence: return "智力";
                case AttributeType.Charisma: return "魅力";
                default: return "无";
            }
        }

        /// <summary>
        /// 生成BG3风格判定描述文本
        /// 格式: "由 {角色名} 进行判定，{属性名}{属性值}"
        /// </summary>
        /// <param name="character">执行判定的角色</param>
        /// <param name="attribute">判定属性</param>
        /// <returns>描述文本</returns>
        public static string GetAdjudicatorDescription(CharacterDefinition character, AttributeType attribute)
        {
            if (character == null)
                return string.Empty;

            if (attribute == AttributeType.None)
                return string.Empty;

            string charName = character.DisplayName ?? character.Id ?? "???";
            string attrName = GetAttributeName(attribute);
            int attrValue = GetCharacterAttribute(character, attribute);

            return $"由 {charName} 进行判定，{attrName}{attrValue}";
        }

        /// <summary>
        /// 生成BG3风格判定描述文本（带骰子难度等级）
        /// </summary>
        /// <param name="character">执行判定的角色</param>
        /// <param name="attribute">判定属性</param>
        /// <param name="targetDC">目标难度等级</param>
        /// <returns>描述文本</returns>
        public static string GetAdjudicatorDescription(CharacterDefinition character, AttributeType attribute, int targetDC)
        {
            if (character == null)
                return string.Empty;

            if (attribute == AttributeType.None)
                return string.Empty;

            string baseDesc = GetAdjudicatorDescription(character, attribute);
            if (string.IsNullOrEmpty(baseDesc))
                return string.Empty;

            string dcLabel = GetDCLabel(targetDC);
            return $"{baseDesc} 难度等级: {targetDC} ({dcLabel})";
        }

        /// <summary>
        /// 获取难度等级标签
        /// </summary>
        private static string GetDCLabel(int dc)
        {
            if (dc <= 5) return "非常简单";
            if (dc <= 10) return "简单";
            if (dc <= 15) return "中等";
            if (dc <= 20) return "困难";
            if (dc <= 25) return "非常困难";
            if (dc <= 30) return "近乎不可能";
            return "传奇";
        }
    }
}
