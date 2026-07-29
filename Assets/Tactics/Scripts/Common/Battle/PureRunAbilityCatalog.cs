using System;
using System.Collections.Generic;
using System.Linq;
using Tactics.Common.Units.Classes;
using Tactics.Roster;

namespace Tactics.Common.Battle
{
    /// <summary>
    /// Immutable Pure Run metadata for one stable career skill id.
    /// Product maximum levels are kept separate from the levels already published as runtime assets.
    /// </summary>
    public sealed class PureRunAbilityDefinition
    {
        private readonly IReadOnlyDictionary<int, string> _abilityConfigPaths;
        private readonly IReadOnlyCollection<int> _implementedPassiveLevels;
        private readonly IReadOnlyCollection<string> _tags;

        internal PureRunAbilityDefinition(
            SkillDefinition skill,
            IReadOnlyDictionary<int, string> abilityConfigPaths,
            IReadOnlyCollection<int> implementedPassiveLevels,
            IReadOnlyCollection<string> tags,
            bool isMapVisible,
            bool isUpgradeVisible,
            bool isBattleVisible)
        {
            Skill = skill;
            _abilityConfigPaths = abilityConfigPaths ?? new Dictionary<int, string>();
            _implementedPassiveLevels = implementedPassiveLevels ?? Array.Empty<int>();
            _tags = tags ?? Array.Empty<string>();
            IsMapVisible = isMapVisible;
            IsUpgradeVisible = isUpgradeVisible;
            IsBattleVisible = isBattleVisible;
        }

        public SkillDefinition Skill { get; }
        public string Id => Skill.Id;
        public RoleType RoleType => Skill.RoleType;
        public SkillType SkillType => Skill.SkillType;
        public int MaxSkillLevel => Skill.MaxSkillLevel;
        public bool IsMapVisible { get; }
        public bool IsUpgradeVisible { get; }
        public bool IsBattleVisible { get; }
        public IReadOnlyDictionary<int, string> AbilityConfigPaths => _abilityConfigPaths;
        public IReadOnlyCollection<string> Tags => _tags;

        public bool HasTag(string tag)
        {
            return !string.IsNullOrWhiteSpace(tag) && _tags.Contains(tag, StringComparer.Ordinal);
        }

        public bool IsLevelImplemented(int level)
        {
            if (level < 1 || level > MaxSkillLevel)
                return false;

            return Skill.SkillType == Tactics.Roster.SkillType.Passive
                ? _implementedPassiveLevels.Contains(level)
                : _abilityConfigPaths.TryGetValue(level, out string path) && !string.IsNullOrWhiteSpace(path);
        }

        public SkillDefinition CreateOffer(int targetLevel)
        {
            return new SkillDefinition
            {
                Id = Skill.Id,
                DisplayName = Skill.DisplayName,
                Description = Skill.Description,
                RoleType = Skill.RoleType,
                SkillType = Skill.SkillType,
                Level = targetLevel,
                DamageBase = Skill.DamageBase,
                MpCost = Skill.MpCost,
                RequiredAttribute = Skill.RequiredAttribute,
                MinimumAttribute = Skill.MinimumAttribute,
                PrerequisiteSkillId = Skill.PrerequisiteSkillId,
                MaxSkillLevel = Skill.MaxSkillLevel,
                IsFirstSliceAvailable = Skill.IsFirstSliceAvailable
            };
        }
    }

    /// <summary>
    /// Canonical Pure Run skill catalog. Stable ids are persisted; level-specific paths are runtime-only.
    /// </summary>
    public static class PureRunAbilityCatalog
    {
        public const string ThrowingTag = "throwing";
        public const string PickupSpearSkillId = "amazon.pickup_spear";

        public const string MagicBaseAttackPath =
            "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/MagicAttack_Graph_Ability.asset";
        public const string MeleeBaseAttackPath =
            "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/MeleeAttack_Graph_Ability.asset";

        private static readonly IReadOnlyDictionary<string, PureRunAbilityDefinition> Definitions =
            CreateDefinitions();

        private static readonly IReadOnlyDictionary<string, (string StableId, int Level)> LegacyAliases =
            CreateLegacyAliases();

