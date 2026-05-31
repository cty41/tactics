using System.Threading.Tasks;
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
            if (moveAbility != null)
            {
            // 选中移动技能，点击目标格子触发，复用现有移动执行链
                moveAbility.Ability.OnAbilitySelected(context.GridController);
                moveAbility.Ability.OnCellClicked(selected.Destination, context.GridController);
            }
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
            await context.Self.ExecuteAbility(command, null, null);
        }

        /// <summary>
        /// 执行技能释放意图 - 复用现有 AIExecuteAbility 链路。
        /// </summary>
        private static async Task ExecuteAbilityUse(IntentCandidate selected, AiContext context)
        {
            if (selected.Ability?.Ability == null || selected.AbilityTargetCell == null) return;

            context.DecisionLog.Info($"AbilityUse: {selected.Ability.Name}");

            // 选中技能，点击目标触发，复用现有技能执行链
            selected.Ability.Ability.OnAbilitySelected(context.GridController);
            selected.Ability.Ability.OnCellClicked(selected.AbilityTargetCell, context.GridController);
        }

        /// <summary>
        /// 执行撤退意图 - 复用现有 Move 能力系统。
        /// </summary>
        private static async Task ExecuteRetreat(IntentCandidate selected, AiContext context)
        {
            if (selected.Destination == null) return;

            context.DecisionLog.Info($"Retreat: Moving to ({selected.Destination.GridCoordinates.x}, {selected.Destination.GridCoordinates.y})");

            var moveAbility = FindMoveAbility(context);
            if (moveAbility != null)
            {
                moveAbility.Ability.OnAbilitySelected(context.GridController);
                moveAbility.Ability.OnCellClicked(selected.Destination, context.GridController);
            }
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
                if (moveAbility != null)
                {
                    moveAbility.Ability.OnAbilitySelected(context.GridController);
                    moveAbility.Ability.OnCellClicked(selected.Destination, context.GridController);
                }
            }

            // 攻击目标 - 使用现有 AttackCommand
            float damage = context.Self.CalculateDamageDealt(selected.Target, selected.Target.CurrentCell, context.Self.CurrentCell);
            var command = new AttackCommand(selected.Target, damage);
            await context.Self.ExecuteAbility(command, null, null);
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
    }
}
