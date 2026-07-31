using System.Threading.Tasks;
using System.Linq;
using System.Threading;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Units;
using Tactics.Common.Units.Abilities;
using Tactics.Runtime.Utilities;

namespace Tactics.Common.AI.MonsterAI
{
    /// <summary>
    /// 意图执行器。
    /// 负责把意图翻译成技能执行动作，只做意图翻译，不承载战斗规则。
    /// 通过 IAiExecutableAbility 接口统一执行，不依赖具体能力实现类型。
    /// </summary>
    public static class IntentExecutor
    {
        /// <summary>
        /// 执行选中的意图。
        /// </summary>
        public static async Task Execute(IntentCandidate selected, AiContext context, CancellationToken cancellationToken = default)
        {
            await ExecuteWithResult(selected, context, cancellationToken);
        }

        /// <summary>
        /// Executes an intent and returns a structured result for patterns and gameplay tests.
        /// </summary>
        public static async Task<AiActionExecutionResult> ExecuteWithResult(IntentCandidate selected, AiContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (selected == null)
            {
                TLog.Warning("[IntentExecutor] Selected intent is null.");
                return AiActionExecutionResult.Failure("Selected intent is null.");
            }

            context.DecisionLog.Info($"Executing intent: {selected.IntentType}");

            try
            {
                switch (selected.IntentType)
                {
                    case IntentType.Engage:
                        await ExecuteEngage(selected, context, cancellationToken);
                        return AiActionExecutionResult.Success("Engage", !Equals(selected.Destination, context.Self.CurrentCell));
                    case IntentType.BasicAttack:
                        await ExecuteBasicAttack(selected, context, cancellationToken);
                        return AiActionExecutionResult.Success("BasicAttack");
                    case IntentType.AbilityUse:
                        return await ExecuteAbilityUse(selected, context, cancellationToken);
                    case IntentType.Retreat:
                        await ExecuteRetreat(selected, context, cancellationToken);
                        return AiActionExecutionResult.Success("Retreat", true);
                    case IntentType.FinishOff:
                        await ExecuteFinishOff(selected, context, cancellationToken);
                        return AiActionExecutionResult.Success("FinishOff", selected.Destination != null);
                    case IntentType.HoldPosition:
                        await ExecuteHoldPosition(selected, context);
                        return AiActionExecutionResult.Success("Wait");
                    default:
                        TLog.Warning($"[IntentExecutor] Unknown intent type: {selected.IntentType}");
                        return AiActionExecutionResult.Failure($"Unknown intent type: {selected.IntentType}");
                }
            }
            catch (System.OperationCanceledException)
            {
                // Cancellation must propagate to the caller (turn transition / battle shutdown);
                // it is not an execution failure to recover from.
                throw;
            }
            catch (System.Exception ex)
            {
                if (IsDestroyed(context?.Self) || IsDestroyed(selected.Target))
                {
                    return AiActionExecutionResult.Failure(
                        $"Intent '{selected.IntentType}' was cancelled because a combatant was destroyed.");
                }

                TLog.Error($"[IntentExecutor] Error executing intent {selected.IntentType}: {ex.Message}");
                await ExecuteHoldPosition(selected, context);
                return AiActionExecutionResult.Failure(ex.Message);
            }
        }

        private static bool IsDestroyed(IUnit unit)
        {
            return unit is UnityEngine.Object unityObject && unityObject == null;
        }

        /// <summary>
        /// 执行接敌意图 - 移动到可攻击位置，复用现有 Move 能力系统。
        /// </summary>
        private static async Task ExecuteEngage(IntentCandidate selected, AiContext context, CancellationToken cancellationToken)
        {
            if (selected.Destination == null) return;

            context.DecisionLog.Info($"Engage: Moving to ({selected.Destination.GridCoordinates.x}, {selected.Destination.GridCoordinates.y})");

            var moveAbility = FindMoveAbility(context);
            if (moveAbility == null)
            {
                TLog.Warning("[IntentExecutor] Move ability not found for Engage.");
                context.DecisionLog.ExecutionResult(null, "Move");
                return;
            }

            await ExecuteMoveAsync(selected.Destination, context, moveAbility);
            cancellationToken.ThrowIfCancellationRequested();
            context.DecisionLog.ExecutionResult(moveAbility.Name, "Move");

            // 移动成功后，若目标在攻击范围内，追加一次攻击
            if (selected.Target != null)
            {
                var attackAbility = FindAttackAbility(context);
                if (attackAbility?.Ability is IAiExecutableAbility aiAttack)
                {
                    await aiAttack.ExecuteEffectsAsync(new[] { selected.Target }, context.GridController);
                    cancellationToken.ThrowIfCancellationRequested();
                    context.DecisionLog.ExecutionResult(attackAbility.Name, "Attack", selected.Target.UnitID);
                }
                else
                {
                    TLog.Warning("[IntentExecutor] Engage follow-up attack: no executable attack ability found.");
                }
            }
        }

