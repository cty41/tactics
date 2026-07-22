using System;
using System.Collections.Generic;
using System.Linq;
using Tactics.Common.Units.Classes;
using Tactics.Roster;
using Tactics.Runtime.Utilities;

namespace Tactics.Common.Battle
{
    /// <summary>
    /// 技能定义（轻量级，用于系统内部）。
    /// </summary>
    public class SkillDefinition
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public RoleType RoleType { get; set; }
        public Tactics.Roster.SkillType SkillType { get; set; }
        public int Level { get; set; }
        public int DamageBase { get; set; }
        public int MpCost { get; set; }
        public AttributeType? RequiredAttribute { get; set; }
        public int MinimumAttribute { get; set; }
        public string PrerequisiteSkillId { get; set; }
        public int MaxSkillLevel { get; set; } = 2;
        public bool IsFirstSliceAvailable { get; set; }
    }

    /// <summary>
    /// 技能槽位状态。
    /// </summary>
    public readonly struct SkillSlotStatus
    {
        public int Used { get; }
        public int Total { get; }
        public int Remaining => Total - Used;

        public SkillSlotStatus(int used, int total)
        {
            Used = used;
            Total = total;
        }
    }

    /// <summary>
    /// 技能系统：管理技能槽位、学习、替换、升级。
    /// </summary>
    public static class SkillSystem
    {
        public const int MaxActiveSlots = 3;
        public const int MaxPassiveSlots = 3;
        public const int MaxLevel = 2;
        public const int MaxCharacterLevel = 12;

        /// <summary>
        /// 检查角色是否可以学习指定技能。
        /// </summary>
        public static bool CanLearnSkill(CharacterDefinition character, SkillDefinition skill)
        {
            if (character == null)
            {
                TLog.Warning("[SkillSystem] CanLearnSkill called with null character.");
                return false;
            }

            if (skill == null)
            {
                TLog.Warning("[SkillSystem] CanLearnSkill called with null skill.");
                return false;
            }

            if (character.RoleType != skill.RoleType)
            {
                TLog.Info($"[SkillSystem] Skill {skill.Id} role mismatch: character is {character.RoleType}, skill requires {skill.RoleType}.");
                return false;
            }

            if (skill.RequiredAttribute.HasValue
                && GetBaseAttributeValue(character, skill.RequiredAttribute.Value) < skill.MinimumAttribute)
            {
                TLog.Info($"[SkillSystem] Skill {skill.Id} requires {skill.RequiredAttribute} {skill.MinimumAttribute}.");
                return false;
            }

            if (!string.IsNullOrEmpty(skill.PrerequisiteSkillId)
                && !HasSkill(character, skill.PrerequisiteSkillId))
            {
                TLog.Info($"[SkillSystem] Skill {skill.Id} requires prerequisite {skill.PrerequisiteSkillId}.");
                return false;
            }

            if (HasSkill(character, skill.Id))
            {
                TLog.Info($"[SkillSystem] Character already has skill {skill.Id}.");
                return false;
            }

            if (skill.SkillType == Tactics.Roster.SkillType.ExtraUtility)
            {
                TLog.Info($"[SkillSystem] Extra utility skill {skill.Id} is granted by its owning mechanic, not learned from offers.");
                return false;
            }

            var slotStatus = GetSkillSlotStatus(character, (Tactics.Roster.SkillType)skill.SkillType);
            if (slotStatus.Remaining <= 0)
            {
                TLog.Info($"[SkillSystem] No remaining {skill.SkillType} skill slots.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 让角色学习指定技能。若对应类型槽位已满，需指定替换的技能索引。
        /// </summary>
        public static bool LearnSkill(CharacterDefinition character, SkillDefinition skill, int? replaceIndex = null)
        {
            if (character == null)
            {
                TLog.Warning("[SkillSystem] LearnSkill called with null character.");
                return false;
            }

            if (skill == null)
            {
                TLog.Warning("[SkillSystem] LearnSkill called with null skill.");
                return false;
            }

            if (character.RoleType != skill.RoleType)
            {
                TLog.Warning($"[SkillSystem] Cannot learn skill {skill.Id}: role mismatch.");
                return false;
            }

            if (HasSkill(character, skill.Id))
            {
                TLog.Warning($"[SkillSystem] Character already has skill {skill.Id}.");
                return false;
            }

            if (skill.SkillType == Tactics.Roster.SkillType.ExtraUtility)
            {
                TLog.Warning($"[SkillSystem] Extra utility skill {skill.Id} cannot be learned from a normal skill slot.");
                return false;
            }

            var slotStatus = GetSkillSlotStatus(character, (Tactics.Roster.SkillType)skill.SkillType);
            if (slotStatus.Remaining <= 0)
            {
                if (replaceIndex == null)
                {
                    TLog.Warning($"[SkillSystem] No remaining {skill.SkillType} skill slots and no replacement specified.");
                    return false;
                }

                var skillsOfType = GetSkillsOfType(character, (Tactics.Roster.SkillType)skill.SkillType);
                if (replaceIndex < 0 || replaceIndex >= skillsOfType.Count)
                {
                    TLog.Warning($"[SkillSystem] Invalid replace index {replaceIndex} for {skill.SkillType} skills.");
                    return false;
                }

                var toReplace = skillsOfType[replaceIndex.Value];
                character.LearnedSkills.Remove(toReplace);
                TLog.Info($"[SkillSystem] Replaced skill {toReplace.SkillId} with {skill.Id}.");
            }

            character.LearnedSkills.Add(new CharacterDefinition.LearnedSkill
            {
                SkillId = skill.Id,
                SkillType = (Tactics.Roster.SkillType)skill.SkillType,
                Level = 1
            });

            PureRunAbilityCatalog.EnsurePickupSpearSkill(character);

            TLog.Info($"[SkillSystem] Character learned skill {skill.Id} ({skill.SkillType}).");
            return true;
        }

        /// <summary>
        /// 升级指定技能一级，最高等级由稳定技能目录决定。
        /// </summary>
        public static bool UpgradeSkill(CharacterDefinition character, string skillId)
        {
            if (character == null)
            {
                TLog.Warning("[SkillSystem] UpgradeSkill called with null character.");
                return false;
            }

            if (string.IsNullOrEmpty(skillId))
            {
                TLog.Warning("[SkillSystem] UpgradeSkill called with empty skillId.");
                return false;
            }

            var learnedSkill = character.LearnedSkills.FirstOrDefault(s => s.SkillId == skillId);
            if (learnedSkill == null)
            {
                TLog.Warning($"[SkillSystem] Skill {skillId} not found on character.");
                return false;
            }

            int maxLevel = FirstSliceSkillCatalog.TryGet(skillId, out var definition)
                ? definition.MaxSkillLevel
                : MaxLevel;
            if (learnedSkill.Level >= maxLevel)
            {
                TLog.Info($"[SkillSystem] Skill {skillId} is already at max level {maxLevel}.");
                return false;
            }

            learnedSkill.Level++;
            TLog.Info($"[SkillSystem] Skill {skillId} upgraded to level {learnedSkill.Level}.");
            return true;
        }

        /// <summary>
        /// 获取指定类型技能的槽位状态。
        /// </summary>
        public static SkillSlotStatus GetSkillSlotStatus(CharacterDefinition character, Tactics.Roster.SkillType type)
        {
            if (type == Tactics.Roster.SkillType.ExtraUtility)
                return new SkillSlotStatus(0, 0);

            if (character?.LearnedSkills == null)
            {
                return new SkillSlotStatus(0, type == (Tactics.Roster.SkillType)SkillType.Active ? MaxActiveSlots : MaxPassiveSlots);
            }

            int used = character.LearnedSkills.Count(s => s.SkillType == (Tactics.Roster.SkillType)type);
            int total = type == (Tactics.Roster.SkillType)SkillType.Active ? MaxActiveSlots : MaxPassiveSlots;
            return new SkillSlotStatus(used, total);
        }

        /// <summary>
        /// 判断角色升级时是否应该显示技能选择界面。
        /// </summary>
        public static bool ShouldShowSkillSelection(CharacterDefinition character, int newLevel)
        {
            if (character == null)
            {
                TLog.Warning("[SkillSystem] ShouldShowSkillSelection called with null character.");
                return false;
            }

            if (newLevel < 2 || newLevel > MaxCharacterLevel)
            {
                return false;
            }

            // 1→2, 2→3, 3→4, 4→5, 5→6, 6→7 级时学习新技能
            if (newLevel >= 2 && newLevel <= 7)
            {
                return true;
            }

            // 7→8, 8→9, 9→10, 10→11, 11→12 级时升级已有技能
            if (newLevel >= 8 && newLevel <= 12)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 判断升级时是否学习新技能（true）还是升级已有技能（false）。
        /// </summary>
        public static bool IsNewSkillLevel(int newLevel)
        {
            return newLevel >= 2 && newLevel <= 7;
        }

        /// <summary>
        /// 判断升级时是否升级已有技能（true）还是学习新技能（false）。
        /// </summary>
        public static bool IsUpgradeSkillLevel(int newLevel)
        {
            return newLevel >= 8 && newLevel <= 12;
        }

        /// <summary>
        /// 检查角色是否已学习指定技能。
        /// </summary>
        public static bool HasSkill(CharacterDefinition character, string skillId)
        {
            if (character?.LearnedSkills == null)
                return false;

            return character.LearnedSkills.Any(s => s.SkillId == skillId);
        }

        /// <summary>
        /// 获取角色指定类型的已学习技能列表。
        /// </summary>
        public static List<CharacterDefinition.LearnedSkill> GetSkillsOfType(CharacterDefinition character, Tactics.Roster.SkillType type)
        {
            if (character?.LearnedSkills == null)
                return new List<CharacterDefinition.LearnedSkill>();

            return character.LearnedSkills.Where(s => s.SkillType == (Tactics.Roster.SkillType)type).ToList();
        }

        /// <summary>
        /// Returns the persistent base attribute value. Equipment bonuses do not unlock skills.
        /// </summary>
        public static int GetBaseAttributeValue(CharacterDefinition character, AttributeType attribute)
        {
            if (character == null)
                return 0;

            return attribute switch
            {
                AttributeType.Strength => character.Strength,
                AttributeType.Agility => character.Agility,
                AttributeType.Constitution => character.Constitution,
                AttributeType.Intelligence => character.Intelligence,
                AttributeType.Charisma => character.Charisma,
                AttributeType.Luck => character.Luck,
                AttributeType.Speed => (int)character.Speed,
                _ => 0
            };
        }
    }
}
