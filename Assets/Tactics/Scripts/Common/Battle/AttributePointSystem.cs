using System.Collections.Generic;
using Tactics.Roster;
using Tactics.Runtime.Utilities;

namespace Tactics.Common.Battle
{
    /// <summary>
    /// 属性加点系统：管理属性与 CharacterDefinition 之间的加点逻辑。
    /// 使用 Tactics.Roster.AttributeType 枚举（Strength, Agility, Constitution, Intelligence, Charisma, Luck）。
    /// 映射关系：Vitality → Constitution，Mentality → Charisma。
    /// </summary>
    public static class AttributePointSystem
    {
        /// <summary>
        /// 获取属性类型的中文显示名称。
        /// </summary>
        public static string GetAttributeDisplayName(AttributeType type)
        {
            return type switch
            {
                AttributeType.Strength => "力量",
                AttributeType.Agility => "敏捷",
                AttributeType.Intelligence => "智力",
                AttributeType.Constitution => "体质",
                AttributeType.Charisma => "精神",
                _ => type.ToString()
            };
        }

        /// <summary>
        /// 获取属性类型的效果描述。
        /// </summary>
        public static string GetAttributeDescription(AttributeType type)
        {
            return type switch
            {
                AttributeType.Strength => "每点增加 2 点物理攻击",
                AttributeType.Agility => "每点增加 1 点速度、2% 闪避",
                AttributeType.Intelligence => "每点增加 2 点魔法攻击、10 点法力上限",
                AttributeType.Constitution => "每点增加 10 点生命上限、1 点物理防御",
                AttributeType.Charisma => "每点增加 1 点魔法防御、2% 状态抗性",
                _ => "未知属性"
            };
        }

        /// <summary>
        /// 获取指定属性类型在投入指定点数后的加成描述。
        /// </summary>
        public static string GetAttributeBonus(AttributeType type, int points)
        {
            if (points <= 0)
                return $"{GetAttributeDisplayName(type)}: 无加成";

            return type switch
            {
                AttributeType.Strength =>
                    $"{GetAttributeDisplayName(type)}: 物理攻击 +{points * 2}",
                AttributeType.Agility =>
                    $"{GetAttributeDisplayName(type)}: 速度 +{points * 1}，闪避 +{points * 2}%",
                AttributeType.Intelligence =>
                    $"{GetAttributeDisplayName(type)}: 魔法攻击 +{points * 2}，法力上限 +{points * 10}",
                AttributeType.Constitution =>
                    $"{GetAttributeDisplayName(type)}: 生命上限 +{points * 10}，物理防御 +{points * 1}",
                AttributeType.Charisma =>
                    $"{GetAttributeDisplayName(type)}: 魔法防御 +{points * 1}，状态抗性 +{points * 2}%",
                _ => $"{GetAttributeDisplayName(type)}: 未知加成"
            };
        }

        /// <summary>
        /// 为角色投入 1 点属性点。
        /// </summary>
        /// <param name="character">目标角色定义。</param>
        /// <param name="type">要投入的属性类型。</param>
        /// <returns>是否成功加点。</returns>
        public static bool ApplyAttributePoint(CharacterDefinition character, AttributeType type)
        {
            if (character == null)
            {
                TLog.Warning("[AttributePointSystem] 目标角色为 null，无法加点。");
                return false;
            }

            if (character.AttributePoints <= 0)
            {
                TLog.Warning($"[AttributePointSystem] 角色 {character.DisplayName} 没有可用属性点。");
                return false;
            }

            character.AttributePoints--;
            character.AllocatedAttributes[type] = character.AllocatedAttributes.GetValueOrDefault(type, 0) + 1;

            switch (type)
            {
                case AttributeType.Strength:
                    character.Strength += 2;
                    break;
                case AttributeType.Agility:
                    character.Agility += 1;
                    character.Speed += 1f;
                    break;
                case AttributeType.Intelligence:
                    character.Intelligence += 2;
                    break;
                case AttributeType.Constitution:
                    character.Constitution += 10;
                    character.DefenceFactor += 1;
                    break;
                case AttributeType.Charisma:
                    character.Charisma += 1;
                    character.Luck += 2;
                    break;
            }

            TLog.Info($"[AttributePointSystem] 角色 {character.DisplayName} 投入 1 点 {GetAttributeDisplayName(type)}，剩余点数: {character.AttributePoints}");
            return true;
        }
    }
}