        public static IEnumerable<PureRunAbilityDefinition> All => Definitions.Values;

        public static IEnumerable<PureRunAbilityDefinition> FormalSkills =>
            Definitions.Values.Where(definition => definition.IsUpgradeVisible);

        public static bool TryGet(string skillId, out PureRunAbilityDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(skillId))
            {
                definition = null;
                return false;
            }

            return Definitions.TryGetValue(skillId, out definition);
        }

        public static string GetBaseAttackPath(RoleType roleType)
        {
            return roleType switch
            {
                RoleType.Mage => MagicBaseAttackPath,
                RoleType.Necromancer => MagicBaseAttackPath,
                RoleType.Amazon => MeleeBaseAttackPath,
                _ => null
            };
        }

        /// <summary>
        /// The three starting-branch skills offered per Pure Run role at run setup.
        /// All entries are formal (upgrade-visible), role-matching and prerequisite-free.
        /// </summary>
        public static IReadOnlyList<string> GetStartingBranchSkillIds(RoleType roleType)
        {
            return roleType switch
            {
                RoleType.Mage => new[] { "mage.fireball", "mage.ice_bolt", "mage.lightning" },
                RoleType.Necromancer => new[]
                {
                    "necromancer.summon_skeleton",
                    "necromancer.amplify_damage",
                    "necromancer.bone_spear"
                },
                RoleType.Amazon => new[] { "amazon.thrust", "amazon.poison_spear", "amazon.combat_techniques" },
                _ => Array.Empty<string>()
            };
        }

