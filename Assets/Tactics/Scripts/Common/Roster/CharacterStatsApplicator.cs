using Tactics.Common.Units;
using UnityEngine;

namespace Tactics.Roster
{
    public static class CharacterStatsApplicator
    {
        public static void ApplyToUnit(CharacterDefinition data, Unit unit)
        {
            if (data == null || unit == null)
                return;

            unit.Strength = data.GetTotalStrength();
            unit.Agility = data.GetTotalAgility();
            unit.Constitution = data.GetTotalConstitution();
            unit.Intelligence = data.GetTotalIntelligence();
            unit.Charisma = data.GetTotalCharisma();
            unit.Luck = data.GetTotalLuck();
            unit.Speed = data.Speed;
            unit.AttackRange = data.AttackRange;
            unit.AttackFactor = data.AttackFactor;
            unit.DefenceFactor = data.DefenceFactor;
        }
    }
}
