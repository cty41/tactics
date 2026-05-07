using System;
using Tactics.Common.Units;

namespace Tactics.Common.Units
{
    /// <summary>
    /// Event arguments for when a unit's mana changes.
    /// </summary>
    public readonly struct ManaChangedEventArgs
    {
        public readonly IUnit AffectedUnit;
        public readonly float OldMana;
        public readonly float NewMana;

        public ManaChangedEventArgs(IUnit affectedUnit, float oldMana, float newMana)
        {
            AffectedUnit = affectedUnit;
            OldMana = oldMana;
            NewMana = newMana;
        }
    }
}
