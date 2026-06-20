using System.Collections.Generic;
using System.Threading.Tasks;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Units;

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
}
