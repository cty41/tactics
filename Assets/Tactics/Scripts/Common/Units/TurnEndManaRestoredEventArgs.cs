using UnityEngine;

namespace Tactics.Common.Units
{
    /// <summary>
    /// Captures a unit's mana recovery and world position at the instant its turn ends.
    /// </summary>
    public readonly struct TurnEndManaRestoredEventArgs
    {
        public readonly IUnit AffectedUnit;
        public readonly float OldMana;
        public readonly float NewMana;
        public readonly Vector3 WorldPosition;

        public TurnEndManaRestoredEventArgs(IUnit affectedUnit, float oldMana, float newMana, Vector3 worldPosition)
        {
            AffectedUnit = affectedUnit;
            OldMana = oldMana;
            NewMana = newMana;
            WorldPosition = worldPosition;
        }
    }
}