        /// <summary>
        /// 执行普攻意图。
        /// </summary>
        private static async Task ExecuteBasicAttack(IntentCandidate selected, AiContext context, CancellationToken cancellationToken)
        {
            if (selected.Target == null) return;

            context.DecisionLog.Info($"BasicAttack: Attacking Unit_{selected.Target.UnitID}");

            var attackAbility = FindAttackAbility(context);
            if (attackAbility?.Ability is IAiExecutableAbility aiAttack)
            {
                await aiAttack.ExecuteEffectsAsync(new[] { selected.Target }, context.GridController);
                cancellationToken.ThrowIfCancellationRequested();
                context.DecisionLog.ExecutionResult(attackAbility.Name, "Attack", selected.Target.UnitID);
                return;
            }

            TLog.Warning("[IntentExecutor] No executable attack ability found for BasicAttack.");
            context.DecisionLog.ExecutionResult(attackAbility?.Name, "Attack", selected.Target.UnitID);
        }

        /// <summary>
        /// 执行技能释放意图 - 复用现有 AIExecuteAbility 链路。
        /// </summary>
        private static async Task<AiActionExecutionResult> ExecuteAbilityUse(IntentCandidate selected, AiContext context, CancellationToken cancellationToken)
        {
            if (selected.Ability?.Ability == null || selected.AbilityTargetCell == null)
                return AiActionExecutionResult.Failure("Ability or target point is missing.");

            context.DecisionLog.Info($"AbilityUse: {selected.Ability.Name}");

            var origin = context.Self.CurrentCell;
            bool moved = false;
            if (selected.Destination != null && !selected.Destination.Equals(origin))
            {
                var moveAbility = FindMoveAbility(context);
                if (moveAbility == null || !await ExecuteMoveAsync(selected.Destination, context, moveAbility))
                {
                    context.DecisionLog.ExecutionResult(null, "MoveFailed");
                    return AiActionExecutionResult.Failure("Movement failed; ability was not cast.");
                }
                moved = true;
                cancellationToken.ThrowIfCancellationRequested();
            }

            var plan = new AiActionPlan(
                context.Self,
                context.GridController,
                origin,
                selected.Destination ?? origin,
                selected.AbilityTargetCell,
                selected.Targets,
                selected.Ability);

            if (selected.Ability.Ability is IPlannedAbilityExecutor plannedExecutor)
            {
                var result = await plannedExecutor.ExecuteAsync(plan);
                cancellationToken.ThrowIfCancellationRequested();
                context.DecisionLog.ExecutionResult(selected.Ability.Name, result.Succeeded ? "UseAbility" : "AbilityFailed", selected.Target?.UnitID);
                if (!result.Succeeded)
                    context.DecisionLog.Info($"AbilityUse failed: {result.FailureReason}");
                return result.Succeeded
                    ? AiActionExecutionResult.Success(selected.Ability.Name, moved)
                    : AiActionExecutionResult.Failure(result.FailureReason, moved);
            }

            if (selected.Ability.Ability is IAiExecutableAbility aiAbility)
            {
                await aiAbility.ExecuteEffectsAsync(selected.Targets, context.GridController);
                cancellationToken.ThrowIfCancellationRequested();
                context.DecisionLog.ExecutionResult(selected.Ability.Name, "UseAbility", selected.Target?.UnitID);
                return AiActionExecutionResult.Success(selected.Ability.Name, moved);
            }

            TLog.Warning($"[IntentExecutor] Ability '{selected.Ability.Name}' does not implement IAiExecutableAbility.");
            return AiActionExecutionResult.Failure($"Ability '{selected.Ability.Name}' is not AI executable.", moved);
        }

        /// <summary>
        /// 执行撤退意图 - 复用现有 Move 能力系统。
        /// </summary>
        private static async Task ExecuteRetreat(IntentCandidate selected, AiContext context, CancellationToken cancellationToken)
        {
            if (selected.Destination == null) return;

            context.DecisionLog.Info($"Retreat: Moving to ({selected.Destination.GridCoordinates.x}, {selected.Destination.GridCoordinates.y})");

            var moveAbility = FindMoveAbility(context);
            if (moveAbility == null)
            {
                TLog.Warning("[IntentExecutor] Move ability not found for Retreat.");
                context.DecisionLog.ExecutionResult(null, "Move");
                return;
            }

            await ExecuteMoveAsync(selected.Destination, context, moveAbility);
            cancellationToken.ThrowIfCancellationRequested();
            context.DecisionLog.ExecutionResult(moveAbility.Name, "Move");
        }

