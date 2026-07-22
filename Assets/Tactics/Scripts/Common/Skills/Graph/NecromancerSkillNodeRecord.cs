using Tactics.Common.Units.Abilities;
using Tactics.Common.Units.Buffs;
using Tactics.Common.AI.MonsterAI;
using UnityEngine;

namespace Tactics.Common.Skills.Graph
{
    public enum NecromancerSkillKind
    {
        SummonSkeleton,
        AmplifyDamage,
        BoneSpear,
        SummonSkeletonMage,
        FearCurse,
        BoneShield
    }

    /// <summary>
    /// Stores level-specific Necromancer dependencies while the executor owns shared
    /// corpse, area, line, fear, and shield transaction rules.
    /// </summary>
    [System.Serializable]
    public sealed class NecromancerSkillNodeRecord : SkillGraphNodeRecord
    {
        [SerializeField] private NecromancerSkillKind _skillKind;
        [SerializeField, Min(1)] private int _level = 1;
        [SerializeField] private BuffConfig _amplifyDamageBuff;
        [SerializeField] private BuffConfig _fearBuff;
        [SerializeField] private string _summonPrefabPath;
        [SerializeField] private AbilityConfig _summonAttack;
        [SerializeField] private AiBrainAsset _summonBrain;

        public NecromancerSkillKind SkillKind { get => _skillKind; set => _skillKind = value; }
        public int Level { get => Mathf.Max(1, _level); set => _level = Mathf.Max(1, value); }
        public BuffConfig AmplifyDamageBuff { get => _amplifyDamageBuff; set => _amplifyDamageBuff = value; }
        public BuffConfig FearBuff { get => _fearBuff; set => _fearBuff = value; }
        public string SummonPrefabPath { get => _summonPrefabPath; set => _summonPrefabPath = value; }
        public AbilityConfig SummonAttack { get => _summonAttack; set => _summonAttack = value; }
        public AiBrainAsset SummonBrain { get => _summonBrain; set => _summonBrain = value; }
        public override SkillGraphNodeType NodeType => SkillGraphNodeType.NecromancerSkill;
    }
}
