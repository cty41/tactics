using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Skills.Graph.Testing;
using Tactics.Common.Units;
using Tactics.Common.Units.Abilities;
using Tactics.Editor.SkillGraphEditor;
using Tactics.Consumables;
using Tactics.Roguelike;
using Tactics.Roster;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tactics.Tests.PlayMode
{
    public class SkillGraphRuntimeTests
    {
        [SetUp]
        public void SetUp()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            GameTimeService.ForceResume();
            GameTimeService.SetPlaybackSpeed(GamePlaybackSpeed.Normal);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            GameTimeService.ForceResume();
            GameTimeService.SetPlaybackSpeed(GamePlaybackSpeed.Normal);
        }

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
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
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
        public IEnumerator RuntimeRunner_RestoresCasterMana_WithSelfTargetGraph()
        {
            var world = new SkillGraphTestWorld();
            try
            {
                var graph = SkillGraphTestGraphFactory.CreateSelfManaGraph("SelfMana", 6f);
                var caster = world.CreateUnit("Caster", playerNumber: 0);
                caster.MaxMana = 10f;
                caster.Mana = 2f;
                world.SetTurnContext(world.PlayerOne, new IUnit[] { caster });

                var runner = new SkillGraphRuntimeTestRunner();
                var task = runner.ExecuteAsync(new SkillGraphRuntimeTestRequest
                {
                    Name = "SelfMana",
                    Graph = graph,
                    GridController = world.GridController,
                    Caster = caster
                });

                yield return WaitForTask(task);

                Assert.AreEqual(SkillGraphExecutionState.Completed, task.Result.ExecutionState);
                Assert.That(task.Result.ValidationErrors, Is.Empty);
                Assert.AreEqual(8f, caster.Mana);
            }
            finally
            {
                world.Dispose();
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator ConsumableAbility_CommitsOneChargeAndBlocksSecondUseInSameTurn()
        {
            var world = new SkillGraphTestWorld();
            try
            {
                PureRunSessionStore.Clear();
                var definition = ConsumableDatabase.GetById("life_potion");
                var item = ConsumableInstance.Create(definition);
                var state = PlayerAdventureStateStore.CreatePureRunState(17);
                state.ConsumableInstances.Add(item);
                var character = state.Roster.First();
                character.CarriedConsumableInstanceId = item.InstanceId;
                PureRunSessionStore.SaveState(state);

                var casterCell = world.CreateSquareCell("CasterCell", 0, 0);
                var caster = world.CreateUnit("Caster", playerNumber: 0, casterCell);
                caster.MaxHealth = 10f;
                caster.Health = 3f;
                world.SetTurnContext(world.PlayerOne, new IUnit[] { caster });

                var ability = ConsumableAbilityFactory.Create(caster, item, character.Id);
                ability.Initialize(world.GridController);
                var task = ability.ExecuteForTestAsync(casterCell, world.GridController);
                yield return WaitForTask(task);

                var saved = PlayerAdventureStateStore.LoadRepairAndSave();
                Assert.AreEqual(SkillGraphExecutionState.Completed, task.Result.ExecutionState);
                Assert.AreEqual(10f, caster.Health);
                Assert.That(saved.ConsumableInstances, Is.Empty);
                Assert.That(saved.Roster.First().CarriedConsumableInstanceId, Is.Null);
                Assert.That(ability.CanPerform(world.GridController), Is.False);
            }
            finally
            {
                PureRunSessionStore.Clear();
                world.Dispose();
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_DealsDamageToNearestEnemy_WithSingleTargetGraph()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
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

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesChargeGraph()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var world = new SkillGraphTestWorld();
            try
            {
                var graph = SkillGraphTestGraphFactory.CreateChargeGraph("Charge", distance: 2, maxRange: 3, collisionDamage: 1f);

                var casterCell = world.CreateSquareCell("CasterCell", 0, 0);
                var midCell = world.CreateSquareCell("MidCell", 1, 0);
                var targetCell = world.CreateSquareCell("TargetCell", 2, 0);

                var caster = world.CreateUnit("Caster", playerNumber: 0, casterCell);
                var target = world.CreateUnit("Target", playerNumber: 1, targetCell);
                target.MaxHealth = 10f;
                target.Health = 10f;

                world.SetTurnContext(world.PlayerOne, new IUnit[] { caster });
                world.SetTurnContext(world.PlayerTwo, new IUnit[] { target });

                var runner = new SkillGraphRuntimeTestRunner();
                var task = runner.ExecuteAsync(new SkillGraphRuntimeTestRequest
                {
                    Name = "Charge",
                    Graph = graph,
                    GridController = world.GridController,
                    Caster = caster,
                    PrimaryTarget = target
                });

                yield return WaitForTask(task);

                var result = task.Result;
                Assert.That(result.ValidationErrors, Is.Empty, "Graph validation should pass");
                Assert.AreEqual(SkillGraphExecutionState.Completed, result.ExecutionState,
                    $"Charge failed: {result.LastError}");
            }
            finally
            {
                world.Dispose();
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesApplyBuffGraph()
        {
            var world = new SkillGraphTestWorld();
            try
            {
                var graph = SkillGraphTestGraphFactory.CreateApplyBuffGraph("Buff", buffName: "TestBuff", duration: 3);

                var casterCell = world.CreateSquareCell("CasterCell", 0, 0);
                var targetCell = world.CreateSquareCell("TargetCell", 1, 0);

                var caster = world.CreateUnit("Caster", playerNumber: 0, casterCell);
                var target = world.CreateUnit("Target", playerNumber: 1, targetCell);
                target.Facing = FacingDirection.North;
                world.SetTurnContext(world.PlayerOne, new IUnit[] { caster });
                world.SetTurnContext(world.PlayerTwo, new IUnit[] { target });

                var runner = new SkillGraphRuntimeTestRunner();
                var task = runner.ExecuteAsync(new SkillGraphRuntimeTestRequest
                {
                    Name = "Buff",
                    Graph = graph,
                    GridController = world.GridController,
                    Caster = caster,
                    PrimaryTarget = target
                });

                yield return WaitForTask(task);

                var result = task.Result;
                Assert.AreEqual(SkillGraphExecutionState.Completed, result.ExecutionState);
                Assert.That(result.ValidationErrors, Is.Empty);
                Assert.That(target.Facing, Is.EqualTo(FacingDirection.North));
            }
            finally
            {
                world.Dispose();
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesKnockbackGraph()
        {
            var world = new SkillGraphTestWorld();
            try
            {
                var graph = SkillGraphTestGraphFactory.CreateKnockbackGraph("Knockback", distance: 2, maxRange: 1);

                var casterCell = world.CreateSquareCell("CasterCell", 0, 0);
                var targetCell = world.CreateSquareCell("TargetCell", 1, 0);
                var destinationCell = world.CreateSquareCell("DestinationCell", 3, 0);

                var caster = world.CreateUnit("Caster", playerNumber: 0, casterCell);
                var target = world.CreateUnit("Target", playerNumber: 1, targetCell);
                world.SetTurnContext(world.PlayerOne, new IUnit[] { caster });
                world.SetTurnContext(world.PlayerTwo, new IUnit[] { target });

                var runner = new SkillGraphRuntimeTestRunner();
                var task = runner.ExecuteAsync(new SkillGraphRuntimeTestRequest
                {
                    Name = "Knockback",
                    Graph = graph,
                    GridController = world.GridController,
                    Caster = caster,
                    PrimaryTarget = target
                });

                yield return WaitForTask(task);

                var result = task.Result;
                Assert.AreEqual(SkillGraphExecutionState.Completed, result.ExecutionState);
                Assert.That(result.ValidationErrors, Is.Empty);
                Assert.That(targetCell.CurrentUnits, Is.Empty);
                Assert.That(targetCell.IsTaken, Is.False);
                Assert.That(destinationCell.CurrentUnits, Is.EqualTo(new IUnit[] { target }));
                Assert.That(destinationCell.IsTaken, Is.True);
                Assert.That(target.CurrentCell, Is.SameAs(destinationCell));
            }
            finally
            {
                world.Dispose();
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_KnockbackBeyondMaximumEdgeLeavesOccupancyUnchanged()
        {
            var world = new SkillGraphTestWorld();
            try
            {
                var graph = SkillGraphTestGraphFactory.CreateKnockbackGraph("KnockbackEdge", distance: 2, maxRange: 1);
                var casterCell = world.CreateSquareCell("CasterCell", 7, 9);
                var targetCell = world.CreateSquareCell("TargetCell", 8, 9);
                var edgeCell = world.CreateSquareCell("EdgeCell", 9, 9);
                var caster = world.CreateUnit("Caster", playerNumber: 0, casterCell);
                var target = world.CreateUnit("Target", playerNumber: 1, targetCell);
                world.SetTurnContext(world.PlayerOne, new IUnit[] { caster });
                world.SetTurnContext(world.PlayerTwo, new IUnit[] { target });

                var runner = new SkillGraphRuntimeTestRunner();
                var task = runner.ExecuteAsync(new SkillGraphRuntimeTestRequest
                {
                    Name = "KnockbackEdge",
                    Graph = graph,
                    GridController = world.GridController,
                    Caster = caster,
                    PrimaryTarget = target
                });
                yield return WaitForTask(task);

                Assert.That(task.Result.ExecutionState, Is.EqualTo(SkillGraphExecutionState.Completed));
                Assert.That(targetCell.CurrentUnits, Is.EqualTo(new IUnit[] { target }));
                Assert.That(targetCell.IsTaken, Is.True);
                Assert.That(edgeCell.CurrentUnits, Is.Empty,
                    "Out-of-bounds knockback must not clamp the target onto the legal edge cell.");
                Assert.That(edgeCell.IsTaken, Is.False);
                Assert.That(target.CurrentCell, Is.SameAs(targetCell));
                Assert.That(target.CurrentCell.GridCoordinates.x, Is.EqualTo(8));
                Assert.That(target.CurrentCell.GridCoordinates.y, Is.EqualTo(9));
            }
            finally
            {
                world.Dispose();
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesAllyHealGraph()
        {
            var world = new SkillGraphTestWorld();
            try
            {
                var graph = SkillGraphTestGraphFactory.CreateAllyHealGraph("AllyHeal", healAmount: 5f, maxRange: 2);

                var casterCell = world.CreateSquareCell("CasterCell", 0, 0);
                var allyCell = world.CreateSquareCell("AllyCell", 1, 0);

                var caster = world.CreateUnit("Caster", playerNumber: 0, casterCell);
                var ally = world.CreateUnit("Ally", playerNumber: 0, allyCell);
                ally.MaxHealth = 10f;
                ally.Health = 3f;
                world.SetTurnContext(world.PlayerOne, new IUnit[] { caster, ally });

                var runner = new SkillGraphRuntimeTestRunner();
                var task = runner.ExecuteAsync(new SkillGraphRuntimeTestRequest
                {
                    Name = "AllyHeal",
                    Graph = graph,
                    GridController = world.GridController,
                    Caster = caster,
                    PrimaryTarget = ally
                });

                yield return WaitForTask(task);

                var result = task.Result;
                Assert.AreEqual(SkillGraphExecutionState.Completed, result.ExecutionState);
                Assert.That(result.ValidationErrors, Is.Empty);
                Assert.AreEqual(8f, ally.Health);
            }
            finally
            {
                world.Dispose();
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ExecutesProjectileGraph()
        {
            var world = new SkillGraphTestWorld();
            try
            {
                var graph = SkillGraphTestGraphFactory.CreateProjectileGraph("Projectile", baseDamage: 7f, travelTime: 0.05f);

                var casterCell = world.CreateSquareCell("CasterCell", 0, 0);
                var targetCell = world.CreateSquareCell("TargetCell", 1, 0);

                var caster = world.CreateUnit("Caster", playerNumber: 0, casterCell);
                var target = world.CreateUnit("Target", playerNumber: 1, targetCell);
                target.MaxHealth = 10f;
                target.Health = 10f;
                world.SetTurnContext(world.PlayerOne, new IUnit[] { caster });
                world.SetTurnContext(world.PlayerTwo, new IUnit[] { target });

                var runner = new SkillGraphRuntimeTestRunner();
                var task = runner.ExecuteAsync(new SkillGraphRuntimeTestRequest
                {
                    Name = "Projectile",
                    Graph = graph,
                    GridController = world.GridController,
                    Caster = caster,
                    PrimaryTarget = target
                });

                yield return WaitForTask(task);

                var result = task.Result;
                Assert.AreEqual(SkillGraphExecutionState.Completed, result.ExecutionState);
                Assert.That(result.ValidationErrors, Is.Empty);
                Assert.That(result.ExecutionEvents, Has.Some.Matches<SkillGraphExecutionEvent>(e => e.EventType == "ProjectileLaunched"));
                Assert.That(result.ExecutionEvents, Has.Some.Matches<SkillGraphExecutionEvent>(e => e.EventType == "ProjectileHit"));
            }
            finally
            {
                world.Dispose();
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator RuntimeRunner_ProjectileTravelStopsWhileGameIsPaused()
        {
            var world = new SkillGraphTestWorld();
            try
            {
                var graph = SkillGraphTestGraphFactory.CreateProjectileGraph(
                    "PausedProjectile",
                    baseDamage: 7f,
                    travelTime: 0.05f);
                var casterCell = world.CreateSquareCell("CasterCell", 0, 0);
                var targetCell = world.CreateSquareCell("TargetCell", 1, 0);
                var caster = world.CreateUnit("Caster", playerNumber: 0, casterCell);
                var target = world.CreateUnit("Target", playerNumber: 1, targetCell);
                world.SetTurnContext(world.PlayerOne, new IUnit[] { caster });
                world.SetTurnContext(world.PlayerTwo, new IUnit[] { target });

                var task = new SkillGraphRuntimeTestRunner().ExecuteAsync(new SkillGraphRuntimeTestRequest
                {
                    Name = "PausedProjectile",
                    Graph = graph,
                    GridController = world.GridController,
                    Caster = caster,
                    PrimaryTarget = target
                });

                GameTimeService.Pause();
                yield return new WaitForSecondsRealtime(0.08f);
                Assert.That(task.IsCompleted, Is.False, "Projectile travel must stop while gameplay is paused.");

                GameTimeService.Resume();
                yield return WaitForTask(task);
                Assert.That(task.Result.ExecutionState, Is.EqualTo(SkillGraphExecutionState.Completed));
            }
            finally
            {
                world.Dispose();
            }
        }

        // ── 真资产集成验证 ──

        [UnityTest]
        public IEnumerator AssetIntegration_LoadsFromDisk_AndValidates()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var graph = AssetDatabase.LoadAssetAtPath<SkillGraphAsset>(
                "Assets/Tactics/Battle/Abilities/SkillGraphs/MeleeAttack_Graph.asset");

            Assert.IsNotNull(graph, "MeleeAttack_Graph.asset not found");
            Assert.IsTrue(graph.Nodes.Count > 0, "Graph has no nodes");

            var valid = SkillGraphValidation.Validate(graph, out var errors, out _);
            Assert.IsTrue(valid, $"Validation failed: {string.Join(", ", errors)}");

            yield return null;
        }

        [UnityTest]
        public IEnumerator AssetIntegration_BridgeSync_CreatesAndValidates()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var graph = AssetDatabase.LoadAssetAtPath<SkillGraphAsset>(
                "Assets/Tactics/Battle/Abilities/SkillGraphs/MeleeAttack_Graph.asset");
            Assert.IsNotNull(graph);

            var bridge = SkillGraphAbilityConfigGenerator.FindAbilityConfigForGraph(graph);
            Assert.IsNotNull(bridge, "AbilityConfig bridge not found for MeleeAttack_Graph");
            Assert.AreEqual(graph, bridge.SkillGraph, "Bridge SkillGraph reference mismatch");

            var status = SkillGraphMcpFacade.GetBridgeSyncStatus(
                "Assets/Tactics/Battle/Abilities/SkillGraphs/MeleeAttack_Graph.asset");
            Assert.IsTrue(status.GraphExists, "Graph should exist");
            Assert.IsTrue(status.BridgeExists, "Bridge should exist");
            Assert.IsTrue(status.IsGraphReferenceMatch, "Graph reference should match");

            yield return null;
        }

        [UnityTest]
        public IEnumerator AssetIntegration_MultipleGraphs_AllValid()
        {
            string[] graphPaths = new[]
            {
                "Assets/Tactics/Battle/Abilities/SkillGraphs/MeleeAttack_Graph.asset",
                "Assets/Tactics/Battle/Abilities/SkillGraphs/RangedAttack_Graph.asset",
                "Assets/Tactics/Battle/Abilities/SkillGraphs/MagicAttack_Graph.asset",
                "Assets/Tactics/Battle/Abilities/SkillGraphs/HeavyShot_Graph.asset",
                "Assets/Tactics/Battle/Abilities/SkillGraphs/Fireball_Graph.asset",
            };

            foreach (var path in graphPaths)
            {
                var graph = AssetDatabase.LoadAssetAtPath<SkillGraphAsset>(path);
                Assert.IsNotNull(graph, $"Graph not found: {path}");

                var valid = SkillGraphValidation.Validate(graph, out var errors, out _);
                Assert.IsTrue(valid, $"Validation failed for {path}: {string.Join(", ", errors)}");
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator AssetIntegration_ProjectileGraph_RunsEndToEnd()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var graph = AssetDatabase.LoadAssetAtPath<SkillGraphAsset>(
                "Assets/Tactics/Battle/Abilities/SkillGraphs/RangedAttack_Graph.asset");
            Assert.IsNotNull(graph, "RangedAttack_Graph.asset not found");

            var world = new SkillGraphTestWorld();
            try
            {
                var casterCell = world.CreateSquareCell("CasterCell", 0, 0);
                var targetCell = world.CreateSquareCell("TargetCell", 1, 0);
                var caster = world.CreateUnit("Caster", 0, casterCell);
                var target = world.CreateUnit("Target", 1, targetCell);
                target.Health = 10f;
                target.MaxHealth = 10f;
                world.SetTurnContext(world.PlayerOne, new IUnit[] { caster });
                world.SetTurnContext(world.PlayerTwo, new IUnit[] { target });

                var runner = new SkillGraphRuntimeTestRunner();
                var task = runner.ExecuteAsync(new SkillGraphRuntimeTestRequest
                {
                    Name = "RangedAttack",
                    Graph = graph,
                    GridController = world.GridController,
                    Caster = caster,
                    PrimaryTarget = target
                });

                yield return WaitForTask(task);

                var result = task.Result;
                Assert.That(result.ValidationErrors, Is.Empty, $"Validation failed: {string.Join(", ", result.ValidationErrors)}");
                Assert.AreEqual(SkillGraphExecutionState.Completed, result.ExecutionState,
                    $"Execution failed: {result.LastError}");
                Assert.That(result.ExecutionEvents, Has.Some.Matches<SkillGraphExecutionEvent>(e => e.EventType == "ProjectileLaunched"));
            }
            finally
            {
                world.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator AssetIntegration_ApplySpec_CreatesNewGraph()
        {
            var spec = new SkillGraphSpec
            {
                DisplayName = "TestApplySpec",
                Nodes = new System.Collections.Generic.List<SkillNodeSpec>
                {
                    new() { Id = "start", Type = "Start" },
                    new() { Id = "self", Type = "SelectSelf" },
                    new() { Id = "heal", Type = "ApplyHeal", Parameters = new System.Collections.Generic.Dictionary<string, object> { ["healAmount"] = 5f } },
                    new() { Id = "finish", Type = "Finish" }
                },
                Edges = new System.Collections.Generic.List<SkillEdgeSpec>
                {
                    new() { Source = "start", Target = "self" },
                    new() { Source = "self", Target = "heal" },
                    new() { Source = "heal", Target = "finish" }
                }
            };

            string testPath = "Assets/Tactics/Battle/Abilities/SkillGraphs/_TestApplySpec.asset";
            var result = SkillGraphMcpFacade.ApplySpec(testPath, spec);

            Assert.IsTrue(result.Success, $"ApplySpec failed: {string.Join(", ", result.CompileErrors.Concat(result.ValidationErrors))}");
            Assert.IsNotNull(result.Asset);
            Assert.IsTrue(result.IsValid);

            var world = new SkillGraphTestWorld();
            try
            {
                var caster = world.CreateUnit("Caster", 0);
                caster.Health = 5f;
                caster.MaxHealth = 10f;
                world.SetTurnContext(world.PlayerOne, new IUnit[] { caster });

                var runner = new SkillGraphRuntimeTestRunner();
                var task = runner.ExecuteAsync(new SkillGraphRuntimeTestRequest
                {
                    Name = "TestApplySpec",
                    Graph = result.Asset,
                    GridController = world.GridController,
                    Caster = caster
                });

                yield return WaitForTask(task);

                Assert.AreEqual(SkillGraphExecutionState.Completed, task.Result.ExecutionState);
                Assert.AreEqual(10f, caster.Health);
            }
            finally
            {
                world.Dispose();
                AssetDatabase.DeleteAsset(testPath);
            }
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
