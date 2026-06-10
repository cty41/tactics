using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Skills.Graph.Testing;
using Tactics.Common.Units;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tactics.Tests.PlayMode
{
    public class SkillGraphRuntimeTests
    {
        [UnityTest]
        public IEnumerator RuntimeRunner_AbortsInvalidGraph_BeforeExecution()
        {
            var world = new SkillGraphTestWorld();

            try
            {
                var graph = SkillGraphTestGraphFactory.CreateSelfHealGraph(
                    displayName: "InvalidSelfHeal",
                    healAmount: 5f,
                    includeFinishNode: false);

                var caster = world.CreateUnit("Caster", playerNumber: 0);
                world.SetTurnContext(world.PlayerOne, new IUnit[] { caster });

                var runner = new SkillGraphRuntimeTestRunner();
                var task = runner.ExecuteAsync(new SkillGraphRuntimeTestRequest
                {
                    Name = "InvalidSelfHeal",
                    Graph = graph,
                    GridController = world.GridController,
                    Caster = caster
                });

                yield return WaitForTask(task);

                var result = task.Result;

                Assert.AreEqual(SkillGraphExecutionState.Aborted, result.ExecutionState);
                Assert.That(result.ValidationErrors.Any(d => d.Code == SkillGraphValidation.NoTerminalNode), Is.True);
                Assert.That(result.LastError, Does.Contain("validation"));
            }
            finally
            {
                world.Dispose();
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_HealsCaster_WithSelfTargetGraph()
        {
            var world = new SkillGraphTestWorld();

            try
            {
                var graph = SkillGraphTestGraphFactory.CreateSelfHealGraph(
                    displayName: "SelfHeal",
                    healAmount: 5f);

                var caster = world.CreateUnit("Caster", playerNumber: 0);
                caster.MaxHealth = 10f;
                caster.Health = 6f;

                world.SetTurnContext(world.PlayerOne, new IUnit[] { caster });

                var runner = new SkillGraphRuntimeTestRunner();
                var task = runner.ExecuteAsync(new SkillGraphRuntimeTestRequest
                {
                    Name = "SelfHeal",
                    Graph = graph,
                    GridController = world.GridController,
                    Caster = caster
                });

                yield return WaitForTask(task);

                var result = task.Result;

                Assert.AreEqual(SkillGraphExecutionState.Completed, result.ExecutionState);
                Assert.That(result.ValidationErrors, Is.Empty);
                Assert.AreEqual(10f, caster.Health);
                Assert.NotNull(result.PrimaryTarget);
                Assert.AreEqual(10f, result.PrimaryTarget.Health);
            }
            finally
            {
                world.Dispose();
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_DealsDamageToNearestEnemy_WithSingleTargetGraph()
        {
            var world = new SkillGraphTestWorld();

            try
            {
                var graph = SkillGraphTestGraphFactory.CreateSingleTargetDamageGraph(
                    displayName: "SingleTargetDamage",
                    baseDamage: 7f);

                var casterCell = world.CreateSquareCell("CasterCell", 0, 0);
                var targetCell = world.CreateSquareCell("TargetCell", 1, 0);

                var caster = world.CreateUnit("Caster", playerNumber: 0, casterCell);
                var target = world.CreateUnit("Target", playerNumber: 1, targetCell);

                target.MaxHealth = 10f;
                target.Health = 10f;
                target.DefenceFactor = 0;

                world.SetTurnContext(world.PlayerOne, new IUnit[] { caster });

                var runner = new SkillGraphRuntimeTestRunner();
                var task = runner.ExecuteAsync(new SkillGraphRuntimeTestRequest
                {
                    Name = "SingleTargetDamage",
                    Graph = graph,
                    GridController = world.GridController,
                    Caster = caster
                });

                yield return WaitForTask(task);

                var result = task.Result;

                Assert.AreEqual(SkillGraphExecutionState.Completed, result.ExecutionState);
                Assert.That(result.ValidationErrors, Is.Empty);
                Assert.AreEqual(3f, target.Health);
                Assert.NotNull(result.PrimaryTarget);
                Assert.AreEqual(1, result.PrimaryTarget.PlayerNumber);
                Assert.AreEqual(3f, result.PrimaryTarget.Health);
            }
            finally
            {
                world.Dispose();
            }

            yield return null;
        }

        private static IEnumerator WaitForTask<T>(Task<T> task)
        {
            yield return new WaitUntil(() => task.IsCompleted);

            if (task.IsFaulted)
            {
                throw task.Exception ?? new System.Exception("Task faulted.");
            }
        }
    }
}
