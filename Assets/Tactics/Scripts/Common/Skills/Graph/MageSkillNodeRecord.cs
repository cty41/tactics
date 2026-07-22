using Tactics.Common.Units.Buffs;
using UnityEngine;

namespace Tactics.Common.Skills.Graph
{
    public enum MageSkillKind
    {
        Fireball,
        IceBolt,
        Lightning,
        SummonFireDemon,
        IceArmor
    }

    /// <summary>
    /// Data-driven level parameters for Mage skills whose behavior cannot be expressed by
    /// the small generic graph nodes without duplicating target and atomicity rules.
    /// </summary>
    [System.Serializable]
    public sealed class MageSkillNodeRecord : SkillGraphNodeRecord
    {
        [SerializeField] private MageSkillKind _skillKind;
        [SerializeField, Min(1)] private int _level = 1;
        [SerializeField] private BuffConfig _burningBuff;
        [SerializeField] private BuffConfig _slowBuff;
        [SerializeField] private BuffConfig _stunBuff;
        [SerializeField] private BuffConfig _iceArmorBuff;
        [SerializeField] private string _fireDemonPrefabPath;

        public MageSkillKind SkillKind { get => _skillKind; set => _skillKind = value; }
        public int Level { get => Mathf.Max(1, _level); set => _level = Mathf.Max(1, value); }
        public BuffConfig BurningBuff { get => _burningBuff; set => _burningBuff = value; }
        public BuffConfig SlowBuff { get => _slowBuff; set => _slowBuff = value; }
        public BuffConfig StunBuff { get => _stunBuff; set => _stunBuff = value; }
        public BuffConfig IceArmorBuff { get => _iceArmorBuff; set => _iceArmorBuff = value; }
        public string FireDemonPrefabPath { get => _fireDemonPrefabPath; set => _fireDemonPrefabPath = value; }
        public override SkillGraphNodeType NodeType => SkillGraphNodeType.MageSkill;
    }
}
