using System;

namespace Tactics.Roster
{
    /// <summary>Serializable character data aligned with <see cref="Tactics.Common.Units.Unit"/> combat fields.</summary>
    [Serializable]
    public class CharacterDefinition
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public int Level { get; set; }

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

        public static CharacterDefinition CreateDefault(string id, string displayName, int strengthBonus = 0, int intelligenceBonus = 0)
        {
            return new CharacterDefinition
            {
                Id = id,
                DisplayName = displayName,
                Level = 1,
                Strength = 5 + strengthBonus,
                Agility = 5,
                Constitution = 5,
                Intelligence = 5 + intelligenceBonus,
                Charisma = 5,
                Luck = 5,
                Speed = 5f,
                ActionPoints = 1f,
                AttackRange = 1,
                AttackFactor = 1,
                DefenceFactor = 1
            };
        }
    }
}
