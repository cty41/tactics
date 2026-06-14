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
    public class SkillGraphRunnerProtectionTests
    {
        [SetUp]
        public void SetUp()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
        }

        [UnityTest]
        public IEnumerator Runner_AbortsWhenNoEntryNode()
        {
            var world = new SkillGraphTestWorld();
            try
            {
                var graph = ScriptableObject.CreateInstance<SkillGraphAsset>();
                graph.DisplayName = "NoEntry";
                graph.Nodes.Add(new FinishNodeRecord { NodeId = "finish" });

                var caster = world.CreateUnit("Caster", playerNumber: 0);
                world.SetTurnContext(world.PlayerOne, new IUnit[] { caster });

                var runner = new SkillGraphRuntimeTestRunner();
                var task = runner.ExecuteAsync(new SkillGraphRuntimeTestRequest
                {
                    Name = "NoEntry",
                    Graph = graph,
                    GridController = world.GridController,
                    Caster = caster
                });

                yield return WaitForTask(task);

                var result = task.Result;
                Assert.AreEqual(SkillGraphExecutionState.Aborted, result.ExecutionState);
                Assert.That(result.ValidationErrors.Count, Is.GreaterThan(0));
            }
            finally
            {
                world.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator Runner_AbortsOnMaxStepsExceeded()
        {
            var world = new SkillGraphTestWorld();
            try
            {
                var graph = ScriptableObject.CreateInstance<SkillGraphAsset>();
                graph.DisplayName = "InfiniteLoop";

                var start = new StartNodeRecord { NodeId = "start" };
                var target = new SelectPrimaryTargetNodeRecord { NodeId = "target" };
                target.MinRange = 1;
                target.MaxRange = 3;
                var damage = new ApplyDamageNodeRecord { NodeId = "damage" };
                damage.BaseDamage = 1f;
                var heal = new ApplyHealNodeRecord { NodeId = "heal" };
                heal.HealAmount = 1f;
                var finish = new FinishNodeRecord { NodeId = "finish" };

                graph.Nodes.Add(start);
                graph.Nodes.Add(target);
                graph.Nodes.Add(damage);
                graph.Nodes.Add(heal);
                graph.Nodes.Add(finish);

                graph.AddEdge("start", "target");
                graph.AddEdge("target", "damage");
                graph.AddEdge("damage", "heal");
                graph.AddEdge("heal", "target");

                var caster = world.CreateUnit("Caster", playerNumber: 0);
                var enemy = world.CreateUnit("Enemy", playerNumber: 1);
                var casterCell = world.CreateSquareCell("CasterCell", 0, 0);
                var enemyCell = world.CreateSquareCell("EnemyCell", 1, 0);
                world.PlaceUnit(caster, casterCell);
                world.PlaceUnit(enemy, enemyCell);
                world.SetTurnContext(world.PlayerOne, new IUnit[] { caster });
                world.SetTurnContext(world.PlayerTwo, new IUnit[] { enemy });

                var runner = new SkillGraphRuntimeTestRunner();
                var task = runner.ExecuteAsync(new SkillGraphRuntimeTestRequest
                {
                    Name = "InfiniteLoop",
                    Graph = graph,
                    GridController = world.GridController,
                    Caster = caster,
                    PrimaryTarget = enemy,
                    MaxSteps = 5
                });

                yield return WaitForTask(task);

                var result = task.Result;
                Assert.AreEqual(SkillGraphExecutionState.Aborted, result.ExecutionState);
                Assert.That(result.LastError, Does.Contain("max step"));
            }
            finally
            {
                world.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator Runner_FailsWhenDamageNodeHasNoTarget()
        {
            var world = new SkillGraphTestWorld();
            try
            {
                var graph = SkillGraphTestGraphFactory.CreateSingleTargetDamageGraph(
                    "NoTargetDamage", baseDamage: 5f);

                var caster = world.CreateUnit("Caster", playerNumber: 0);
                world.SetTurnContext(world.PlayerOne, new IUnit[] { caster });

                var runner = new SkillGraphRuntimeTestRunner();
                var task = runner.ExecuteAsync(new SkillGraphRuntimeTestRequest
                {
                    Name = "NoTargetDamage",
                    Graph = graph,
                    GridController = world.GridController,
                    Caster = caster,
                    PrimaryTarget = null
                });

                yield return WaitForTask(task);

                var result = task.Result;
                Assert.AreEqual(SkillGraphExecutionState.Failed, result.ExecutionState);
            }
            finally
            {
                world.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator Runner_RecordsExecutionEvents_ForProjectileGraph()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var world = new SkillGraphTestWorld();
            try
            {
                var graph = SkillGraphTestGraphFactory.CreateProjectileGraph("ProjectileTest", 7f);
                var casterCell = world.CreateSquareCell("CasterCell", 0, 0);
                var targetCell = world.CreateSquareCell("TargetCell", 1, 0);
                var caster = world.CreateUnit("Caster", playerNumber: 0, casterCell);
                var target = world.CreateUnit("Target", playerNumber: 1, targetCell);
                world.SetTurnContext(world.PlayerOne, new IUnit[] { caster });
                world.SetTurnContext(world.PlayerTwo, new IUnit[] { target });

                var runner = new SkillGraphRuntimeTestRunner();
                var task = runner.ExecuteAsync(new SkillGraphRuntimeTestRequest
                {
                    Name = "ProjectileTest",
                    Graph = graph,
                    GridController = world.GridController,
                    Caster = caster,
                    PrimaryTarget = target
                });

                yield return WaitForTask(task);

                var result = task.Result;
                Assert.AreEqual(SkillGraphExecutionState.Completed, result.ExecutionState);
                Assert.That(result.ExecutionEvents, Has.Some.Matches<SkillGraphExecutionEvent>(e => e.EventType == "ProjectileLaunched"));
                Assert.That(result.ExecutionEvents, Has.Some.Matches<SkillGraphExecutionEvent>(e => e.EventType == "ProjectileHit"));
            }
            finally
            {
                world.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator Runner_RecordsStageResults()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var world = new SkillGraphTestWorld();
            try
            {
                var graph = SkillGraphTestGraphFactory.CreateSelfHealGraph("StageTest", 5f);
                var caster = world.CreateUnit("Caster", playerNumber: 0);
                caster.Health = 6f;
                world.SetTurnContext(world.PlayerOne, new IUnit[] { caster });

                var runner = new SkillGraphRuntimeTestRunner();
                var task = runner.ExecuteAsync(new SkillGraphRuntimeTestRequest
                {
                    Name = "StageTest",
                    Graph = graph,
                    GridController = world.GridController,
                    Caster = caster
                });

                yield return WaitForTask(task);

                var result = task.Result;
                Assert.AreEqual(SkillGraphExecutionState.Completed, result.ExecutionState);
                Assert.That(result.StageResults.Count, Is.GreaterThan(0));
                Assert.That(result.StageResults.Last().State, Is.EqualTo(SkillGraphExecutionState.Completed));
            }
            finally
            {
                world.Dispose();
            }
        }

        private static IEnumerator WaitForTask<T>(Task<T> task)
        {
            yield return new WaitUntil(() => task.IsCompleted);
            if (task.IsFaulted) throw task.Exception ?? new System.Exception("Task faulted.");
        }
    }
}
