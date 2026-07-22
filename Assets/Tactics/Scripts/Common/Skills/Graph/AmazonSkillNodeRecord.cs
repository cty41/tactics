using Tactics.Common.Units.Buffs;
using UnityEngine;

namespace Tactics.Common.Skills.Graph
{
    public enum AmazonSkillKind
    {
        Thrust,
        MultiStab,
        PoisonSpear,
        RecoverSpear,
        PickupSpear,
        Decoy
    }

    /// <summary>Level-authored Amazon mechanics that share spear and decoy battle state.</summary>
    [System.Serializable]
    public sealed class AmazonSkillNodeRecord : SkillGraphNodeRecord
    {
        [SerializeField] private AmazonSkillKind _skillKind;
        [SerializeField] private int _level = 1;
        [SerializeField] private BuffConfig _poisonBuff;

        public AmazonSkillKind SkillKind { get => _skillKind; set => _skillKind = value; }
        public int Level { get => _level; set => _level = Mathf.Max(1, value); }
        public BuffConfig PoisonBuff { get => _poisonBuff; set => _poisonBuff = value; }
        public override SkillGraphNodeType NodeType => SkillGraphNodeType.AmazonSkill;
    }
}
