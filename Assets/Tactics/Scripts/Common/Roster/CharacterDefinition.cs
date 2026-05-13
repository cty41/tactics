using System;
using System.Collections.Generic;
using Tactics.Common.Units.Classes;
using Tactics.Equipment;

namespace Tactics.Roster
{
    /// <summary>Base attribute types matching <see cref="CharacterDefinition"/> fields.</summary>
    public enum AttributeType
    {
        Strength,
        Agility,
        Constitution,
        Intelligence,
        Charisma,
        Luck,
        Speed
    }

    /// <summary>Skill activation type.</summary>
    public enum SkillType
    {
        Active,
        Passive
    }

    /// <summary>Serializable character data aligned with <see cref="Tactics.Common.Units.Unit"/> combat fields.</summary>
    [Serializable]
    public class CharacterDefinition
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public int Level { get; set; }
        public int Experience { get; set; }
        public int AttributePoints { get; set; }
        public Dictionary<AttributeType, int> AllocatedAttributes { get; set; }
        public List<LearnedSkill> LearnedSkills { get; set; }
        public int Gold { get; set; }

        public int Strength { get; set; }
        public int Agility { get; set; }
        public int Constitution { get; set; }
        public int Intelligence { get; set; }
        public int Charisma { get; set; }
        public int Luck { get; set; }

        public float Speed { get; set; }
        public float ActionPoints { get; set; }
        public int AttackRange { get; set; }
        public int AttackFactor { get; set; }
        public int DefenceFactor { get; set; }
        public RoleType RoleType { get; set; }
        public string PrefabPath { get; set; }

        public const string PrefabPathPrefix = "Assets/Tactics/Arts/Prefabs/Units/";
        private const string PrefabExtension = ".prefab";

        public static string ResolvePrefabPath(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            if (name.StartsWith("Assets/", System.StringComparison.Ordinal))
                return name;
            return PrefabPathPrefix + name + PrefabExtension;
        }

        public Dictionary<EquipmentSlot, string> Equipment { get; set; } = new Dictionary<EquipmentSlot, string>();

        public int GetTotalStrength()
        {
            int bonus = 0;
            foreach (var kvp in Equipment)
            {
                var def = EquipmentDatabase.GetById(kvp.Value);
                if (def != null) bonus += def.StrengthBonus;
            }
            return Strength + bonus;
        }

        public int GetTotalAgility()
        {
            int bonus = 0;
            foreach (var kvp in Equipment)
            {
                var def = EquipmentDatabase.GetById(kvp.Value);
                if (def != null) bonus += def.AgilityBonus;
            }
            return Agility + bonus;
        }

        public int GetTotalConstitution()
        {
            int bonus = 0;
            foreach (var kvp in Equipment)
            {
                var def = EquipmentDatabase.GetById(kvp.Value);
                if (def != null) bonus += def.ConstitutionBonus;
            }
            return Constitution + bonus;
        }

        public int GetTotalIntelligence()
        {
            int bonus = 0;
            foreach (var kvp in Equipment)
            {
                var def = EquipmentDatabase.GetById(kvp.Value);
                if (def != null) bonus += def.IntelligenceBonus;
            }
            return Intelligence + bonus;
        }

        public int GetTotalCharisma()
        {
            int bonus = 0;
            foreach (var kvp in Equipment)
            {
                var def = EquipmentDatabase.GetById(kvp.Value);
                if (def != null) bonus += def.CharismaBonus;
            }
            return Charisma + bonus;
        }

        public int GetTotalLuck()
        {
            int bonus = 0;
            foreach (var kvp in Equipment)
            {
                var def = EquipmentDatabase.GetById(kvp.Value);
                if (def != null) bonus += def.LuckBonus;
            }
            return Luck + bonus;
        }

        public static CharacterDefinition CreateDefault(string id, string displayName, int strengthBonus = 0, int intelligenceBonus = 0, int agilityBonus = 0, RoleType roleType = RoleType.Barbarian)
        {
            return new CharacterDefinition
            {
                Id = id,
                DisplayName = displayName,
                Level = 1,
                Strength = 5 + strengthBonus,
                Agility = 5 + agilityBonus,
                Constitution = 5,
                Intelligence = 5 + intelligenceBonus,
                Charisma = 5,
                Luck = 5,
                Speed = 5f,
                ActionPoints = 1f,
                AttackRange = 1,
                AttackFactor = 1,
                DefenceFactor = 1,
                RoleType = roleType,
                Experience = 0,
                AttributePoints = 0,
                AllocatedAttributes = new Dictionary<AttributeType, int>(),
                LearnedSkills = new List<LearnedSkill>(),
                Gold = 0
            };
        }

        /// <summary>A skill learned by the character.</summary>
        [Serializable]
        public class LearnedSkill
        {
            public string SkillId { get; set; }
            public SkillType SkillType { get; set; }
            public int Level { get; set; }
        }
    }
}
