using System;

namespace Tactics.Equipment
{
    [Serializable]
    public class EquipmentDefinition
    {
        public string Id;
        public string DisplayName;
        public EquipmentSlot Slot;
        public int StrengthBonus;
        public int AgilityBonus;
        public int ConstitutionBonus;
        public int IntelligenceBonus;
        public int CharismaBonus;
        public int LuckBonus;
    }
}