        /// <summary>
        /// Resolves the exact level when present. Missing levels fall back only to a lower published level.
        /// Callers must report the mismatch; this fallback is a save-safety guard, not release validation.
        /// </summary>
        public static bool TryResolveAbilityPath(
            string skillId,
            int requestedLevel,
            out string abilityConfigPath,
            out int resolvedLevel)
        {
            abilityConfigPath = null;
            resolvedLevel = 0;
            if (!TryGet(skillId, out var definition) || definition.SkillType == SkillType.Passive)
                return false;

            int clampedLevel = Math.Max(1, Math.Min(requestedLevel, definition.MaxSkillLevel));
            for (int level = clampedLevel; level >= 1; level--)
            {
                if (!definition.AbilityConfigPaths.TryGetValue(level, out string path) ||
                    string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                abilityConfigPath = path;
                resolvedLevel = level;
                return true;
            }

            return false;
        }

        public static IEnumerable<string> GetPublishedAbilityPaths()
        {
            yield return MagicBaseAttackPath;
            yield return MeleeBaseAttackPath;

            foreach (var path in Definitions.Values
                         .SelectMany(definition => definition.AbilityConfigPaths.Values)
                         .Where(path => !string.IsNullOrWhiteSpace(path))
                         .Distinct(StringComparer.Ordinal))
            {
                yield return path;
            }
        }

        /// <summary>
        /// Converts known legacy ids to stable ids and preserves the highest encoded/persisted level.
        /// Unknown ids are intentionally retained by the save repair layer.
        /// </summary>
        public static bool TryNormalizeLegacySkillId(
            string skillId,
            int persistedLevel,
            out string stableId,
            out int normalizedLevel)
        {
            stableId = null;
            normalizedLevel = Math.Max(1, persistedLevel);
            if (string.IsNullOrWhiteSpace(skillId))
                return false;

            if (Definitions.TryGetValue(skillId, out var canonical))
            {
                stableId = canonical.Id;
                normalizedLevel = Math.Min(normalizedLevel, canonical.MaxSkillLevel);
                return true;
            }

            if (!LegacyAliases.TryGetValue(skillId, out var alias) ||
                !Definitions.TryGetValue(alias.StableId, out var definition))
            {
                return false;
            }

            stableId = alias.StableId;
            normalizedLevel = Math.Min(
                Math.Max(normalizedLevel, alias.Level),
                definition.MaxSkillLevel);
            return true;
        }

        /// <summary>
        /// Repairs stable ids, levels, types and duplicates for one Pure Run character.
        /// </summary>
        public static bool RepairLearnedSkills(CharacterDefinition character)
        {
            if (character == null)
                return false;

            bool changed = character.LearnedSkills == null;
            character.LearnedSkills ??= new List<CharacterDefinition.LearnedSkill>();
            var repaired = new List<CharacterDefinition.LearnedSkill>();
            var canonicalById = new Dictionary<string, CharacterDefinition.LearnedSkill>(StringComparer.Ordinal);

            foreach (var learned in character.LearnedSkills)
            {
                if (learned == null)
                {
                    changed = true;
                    continue;
                }

                if (!TryNormalizeLegacySkillId(learned.SkillId, learned.Level, out string stableId, out int level) ||
                    !Definitions.TryGetValue(stableId, out var definition))
                {
                    repaired.Add(learned);
                    continue;
                }

                if (canonicalById.TryGetValue(stableId, out var existing))
                {
                    existing.Level = Math.Max(existing.Level, level);
                    changed = true;
                    continue;
                }

                if (!string.Equals(learned.SkillId, stableId, StringComparison.Ordinal) ||
                    learned.Level != level ||
                    learned.SkillType != definition.SkillType)
                {
                    learned.SkillId = stableId;
                    learned.Level = level;
                    learned.SkillType = definition.SkillType;
                    changed = true;
                }

                canonicalById.Add(stableId, learned);
                repaired.Add(learned);
            }

            if (changed)
                character.LearnedSkills = repaired;

            return changed;
        }

        /// <summary>
        /// Grants the hidden, slot-free spear pickup skill when an Amazon owns any throwing skill.
        /// </summary>
        public static bool EnsurePickupSpearSkill(CharacterDefinition character)
        {
            if (character?.RoleType != RoleType.Amazon || character.LearnedSkills == null)
                return false;

            bool hasThrowingSkill = character.LearnedSkills.Any(learned =>
                learned != null &&
                TryGet(learned.SkillId, out var definition) &&
                definition.HasTag(ThrowingTag));
            if (!hasThrowingSkill || character.LearnedSkills.Any(learned => learned?.SkillId == PickupSpearSkillId))
                return false;

            character.LearnedSkills.Add(new CharacterDefinition.LearnedSkill
            {
                SkillId = PickupSpearSkillId,
                SkillType = SkillType.ExtraUtility,
                Level = 1
            });
            return true;
        }

        private static IReadOnlyDictionary<string, PureRunAbilityDefinition> CreateDefinitions()
        {
            var definitions = new Dictionary<string, PureRunAbilityDefinition>(StringComparer.Ordinal);

            Add(definitions, "mage.fireball", "火球术", "发射火球造成魔法伤害。", RoleType.Mage,
                SkillType.Active, AttributeType.Intelligence, 5, null, 3,
                Paths(
                    (1, "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/Fireball_Lv1_Ability.asset"),
                    (2, "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/Fireball_Lv2_Ability.asset"),
                    (3, "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/Fireball_Lv3_Ability.asset")));
            Add(definitions, "mage.ice_bolt", "寒冰箭", "发射寒冰箭伤害并减速敌人。", RoleType.Mage,
                SkillType.Active, AttributeType.Intelligence, 5, null, 3,
                Paths(
                    (1, "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/IceBolt_Graph_Ability.asset"),
                    (2, "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/IceBolt_Lv2_Graph_Ability.asset"),
                    (3, "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/IceBolt_Lv3_Graph_Ability.asset")));
            Add(definitions, "mage.lightning", "霹雳闪电", "以闪电直接打击指定敌人。", RoleType.Mage,
                SkillType.Active, AttributeType.Intelligence, 5, null, 3,
                Paths(
                    (1, "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/Lightning_Graph_Ability.asset"),
                    (2, "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/Lightning_Lv2_Graph_Ability.asset"),
                    (3, "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/Lightning_Lv3_Graph_Ability.asset")));
            Add(definitions, "mage.summon_fire_demon", "召唤火魔", "召唤火魔协助战斗。", RoleType.Mage,
                SkillType.Active, AttributeType.Intelligence, 7, "mage.fireball", 2,
                Paths(
                    (1, "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/SummonFireDemon_Graph_Ability.asset"),
                    (2, "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/SummonFireDemon_Lv2_Graph_Ability.asset")));
            Add(definitions, "mage.ice_armor", "冰甲", "获得可降低所受伤害的冰甲。", RoleType.Mage,
                SkillType.Active, AttributeType.Intelligence, 7, "mage.ice_bolt", 2,
                Paths(
                    (1, "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/IceArmor_Graph_Ability.asset"),
                    (2, "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/IceArmor_Lv2_Graph_Ability.asset")));
            Add(definitions, "mage.teleport", "瞬移术", "瞬移到范围内的合法空格。", RoleType.Mage,
                SkillType.Active, AttributeType.Intelligence, 7, "mage.lightning", 2,
                Paths(
                    (1, "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/Teleport_Graph_Ability.asset"),
                    (2, "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/Teleport_Lv2_Graph_Ability.asset")));

            Add(definitions, "necromancer.summon_skeleton", "召唤骷髅", "消耗尸体召唤骷髅战士。", RoleType.Necromancer,
                SkillType.Active, AttributeType.Charisma, 5, null, 3,
                Paths(
                    (1, "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/SummonSkeleton_Graph_Ability.asset"),
                    (2, "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/SummonSkeleton_Lv2_Graph_Ability.asset"),
                    (3, "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/SummonSkeleton_Lv3_Graph_Ability.asset")));
            Add(definitions, "necromancer.amplify_damage", "伤害加深诅咒", "令目标承受更多伤害。", RoleType.Necromancer,
                SkillType.Active, AttributeType.Charisma, 5, null, 3,
                Paths(
                    (1, "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/Curse_Graph_Ability.asset"),
                    (2, "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/Curse_Lv2_Graph_Ability.asset"),
                    (3, "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/Curse_Lv3_Graph_Ability.asset")));
            Add(definitions, "necromancer.bone_spear", "骨矛", "发射可贯穿目标的骨矛。", RoleType.Necromancer,
                SkillType.Active, AttributeType.Intelligence, 5, null, 3,
                Paths(
                    (1, "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/BoneSpear_Graph_Ability.asset"),
                    (2, "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/BoneSpear_Lv2_Graph_Ability.asset"),
                    (3, "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/BoneSpear_Lv3_Graph_Ability.asset")));
            Add(definitions, "necromancer.skeleton_mage", "骷髅法师", "消耗尸体召唤骷髅法师。", RoleType.Necromancer,
                SkillType.Active, AttributeType.Charisma, 7, "necromancer.summon_skeleton", 2,
                Paths(
                    (1, "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/SkeletonMage_Graph_Ability.asset"),
                    (2, "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/SkeletonMage_Lv2_Graph_Ability.asset")));
            Add(definitions, "necromancer.fear_curse", "恐惧诅咒", "使范围内敌人陷入恐惧。", RoleType.Necromancer,
                SkillType.Active, AttributeType.Charisma, 7, "necromancer.amplify_damage", 2,
                Paths(
                    (1, "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/FearCurse_Graph_Ability.asset"),
                    (2, "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/FearCurse_Lv2_Graph_Ability.asset")));
            Add(definitions, "necromancer.bone_shield", "骨盾", "生成可吸收伤害的骨盾。", RoleType.Necromancer,
                SkillType.Active, AttributeType.Charisma, 7, "necromancer.bone_spear", 2,
                Paths(
                    (1, "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/BoneShield_Graph_Ability.asset"),
                    (2, "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/BoneShield_Lv2_Graph_Ability.asset")));

            Add(definitions, "amazon.thrust", "突刺", "攻击前方直线上的敌人。", RoleType.Amazon,
                SkillType.Active, AttributeType.Agility, 5, null, 3,
                Paths(
                    (1, "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/Thrust_Graph_Ability.asset"),
                    (2, "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/Thrust_Lv2_Graph_Ability.asset"),
                    (3, "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/Thrust_Lv3_Graph_Ability.asset")));
            Add(definitions, "amazon.poison_spear", "毒矛", "投掷长矛并使敌人中毒。", RoleType.Amazon,
                SkillType.Active, AttributeType.Agility, 5, null, 3,
                Paths(
                    (1, "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/PoisonSpear_Graph_Ability.asset"),
                    (2, "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/PoisonSpear_Lv2_Graph_Ability.asset"),
                    (3, "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/PoisonSpear_Lv3_Graph_Ability.asset")),
                tags: new[] { ThrowingTag });
            Add(definitions, "amazon.combat_techniques", "战斗技巧", "通过战斗技巧闪避攻击并强化伤害。", RoleType.Amazon,
                SkillType.Passive, AttributeType.Luck, 5, null, 3,
                paths: null, implementedPassiveLevels: new[] { 1, 2, 3 });
            Add(definitions, "amazon.multi_stab", "连续刺击", "连续选择多个目标发动刺击。", RoleType.Amazon,
                SkillType.Active, AttributeType.Agility, 7, "amazon.thrust", 2,
                Paths(
                    (1, "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/MultiStab_Graph_Ability.asset"),
                    (2, "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/MultiStab_Lv2_Graph_Ability.asset")));
            Add(definitions, "amazon.recover_spear", "召唤长矛", "将落地长矛召回到指定空格。", RoleType.Amazon,
                SkillType.Active, AttributeType.Agility, 7, "amazon.poison_spear", 2,
                Paths(
                    (1, "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/RecoverSpear_Graph_Ability.asset"),
                    (2, "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/RecoverSpear_Lv2_Graph_Ability.asset")),
                tags: new[] { ThrowingTag });
            Add(definitions, "amazon.decoy", "分身", "召唤分身吸引敌人攻击。", RoleType.Amazon,
                SkillType.Active, AttributeType.Luck, 7, "amazon.combat_techniques", 2,
                Paths(
                    (1, "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/Decoy_Graph_Ability.asset"),
                    (2, "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/Decoy_Lv2_Graph_Ability.asset")));

            Add(definitions, PickupSpearSkillId, "拾取长矛", "在相邻格免费拾取落地长矛。", RoleType.Amazon,
                SkillType.ExtraUtility, null, 0, null, 1,
                Paths((1, "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/PickupSpear_Graph_Ability.asset")),
                isMapVisible: false, isUpgradeVisible: false, isBattleVisible: true);

            return definitions;
        }

        private static void Add(
            IDictionary<string, PureRunAbilityDefinition> definitions,
            string id,
            string displayName,
            string description,
            RoleType roleType,
            SkillType skillType,
            AttributeType? requiredAttribute,
            int minimumAttribute,
            string prerequisiteSkillId,
            int maxSkillLevel,
            IReadOnlyDictionary<int, string> paths,
            IReadOnlyCollection<int> implementedPassiveLevels = null,
            IReadOnlyCollection<string> tags = null,
            bool isMapVisible = true,
            bool isUpgradeVisible = true,
            bool isBattleVisible = true)
        {
            var skill = new SkillDefinition
            {
                Id = id,
                DisplayName = displayName,
                Description = description,
                RoleType = roleType,
                SkillType = skillType,
                Level = 1,
                RequiredAttribute = requiredAttribute,
                MinimumAttribute = minimumAttribute,
                PrerequisiteSkillId = prerequisiteSkillId,
                MaxSkillLevel = maxSkillLevel,
                IsFirstSliceAvailable = true
            };

            definitions.Add(id, new PureRunAbilityDefinition(
                skill,
                paths,
                implementedPassiveLevels,
                tags,
                isMapVisible,
                isUpgradeVisible,
                isBattleVisible));
        }

        private static IReadOnlyDictionary<int, string> Paths(params (int Level, string Path)[] entries)
        {
            return entries.ToDictionary(entry => entry.Level, entry => entry.Path);
        }

        private static IReadOnlyDictionary<string, (string StableId, int Level)> CreateLegacyAliases()
        {
            var aliases = new Dictionary<string, (string StableId, int Level)>(StringComparer.OrdinalIgnoreCase)
            {
                ["mage_frost_1"] = ("mage.ice_bolt", 1),
                ["mage_frost_2"] = ("mage.ice_bolt", 2)
            };

            foreach (var definition in Definitions.Values)
            {
                string prefix = definition.Id.Replace('.', '_');
                for (int level = 1; level <= definition.MaxSkillLevel; level++)
                    aliases.TryAdd($"{prefix}_{level}", (definition.Id, level));
            }

            return aliases;
        }
    }
}
