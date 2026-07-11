using System;
using System.Collections.Generic;
using Tactics.Common.Units.Classes;
using Tactics.Roster;

namespace Tactics.Common.Battle
{
    /// <summary>
    /// Canonical definitions for the first-slice basic and advanced class skills.
    /// Runtime ability assets reference these stable ids while SkillSystem owns unlock validation.
    /// </summary>
    public static class FirstSliceSkillCatalog
    {
        private static readonly IReadOnlyDictionary<string, SkillDefinition> Definitions =
            new Dictionary<string, SkillDefinition>(StringComparer.Ordinal)
            {
                ["mage.fireball"] = Create("mage.fireball", "火球术", RoleType.Mage, SkillType.Active, AttributeType.Intelligence, 5, null, 3),
                ["mage.ice_bolt"] = Create("mage.ice_bolt", "寒冰箭", RoleType.Mage, SkillType.Active, AttributeType.Intelligence, 5, null, 3),
                ["mage.lightning"] = Create("mage.lightning", "霹雳闪电", RoleType.Mage, SkillType.Active, AttributeType.Intelligence, 5, null, 3),
                ["mage.summon_fire_demon"] = Create("mage.summon_fire_demon", "召唤火魔", RoleType.Mage, SkillType.Active, AttributeType.Intelligence, 7, "mage.fireball", 2),
                ["mage.ice_armor"] = Create("mage.ice_armor", "冰甲", RoleType.Mage, SkillType.Active, AttributeType.Intelligence, 7, "mage.ice_bolt", 2),
                ["mage.teleport"] = Create("mage.teleport", "瞬移术", RoleType.Mage, SkillType.Active, AttributeType.Intelligence, 7, "mage.lightning", 2),

                ["necromancer.summon_skeleton"] = Create("necromancer.summon_skeleton", "召唤骷髅", RoleType.Necromancer, SkillType.Active, AttributeType.Charisma, 5, null, 3),
                ["necromancer.amplify_damage"] = Create("necromancer.amplify_damage", "伤害加深诅咒", RoleType.Necromancer, SkillType.Active, AttributeType.Charisma, 5, null, 3),
                ["necromancer.bone_spear"] = Create("necromancer.bone_spear", "骨矛", RoleType.Necromancer, SkillType.Active, AttributeType.Intelligence, 5, null, 3),
                ["necromancer.skeleton_mage"] = Create("necromancer.skeleton_mage", "骷髅法师", RoleType.Necromancer, SkillType.Active, AttributeType.Charisma, 7, "necromancer.summon_skeleton", 2),
                ["necromancer.fear_curse"] = Create("necromancer.fear_curse", "恐惧诅咒", RoleType.Necromancer, SkillType.Active, AttributeType.Charisma, 7, "necromancer.amplify_damage", 2),
                ["necromancer.bone_shield"] = Create("necromancer.bone_shield", "骨盾", RoleType.Necromancer, SkillType.Active, AttributeType.Charisma, 7, "necromancer.bone_spear", 2),

                ["amazon.thrust"] = Create("amazon.thrust", "突刺", RoleType.Amazon, SkillType.Active, AttributeType.Agility, 5, null, 3),
                ["amazon.poison_spear"] = Create("amazon.poison_spear", "毒矛", RoleType.Amazon, SkillType.Active, AttributeType.Agility, 5, null, 3),
                ["amazon.combat_techniques"] = Create("amazon.combat_techniques", "战斗技巧", RoleType.Amazon, SkillType.Passive, AttributeType.Luck, 5, null, 3),
                ["amazon.multi_stab"] = Create("amazon.multi_stab", "连续刺击", RoleType.Amazon, SkillType.Active, AttributeType.Agility, 7, "amazon.thrust", 2),
                ["amazon.recover_spear"] = Create("amazon.recover_spear", "回收长矛", RoleType.Amazon, SkillType.Active, AttributeType.Agility, 7, "amazon.poison_spear", 2),
                ["amazon.decoy"] = Create("amazon.decoy", "分身", RoleType.Amazon, SkillType.Active, AttributeType.Luck, 7, "amazon.combat_techniques", 2)
            };

        public static IEnumerable<SkillDefinition> All => Definitions.Values;

        public static bool TryGet(string skillId, out SkillDefinition definition)
        {
            return Definitions.TryGetValue(skillId, out definition);
        }

        private static SkillDefinition Create(
            string id,
            string displayName,
            RoleType roleType,
            SkillType skillType,
            AttributeType requiredAttribute,
            int minimumAttribute,
            string prerequisiteSkillId,
            int maxSkillLevel)
        {
            return new SkillDefinition
            {
                Id = id,
                DisplayName = displayName,
                RoleType = roleType,
                SkillType = skillType,
                RequiredAttribute = requiredAttribute,
                MinimumAttribute = minimumAttribute,
                PrerequisiteSkillId = prerequisiteSkillId,
                MaxSkillLevel = maxSkillLevel,
                IsFirstSliceAvailable = true
            };
        }
    }
}
