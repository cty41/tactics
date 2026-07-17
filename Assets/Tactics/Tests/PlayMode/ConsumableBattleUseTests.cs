using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Skills.Graph.Testing;
using Tactics.Common.Units;
using Tactics.Common.Units.Buffs;
using Tactics.Consumables;
using Tactics.Roguelike;
using Tactics.Roster;
using UnityEditor;
using UnityEngine.TestTools;

namespace Tactics.Tests.PlayMode
{
    public class ConsumableBattleUseTests
    {
        [SetUp]
        public void SetUp()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            PureRunSessionStore.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            PureRunSessionStore.Clear();
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
        }

        [UnityTest]
        public IEnumerator LifePotion_HealsExplicitAdjacentAllyAndConsumesInstance()
        {
            var world = new SkillGraphTestWorld();
            try
            {
                var casterCell = world.CreateSquareCell("Caster", 0, 0);
                var targetCell = world.CreateSquareCell("Target", 1, 0);
                var caster = world.CreateUnit("Caster", 0, casterCell);
                var target = world.CreateUnit("Target", 0, targetCell);
                target.MaxHealth = 20f;
                target.Health = 2f;
                caster.MovementPoints = 0;
                caster.MarkBasicAbilityUsed("already-used-skill");
                world.SetTurnContext(world.PlayerOne, new IUnit[] { caster });

                var ability = CreateCarriedAbility(caster, "life_potion", "life_adjacent");
                var task = ability.ExecuteForTestAsync(targetCell, world.GridController);
                yield return WaitForTask(task);

                var saved = PlayerAdventureStateStore.LoadRepairAndSave();
                Assert.That(task.Result.ExecutionState, Is.EqualTo(SkillGraphExecutionState.Completed));
                Assert.That(target.Health, Is.EqualTo(10f));
                Assert.That(saved.ConsumableInstances, Is.Empty);
                Assert.That(saved.Roster.First().CarriedConsumableInstanceId, Is.Null);
            }
            finally
            {
                world.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator ManaPotion_RestoresExplicitAdjacentAllyAndConsumesInstance()
        {
            var world = new SkillGraphTestWorld();
            try
            {
                var casterCell = world.CreateSquareCell("Caster", 0, 0);
                var targetCell = world.CreateSquareCell("Target", 1, 0);
                var caster = world.CreateUnit("Caster", 0, casterCell);
                var target = world.CreateUnit("Target", 0, targetCell);
                target.MaxMana = 20f;
                target.Mana = 2f;
                world.SetTurnContext(world.PlayerOne, new IUnit[] { caster });

                var ability = CreateCarriedAbility(caster, "mana_potion", "mana_adjacent");
                var task = ability.ExecuteForTestAsync(targetCell, world.GridController);
                yield return WaitForTask(task);

                Assert.That(task.Result.ExecutionState, Is.EqualTo(SkillGraphExecutionState.Completed));
                Assert.That(target.Mana, Is.EqualTo(8f));
                Assert.That(PlayerAdventureStateStore.LoadRepairAndSave().ConsumableInstances, Is.Empty);
            }
            finally
            {
                world.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator FullHealthAndHealingImmuneTargets_StillCompleteAndConsume()
        {
            foreach (bool canReceiveHealing in new[] { true, false })
            {
                var task = VerifyZeroEffectHealingCase(canReceiveHealing);
                yield return WaitForTask(task);
            }
        }

        [UnityTest]
        public IEnumerator InvalidEnemyDiagonalRemoteAndDownedTargets_DoNotConsume()
        {
            var cases = new[]
            {
                (x: 1, y: 0, playerNumber: 1, downed: false, name: "enemy"),
                (x: 1, y: 1, playerNumber: 0, downed: false, name: "diagonal"),
                (x: 2, y: 0, playerNumber: 0, downed: false, name: "remote"),
                (x: 1, y: 0, playerNumber: 0, downed: true, name: "downed")
            };

            foreach (var testCase in cases)
            {
                var task = VerifyInvalidTargetCase(
                    testCase.x,
                    testCase.y,
                    testCase.playerNumber,
                    testCase.downed,
                    testCase.name);
                yield return WaitForTask(task);
            }
        }

        [UnityTest]
        public IEnumerator CleansingPotion_RemovesEveryHarmfulBuffAndKeepsBeneficialBuff()
        {
            var world = new SkillGraphTestWorld();
            try
            {
                var casterCell = world.CreateSquareCell("Caster", 0, 0);
                var targetCell = world.CreateSquareCell("Target", 1, 0);
                var caster = world.CreateUnit("Caster", 0, casterCell);
                var target = world.CreateUnit("Target", 0, targetCell);
                world.SetTurnContext(world.PlayerOne, new IUnit[] { caster });

                var counter = AssetDatabase.LoadAssetAtPath<BuffConfig>("Assets/Tactics/Battle/Buffs/Counter.asset");
                var frozen = AssetDatabase.LoadAssetAtPath<BuffConfig>("Assets/Tactics/Battle/Buffs/Frozen.asset");
                var poison = AssetDatabase.LoadAssetAtPath<BuffConfig>("Assets/Tactics/ScriptableObjects/Buffs/Poison.asset");
                Assert.That(counter, Is.Not.Null);
                Assert.That(frozen, Is.Not.Null);
                Assert.That(poison, Is.Not.Null);
                target.AddBuff(new Buff(counter, target, 3));
                target.AddBuff(new Buff(frozen, target, 3));
                target.AddBuff(new Buff(poison, target, 3));

                var ability = CreateCarriedAbility(caster, "cleansing_potion", "cleanse_all");
                var task = ability.ExecuteForTestAsync(targetCell, world.GridController);
                yield return WaitForTask(task);

                var activeNames = target.GetActiveBuffs().Select(buff => buff.BuffName).ToList();
                Assert.That(task.Result.ExecutionState, Is.EqualTo(SkillGraphExecutionState.Completed));
                Assert.That(activeNames, Does.Contain("Counter"));
                Assert.That(activeNames, Does.Not.Contain("Frozen"));
                Assert.That(activeNames, Does.Not.Contain("Poison"));
            }
            finally
            {
                world.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator SecondCarriedConsumable_IsBlockedInSameRoundAfterSuccessfulUse()
        {
            var world = new SkillGraphTestWorld();
            try
            {
                var casterCell = world.CreateSquareCell("Caster", 0, 0);
                var caster = world.CreateUnit("Caster", 0, casterCell);
                caster.MaxHealth = 20f;
                caster.Health = 5f;
                world.SetTurnContext(world.PlayerOne, new IUnit[] { caster });

                var firstAbility = CreateCarriedAbility(caster, "life_potion", "round_first");
                var firstTask = firstAbility.ExecuteForTestAsync(casterCell, world.GridController);
                yield return WaitForTask(firstTask);
                Assert.That(firstTask.Result.ExecutionState, Is.EqualTo(SkillGraphExecutionState.Completed));

                var state = PlayerAdventureStateStore.LoadRepairAndSave();
                var secondItem = ConsumableInstance.Create(ConsumableDatabase.GetById("mana_potion"), "round_second");
                state.ConsumableInstances.Add(secondItem);
                state.Roster.First().CarriedConsumableInstanceId = secondItem.InstanceId;
                PureRunSessionStore.SaveState(state);
                var secondAbility = ConsumableAbilityFactory.Create(caster, secondItem, state.Roster.First().Id);

                Assert.That(secondAbility.CanPerform(world.GridController), Is.False);
                Assert.That(PlayerAdventureStateStore.LoadRepairAndSave().ConsumableInstances.Single().InstanceId,
                    Is.EqualTo(secondItem.InstanceId));
            }
            finally
            {
                world.Dispose();
            }
        }

        private static ConsumableBattleAbility CreateCarriedAbility(Unit caster, string definitionId, string instanceId)
        {
            var state = PlayerAdventureStateStore.CreatePureRunState(20260717);
            var item = ConsumableInstance.Create(ConsumableDatabase.GetById(definitionId), instanceId);
            state.ConsumableInstances.Add(item);
            var character = state.Roster.First();
            character.CarriedConsumableInstanceId = item.InstanceId;
            PureRunSessionStore.SaveState(state);
            return ConsumableAbilityFactory.Create(caster, item, character.Id);
        }

        private static async Task VerifyZeroEffectHealingCase(bool canReceiveHealing)
        {
            PureRunSessionStore.Clear();
            var world = new SkillGraphTestWorld();
            try
            {
                var casterCell = world.CreateSquareCell("Caster", 0, 0);
                var targetCell = world.CreateSquareCell("Target", 1, 0);
                var caster = world.CreateUnit("Caster", 0, casterCell);
                var target = world.CreateUnit("Target", 0, targetCell);
                target.MaxHealth = 20f;
                target.Health = canReceiveHealing ? 20f : 2f;
                target.CanReceiveHealing = canReceiveHealing;
                float initialHealth = target.Health;
                world.SetTurnContext(world.PlayerOne, new IUnit[] { caster });

                var ability = CreateCarriedAbility(
                    caster,
                    "life_potion",
                    canReceiveHealing ? "full_health" : "healing_immune");
                var result = await ability.ExecuteForTestAsync(targetCell, world.GridController);

                Assert.That(result.ExecutionState, Is.EqualTo(SkillGraphExecutionState.Completed));
                Assert.That(target.Health, Is.EqualTo(initialHealth));
                Assert.That(PlayerAdventureStateStore.LoadRepairAndSave().ConsumableInstances, Is.Empty);
            }
            finally
            {
                world.Dispose();
                PureRunSessionStore.Clear();
            }
        }

        private static async Task VerifyInvalidTargetCase(
            int x,
            int y,
            int targetPlayerNumber,
            bool isDowned,
            string caseName)
        {
            PureRunSessionStore.Clear();
            var world = new SkillGraphTestWorld();
            try
            {
                var casterCell = world.CreateSquareCell($"Caster_{caseName}", 0, 0);
                var targetCell = world.CreateSquareCell($"Target_{caseName}", x, y);
                var caster = world.CreateUnit($"Caster_{caseName}", 0, casterCell);
                var target = world.CreateUnit($"Target_{caseName}", targetPlayerNumber, targetCell);
                target.MaxHealth = 20f;
                target.Health = isDowned ? 0f : 2f;
                target.IsDowned = isDowned;
                world.SetTurnContext(world.PlayerOne, new IUnit[] { caster });

                string instanceId = $"invalid_{caseName}";
                var ability = CreateCarriedAbility(caster, "life_potion", instanceId);
                var result = await ability.ExecuteForTestAsync(targetCell, world.GridController);
                var state = PlayerAdventureStateStore.LoadRepairAndSave();

                Assert.That(result.ExecutionState, Is.EqualTo(SkillGraphExecutionState.Failed), caseName);
                Assert.That(state.ConsumableInstances.Single().InstanceId, Is.EqualTo(instanceId), caseName);
                Assert.That(state.Roster.First().CarriedConsumableInstanceId, Is.EqualTo(instanceId), caseName);
            }
            finally
            {
                world.Dispose();
                PureRunSessionStore.Clear();
            }
        }

        private static IEnumerator WaitForTask(Task task)
        {
            while (!task.IsCompleted)
                yield return null;
            if (task.IsFaulted)
                Assert.Fail(task.Exception?.ToString());
        }
    }
}
