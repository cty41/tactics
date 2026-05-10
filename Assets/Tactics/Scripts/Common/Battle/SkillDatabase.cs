using System;
using System.Collections.Generic;
using System.Linq;
using Tactics.Common.Units.Classes;
using Tactics.Roster;
using Tactics.Runtime.Utilities;

namespace Tactics.Common.Battle
{
    /// <summary>
    /// Skill database with hard-coded definitions per role and skill type.
    /// Each skill has Level 1 and Level 2 variants.
    /// </summary>
    public static class SkillDatabase
    {
        private static readonly Dictionary<string, SkillDefinition> _definitions = new Dictionary<string, SkillDefinition>();
        private static bool _isLoaded;

        public static void Load()
        {
            if (_isLoaded)
                return;

            InitializeDefinitions();
            _isLoaded = true;
            TLog.Info($"[SkillDatabase] Loaded {_definitions.Count} skill definitions.");
        }

        private static void InitializeDefinitions()
        {
            // Barbarian - Active (Melee)
            AddSkill("barb_slash_1", "野蛮斩击", "对前方敌人造成物理伤害", RoleType.Barbarian, SkillType.Active, 1);
            AddSkill("barb_slash_2", "野蛮斩击 II", "对前方敌人造成大量物理伤害", RoleType.Barbarian, SkillType.Active, 2);
            AddSkill("barb_charge_1", "冲锋", "冲向目标并造成伤害", RoleType.Barbarian, SkillType.Active, 1);
            AddSkill("barb_charge_2", "冲锋 II", "冲向目标并造成大量伤害，附带击退", RoleType.Barbarian, SkillType.Active, 2);
            AddSkill("barb_cleave_1", "旋风斩", "对周围所有敌人造成伤害", RoleType.Barbarian, SkillType.Active, 1);
            AddSkill("barb_cleave_2", "旋风斩 II", "对周围所有敌人造成大量伤害", RoleType.Barbarian, SkillType.Active, 2);

            // Barbarian - Passive (Survival)
            AddSkill("barb_tough_1", "坚韧", "最大生命值 +10%", RoleType.Barbarian, SkillType.Passive, 1);
            AddSkill("barb_tough_2", "坚韧 II", "最大生命值 +20%", RoleType.Barbarian, SkillType.Passive, 2);
            AddSkill("barb_rage_1", "狂怒", "生命值低于30%时攻击力 +15%", RoleType.Barbarian, SkillType.Passive, 1);
            AddSkill("barb_rage_2", "狂怒 II", "生命值低于30%时攻击力 +30%", RoleType.Barbarian, SkillType.Passive, 2);
            AddSkill("barb_iron_1", "铁壁", "受到物理伤害 -10%", RoleType.Barbarian, SkillType.Passive, 1);
            AddSkill("barb_iron_2", "铁壁 II", "受到物理伤害 -20%", RoleType.Barbarian, SkillType.Passive, 2);

            // Mage - Active (Magic)
            AddSkill("mage_fireball_1", "火球术", "发射火球造成魔法伤害", RoleType.Mage, SkillType.Active, 1);
            AddSkill("mage_fireball_2", "火球术 II", "发射大火球造成大量魔法伤害", RoleType.Mage, SkillType.Active, 2);
            AddSkill("mage_frost_1", "冰霜箭", "发射冰箭造成伤害并减速", RoleType.Mage, SkillType.Active, 1);
            AddSkill("mage_frost_2", "冰霜箭 II", "发射冰箭造成大量伤害并冻结", RoleType.Mage, SkillType.Active, 2);
            AddSkill("mage_lightning_1", "闪电链", "释放闪电在敌人间跳跃", RoleType.Mage, SkillType.Active, 1);
            AddSkill("mage_lightning_2", "闪电链 II", "释放强力闪电在敌人间跳跃", RoleType.Mage, SkillType.Active, 2);

            // Mage - Passive (Mana)
            AddSkill("mage_mana_1", "法力充盈", "最大法力值 +15%", RoleType.Mage, SkillType.Passive, 1);
            AddSkill("mage_mana_2", "法力充盈 II", "最大法力值 +30%", RoleType.Mage, SkillType.Passive, 2);
            AddSkill("mage_intellect_1", "聪慧", "智力 +10%", RoleType.Mage, SkillType.Passive, 1);
            AddSkill("mage_intellect_2", "聪慧 II", "智力 +20%", RoleType.Mage, SkillType.Passive, 2);
            AddSkill("mage_regen_1", "法力回复", "每回合回复5%法力", RoleType.Mage, SkillType.Passive, 1);
            AddSkill("mage_regen_2", "法力回复 II", "每回合回复10%法力", RoleType.Mage, SkillType.Passive, 2);

            // Hunter - Active (Ranged)
            AddSkill("hunter_shot_1", "精准射击", "对单个目标造成远程伤害", RoleType.Hunter, SkillType.Active, 1);
            AddSkill("hunter_shot_2", "精准射击 II", "对单个目标造成大量远程伤害", RoleType.Hunter, SkillType.Active, 2);
            AddSkill("hunter_multishot_1", "多重射击", "同时射击多个目标", RoleType.Hunter, SkillType.Active, 1);
            AddSkill("hunter_multishot_2", "多重射击 II", "同时射击更多目标", RoleType.Hunter, SkillType.Active, 2);
            AddSkill("hunter_trap_1", "陷阱", "布置陷阱使敌人定身", RoleType.Hunter, SkillType.Active, 1);
            AddSkill("hunter_trap_2", "陷阱 II", "布置陷阱使敌人定身并中毒", RoleType.Hunter, SkillType.Active, 2);

            // Hunter - Passive (Agility)
            AddSkill("hunter_swift_1", "迅捷", "敏捷 +10%", RoleType.Hunter, SkillType.Passive, 1);
            AddSkill("hunter_swift_2", "迅捷 II", "敏捷 +20%", RoleType.Hunter, SkillType.Passive, 2);
            AddSkill("hunter_eagle_1", "鹰眼", "攻击范围 +1", RoleType.Hunter, SkillType.Passive, 1);
            AddSkill("hunter_eagle_2", "鹰眼 II", "攻击范围 +2", RoleType.Hunter, SkillType.Passive, 2);
            AddSkill("hunter_dodge_1", "闪避", "闪避率 +10%", RoleType.Hunter, SkillType.Passive, 1);
            AddSkill("hunter_dodge_2", "闪避 II", "闪避率 +20%", RoleType.Hunter, SkillType.Passive, 2);

            // Healer - Active (Healing)
            AddSkill("heal_heal_1", "治疗术", "恢复单个友方生命值", RoleType.Healer, SkillType.Active, 1);
            AddSkill("heal_heal_2", "治疗术 II", "恢复单个友方大量生命值", RoleType.Healer, SkillType.Active, 2);
            AddSkill("heal_group_1", "群体治疗", "恢复周围友方生命值", RoleType.Healer, SkillType.Active, 1);
            AddSkill("heal_group_2", "群体治疗 II", "恢复周围友方大量生命值", RoleType.Healer, SkillType.Active, 2);
            AddSkill("heal_bless_1", "祝福", "为友方附加攻击力提升", RoleType.Healer, SkillType.Active, 1);
            AddSkill("heal_bless_2", "祝福 II", "为友方附加大幅攻击力提升", RoleType.Healer, SkillType.Active, 2);

            // Healer - Passive (Recovery)
            AddSkill("heal_regen_1", "生命回复", "每回合回复5%生命值", RoleType.Healer, SkillType.Passive, 1);
            AddSkill("heal_regen_2", "生命回复 II", "每回合回复10%生命值", RoleType.Healer, SkillType.Passive, 2);
            AddSkill("heal_aura_1", "治愈光环", "周围友方每回合回复3%生命", RoleType.Healer, SkillType.Passive, 1);
            AddSkill("heal_aura_2", "治愈光环 II", "周围友方每回合回复6%生命", RoleType.Healer, SkillType.Passive, 2);
            AddSkill("heal_divine_1", "神圣庇护", "受到致命伤害时保留1点生命（每场战斗1次）", RoleType.Healer, SkillType.Passive, 1);
            AddSkill("heal_divine_2", "神圣庇护 II", "受到致命伤害时保留1点生命并回复20%生命（每场战斗1次）", RoleType.Healer, SkillType.Passive, 2);

            // Rogue - Active (Sneak)
            AddSkill("rogue_backstab_1", "背刺", "从背后攻击造成暴击伤害", RoleType.Rogue, SkillType.Active, 1);
            AddSkill("rogue_backstab_2", "背刺 II", "从背后攻击造成大量暴击伤害", RoleType.Rogue, SkillType.Active, 2);
            AddSkill("rogue_smoke_1", "烟雾弹", "释放烟雾使自身隐身", RoleType.Rogue, SkillType.Active, 1);
            AddSkill("rogue_smoke_2", "烟雾弹 II", "释放烟雾使自身隐身并回复少量生命", RoleType.Rogue, SkillType.Active, 2);
            AddSkill("rogue_poison_1", "涂毒", "武器附加毒素持续造成伤害", RoleType.Rogue, SkillType.Active, 1);
            AddSkill("rogue_poison_2", "涂毒 II", "武器附加强力毒素持续造成大量伤害", RoleType.Rogue, SkillType.Active, 2);

            // Rogue - Passive (Critical)
            AddSkill("rogue_crit_1", "致命", "暴击率 +10%", RoleType.Rogue, SkillType.Passive, 1);
            AddSkill("rogue_crit_2", "致命 II", "暴击率 +20%", RoleType.Rogue, SkillType.Passive, 2);
            AddSkill("rogue_lethality_1", "致命一击", "暴击伤害 +25%", RoleType.Rogue, SkillType.Passive, 1);
            AddSkill("rogue_lethality_2", "致命一击 II", "暴击伤害 +50%", RoleType.Rogue, SkillType.Passive, 2);
            AddSkill("rogue_shadow_1", "暗影步", "移动后首次攻击暴击率 +20%", RoleType.Rogue, SkillType.Passive, 1);
            AddSkill("rogue_shadow_2", "暗影步 II", "移动后首次攻击暴击率 +40%", RoleType.Rogue, SkillType.Passive, 2);
        }