        /// <summary>
        /// 执行追击残血意图。
        /// </summary>
        private static async Task ExecuteFinishOff(IntentCandidate selected, AiContext context, CancellationToken cancellationToken)
        {
            if (selected.Target == null) return;

            context.DecisionLog.Info($"FinishOff: Attacking low health target Unit_{selected.Target.UnitID}");

            // 先移动到目标附近（如果需要）
            if (selected.Destination != null)
            {
                var moveAbility = FindMoveAbility(context);
                if (moveAbility == null)
                {
                    TLog.Warning("[IntentExecutor] Move ability not found for FinishOff.");
                    context.DecisionLog.ExecutionResult(null, "Move");
                    return;
                }

                bool moved = await ExecuteMoveAsync(selected.Destination, context, moveAbility);
                cancellationToken.ThrowIfCancellationRequested();
                if (!moved)
                {
                    TLog.Warning("[IntentExecutor] FinishOff movement failed; skipping follow-up attack.");
                    context.DecisionLog.ExecutionResult(null, "Move");
                    return;
                }
            }

            // 攻击目标
            var attackAbility = FindAttackAbility(context);
            if (attackAbility?.Ability is IAiExecutableAbility aiAttack)
            {
                await aiAttack.ExecuteEffectsAsync(new[] { selected.Target }, context.GridController);
                cancellationToken.ThrowIfCancellationRequested();
                context.DecisionLog.ExecutionResult(attackAbility.Name, "Attack", selected.Target.UnitID);
                return;
            }

            TLog.Warning("[IntentExecutor] No executable attack ability found for FinishOff.");
            context.DecisionLog.ExecutionResult(attackAbility?.Name, "Attack", selected.Target.UnitID);
        }

        /// <summary>
        /// 执行待机/占位意图。
        /// </summary>
        private static async Task ExecuteHoldPosition(IntentCandidate selected, AiContext context)
        {
            context.DecisionLog.Info("HoldPosition: Not moving or attacking.");
            context.DecisionLog.ExecutionResult(null, "Wait");
            await Task.CompletedTask;
        }

        private static AbilityInfo FindAttackAbility(AiContext context)
        {
            foreach (var ability in context.AvailableAbilities)
            {
                var name = ability.Name ?? "";
                if (name == "Melee Attack" || name == "Ranged Attack" || name == "Magic Attack" ||
                    name == "Attack" || name == "MeleeAttack" || name == "RangedAttack")
                    return ability;
            }
            return null;
        }

        private static AbilityInfo FindMoveAbility(AiContext context)
        {
            foreach (var ability in context.AvailableAbilities)
            {
                if (ability.Name == "Move")
                    return ability;
            }
            return null;
        }

        private static async Task<bool> ExecuteMoveAsync(ICell destination, AiContext context, AbilityInfo moveAbility)
        {
            if (destination == null) return false;
            if (destination.Equals(context.Self.CurrentCell)) return true;

            context.Self.CachePaths(context.GridController.CellManager);
            var path = context.Self.FindPath(destination, context.GridController.CellManager).ToList();
            if (path.Count == 0)
            {
                TLog.Warning("[IntentExecutor] Move path is empty.");
                return false;
            }

            if (moveAbility.Ability is IAiExecutableAbility aiMove)
            {
                return await aiMove.ExecuteMoveForAI(destination, path, context.GridController);
            }

            // 兼容兜底：当 IAiExecutableAbility 不可用时仍保留 MoveCommand
            return await ExecuteCommandForAI(new MoveCommand(context.Self.CurrentCell, destination, path), destination, context);
        }

        private static async Task<bool> ExecuteCommandForAI(ICommand command, ICell destination, AiContext context)
        {
            var tcs = new TaskCompletionSource<bool>();
            context.Self.AIExecuteAbility(command, context.GridController, tcs);
            
            // 添加超时机制，避免 tcs 永远挂起
            var timeoutTask = Task.Delay(2000);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);
            if (completedTask == timeoutTask)
            {
                TLog.Warning("[IntentExecutor] Command execution timed out (2s).");
                return destination != null && destination.Equals(context.Self.CurrentCell);
            }
            return await tcs.Task && destination != null && destination.Equals(context.Self.CurrentCell);
        }
    }
}
