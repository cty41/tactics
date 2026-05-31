using System.Collections.Generic;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Units;
using Tactics.Common.Units.Abilities;

namespace Tactics.Common.AI.MonsterAI
{
    /// <summary>
    /// AI 决策上下文。
    /// 单回合只读决策快照，包含所有决策所需信息。
    /// </summary>
    public class AiContext
    {
        /// <summary>当前单位</summary>
        public IUnit Self { get; }

        /// <summary>网格控制器</summary>
        public IGridController GridController { get; }

        /// <summary>所有敌方单位</summary>
        public List<IUnit> Enemies { get; }

        /// <summary>所有友方单位（不包括自己）</summary>
        public List<IUnit> Allies { get; }

        /// <summary>当前可达格子</summary>
        public List<ICell> ReachableCells { get; }

        /// <summary>候选目标列表</summary>
        public List<IUnit> CandidateTargets { get; }

        /// <summary>可用技能列表</summary>
        public List<AbilityInfo> AvailableAbilities { get; }

        /// <summary>AI 脑资产配置</summary>
        public AiBrainAsset BrainAsset { get; }

        /// <summary>调试日志</summary>
        public AiDecisionLog DecisionLog { get; }

        public AiContext(
            IUnit self,
            IGridController gridController,
            List<IUnit> enemies,
            List<IUnit> allies,
            List<ICell> reachableCells,
            List<IUnit> candidateTargets,
            List<AbilityInfo> availableAbilities,
            AiBrainAsset brainAsset,
            AiDecisionLog decisionLog)
        {
            Self = self;
            GridController = gridController;
            Enemies = enemies;
            Allies = allies;
            ReachableCells = reachableCells;
            CandidateTargets = candidateTargets;
            AvailableAbilities = availableAbilities;
            BrainAsset = brainAsset;
            DecisionLog = decisionLog;
        }

        /// <summary>
        /// 获取自身当前血量百分比。
        /// </summary>
        public float GetSelfHealthPercent()
        {
            if (Self.MaxHealth <= 0) return 0f;
            return Self.Health / Self.MaxHealth;
        }

        /// <summary>
        /// 判断自身是否低血量。
        /// </summary>
        public bool IsSelfLowHealth()
        {
            return GetSelfHealthPercent() <= BrainAsset.LowHealthThreshold;
        }

        /// <summary>
        /// 判断目标是否可击杀。
        /// </summary>
        public bool IsTargetKillable(IUnit target)
        {
            if (target == null || target.IsDowned) return false;
            float damage = Self.CalculateExpectedTotalDamage(target);
            return damage >= target.Health;
        }

        /// <summary>
        /// 获取目标血量百分比。
        /// </summary>
        public float GetTargetHealthPercent(IUnit target)
        {
            if (target.MaxHealth <= 0) return 0f;
            return target.Health / target.MaxHealth;
        }
    }

    /// <summary>
    /// 技能信息。
    /// </summary>
    [System.Flags]
    public enum AbilityAiTags
    {
        None = 0,
        Damage = 1 << 0,
        Heal = 1 << 1,
        Buff = 1 << 2,
        Debuff = 1 << 3,
        Control = 1 << 4,
        Aoe = 1 << 5,
        Movement = 1 << 6,
        Utility = 1 << 7
    }

    public class AbilityInfo
    {
        public string Name { get; }
        public int Range { get; }
        public bool IsReady { get; }
        public IAbility Ability { get; }
        public AbilityAiTags Tags { get; }
        public float BaseDamage { get; }
        public float HealAmount { get; }
        public float ControlValue { get; }
        public float UtilityValue { get; }

        public AbilityInfo(
            string name,
            int range,
            bool isReady,
            IAbility ability,
            AbilityAiTags tags = AbilityAiTags.None,
            float baseDamage = 0f,
            float healAmount = 0f,
            float controlValue = 0f,
            float utilityValue = 0f)
        {
            Name = name;
            Range = range;
            IsReady = isReady;
            Ability = ability;
            Tags = tags;
            BaseDamage = baseDamage;
            HealAmount = healAmount;
            ControlValue = controlValue;
            UtilityValue = utilityValue;
        }

        public bool HasTag(AbilityAiTags tag)
        {
            return (Tags & tag) != 0;
        }
    }
}
