using System.Collections.Generic;
using System.Linq;
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
        Utility = 1 << 7,
        Ranged = 1 << 8
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

    /// <summary>
    /// Describes the authoritative BasicAttack ability and target option selected for one origin.
    /// </summary>
    public sealed class BasicAttackTargetQueryResult
    {
        public AbilityInfo Ability { get; }
        public AbilityTargetOption TargetOption { get; }
        public string FailureReason { get; }
        public bool Succeeded => Ability != null && TargetOption != null;

        private BasicAttackTargetQueryResult(
            AbilityInfo ability,
            AbilityTargetOption targetOption,
            string failureReason)
        {
            Ability = ability;
            TargetOption = targetOption;
            FailureReason = failureReason;
        }

        public static BasicAttackTargetQueryResult Success(
            AbilityInfo ability,
            AbilityTargetOption targetOption)
        {
            return new BasicAttackTargetQueryResult(ability, targetOption, null);
        }

        public static BasicAttackTargetQueryResult Failure(string reason, AbilityInfo ability = null)
        {
            return new BasicAttackTargetQueryResult(ability, null, reason);
        }
    }

    /// <summary>
    /// Resolves BasicAttack legality through the same ability query used by player input and
    /// execution-time revalidation.
    /// </summary>
    /// <remarks>
    /// BasicAttack planning must never reconstruct range from Unit.AttackRange. A missing
    /// targeting provider is a structured failure because it cannot prove execution legality.
    /// </remarks>
    public static class AiBasicAttackTargeting
    {
        private static readonly HashSet<string> AttackAbilityNames = new()
        {
            "Melee Attack",
            "Ranged Attack",
            "Magic Attack",
            "Attack",
            "MeleeAttack",
            "RangedAttack"
        };

        public static AbilityInfo FindAttackAbility(AiContext context)
        {
            return context?.AvailableAbilities?
                .FirstOrDefault(ability => AttackAbilityNames.Contains(ability?.Name ?? string.Empty));
        }

        public static BasicAttackTargetQueryResult Resolve(
            AiContext context,
            IUnit target,
            ICell origin = null)
        {
            if (context?.Self == null || context.GridController == null)
                return BasicAttackTargetQueryResult.Failure("AI context is missing its actor or grid.");
            if (IsDestroyed(target) || target.IsDowned || target.CurrentCell == null)
                return BasicAttackTargetQueryResult.Failure("BasicAttack target is no longer valid.");

            var ability = FindAttackAbility(context);
            if (ability?.Ability == null)
                return BasicAttackTargetQueryResult.Failure("No BasicAttack ability is available.");
            if (!ability.IsReady || !ability.Ability.CanPerform(context.GridController))
                return BasicAttackTargetQueryResult.Failure("BasicAttack ability is not currently available.", ability);
            if (ability.Ability is not IAbilityTargetingProvider targetingProvider)
            {
                return BasicAttackTargetQueryResult.Failure(
                    $"BasicAttack ability '{ability.Name}' has no authoritative target query.",
                    ability);
            }

            var potentialTargets = new List<IUnit>();
            AddDistinct(potentialTargets, context.Enemies);
            AddDistinct(potentialTargets, context.Allies);
            AddDistinct(potentialTargets, new[] { context.Self, target });

            var query = new AbilityTargetQuery(
                context.Self,
                origin ?? context.Self.CurrentCell,
                context.GridController,
                potentialTargets);
            var option = targetingProvider.QueryTargets(query).Options.FirstOrDefault(candidate =>
                Equals(candidate.TargetPoint, target.CurrentCell) && candidate.Targets.Contains(target));

            return option != null
                ? BasicAttackTargetQueryResult.Success(ability, option)
                : BasicAttackTargetQueryResult.Failure(
                    $"Target Unit_{target.UnitID} is not legal for '{ability.Name}'.",
                    ability);
        }

        private static void AddDistinct(List<IUnit> destination, IEnumerable<IUnit> candidates)
        {
            if (candidates == null)
                return;

            foreach (var candidate in candidates)
            {
                if (!IsDestroyed(candidate) && !destination.Contains(candidate))
                    destination.Add(candidate);
            }
        }

        private static bool IsDestroyed(IUnit unit)
        {
            return unit == null ||
                   unit is UnityEngine.Object unityObject && unityObject == null;
        }
    }
}
