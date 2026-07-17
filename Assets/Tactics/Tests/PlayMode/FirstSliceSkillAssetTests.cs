using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using Tactics.AssetPipeline;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Skills.Graph.Testing;
using Tactics.Common.Testing.Gameplay;
using Tactics.Common.Units;
using Tactics.Common.Units.Abilities;
using Tactics.Common.Units.Buffs;
using Tactics.Common.Units.Classes;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tactics.Tests.PlayMode
{
    /// <summary>
    /// Verifies that first-slice role and projectile assets load through GameAssetManager
    /// and execute with the production SkillGraph runner.
    /// </summary>
    public class FirstSliceSkillAssetTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            var initializeTask = TestGameAssetHelper.EnsureInitialized();
            yield return new WaitUntil(() => initializeTask.IsCompleted);
            Assert.That(initializeTask.Result, Is.Not.Null);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            TestGameAssetHelper.Cleanup();
            yield return null;
        }

        [Test]
        public void RoleConfigs_LoadFirstSliceAbilityEntries()
        {
            var mage = GameAssetManager.Instance.Load<RoleConfig>("Assets/Tactics/Battle/Classes/Mage.asset");
            var necromancer = GameAssetManager.Instance.Load<RoleConfig>("Assets/Tactics/Battle/Classes/Necromancer.asset");
            var amazon = GameAssetManager.Instance.Load<RoleConfig>("Assets/Tactics/Battle/Classes/Amazon.asset");

            Assert.That(mage, Is.Not.Null);
            Assert.That(mage.Abilities.Count, Is.GreaterThanOrEqualTo(5));
            Assert.That(necromancer, Is.Not.Null);
            Assert.That(necromancer.RoleType, Is.EqualTo(RoleType.Necromancer));
            Assert.That(necromancer.Abilities.Count, Is.GreaterThanOrEqualTo(5));
            Assert.That(amazon, Is.Not.Null);
            Assert.That(amazon.RoleType, Is.EqualTo(RoleType.Amazon));
            Assert.That(amazon.Abilities.Count, Is.GreaterThanOrEqualTo(4));
        }

        [Test]
        public void NecromancerSummonGraphs_DeclareHealingPolicy()
        {
            AssertSummonHealingPolicy(
                "Assets/Tactics/Battle/Abilities/SkillGraphs/SummonSkeleton_Graph.asset",
                false);
            AssertSummonHealingPolicy(
                "Assets/Tactics/Battle/Abilities/SkillGraphs/SkeletonMage_Graph.asset",
                false);
            AssertSummonHealingPolicy(
                "Assets/Tactics/Battle/Abilities/SkillGraphs/SummonFireDemon_Graph.asset",
                true);
        }

        [UnityTest]
        public IEnumerator ProjectileGraphs_LoadAndExecuteAgainstRealAssets()
        {
            yield return ExecuteProjectile("Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/IceBolt_Graph_Ability.asset", 5);
            yield return ExecuteProjectile("Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/BoneSpear_Graph_Ability.asset", 5);
            yield return ExecuteProjectile("Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/PoisonSpear_Graph_Ability.asset", 6);
            yield return ExecuteProjectile("Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/Lightning_Graph_Ability.asset", 5);
        }

        [UnityTest]
        public IEnumerator Teleport_LoadsAndRelocatesCasterWithoutPathfinding()
        {
            var config = GameAssetManager.Instance.Load<SkillGraphAbilityConfig>(
                "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/Teleport_Graph_Ability.asset");
            Assert.That(config, Is.Not.Null);
            Assert.That(config.TargetRange, Is.EqualTo(6));

            var world = new SkillGraphTestWorld();
            try
            {
                var source = world.CreateSquareCell("TeleportSource", 0, 0);
                var destination = world.CreateSquareCell("TeleportDestination", 2, 0);
                var caster = world.CreateUnit("Caster", 0, source);
                caster.Mana = 20f;
                caster.MaxMana = 20f;
                world.SetTurnContext(world.PlayerOne, new[] { caster });

                var ability = new SkillGraphAbilityImpl(caster, config);
                Task<SkillGraphRuntimeTestResult> task = ability.ExecuteForTestAsync(destination, world.GridController);

                yield return new WaitUntil(() => task.IsCompleted);
                Assert.That(task.IsFaulted, Is.False);
                Assert.That(task.Result.ExecutionState, Is.EqualTo(SkillGraphExecutionState.Completed), task.Result.LastError);
                Assert.That(caster.CurrentCell, Is.SameAs(destination));
                Assert.That(source.CurrentUnits.Contains(caster), Is.False);
                Assert.That(destination.CurrentUnits.Contains(caster), Is.True);
            }
            finally
            {
                world.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator MultiStab_LoadsAndAppliesThreeDamageSegments()
        {
            var config = GameAssetManager.Instance.Load<SkillGraphAbilityConfig>(
                "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/MultiStab_Graph_Ability.asset");
            Assert.That(config, Is.Not.Null);
            Assert.That(config.TargetRange, Is.EqualTo(3));

            var world = new SkillGraphTestWorld();
            try
            {
                var casterCell = world.CreateSquareCell("MultiStabCaster", 0, 0);
                var targetCell = world.CreateSquareCell("MultiStabTarget", 1, 0);
                var caster = world.CreateUnit("Caster", 0, casterCell);
                var target = world.CreateUnit("Target", 1, targetCell);
                caster.Mana = 20f;
                caster.MaxMana = 20f;
                target.Health = 20f;
                target.MaxHealth = 20f;
                world.SetTurnContext(world.PlayerOne, new[] { caster });
                world.SetTurnContext(world.PlayerTwo, new[] { target });

                var ability = new SkillGraphAbilityImpl(caster, config);
                Task<SkillGraphRuntimeTestResult> task = ability.ExecuteForTestAsync(targetCell, world.GridController);

                yield return new WaitUntil(() => task.IsCompleted);
                Assert.That(task.IsFaulted, Is.False);
                Assert.That(task.Result.ExecutionState, Is.EqualTo(SkillGraphExecutionState.Completed), task.Result.LastError);
                Assert.That(target.Health, Is.LessThanOrEqualTo(11f));
            }
            finally
            {
                world.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator BoneShield_AbsorbsMeleePhysicalDamage()
        {
            var config = GameAssetManager.Instance.Load<SkillGraphAbilityConfig>(
                "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/BoneShield_Graph_Ability.asset");
            var world = new SkillGraphTestWorld();
            try
            {
                var casterCell = world.CreateSquareCell("ShieldCaster", 0, 0);
                var caster = world.CreateUnit("Caster", 0, casterCell);
                caster.Charisma = 5;
                caster.Mana = 20f;
                world.SetTurnContext(world.PlayerOne, new[] { caster });
                var ability = new SkillGraphAbilityImpl(caster, config);
                var task = ability.ExecuteForTestAsync(casterCell, world.GridController);
                yield return new WaitUntil(() => task.IsCompleted);
                Assert.That(task.Result.ExecutionState, Is.EqualTo(SkillGraphExecutionState.Completed));
                Assert.That(CombatComponent.GetDamageShield(caster), Is.EqualTo(10f));
                CombatComponent.ApplyDamage(caster, caster, 6f, false, ElementType.None, false, false, false);
                Assert.That(caster.Health, Is.EqualTo(caster.MaxHealth));
                Assert.That(CombatComponent.GetDamageShield(caster), Is.EqualTo(5f));
            }
            finally { world.Dispose(); }
        }

        [UnityTest]
        public IEnumerator IceArmor_ReducesIncomingDamage()
        {
            var config = GameAssetManager.Instance.Load<SkillGraphAbilityConfig>(
                "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/IceArmor_Graph_Ability.asset");
            var world = new SkillGraphTestWorld();
            try
            {
                var cell = world.CreateSquareCell("IceArmorCell", 0, 0);
                var caster = world.CreateUnit("IceArmorCaster", 0, cell);
                caster.Mana = 20f;
                caster.MaxMana = 20f;
                caster.Health = 20f;
                caster.MaxHealth = 20f;
                world.SetTurnContext(world.PlayerOne, new[] { caster });

                var ability = new SkillGraphAbilityImpl(caster, config);
                var task = ability.ExecuteForTestAsync(cell, world.GridController);
                yield return new WaitUntil(() => task.IsCompleted);
                Assert.That(task.Result.ExecutionState, Is.EqualTo(SkillGraphExecutionState.Completed));
                Assert.That(caster.BuffComponent.HasBuff(BuffEffectType.DamageReduction), Is.True);

                CombatComponent.ApplyDamage(caster, caster, 10f, false, ElementType.None, false, false, false);
                Assert.That(caster.Health, Is.GreaterThan(12f));
            }
            finally { world.Dispose(); }
        }

        [UnityTest]
        public IEnumerator FearCurse_AffectsFiveCrossCellsOnly()
        {
            var config = GameAssetManager.Instance.Load<SkillGraphAbilityConfig>(
                "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/FearCurse_Graph_Ability.asset");
            var world = new SkillGraphTestWorld();
            try
            {
                var casterCell = world.CreateSquareCell("FearCaster", 0, 0);
                var center = world.CreateSquareCell("FearCenter", 2, 2);
                var north = world.CreateSquareCell("FearNorth", 2, 3);
                var south = world.CreateSquareCell("FearSouth", 2, 1);
                var east = world.CreateSquareCell("FearEast", 3, 2);
                var west = world.CreateSquareCell("FearWest", 1, 2);
                var diagonal = world.CreateSquareCell("FearDiagonal", 1, 1);
                var caster = world.CreateUnit("FearCasterUnit", 0, casterCell);
                caster.Mana = 20f;
                caster.MaxMana = 20f;
                var enemies = new[]
                {
                    world.CreateUnit("FearCenterEnemy", 1, center),
                    world.CreateUnit("FearNorthEnemy", 1, north),
                    world.CreateUnit("FearSouthEnemy", 1, south),
                    world.CreateUnit("FearEastEnemy", 1, east),
                    world.CreateUnit("FearWestEnemy", 1, west),
                    world.CreateUnit("FearDiagonalEnemy", 1, diagonal)
                };
                world.SetTurnContext(world.PlayerOne, new[] { caster });
                world.SetTurnContext(world.PlayerTwo, enemies);

                var ability = new SkillGraphAbilityImpl(caster, config);
                var task = ability.ExecuteForTestAsync(center, world.GridController);
                yield return new WaitUntil(() => task.IsCompleted);
                Assert.That(task.Result.ExecutionState, Is.EqualTo(SkillGraphExecutionState.Completed), task.Result.LastError);
                for (int i = 0; i < enemies.Length - 1; i++)
                    Assert.That(enemies[i].BuffComponent.CanAct, Is.False, $"Cross target {i} should be feared.");
                Assert.That(enemies[5].BuffComponent.CanAct, Is.True, "Diagonal target must not be affected.");
            }
            finally { world.Dispose(); }
        }

        [UnityTest]
        public IEnumerator PoisonSpear_AppliesPoisonBuffAfterHit()
        {
            var config = GameAssetManager.Instance.Load<SkillGraphAbilityConfig>(
                "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/PoisonSpear_Graph_Ability.asset");
            var world = new SkillGraphTestWorld();
            try
            {
                var casterCell = world.CreateSquareCell("PoisonCaster", 0, 0);
                var targetCell = world.CreateSquareCell("PoisonTarget", 1, 0);
                var caster = world.CreateUnit("PoisonCasterUnit", 0, casterCell);
                var target = world.CreateUnit("PoisonTargetUnit", 1, targetCell);
                caster.Mana = 20f;
                caster.MaxMana = 20f;
                world.SetTurnContext(world.PlayerOne, new[] { caster });
                world.SetTurnContext(world.PlayerTwo, new[] { target });

                var ability = new SkillGraphAbilityImpl(caster, config);
                var task = ability.ExecuteForTestAsync(targetCell, world.GridController);
                yield return new WaitUntil(() => task.IsCompleted);
                Assert.That(task.Result.ExecutionState, Is.EqualTo(SkillGraphExecutionState.Completed), task.Result.LastError);
                Assert.That(target.BuffComponent.HasBuff(BuffEffectType.Poison), Is.True);
            }
            finally { world.Dispose(); }
        }

        [UnityTest]
        public IEnumerator Thrust_DamagesAdjacentTarget()
        {
            var config = GameAssetManager.Instance.Load<SkillGraphAbilityConfig>(
                "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/Thrust_Graph_Ability.asset");
            var world = new SkillGraphTestWorld();
            try
            {
                var casterCell = world.CreateSquareCell("ThrustCaster", 0, 0);
                var targetCell = world.CreateSquareCell("ThrustTarget", 1, 0);
                var caster = world.CreateUnit("ThrustCasterUnit", 0, casterCell);
                var target = world.CreateUnit("ThrustTargetUnit", 1, targetCell);
                caster.Mana = 20f;
                caster.MaxMana = 20f;
                target.Health = 20f;
                target.MaxHealth = 20f;
                world.SetTurnContext(world.PlayerOne, new[] { caster });
                world.SetTurnContext(world.PlayerTwo, new[] { target });

                var ability = new SkillGraphAbilityImpl(caster, config);
                var task = ability.ExecuteForTestAsync(targetCell, world.GridController);
                yield return new WaitUntil(() => task.IsCompleted);
                Assert.That(task.Result.ExecutionState, Is.EqualTo(SkillGraphExecutionState.Completed), task.Result.LastError);
                Assert.That(target.Health, Is.LessThan(20f));
            }
            finally { world.Dispose(); }
        }

        [UnityTest]
        public IEnumerator RecoverSpear_DropsProjectileNearLastHitCell()
        {
            var config = GameAssetManager.Instance.Load<SkillGraphAbilityConfig>(
                "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/RecoverSpear_Graph_Ability.asset");
            var world = new SkillGraphTestWorld();
            try
            {
                var casterCell = world.CreateSquareCell("RecoverCaster", 0, 0);
                var targetCell = world.CreateSquareCell("RecoverTarget", 1, 0);
                world.CreateSquareCell("RecoverDropCell", 1, 1);
                var caster = world.CreateUnit("RecoverCasterUnit", 0, casterCell);
                var target = world.CreateUnit("RecoverTargetUnit", 1, targetCell);
                world.SetTurnContext(world.PlayerOne, new[] { caster });
                world.SetTurnContext(world.PlayerTwo, new[] { target });

                var runner = new SkillGraphRuntimeTestRunner();
                var task = runner.ExecuteAsync(new SkillGraphRuntimeTestRequest
                {
                    Name = config.DisplayName,
                    Graph = config.SkillGraph,
                    GridController = world.GridController,
                    Caster = caster,
                    PrimaryTarget = target
                });
                yield return new WaitUntil(() => task.IsCompleted);
                Assert.That(task.Result.ExecutionState, Is.EqualTo(SkillGraphExecutionState.Completed));
                var dropped = task.Result.ExecutionEvents.Find(entry => entry.EventType == "ProjectileDropped");
                Assert.That(dropped, Is.Not.Null);
                Assert.That(dropped.CellCoordinates, Is.Not.EqualTo(targetCell.GridCoordinates.ToString()));
            }
            finally { world.Dispose(); }
        }

        [UnityTest]
        public IEnumerator SummonAndDecoy_SpawnAdjacentUnit()
        {
            var configPaths = new[]
            {
                "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/SummonFireDemon_Graph_Ability.asset",
                "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/SkeletonMage_Graph_Ability.asset",
                "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/Decoy_Graph_Ability.asset"
            };
            foreach (var configPath in configPaths)
            {
                var config = GameAssetManager.Instance.Load<SkillGraphAbilityConfig>(configPath);
                var world = new SkillGraphTestWorld();
                try
                {
                    var casterCell = world.CreateSquareCell("SummonCaster", 0, 0);
                    var adjacent = world.CreateSquareCell("SummonAdjacent", 1, 0);
                    var caster = world.CreateUnit("Summoner", 0, casterCell);
                    caster.Mana = 20f;
                    caster.MaxMana = 20f;
                    world.SetTurnContext(world.PlayerOne, new[] { caster });

                    var ability = new SkillGraphAbilityImpl(caster, config);
                    var task = ability.ExecuteForTestAsync(adjacent, world.GridController);
                    yield return new WaitUntil(() => task.IsCompleted);
                    Assert.That(task.Result.ExecutionState, Is.EqualTo(SkillGraphExecutionState.Completed), task.Result.LastError);
                    Assert.That(caster.SummonedUnit, Is.Not.Null);
                    Assert.That(caster.SummonedUnit.CurrentCell.GetDistance(casterCell), Is.EqualTo(1));
                }
                finally { world.Dispose(); }
            }
        }

        private static IEnumerator ExecuteProjectile(string configPath, int expectedRange)
        {
            var config = GameAssetManager.Instance.Load<SkillGraphAbilityConfig>(configPath);
            Assert.That(config, Is.Not.Null, $"Missing ability config: {configPath}");
            Assert.That(config.SkillGraph, Is.Not.Null);
            Assert.That(config.TargetRange, Is.EqualTo(expectedRange));

            var world = new SkillGraphTestWorld();
            try
            {
                var casterCell = world.CreateSquareCell("CasterCell", 0, 0);
                var targetCell = world.CreateSquareCell("TargetCell", 1, 0);
                var caster = world.CreateUnit("Caster", 0, casterCell);
                var target = world.CreateUnit("Target", 1, targetCell);
                target.Health = 20f;
                target.MaxHealth = 20f;
                world.SetTurnContext(world.PlayerOne, new[] { caster });
                world.SetTurnContext(world.PlayerTwo, new[] { target });

                var runner = new SkillGraphRuntimeTestRunner();
                Task<SkillGraphRuntimeTestResult> task = runner.ExecuteAsync(new SkillGraphRuntimeTestRequest
                {
                    Name = config.DisplayName,
                    Graph = config.SkillGraph,
                    GridController = world.GridController,
                    Caster = caster,
                    PrimaryTarget = target
                });

                yield return new WaitUntil(() => task.IsCompleted);
                Assert.That(task.IsFaulted, Is.False);
                Assert.That(task.Result.ExecutionState, Is.EqualTo(SkillGraphExecutionState.Completed));
                Assert.That(task.Result.ExecutionEvents,
                    Has.Some.Matches<SkillGraphExecutionEvent>(entry => entry.EventType == "ProjectileLaunched"));
                Assert.That(target.Health, Is.LessThan(20f));
            }
            finally
            {
                world.Dispose();
            }
        }

        private static void AssertSummonHealingPolicy(string graphPath, bool expected)
        {
            var graph = GameAssetManager.Instance.Load<SkillGraphAsset>(graphPath);
            Assert.That(graph, Is.Not.Null, $"Missing summon graph: {graphPath}");

            SummonUnitNodeRecord summonNode = null;
            foreach (var node in graph.Nodes)
            {
                if (node is SummonUnitNodeRecord candidate)
                {
                    summonNode = candidate;
                    break;
                }
            }

            Assert.That(summonNode, Is.Not.Null, $"Missing summon node: {graphPath}");
            Assert.That(summonNode.CanReceiveHealing, Is.EqualTo(expected), graphPath);
        }
    }
}
