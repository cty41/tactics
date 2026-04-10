using Tactics.Tbsf.Common.Units;
using UnityEngine;

namespace Tactics.Roster
{
    public static class CharacterStatsApplicator
    {
        public static void ApplyToUnit(CharacterDefinition data, Unit unit)
        {
            if (data == null || unit == null)
                return;

            unit.Strength = data.Strength;
            unit.Agility = data.Agility;
            unit.Constitution = data.Constitution;
            unit.Intelligence = data.Intelligence;
            unit.Charisma = data.Charisma;
            unit.Luck = data.Luck;
            unit.Speed = data.Speed;
            unit.ActionPoints = data.ActionPoints;
            unit.AttackRange = data.AttackRange;
            unit.AttackFactor = data.AttackFactor;
            unit.DefenceFactor = data.DefenceFactor;
        }
    }
}
