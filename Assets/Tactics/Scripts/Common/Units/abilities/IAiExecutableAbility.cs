using System.Collections.Generic;
using System.Threading.Tasks;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Units;
using Tactics.Common.AI.MonsterAI;

namespace Tactics.Common.Units.Abilities
{
    /// <summary>
    /// AI 可执行能力契约。
    /// 抽象 AI 执行层与具体能力实现（GenericAbilityImpl / SkillGraphAbilityImpl）之间的耦合。
    /// </summary>
    public interface IAiExecutableAbility
    {
        /// <summary>
        /// 通过 AI 执行攻击/技能效果。
        /// </summary>
        Task ExecuteEffectsAsync(IEnumerable<IUnit> targets, IGridController gridController);

        /// <summary>
        /// 通过 AI 执行移动。
        /// </summary>
        Task<bool> ExecuteMoveForAI(ICell destination, IEnumerable<ICell> path, IGridController gridController);
    }

    /// <summary>
    /// Provides the authoritative target legality query for player input, AI planning,
    /// and execution-time revalidation.
    /// </summary>
    public interface IAbilityTargetingProvider
    {
        AbilityTargetResult QueryTargets(AbilityTargetQuery query);
    }

    /// <summary>
    /// A legal target option exposed by an ability to the AI layer.
    /// </summary>
    public sealed class AbilityTargetQuery
    {
        public IUnit Caster { get; }
        public ICell OriginCell { get; }
        public IGridController GridController { get; }
        public IReadOnlyList<IUnit> PotentialTargets { get; }

        public AbilityTargetQuery(
            IUnit caster,
            ICell originCell,
            IGridController gridController,
            IEnumerable<IUnit> potentialTargets)
        {
            Caster = caster;
            OriginCell = originCell;
            GridController = gridController;
            PotentialTargets = potentialTargets == null
                ? new List<IUnit>()
                : new List<IUnit>(potentialTargets);
        }
    }

    /// <summary>
    /// Contains every legal click point and the units affected by selecting it.
    /// </summary>
    public sealed class AbilityTargetResult
    {
        public IReadOnlyList<AbilityTargetOption> Options { get; }

        public AbilityTargetResult(IEnumerable<AbilityTargetOption> options)
        {
            Options = options == null
                ? new List<AbilityTargetOption>()
                : new List<AbilityTargetOption>(options);
        }
    }

    /// <summary>
    /// A legal target point plus the targets used for planning and scoring.
    /// </summary>
    public sealed class AbilityTargetOption
    {
        public ICell TargetPoint { get; }
        public IReadOnlyList<IUnit> Targets { get; }
        public IUnit PrimaryTarget => Targets.Count > 0 ? Targets[0] : null;

        public AbilityTargetOption(ICell targetPoint, IEnumerable<IUnit> targets)
        {
            TargetPoint = targetPoint;
            Targets = targets == null ? new List<IUnit>() : new List<IUnit>(targets);
        }
    }

    /// <summary>
    /// Executes a fully planned action after revalidating its selected target point.
    /// </summary>
    public interface IPlannedAbilityExecutor
    {
        Task<AiActionExecutionResult> ExecuteAsync(AiActionPlan plan);
    }
}
