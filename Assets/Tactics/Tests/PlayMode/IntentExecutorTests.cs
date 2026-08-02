using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Tactics.Common.AI.MonsterAI;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Testing.Gameplay;
using Tactics.Common.Units;
using Tactics.Common.Units.Abilities;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tactics.Tests.PlayMode
{
    /// <summary>
    /// IntentExecutor 白盒测试。
    /// 覆盖 FindAttackAbility 名称匹配逻辑，防止 "Melee Attack" vs "MeleeAttack" 空格不匹配 bug 回归。
    /// </summary>
    public class IntentExecutorTests
    {
        [Test]
        public void FindAttackAbility_RecognizesMeleeAttack_WithSpace()
        {
            var context = CreateContextWithAbility("Melee Attack");
            var result = InvokeFindAttackAbility(context);
            Assert.IsNotNull(result, "FindAttackAbility should recognize 'Melee Attack' (with space).");
            Assert.AreEqual("Melee Attack", result.Name);
        }

        [Test]
        public void FindAttackAbility_RecognizesRangedAttack_WithSpace()
        {
            var context = CreateContextWithAbility("Ranged Attack");
            var result = InvokeFindAttackAbility(context);
            Assert.IsNotNull(result, "FindAttackAbility should recognize 'Ranged Attack' (with space).");
            Assert.AreEqual("Ranged Attack", result.Name);
        }

        [Test]
        public void FindAttackAbility_RecognizesMagicAttack_WithSpace()
        {
            var context = CreateContextWithAbility("Magic Attack");
            var result = InvokeFindAttackAbility(context);
            Assert.IsNotNull(result, "FindAttackAbility should recognize 'Magic Attack' (with space).");
            Assert.AreEqual("Magic Attack", result.Name);
        }

        [Test]
        public void FindAttackAbility_RecognizesMeleeAttack_WithoutSpace()
        {
            var context = CreateContextWithAbility("MeleeAttack");
            var result = InvokeFindAttackAbility(context);
            Assert.IsNotNull(result, "FindAttackAbility should recognize 'MeleeAttack' (without space).");
            Assert.AreEqual("MeleeAttack", result.Name);
        }

        [Test]
        public void FindAttackAbility_RecognizesRangedAttack_WithoutSpace()
        {
            var context = CreateContextWithAbility("RangedAttack");
            var result = InvokeFindAttackAbility(context);
            Assert.IsNotNull(result, "FindAttackAbility should recognize 'RangedAttack' (without space).");
            Assert.AreEqual("RangedAttack", result.Name);
        }

        [Test]
        public void FindAttackAbility_ReturnsNull_ForMoveOnly()
        {
            var context = CreateContextWithAbility("Move");
            var result = InvokeFindAttackAbility(context);
            Assert.IsNull(result, "FindAttackAbility should return null for 'Move' ability.");
        }

        [Test]
        public void FindAttackAbility_ReturnsNull_ForHeal()
        {
            var context = CreateContextWithAbility("Heal");
            var result = InvokeFindAttackAbility(context);
            Assert.IsNull(result, "FindAttackAbility should return null for 'Heal' ability.");
        }

        [Test]
        public void FindAttackAbility_ReturnsNull_ForEmpty()
        {
            var context = CreateContextWithNoAbilities();
            var result = InvokeFindAttackAbility(context);
            Assert.IsNull(result, "FindAttackAbility should return null when no abilities exist.");
        }

        [Test]
        public void FindMoveAbility_RecognizesMove()
        {
            var context = CreateContextWithAbility("Move");
            var result = InvokeFindMoveAbility(context);
            Assert.IsNotNull(result, "FindMoveAbility should recognize 'Move'.");
            Assert.AreEqual("Move", result.Name);
        }

        [Test]
        public void FindMoveAbility_ReturnsNull_ForMeleeAttack()
        {
            var context = CreateContextWithAbility("Melee Attack");
            var result = InvokeFindMoveAbility(context);
            Assert.IsNull(result, "FindMoveAbility should return null for 'Melee Attack'.");
        }

        [Test]
        public async Task ExecuteBasicAttack_WithoutAuthoritativeContext_ReturnsStructuredFailure()
        {
            var targetObject = new GameObject("IntentExecutorTarget");
            var target = targetObject.AddComponent<Unit>();
            var aiAbility = new FakeAiExecutableAbility();
            var abilityInfo = new AbilityInfo(
                "Melee Attack",
                1,
                true,
                aiAbility,
                AbilityAiTags.Damage,
                5f,
                0f,
                0f,
                0f
            );
            var context = CreateContextWithAbilities(new[] { abilityInfo }, new[] { target });
            var selected = CreateSelectedCandidate(IntentType.BasicAttack, context);

            try
            {
                var result = await IntentExecutor.ExecuteWithResult(selected, context);

                Assert.IsFalse(result.Succeeded,
                    "BasicAttack must fail closed when its authoritative actor/grid context is missing.");
                Assert.IsFalse(aiAbility.EffectsAsyncCalled,
                    "Legacy IAiExecutableAbility execution must not bypass the planned target contract.");
                var executionEntry = context.DecisionLog.GetEntries().LastOrDefault(e => e.Type == AiDecisionLog.LogType.ExecutionResult);
                Assert.IsNotNull(executionEntry, "ExecutionResult should be recorded.");
                Assert.IsTrue(executionEntry.Message.Contains("AttackFailed"), executionEntry.Message);
            }
            finally
            {
                Object.DestroyImmediate(targetObject);
            }
        }

        #region Helpers

        [Test]
        public async Task ExecuteWithResult_ThrowsBeforeExecuting_WhenCancellationAlreadyRequested()
        {
            var targetObject = new GameObject("IntentExecutorPrecancelledTarget");
            var target = targetObject.AddComponent<Unit>();
            var aiAbility = new FakeAiExecutableAbility();
            var abilityInfo = new AbilityInfo(
                "Melee Attack",
                1,
                true,
                aiAbility,
                AbilityAiTags.Damage,
                5f,
                0f,
                0f,
                0f
            );
            var context = CreateContextWithAbilities(new[] { abilityInfo }, new[] { target });
            var selected = CreateSelectedCandidate(IntentType.BasicAttack, context);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            try
            {
                Assert.CatchAsync<System.OperationCanceledException>(
                    async () => await IntentExecutor.ExecuteWithResult(selected, context, cts.Token));
                Assert.IsFalse(aiAbility.EffectsAsyncCalled,
                    "A cancelled intent must not execute any ability.");
            }
            finally
            {
                Object.DestroyImmediate(targetObject);
            }
        }

        [Test]
        public async Task ExecuteWithResult_CancelDuringAbilityExecution_PropagatesCancellation()
        {
            var targetObject = new GameObject("IntentExecutorMidCancelTarget");
            var target = targetObject.AddComponent<Unit>();
            var cellObject = new GameObject("IntentExecutorMidCancelCell");
            var targetCell = cellObject.AddComponent<Square>();
            target.CurrentCell = targetCell;
            using var cts = new CancellationTokenSource();
            var aiAbility = new FakeAiExecutableAbility { CancelDuringExecuteEffects = cts };
            var abilityInfo = new AbilityInfo(
                "Melee Attack",
                1,
                true,
                aiAbility,
                AbilityAiTags.Damage,
                5f,
                0f,
                0f,
                0f
            );
            var context = CreateContextWithAbilities(new[] { abilityInfo }, new[] { target });
            var selected = new IntentCandidate(
                IntentType.AbilityUse,
                ActionType.UseAbility,
                target,
                targetCell,
                abilityInfo,
                10f,
                new List<IUnit> { target },
                targetCell);

            try
            {
                Assert.CatchAsync<System.OperationCanceledException>(
                    async () => await IntentExecutor.ExecuteWithResult(selected, context, cts.Token));
                Assert.IsTrue(aiAbility.EffectsAsyncCalled,
                    "The ability starts before cancellation lands mid-execution.");
                var executionEntry = context.DecisionLog.GetEntries()
                    .LastOrDefault(e => e.Type == AiDecisionLog.LogType.ExecutionResult);
                Assert.IsNull(executionEntry,
                    "No execution result may be recorded once cancellation interrupts the action.");
            }
            finally
            {
                Object.DestroyImmediate(targetObject);
                Object.DestroyImmediate(cellObject);
            }
        }

        /// <summary>
        /// Creates an AiContext with a single ability of the given name.
        /// Uses reflection to build the context since AiContext has no public constructor for testing.
        /// </summary>
        private static AiContext CreateContextWithAbility(string abilityName)
        {
            // Create a minimal AiContext with AvailableAbilities containing one entry
            var abilityInfo = new AbilityInfo(
                abilityName,
                1,      // range
                true,   // isReady
                null,   // ability (not needed for name matching)
                AbilityAiTags.Damage,
                5f,     // baseDamage
                0f,     // healAmount
                0f,     // controlValue
                0f      // utilityValue
            );

            return CreateContextWithAbilities(new[] { abilityInfo });
        }

        private static AiContext CreateContextWithNoAbilities()
        {
            return CreateContextWithAbilities(new AbilityInfo[0]);
        }

        private static AiContext CreateContextWithAbilities(AbilityInfo[] abilities, IEnumerable<IUnit> candidateTargets = null)
        {
            var targetList = candidateTargets?.ToList();
            // AiContext constructor: (self, gridController, enemies, allies, reachableCells, candidateTargets, availableAbilities, brainAsset, decisionLog)
            var context = new AiContext(
                targetList?.FirstOrDefault(),
                null,   // gridController
                null,   // enemies
                null,   // allies
                null,   // reachableCells
                targetList,
                abilities.ToList(),
                null,   // brainAsset
                new AiDecisionLog(false)
            );
            return context;
        }

        /// <summary>
        /// Invokes the private static FindAttackAbility method via reflection.
        /// </summary>
        private static AbilityInfo InvokeFindAttackAbility(AiContext context)
        {
            var method = typeof(IntentExecutor).GetMethod("FindAttackAbility", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "FindAttackAbility method should exist.");
            return (AbilityInfo)method.Invoke(null, new object[] { context });
        }

        /// <summary>
        /// Invokes the private static FindMoveAbility method via reflection.
        /// </summary>
        private static AbilityInfo InvokeFindMoveAbility(AiContext context)
        {
            var method = typeof(IntentExecutor).GetMethod("FindMoveAbility", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "FindMoveAbility method should exist.");
            return (AbilityInfo)method.Invoke(null, new object[] { context });
        }

        private static IntentCandidate CreateSelectedCandidate(IntentType intentType, AiContext context)
        {
            var target = context.CandidateTargets?.FirstOrDefault();
            return new IntentCandidate(intentType, ActionType.Attack, target, target?.CurrentCell, null, 10f, sourceIntentNodeId: null);
        }

        private sealed class FakeAiExecutableAbility : IAbility, IAiExecutableAbility
        {
            public bool EffectsAsyncCalled { get; private set; }
            public CancellationTokenSource CancelDuringExecuteEffects { get; set; }
            public event System.Action<IAbility> AbilitySelected;
            public event System.Action<IAbility> AbilityDeselected;
            public IUnit UnitReference { get; set; }
            public string DisplayName => "FakeAttack";
            public Sprite Icon => null;
            public int Cost => 0;
            public void Initialize(IGridController gridController) { }
            public void Display(IGridController gridController) { }
            public void CleanUp(IGridController gridController) { }
            public void OnUnitClicked(IUnit unit, IGridController gridController) { }
            public void OnUnitHighlighted(IUnit unit, IGridController gridController) { }
            public void OnUnitDehighlighted(IUnit unit, IGridController gridController) { }
            public void OnUnitDestroyed(IGridController gridController) { }
            public void OnCellClicked(ICell cell, IGridController gridController) { }
            public void OnCellHighlighted(ICell cell, IGridController gridController) { }
            public void OnCellDehighlighted(ICell cell, IGridController gridController) { }
            public void OnAbilitySelected(IGridController gridController) { }
            public void OnAbilityDeselected(IGridController gridController) { }
            public void OnTurnStart(IGridController gridController) { }
            public void OnTurnEnd(IGridController gridController) { }
            public bool CanPerform(IGridController gridController) => true;
            public void InvokeAbilitySelected() { AbilitySelected?.Invoke(this); }
            public void InvokeAbilityDeselected() { AbilityDeselected?.Invoke(this); }

            public Task ExecuteEffectsAsync(IEnumerable<IUnit> targets, IGridController gridController)
            {
                EffectsAsyncCalled = true;
                CancelDuringExecuteEffects?.Cancel();
                return Task.CompletedTask;
            }

            public Task<bool> ExecuteMoveForAI(ICell destination, IEnumerable<ICell> path, IGridController gridController)
            {
                return Task.FromResult(false);
            }
        }

        #endregion
    }
}
