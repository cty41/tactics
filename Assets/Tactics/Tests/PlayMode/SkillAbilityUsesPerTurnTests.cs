using System;
using System.Collections;
using NUnit.Framework;
using Tactics.Common.Controllers.GridStates;
using Tactics.Common.Controllers;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Skills.Graph.Testing;
using Tactics.Common.Units;
using Tactics.Common.Units.Abilities;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tactics.Tests.PlayMode
{
    public class SkillAbilityUsesPerTurnTests
    {
        [Test]
        public void Unit_AbilityUseCount_ResetsOnPrepareForTurn()
        {
            using var world = new SkillGraphTestWorld();
            var unit = world.CreateUnit("Unit", playerNumber: 0);

            unit.MarkAbilityUsedThisTurn("ChargeStrike");
            unit.MarkAbilityUsedThisTurn("ChargeStrike");

            Assert.That(unit.GetAbilityUseCountThisTurn("ChargeStrike"), Is.EqualTo(2));

            unit.PrepareForTurn();

            Assert.That(unit.GetAbilityUseCountThisTurn("ChargeStrike"), Is.Zero);
        }

        [Test]
        public void Unit_MarkBasicAbilityUsed_AlsoCounts()
        {
            using var world = new SkillGraphTestWorld();
            var unit = world.CreateUnit("Unit", playerNumber: 0);

            unit.MarkBasicAbilityUsed("Move");

            Assert.That(unit.GetAbilityUseCountThisTurn("Move"), Is.EqualTo(1));
        }

        [Test]
        public void Unit_MarkBasicAbilityUsed_RepeatedCallCountsOnceAndRaisesEventEachTime()
        {
            using var world = new SkillGraphTestWorld();
            var unit = world.CreateUnit("Unit", playerNumber: 0);
            int eventCount = 0;
            unit.BasicAbilityUsed += _ => eventCount++;

            unit.MarkBasicAbilityUsed("Move");
            unit.MarkBasicAbilityUsed("Move");

            Assert.That(unit.GetAbilityUseCountThisTurn("Move"), Is.EqualTo(1));
            Assert.That(eventCount, Is.EqualTo(2));
        }

        [TestCase(null)]
        [TestCase("")]
        public void Unit_AbilityUseTracking_InvalidNameIsIgnored(string abilityName)
        {
            using var world = new SkillGraphTestWorld();
            var unit = world.CreateUnit("Unit", playerNumber: 0);
            int eventCount = 0;
            unit.BasicAbilityUsed += _ => eventCount++;

            Assert.DoesNotThrow(() => unit.GetAbilityUseCountThisTurn(abilityName));
            Assert.DoesNotThrow(() => unit.MarkAbilityUsedThisTurn(abilityName));
            Assert.DoesNotThrow(() => unit.MarkBasicAbilityUsed(abilityName));

            Assert.That(unit.GetAbilityUseCountThisTurn(abilityName), Is.Zero);
            Assert.That(eventCount, Is.Zero);
        }

        [Test]
        public void SkillAbility_LimitedUseWithoutStableName_IsDisabled()
        {
            using var world = new SkillGraphTestWorld();
            var casterCell = world.CreateSquareCell("CasterCell", 0, 0);
            var caster = world.CreateUnit("Caster", playerNumber: 0, casterCell);
            var graph = SkillGraphTestGraphFactory.CreateSelfHealGraph("UnnamedLimitedSelfHeal", healAmount: 1f);
            var config = SkillGraphAbilityConfig.CreateRuntime(
                displayName: "",
                graph: graph,
                targetRange: 0,
                maxUsesPerTurn: 1);
            var ability = new SkillGraphAbilityImpl(caster, config);

            var availability = ability.GetAvailability(world.GridController);

            Assert.That(availability.CanExecute, Is.False);
            Assert.That(availability.Reason, Is.EqualTo("技能标识缺失"));
            Assert.That(ability.CanPerform(world.GridController), Is.False);
        }

        [UnityTest]
        public IEnumerator SkillAbility_ThrowingCompletedPolicy_StillCountsAndCleansExecutionState()
        {
            using var world = new SkillGraphTestWorld();
            var casterCell = world.CreateSquareCell("CasterCell", 0, 0);
            var caster = world.CreateUnit("Caster", playerNumber: 0, casterCell);
            caster.MaxHealth = 10f;
            caster.Health = 5f;
            world.SetTurnContext(world.PlayerOne, new IUnit[] { caster });

            const string abilityName = "ThrowingPolicySelfHeal";
            var graph = SkillGraphTestGraphFactory.CreateSelfHealGraph(abilityName, healAmount: 1f);
            graph.Targeting.Mode = SkillTargetMode.OrderedMultiTarget;
            var config = SkillGraphAbilityConfig.CreateRuntime(
                abilityName,
                graph,
                targetRange: 0,
                maxUsesPerTurn: 1);
            var policy = new ThrowingCompletedUsePolicy();
            var ability = new SkillGraphAbilityImpl(caster, config, policy);
            world.GridController.GridState = null;

            var task = ability.ExecuteForTestAsync(casterCell, world.GridController);
            yield return new WaitUntil(() => task.IsCompleted);

            Assert.That(task.IsFaulted, Is.True);
            Assert.That(task.Exception?.GetBaseException(), Is.TypeOf<InvalidOperationException>());
            Assert.That(task.Exception?.GetBaseException().Message, Is.EqualTo(ThrowingCompletedUsePolicy.ErrorMessage));
            Assert.That(policy.CommitCount, Is.EqualTo(1));
            Assert.That(caster.GetAbilityUseCountThisTurn(abilityName), Is.EqualTo(1));
            Assert.That(world.GridController.GridState, Is.TypeOf<GridStateAwaitInput>());
            Assert.That(ability.OrderedSelection, Is.Null);
        }

        [UnityTest]
        public IEnumerator SkillAbility_MaxUsesPerTurnOne_BlocksAfterSuccessAndResetsNextTurn()
        {
            using var world = new SkillGraphTestWorld();
            var casterCell = world.CreateSquareCell("CasterCell", 0, 0);
            var caster = world.CreateUnit("Caster", playerNumber: 0, casterCell);
            caster.MaxHealth = 10f;
            caster.Health = 5f;
            world.SetTurnContext(world.PlayerOne, new IUnit[] { caster });

            const string abilityName = "LimitedSelfHeal";
            var graph = SkillGraphTestGraphFactory.CreateSelfHealGraph(abilityName, healAmount: 2f);
            var config = SkillGraphAbilityConfig.CreateRuntime(
                abilityName,
                graph,
                targetRange: 0,
                maxUsesPerTurn: 1);
            var ability = new SkillGraphAbilityImpl(caster, config);

            Assert.That(ability.CanPerform(world.GridController), Is.True);

            var task = ability.ExecuteForTestAsync(casterCell, world.GridController);
            yield return new WaitUntil(() => task.IsCompleted);

            Assert.That(task.IsFaulted, Is.False, task.Exception?.ToString());
            Assert.That(task.Result.ExecutionState, Is.EqualTo(SkillGraphExecutionState.Completed));
            Assert.That(caster.GetAbilityUseCountThisTurn(abilityName), Is.EqualTo(1));
            Assert.That(ability.CanPerform(world.GridController), Is.False);
            Assert.That(ability.GetAvailability(world.GridController).Reason, Is.EqualTo("本回合使用次数已用完"));

            caster.PrepareForTurn();

            Assert.That(ability.CanPerform(world.GridController), Is.True);
        }

        [UnityTest]
        public IEnumerator SkillAbility_PolicyDisplayNameChanges_UsesStableConfigNameForLimit()
        {
            using var world = new SkillGraphTestWorld();
            var casterCell = world.CreateSquareCell("CasterCell", 0, 0);
            var caster = world.CreateUnit("Caster", playerNumber: 0, casterCell);
            caster.MaxHealth = 10f;
            caster.Health = 2f;
            world.SetTurnContext(world.PlayerOne, new IUnit[] { caster });

            const string abilityName = "StableLimitedSelfHeal";
            var graph = SkillGraphTestGraphFactory.CreateSelfHealGraph(abilityName, healAmount: 1f);
            var config = SkillGraphAbilityConfig.CreateRuntime(
                abilityName,
                graph,
                targetRange: 0,
                maxUsesPerTurn: 2);
            var policy = new MutableDisplayNameUsePolicy("PolicyName-0");
            var ability = new SkillGraphAbilityImpl(caster, config, policy);

            var firstUse = ability.ExecuteForTestAsync(casterCell, world.GridController);
            yield return new WaitUntil(() => firstUse.IsCompleted);
            Assert.That(firstUse.IsFaulted, Is.False, firstUse.Exception?.ToString());
            Assert.That(firstUse.Result.ExecutionState, Is.EqualTo(SkillGraphExecutionState.Completed));

            var secondUse = ability.ExecuteForTestAsync(casterCell, world.GridController);
            yield return new WaitUntil(() => secondUse.IsCompleted);
            Assert.That(secondUse.IsFaulted, Is.False, secondUse.Exception?.ToString());
            Assert.That(secondUse.Result.ExecutionState, Is.EqualTo(SkillGraphExecutionState.Completed));

            Assert.That(policy.CommitCount, Is.EqualTo(2));
            Assert.That(caster.GetAbilityUseCountThisTurn(abilityName), Is.EqualTo(2));
            Assert.That(ability.CanPerform(world.GridController), Is.False);
        }

        [UnityTest]
        public IEnumerator SkillAbility_BasicWithPolicy_CountsCompletedUseOnceAndCommitsPolicy()
        {
            using var world = new SkillGraphTestWorld();
            var casterCell = world.CreateSquareCell("CasterCell", 0, 0);
            var caster = world.CreateUnit("Caster", playerNumber: 0, casterCell);
            caster.MaxHealth = 10f;
            caster.Health = 5f;
            world.SetTurnContext(world.PlayerOne, new IUnit[] { caster });

            const string abilityName = "PolicyBasicSelfHeal";
            var graph = SkillGraphTestGraphFactory.CreateSelfHealGraph(abilityName, healAmount: 1f);
            var config = SkillGraphAbilityConfig.CreateRuntime(
                abilityName,
                graph,
                targetRange: 0,
                maxUsesPerTurn: 1,
                isBasicAbility: true);
            var policy = new MutableDisplayNameUsePolicy("PolicyBasic-0");
            var ability = new SkillGraphAbilityImpl(caster, config, policy);

            var use = ability.ExecuteForTestAsync(casterCell, world.GridController);
            yield return new WaitUntil(() => use.IsCompleted);

            Assert.That(use.IsFaulted, Is.False, use.Exception?.ToString());
            Assert.That(use.Result.ExecutionState, Is.EqualTo(SkillGraphExecutionState.Completed));
            Assert.That(policy.CommitCount, Is.EqualTo(1));
            Assert.That(caster.GetAbilityUseCountThisTurn(abilityName), Is.EqualTo(1));
            Assert.That(caster.HasUsedBasicAbilityThisTurn(abilityName), Is.True);
            Assert.That(ability.CanPerform(world.GridController), Is.False);
        }

        [UnityTest]
        public IEnumerator SkillAbility_MaxUsesPerTurnZero_DoesNotTrackSuccessfulUses()
        {
            using var world = new SkillGraphTestWorld();
            var casterCell = world.CreateSquareCell("CasterCell", 0, 0);
            var caster = world.CreateUnit("Caster", playerNumber: 0, casterCell);
            caster.MaxHealth = 10f;
            caster.Health = 5f;
            world.SetTurnContext(world.PlayerOne, new IUnit[] { caster });

            const string abilityName = "UnlimitedSelfHeal";
            var graph = SkillGraphTestGraphFactory.CreateSelfHealGraph(abilityName, healAmount: 2f);
            var config = SkillGraphAbilityConfig.CreateRuntime(
                abilityName,
                graph,
                targetRange: 0,
                maxUsesPerTurn: 0);
            var ability = new SkillGraphAbilityImpl(caster, config);

            var task = ability.ExecuteForTestAsync(casterCell, world.GridController);
            yield return new WaitUntil(() => task.IsCompleted);

            Assert.That(task.IsFaulted, Is.False, task.Exception?.ToString());
            Assert.That(task.Result.ExecutionState, Is.EqualTo(SkillGraphExecutionState.Completed));
            Assert.That(caster.GetAbilityUseCountThisTurn(abilityName), Is.Zero);
            Assert.That(ability.CanPerform(world.GridController), Is.True);
        }

        [UnityTest]
        public IEnumerator SkillAbility_FailedExecution_DoesNotConsumeLimitedUse()
        {
            using var world = new SkillGraphTestWorld();
            var casterCell = world.CreateSquareCell("CasterCell", 0, 0);
            var caster = world.CreateUnit("Caster", playerNumber: 0, casterCell);
            caster.MaxHealth = 10f;
            caster.Health = 5f;
            world.SetTurnContext(world.PlayerOne, new IUnit[] { caster });

            const string abilityName = "InvalidLimitedSelfHeal";
            var graph = SkillGraphTestGraphFactory.CreateSelfHealGraph(
                abilityName,
                healAmount: 2f,
                includeFinishNode: false);
            var config = SkillGraphAbilityConfig.CreateRuntime(
                abilityName,
                graph,
                targetRange: 0,
                maxUsesPerTurn: 1);
            var ability = new SkillGraphAbilityImpl(caster, config);

            var task = ability.ExecuteForTestAsync(casterCell, world.GridController);
            yield return new WaitUntil(() => task.IsCompleted);

            Assert.That(task.IsFaulted, Is.False, task.Exception?.ToString());
            Assert.That(task.Result.ExecutionState, Is.Not.EqualTo(SkillGraphExecutionState.Completed));
            Assert.That(caster.GetAbilityUseCountThisTurn(abilityName), Is.Zero);
            Assert.That(ability.CanPerform(world.GridController), Is.True);
        }

        private sealed class ThrowingCompletedUsePolicy : ISkillGraphUsePolicy
        {
            public const string ErrorMessage = "Completed use policy failed.";

            public string DisplayName => "Throwing Policy";
            public int CommitCount { get; private set; }

            public bool CanPerform(IGridController gridController) => true;

            public void CommitCompletedUse(SkillExecutionContext context)
            {
                CommitCount++;
                throw new InvalidOperationException(ErrorMessage);
            }
        }

        private sealed class MutableDisplayNameUsePolicy : ISkillGraphUsePolicy
        {
            public MutableDisplayNameUsePolicy(string displayName)
            {
                DisplayName = displayName;
            }

            public string DisplayName { get; private set; }
            public int CommitCount { get; private set; }

            public bool CanPerform(IGridController gridController) => true;

            public void CommitCompletedUse(SkillExecutionContext context)
            {
                CommitCount++;
                DisplayName = $"PolicyName-{CommitCount}";
            }
        }
    }
}