        private static void AddSkill(string id, string displayName, string description, RoleType roleType, SkillType skillType, int level)
        {
            if (_definitions.ContainsKey(id))
            {
                TLog.Warning($"[SkillDatabase] Duplicate skill Id: {id}");
                return;
            }

            _definitions[id] = new SkillDefinition
            {
                Id = id,
                DisplayName = displayName,
                Description = description,
                RoleType = roleType,
                SkillType = skillType,
                Level = level
            };
        }

        /// <summary>
        /// Returns all skills matching the given role, type and level.
        /// </summary>
        public static List<SkillDefinition> GetSkillsForRole(RoleType roleType, SkillType skillType, int level)
        {
            if (!_isLoaded)
                Load();

            return _definitions.Values
                .Where(s => s.RoleType == roleType && s.SkillType == skillType && s.Level == level)
                .ToList();
        }

        /// <summary>
        /// Gets a skill definition by its unique Id.
        /// </summary>
        public static SkillDefinition GetSkillById(string skillId)
        {
            if (!_isLoaded)
                Load();

            if (string.IsNullOrEmpty(skillId))
                return null;

            _definitions.TryGetValue(skillId, out var def);
            return def;
        }

        /// <summary>
        /// Randomly returns <paramref name="count"/> skills for selection,
        /// excluding already-learned skills.
        /// </summary>
        public static List<SkillDefinition> GetRandomSkillsForSelection(
            RoleType roleType,
            SkillType skillType,
            int level,
            int count = 3,
            List<CharacterDefinition.LearnedSkill> learnedSkills = null)
        {
            if (!_isLoaded)
                Load();

            var pool = GetSkillsForRole(roleType, skillType, level);
            if (pool.Count == 0)
            {
                TLog.Warning($"[SkillDatabase] No skills found for {roleType} {skillType} level {level}");
                return new List<SkillDefinition>();
            }

            var learnedIds = new HashSet<string>();
            if (learnedSkills != null)
            {
                foreach (var ls in learnedSkills)
                {
                    if (ls != null && !string.IsNullOrEmpty(ls.SkillId))
                        learnedIds.Add(ls.SkillId);
                }
            }

            var available = pool.Where(s => !learnedIds.Contains(s.Id)).ToList();
            if (available.Count == 0)
            {
                TLog.Info($"[SkillDatabase] All skills already learned for {roleType} {skillType} level {level}");
                return new List<SkillDefinition>();
            }

            var result = new List<SkillDefinition>();
            var rng = new System.Random();
            int pickCount = Math.Min(count, available.Count);

            while (result.Count < pickCount)
            {
                int index = rng.Next(available.Count);
                var skill = available[index];
                if (!result.Contains(skill))
                    result.Add(skill);
            }

            return result;
        }

        /// <summary>
        /// Returns Level 1 skills that the character has already learned and can be upgraded.
        /// </summary>
        public static List<SkillDefinition> GetUpgradeableSkills(CharacterDefinition character)
        {
            if (!_isLoaded)
                Load();

            if (character == null || character.LearnedSkills == null || character.LearnedSkills.Count == 0)
                return new List<SkillDefinition>();

            var upgradeable = new List<SkillDefinition>();
            foreach (var learned in character.LearnedSkills)
            {
                if (learned == null || string.IsNullOrEmpty(learned.SkillId))
                    continue;

                if (learned.Level != 1)
                    continue;

                var def = GetSkillById(learned.SkillId);
                if (def == null)
                    continue;

                // Check if Level 2 version exists
                string level2Id = learned.SkillId.Replace("_1", "_2");
                var level2Def = GetSkillById(level2Id);
                if (level2Def != null && level2Def.Level == 2)
                    upgradeable.Add(def);
            }

            return upgradeable;
        }
    }

}
