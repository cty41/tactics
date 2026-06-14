using System.Threading.Tasks;
using System.Linq;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Units;
using Tactics.Common.Units.Abilities;
using Tactics.Runtime.Utilities;

namespace Tactics.Common.AI.MonsterAI
{
    /// <summary>
    /// 意图执行器。
    /// 负责把意图翻译成现有移动/攻击/技能命令，只做意图翻译，不承载战斗规则。
    /// </summary>
    public static class IntentExecutor
    {
        /// <summary>
        /// 执行选中的意图。
        /// </summary>
        public static async Task Execute(IntentCandidate selected, AiContext context)
        {
            if (selected == null)
            {
                TLog.Warning("[IntentExecutor] Selected intent is null.");
                return;
            }

            context.DecisionLog.Info($"Executing intent: {selected.IntentType}");

            try
            {
                switch (selected.IntentType)
                {
                    case IntentType.Engage:
                        await ExecuteEngage(selected, context);
                        break;
                    case IntentType.BasicAttack:
                        await ExecuteBasicAttack(selected, context);
                        break;
                    case IntentType.AbilityUse:
                        await ExecuteAbilityUse(selected, context);
                        break;
                    case IntentType.Retreat:
                        await ExecuteRetreat(selected, context);
                        break;
                    case IntentType.FinishOff:
                        await ExecuteFinishOff(selected, context);
                        break;
                    case IntentType.HoldPosition:
                        await ExecuteHoldPosition(selected, context);
                        break;
                    default:
                        TLog.Warning($"[IntentExecutor] Unknown intent type: {selected.IntentType}");
                        break;
                }
            }
            catch (System.Exception ex)
            {
                TLog.Error($"[IntentExecutor] Error executing intent {selected.IntentType}: {ex.Message}");
                await ExecuteHoldPosition(selected, context);
            }
        }

        /// <summary>
        /// 执行接敌意图 - 移动到可攻击位置，复用现有 Move 能力系统。
        /// </summary>
        private static async Task ExecuteEngage(IntentCandidate selected, AiContext context)
        {
            if (selected.Destination == null) return;

            context.DecisionLog.Info($"Engage: Moving to ({selected.Destination.GridCoordinates.x}, {selected.Destination.GridCoordinates.y})");

            var moveAbility = FindMoveAbility(context);
            if (moveAbility == null)
            {
                TLog.Warning("[IntentExecutor] Move ability not found for Engage.");
                return;
            }

            await ExecuteMoveAsync(selected.Destination, context, moveAbility);
        }

        /// <summary>
        /// 执行普攻意图 - 使用现有 AttackCommand。
        /// </summary>
        private static async Task ExecuteBasicAttack(IntentCandidate selected, AiContext context)
        {
            if (selected.Target == null) return;

            context.DecisionLog.Info($"BasicAttack: Attacking Unit_{selected.Target.UnitID}");

            float damage = context.Self.CalculateDamageDealt(selected.Target, selected.Target.CurrentCell, context.Self.CurrentCell);
            var command = new AttackCommand(selected.Target, damage);
            await ExecuteCommandForAI(command, context);
        }

        /// <summary>
        /// 执行技能释放意图 - 复用现有 AIExecuteAbility 链路。
        /// </summary>
        private static async Task ExecuteAbilityUse(IntentCandidate selected, AiContext context)
        {
            if (selected.Ability?.Ability == null || selected.AbilityTargetCell == null) return;

            context.DecisionLog.Info($"AbilityUse: {selected.Ability.Name}");

            if (selected.Ability.Ability is GenericAbilityImpl generic)
            {
                await generic.ExecuteEffectsAsync(selected.Targets, context.GridController);
                return;
            }

            TLog.Warning($"[IntentExecutor] Ability '{selected.Ability.Name}' does not support awaitable AI execution.");
        }

        /// <summary>
        /// 执行撤退意图 - 复用现有 Move 能力系统。
        /// </summary>
        private static async Task ExecuteRetreat(IntentCandidate selected, AiContext context)
        {
            if (selected.Destination == null) return;

            context.DecisionLog.Info($"Retreat: Moving to ({selected.Destination.GridCoordinates.x}, {selected.Destination.GridCoordinates.y})");

            var moveAbility = FindMoveAbility(context);
            if (moveAbility == null)
            {
                TLog.Warning("[IntentExecutor] Move ability not found for Retreat.");
                return;
            }

            await ExecuteMoveAsync(selected.Destination, context, moveAbility);
        }

        /// <summary>
        /// 执行追击残血意图。
        /// </summary>
        private static async Task ExecuteFinishOff(IntentCandidate selected, AiContext context)
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
                    return;
                }

                bool moved = await ExecuteMoveAsync(selected.Destination, context, moveAbility);
                if (!moved)
                {
                    TLog.Warning("[IntentExecutor] FinishOff movement failed; skipping follow-up attack.");
                    return;
                }
            }

            // 攻击目标 - 使用现有 AttackCommand
            float damage = context.Self.CalculateDamageDealt(selected.Target, selected.Target.CurrentCell, context.Self.CurrentCell);
            var command = new AttackCommand(selected.Target, damage);
            await ExecuteCommandForAI(command, context);
        }

        /// <summary>
        /// 执行待机/占位意图。
        /// </summary>
        private static async Task ExecuteHoldPosition(IntentCandidate selected, AiContext context)
        {
            context.DecisionLog.Info("HoldPosition: Not moving or attacking.");
            await Task.CompletedTask;
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

            if (moveAbility.Ability is GenericAbilityImpl genericMove)
            {
                return await genericMove.ExecuteMoveForAI(destination, path, context.GridController);
            }

            await ExecuteCommandForAI(new MoveCommand(context.Self.CurrentCell, destination, path), context);
            return true;
        }

        private static async Task ExecuteCommandForAI(ICommand command, AiContext context)
        {
            var tcs = new TaskCompletionSource<bool>();
            context.Self.AIExecuteAbility(command, context.GridController, tcs);
            
            // 添加超时机制，避免 tcs 永远挂起
            var timeoutTask = Task.Delay(2000);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);
            if (completedTask == timeoutTask)
            {
                TLog.Info("[IntentExecutor] Command execution timed out (2s), assuming executed.");
            }
        }
    }
}
