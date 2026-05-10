using Tactics.Runtime.Utilities;
using Tactics.Roster;

namespace Tactics.Common.Battle
{
    public enum EnemyType
    {
        Normal,
        Elite,
        Boss
    }

    public static class ExperienceTable
    {
        private static readonly int[] ExperienceToNextLevel = new int[]
        {
            100,    // Level 1 → 2
            150,    // Level 2 → 3
            225,    // Level 3 → 4
            338,    // Level 4 → 5
            507,    // Level 5 → 6
            760,    // Level 6 → 7
            1140,   // Level 7 → 8
            1710,   // Level 8 → 9
            2565,   // Level 9 → 10
            3848,   // Level 10 → 11
            5772    // Level 11 → 12
        };

        public static int GetExperienceToNextLevel(int currentLevel)
        {
            if (currentLevel < 1 || currentLevel >= ExperienceToNextLevel.Length + 1)
            {
                TLog.Warning($"[ExperienceTable] Invalid level {currentLevel}, returning 0.");
                return 0;
            }

            return ExperienceToNextLevel[currentLevel - 1];
        }

        public static int GetMaxLevel()
        {
            return ExperienceToNextLevel.Length + 1;
        }
    }

    public static class ExperienceSystem
    {
        public static int CalculateExperienceReward(int enemyLevel, EnemyType enemyType)
        {
            int baseExperience = enemyLevel * 10;

            float multiplier = enemyType switch
            {
                EnemyType.Normal => 1.0f,
                EnemyType.Elite => 1.5f,
                EnemyType.Boss => 3.0f,
                _ => 1.0f
            };

            return (int)(baseExperience * multiplier);
        }

        public static bool CheckLevelUp(CharacterDefinition character)
        {
            if (character == null)
            {
                TLog.Warning("[ExperienceSystem] CheckLevelUp called with null character.");
                return false;
            }

            int maxLevel = ExperienceTable.GetMaxLevel();
            if (character.Level >= maxLevel)
            {
                return false;
            }

            int requiredExp = ExperienceTable.GetExperienceToNextLevel(character.Level);
            return character.Experience >= requiredExp;
        }
    }
}
