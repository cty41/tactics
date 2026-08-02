using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Tactics.AssetPipeline;
using Tactics.Common.AI.MonsterAI;
using Tactics.Common.Battle;
using Tactics.Common.Battle.Runtime;
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
    public sealed class MageSkillLevelTests
    {
        private const string ConfigRoot = "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/";

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            MageSkillRandom.Reset();
            var initializeTask = TestGameAssetHelper.EnsureInitialized();
            yield return new WaitUntil(() => initializeTask.IsCompleted);
            Assert.That(initializeTask.Result, Is.Not.Null);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            MageSkillRandom.Reset();
            TestGameAssetHelper.Cleanup();
            yield return null;
        }

        [Test]
        public void PublishedMageLevels_LoadRealGraphsAndFireDemonAssets()
        {
            var expectedLevels = new Dictionary<string, int>
            {
                ["mage.fireball"] = 3,
                ["mage.ice_bolt"] = 3,
                ["mage.lightning"] = 3,
                ["mage.summon_fire_demon"] = 2,
                ["mage.ice_armor"] = 2,
                ["mage.teleport"] = 2
            };

            foreach (var pair in expectedLevels)
            {
                Assert.That(PureRunAbilityCatalog.TryGet(pair.Key, out var definition), Is.True, pair.Key);
                for (int level = 1; level <= pair.Value; level++)
                {
                    Assert.That(definition.AbilityConfigPaths.TryGetValue(level, out string path), Is.True, $"{pair.Key} Lv{level}");
                    var config = GameAssetManager.Instance.Load<SkillGraphAbilityConfig>(path);
                    Assert.That(config, Is.Not.Null, path);
                    Assert.That(config.SkillGraph, Is.Not.Null, path);
                    Assert.That(SkillGraphValidation.Validate(config.SkillGraph, out var errors, out _), Is.True,
                        $"{path}: {string.Join("; ", errors.Select(error => error.Message))}");
                }
            }

            Assert.That(GameAssetManager.Instance.Load<GameObject>(
                "Assets/Tactics/Arts/Prefabs/Units/FireDemon.prefab"), Is.Not.Null);
            var brain = GameAssetManager.Instance.Load<AiBrainAsset>("Assets/Tactics/AI/FireDemonBrain.asset");
            Assert.That(brain, Is.Not.Null);
            Assert.That(brain.PreferredMinimumRange, Is.EqualTo(2));
            Assert.That(brain.PreferredMaximumRange, Is.EqualTo(3));
            var role = GameAssetManager.Instance.Load<RoleConfig>("Assets/Tactics/Battle/Classes/FireDemon.asset");
            Assert.That(role, Is.Not.Null);
            Assert.That(role.Abilities.Single().DisplayName, Is.EqualTo("火魔攻击"));
        }

        [Test]
        public void FireDemonBrain_RepositionsFromAdjacentEnemyIntoPreferredRange()
        {
            var world = new SkillGraphTestWorld();
            try
            {
                for (int x = -3; x <= 3; x++)
                    world.CreateSquareCell($"Cell_{x}", x, 0);

                var fireDemon = world.CreateUnit("FireDemon", 0, world.CellManager.GetCellAt(new Tactics.Common.Utilities.Vector2IntImpl(0, 0)));
                var enemy = world.CreateUnit("Enemy", 1, world.CellManager.GetCellAt(new Tactics.Common.Utilities.Vector2IntImpl(1, 0)));
                Prepare(fireDemon, enemy);
                fireDemon.AttackRange = 3;
                fireDemon.MovementPoints = 4f;
                fireDemon.MaxMovementPoints = 4f;

                var brain = GameAssetManager.Instance.Load<AiBrainAsset>("Assets/Tactics/AI/FireDemonBrain.asset");
                var context = AiContextBuilder.Build(fireDemon, world.GridController, brain);
                var candidates = IntentGenerator.Generate(context);
                RuleFilter.Filter(candidates, context);
                IntentScorer.Score(candidates, context);
                var selected = IntentResolver.Resolve(candidates, context);

                Assert.That(selected.Action, Is.EqualTo(ActionType.Move));
                Assert.That(selected.Destination.GetDistance(enemy.CurrentCell), Is.InRange(2, 3));
            }
            finally
            {
                world.Dispose();
            }
        }

        [Test]
        public async Task GameplayRunner_ExecutesMageLevelPlan()
        {
            string path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "Tests",
                "gameplay-specs",
                "mage",
                "mage-skill-levels.plan.json"));
            Assert.That(File.Exists(path), Is.True, $"Plan file not found: {path}");

            var plan = ExecutableScenarioPlanLoader.FromFile(path);
            var result = await new GameplayRuntimeRunner().ExecuteAsync(plan);
            Assert.That(result.Passed, Is.True, string.Join("\n", result.Diagnostics));
        }

        [UnityTest]
        public IEnumerator FireballLevelThree_DetonatesOnlyPrimaryBeforeCrossSplash()
        {
            var world = CreateLineWorld(out var caster, out var primary, out var adjacent);
            try
            {
                var ignite = GameAssetManager.Instance.Load<BuffConfig>("Assets/Tactics/Battle/Buffs/Ignite.asset");
                primary.AddBuff(new Buff(ignite, caster, 2));
                adjacent.AddBuff(new Buff(ignite, caster, 2));

                yield return Execute(world, caster, primary.CurrentCell, "Fireball_Lv3_Ability.asset");

                Assert.That(primary.Health, Is.EqualTo(14f), "Primary takes 2 detonation then 4 direct damage.");
                Assert.That(adjacent.Health, Is.EqualTo(18f), "Adjacent target only takes 2 splash damage.");
                Assert.That(BurningStacks(primary), Is.EqualTo(3));
                Assert.That(BurningStacks(adjacent), Is.EqualTo(5), "Adjacent pre-existing Burning is not detonated.");
            }
            finally
            {
                world.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator IceBoltLevels_ExtendSlowAndLevelThreeBouncesOnce()
        {
            var world = new SkillGraphTestWorld();
            try
            {
                var casterCell = world.CreateSquareCell("Caster", 0, 0);
                world.CreateSquareCell("Line", 1, 0);
                var primaryCell = world.CreateSquareCell("Primary", 2, 0);
                var bounceCell = world.CreateSquareCell("Bounce", 2, 2);
                var farCell = world.CreateSquareCell("Far", 6, 2);
                var caster = world.CreateUnit("Mage", 0, casterCell);
                var primary = world.CreateUnit("PrimaryEnemy", 1, primaryCell);
                var bounce = world.CreateUnit("BounceEnemy", 1, bounceCell);
                var far = world.CreateUnit("FarEnemy", 1, farCell);
                Prepare(caster, primary, bounce, far);
                world.SetTurnContext(world.PlayerOne, new[] { caster });
                world.SetTurnContext(world.PlayerTwo, new[] { primary, bounce, far });

                yield return Execute(world, caster, primaryCell, "IceBolt_Lv2_Graph_Ability.asset");
                Assert.That(Status(primary, BuffEffectType.Slow).RemainingTurns, Is.EqualTo(2));
                primary.RemoveBuff(Status(primary, BuffEffectType.Slow));
                primary.Health = 20f;

                yield return Execute(world, caster, primaryCell, "IceBolt_Lv3_Graph_Ability.asset");
                Assert.That(primary.Health, Is.EqualTo(12f));
                Assert.That(bounce.Health, Is.EqualTo(16f));
                Assert.That(far.Health, Is.EqualTo(20f));
                Assert.That(Status(primary, BuffEffectType.Slow).RemainingTurns, Is.EqualTo(2));
                Assert.That(Status(bounce, BuffEffectType.Slow).RemainingTurns, Is.EqualTo(1));
            }
            finally
            {
                world.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator LightningLevels_AreInstantAndUseDeterministicStunChance()
        {
            var world = new SkillGraphTestWorld();
            var runtimeScope = new BattleRuntimeScope();
            try
            {
                var casterCell = world.CreateSquareCell("Caster", 0, 0);
                var wallCell = world.CreateSquareCell("Wall", 1, 0, movementCost: 99f);
                var targetCell = world.CreateSquareCell("Target", 2, 0);
                var caster = world.CreateUnit("Mage", 0, casterCell);
                var target = world.CreateUnit("Enemy", 1, targetCell);
                Prepare(caster, target);
                world.SetTurnContext(world.PlayerOne, new[] { caster });
                world.SetTurnContext(world.PlayerTwo, new[] { target });
                MageSkillRandom.SetProviderForTests(() => 0d);

                var task = ExecuteTask(
                    world,
                    caster,
                    targetCell,
                    "Lightning_Lv3_Graph_Ability.asset",
                    runtimeScope);
                yield return new WaitUntil(() => task.IsCompleted);
                AssertExecution(task);
                Assert.That(task.Result.ExecutionEvents.Any(entry => entry.EventType == "ProjectileLaunched"), Is.False);
                Assert.That(task.Result.ExecutionEvents.Any(entry => entry.EventType == "VisualCueStarted"), Is.True);
                var lightningCue = GameObject.Find("LightningImpact_Vfx");
                Assert.That(lightningCue, Is.Not.Null);
                Assert.That(lightningCue.GetComponentsInChildren<ParticleSystem>(true), Is.Not.Empty);
                Assert.That(target.Health, Is.EqualTo(9f));
                Assert.That(Status(target, BuffEffectType.Stun), Is.Not.Null);
                Assert.That(wallCell, Is.Not.Null, "Intervening blocked terrain does not stop instant lightning.");
                yield return new WaitForSeconds(0.7f);
                Assert.That(GameObject.Find("LightningImpact_Vfx"), Is.Null);
            }
            finally
            {
                runtimeScope.Cancel();
                runtimeScope.Dispose();
                world.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator SummonFireDemonLevelTwo_AllowsPartialSpawnThenAtomicallyReplacesOldSummons()
        {
            var world = new SkillGraphTestWorld();
            try
            {
                var casterCell = world.CreateSquareCell("Caster", 0, 0);
                var onlyOpenCell = world.CreateSquareCell("OnlyOpen", 1, 0);
                var caster = world.CreateUnit("Mage", 0, casterCell);
                Prepare(caster);
                world.SetTurnContext(world.PlayerOne, new[] { caster });

                yield return Execute(world, caster, casterCell, "SummonFireDemon_Lv2_Graph_Ability.asset");
                var registry = SummonRegistry.For(world.GridController);
                var first = registry.GetOrdered(caster, "FireDemon").Single();
                Assert.That(first.CurrentCell, Is.SameAs(onlyOpenCell));
                Assert.That(first.CanReceiveHealing, Is.True);

                var secondOpenCell = world.CreateSquareCell("SecondOpen", 0, 1);
                caster.Mana = 20f;
                yield return Execute(world, caster, casterCell, "SummonFireDemon_Lv2_Graph_Ability.asset");
                var replacements = registry.GetOrdered(caster, "FireDemon");
                Assert.That(replacements.Count, Is.EqualTo(2));
                Assert.That(replacements, Has.None.SameAs(first));
                Assert.That(first.IsDowned, Is.True);
                Assert.That(replacements.Select(unit => unit.CurrentCell), Does.Contain(secondOpenCell));

                var lifetimeUnit = replacements.First();
                for (int action = 0; action < 5; action++)
                    lifetimeUnit.OnTurnEnd(world.GridController);
                Assert.That(lifetimeUnit.IsDowned, Is.True, "Fire Demon expires after its fifth completed action.");

                var battleEndUnit = replacements.Last();
                registry.Clear(despawnSummons: true);
                Assert.That(battleEndUnit.IsDowned, Is.True, "Battle cleanup despawns surviving Fire Demons.");
                Assert.That(battleEndUnit.CurrentCell, Is.Null);
            }
            finally
            {
                world.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator IceArmorLevelTwo_ReducesDamageAndSlowsAdjacentMeleeAttacker()
        {
            var world = new SkillGraphTestWorld();
            try
            {
                var casterCell = world.CreateSquareCell("Caster", 0, 0);
                var attackerCell = world.CreateSquareCell("Attacker", 1, 0);
                var caster = world.CreateUnit("Mage", 0, casterCell);
                var attacker = world.CreateUnit("Attacker", 1, attackerCell);
                Prepare(caster, attacker);
                world.SetTurnContext(world.PlayerOne, new[] { caster });

                yield return Execute(world, caster, casterCell, "IceArmor_Lv2_Graph_Ability.asset");
                CombatComponent.ApplyDamage(attacker, caster, 8f, false, DamageCategory.Physical, ElementType.None,
                    canTriggerBeforeAttacked: false, canCrit: false, canTriggerDamageTaken: true);

                Assert.That(caster.Health, Is.EqualTo(14f));
                Assert.That(Status(attacker, BuffEffectType.Slow).RemainingTurns, Is.EqualTo(2));
            }
            finally
            {
                world.Dispose();
            }
        }

        [Test]
        public void TeleportLevels_ChangeManaAndVisibilityRequirementOnly()
        {
            var levelOne = GameAssetManager.Instance.Load<SkillGraphAbilityConfig>($"{ConfigRoot}Teleport_Graph_Ability.asset");
            var levelTwo = GameAssetManager.Instance.Load<SkillGraphAbilityConfig>($"{ConfigRoot}Teleport_Lv2_Graph_Ability.asset");
            Assert.That(levelOne.ManaCost, Is.EqualTo(8));
            Assert.That(levelTwo.ManaCost, Is.EqualTo(5));
            Assert.That(levelOne.SkillGraph.Nodes.OfType<TeleportNodeRecord>().Single().RequiresLineOfSight, Is.True);
            Assert.That(levelTwo.SkillGraph.Nodes.OfType<TeleportNodeRecord>().Single().RequiresLineOfSight, Is.False);
            Assert.That(levelOne.TargetRange, Is.EqualTo(6));
            Assert.That(levelTwo.TargetRange, Is.EqualTo(6));
        }

        private static SkillGraphTestWorld CreateLineWorld(out Unit caster, out Unit primary, out Unit adjacent)
        {
            var world = new SkillGraphTestWorld();
            var casterCell = world.CreateSquareCell("Caster", 0, 0);
            world.CreateSquareCell("Line", 1, 0);
            var primaryCell = world.CreateSquareCell("Primary", 2, 0);
            var adjacentCell = world.CreateSquareCell("Adjacent", 2, 1);
            caster = world.CreateUnit("Mage", 0, casterCell);
            primary = world.CreateUnit("PrimaryEnemy", 1, primaryCell);
            adjacent = world.CreateUnit("AdjacentEnemy", 1, adjacentCell);
            Prepare(caster, primary, adjacent);
            world.SetTurnContext(world.PlayerOne, new[] { caster });
            world.SetTurnContext(world.PlayerTwo, new[] { primary, adjacent });
            return world;
        }

        private static void Prepare(params Unit[] units)
        {
            foreach (var unit in units)
            {
                unit.Health = 20f;
                unit.MaxHealth = 20f;
                unit.Mana = 20f;
                unit.MaxMana = 20f;
                unit.DefenceFactor = 0;
                unit.Luck = 0;
            }
        }

        private static IEnumerator Execute(
            SkillGraphTestWorld world, Unit caster, Tactics.Common.Cells.ICell targetCell, string configFile)
        {
            var task = ExecuteTask(world, caster, targetCell, configFile);
            yield return new WaitUntil(() => task.IsCompleted);
            AssertExecution(task);
        }

        private static Task<SkillGraphRuntimeTestResult> ExecuteTask(
            SkillGraphTestWorld world,
            Unit caster,
            Tactics.Common.Cells.ICell targetCell,
            string configFile,
            IBattleRuntimeScope runtimeScope = null)
        {
            var config = GameAssetManager.Instance.Load<SkillGraphAbilityConfig>($"{ConfigRoot}{configFile}");
            Assert.That(config, Is.Not.Null, configFile);
            return new SkillGraphAbilityImpl(caster, config).ExecuteForTestAsync(
                targetCell,
                world.GridController,
                runtimeScope);
        }

        private static void AssertExecution(Task<SkillGraphRuntimeTestResult> task)
        {
            Assert.That(task.IsFaulted, Is.False, task.Exception?.ToString());
            Assert.That(task.Result.ExecutionState, Is.EqualTo(SkillGraphExecutionState.Completed), task.Result.LastError);
        }

        private static Buff Status(IUnit unit, BuffEffectType type) =>
            unit.GetActiveBuffs().SingleOrDefault(buff => buff.Config.EffectType == type);

        private static int BurningStacks(IUnit unit) => Status(unit, BuffEffectType.Burning)?.StackCount ?? 0;
    }
}
