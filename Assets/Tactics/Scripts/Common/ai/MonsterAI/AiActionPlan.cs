using System.Collections.Generic;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Units;

namespace Tactics.Common.AI.MonsterAI
{
    /// <summary>
    /// Immutable action selected by AI scoring.
    /// </summary>
    public sealed class AiActionPlan
    {
        public IUnit Actor { get; }
        public IGridController GridController { get; }
        public ICell Origin { get; }
        public ICell Destination { get; }
        public ICell TargetPoint { get; }
        public IReadOnlyList<IUnit> Targets { get; }
        public AbilityInfo Ability { get; }

        public AiActionPlan(
            IUnit actor,
            IGridController gridController,
            ICell origin,
            ICell destination,
            ICell targetPoint,
            IEnumerable<IUnit> targets,
            AbilityInfo ability)
        {
            Actor = actor;
            GridController = gridController;
            Origin = origin;
            Destination = destination;
            TargetPoint = targetPoint;
            Targets = targets == null ? new List<IUnit>() : new List<IUnit>(targets);
            Ability = ability;
        }
    }

    /// <summary>
    /// Structured result of movement and ability execution.
    /// </summary>
    public sealed class AiActionExecutionResult
    {
        public bool Succeeded { get; }
        public bool Moved { get; }
        public bool UsedFallback { get; }
        public string AbilityName { get; }
        public string FailureReason { get; }

        private AiActionExecutionResult(bool succeeded, bool moved, bool usedFallback, string abilityName, string failureReason)
        {
            Succeeded = succeeded;
            Moved = moved;
            UsedFallback = usedFallback;
            AbilityName = abilityName;
            FailureReason = failureReason;
        }

        public static AiActionExecutionResult Success(string abilityName, bool moved = false, bool usedFallback = false)
        {
            return new AiActionExecutionResult(true, moved, usedFallback, abilityName, null);
        }

        public static AiActionExecutionResult Failure(string reason, bool moved = false)
        {
            return new AiActionExecutionResult(false, moved, false, null, reason);
        }
    }

    /// <summary>
    /// Structured output exposed to gameplay tests and debug commands.
    /// </summary>
    public sealed class AiTurnResult
    {
        public AiActionPlan Plan { get; }
        public AiActionExecutionResult Execution { get; }
        public int PatternStep { get; }

        public AiTurnResult(AiActionPlan plan, AiActionExecutionResult execution, int patternStep = -1)
        {
            Plan = plan;
            Execution = execution;
            PatternStep = patternStep;
        }
    }
}
