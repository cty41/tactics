using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using Tactics.AssetPipeline;
using Tactics.Common.Battle;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Skills.Graph.Testing;
using Tactics.Common.Testing.Gameplay;
using Tactics.Common.Units;
using Tactics.Common.Units.Abilities;
using Tactics.Common.Units.Buffs;
using Tactics.Common.Units.Classes;
using Tactics.Roster;
using Tactics.Units;
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
        public void NonPureUnit_WithoutInjectedLoadoutStillUsesRoleConfigAbilities()
        {
            var roleConfig = GameAssetManager.Instance.Load<RoleConfig>(
                "Assets/Tactics/Battle/Classes/Mage.asset");
            Assert.That(roleConfig, Is.Not.Null);

            var gameObject = new GameObject("NonPureRoleConfigUnit");
            try
            {
                var unit = gameObject.AddComponent<Unit>();
                var roleConfigField = typeof(Unit).GetField(
                    "_roleConfig",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(roleConfigField, Is.Not.Null);
                roleConfigField.SetValue(unit, roleConfig);

                unit.Initialize(null);

                Assert.That(unit.UsesInjectedAbilityConfigs, Is.False);
                var actualNames = unit.GetBaseAbilities().Select(ability => ability.DisplayName).ToList();
                foreach (var config in roleConfig.Abilities.Where(config => config != null))
                    Assert.That(actualNames, Does.Contain(config.DisplayName), config.DisplayName);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void PureRunCatalog_AllPublishedAbilityConfigsLoad()
        {
            foreach (string path in PureRunAbilityCatalog.GetPublishedAbilityPaths())
            {
                var config = GameAssetManager.Instance.Load<AbilityConfig>(path);
                Assert.That(config, Is.Not.Null, path);
            }
        }

        [Test]
        public void PureRunBinder_InjectsOnlyBaseAttackAndLearnedExactLevel()
        {
            var mage = CharacterDefinition.CreateDefault("pure_run_mage", "Mage", roleType: RoleType.Mage);
            mage.LearnedSkills.Add(new CharacterDefinition.LearnedSkill
            {
                SkillId = "mage.fireball",
                SkillType = SkillType.Active,
                Level = 2
            });

            var gameObject = new GameObject("PureRunBinderTestUnit");
            try
            {
                var unit = gameObject.AddComponent<TilemapUnit>();
                var result = PureRunAbilityBinder.Bind(
                    mage,
                    unit,
                    path => GameAssetManager.Instance.Load<AbilityConfig>(path));

                Assert.That(unit.UsesInjectedAbilityConfigs, Is.True);
                Assert.That(unit.GetLearnedSkillLevel("mage.fireball"), Is.EqualTo(2));
                Assert.That(result.MissingSkillIds, Is.Empty);
                Assert.That(result.LoadedPaths, Is.EquivalentTo(new[]
                {
                    PureRunAbilityCatalog.MagicBaseAttackPath,
                    "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/Fireball_Lv2_Ability.asset"
                }));
                Assert.That(result.LoadedPaths, Does.Not.Contain(
                    "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/Fireball_Lv1_Ability.asset"));
                Assert.That(result.LoadedPaths, Does.Not.Contain(
                    "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/IceBolt_Graph_Ability.asset"));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void PureRunFireballUpgrade_PersistsAndBindsLevelTwoForNextBattle()
        {
            const int testSlot = 2;
            bool hadPreviousSave = PlayerAdventureStateStore.HasSave(testSlot);
            var previousState = hadPreviousSave
                ? PlayerAdventureStateStore.Load(testSlot)
                : null;
            GameObject gameObject = null;
            try
            {
                var state = PlayerAdventureStateStore.CreatePureRunState(20260722);
                var mage = state.Roster.Single(character => character.RoleType == RoleType.Mage);
                mage.Level = 2;
                mage.StartingBranchSkillId = "mage.fireball";
                mage.LearnedSkills.Clear();
                mage.LearnedSkills.Add(new CharacterDefinition.LearnedSkill
                {
                    SkillId = "mage.fireball",
                    SkillType = SkillType.Active,
                    Level = 1
                });

                var choices = PureRunProgression.BuildSkillChoices(mage, state.RunSeed, mage.Level);
                Assert.That(choices, Has.Some.Matches<SkillDefinition>(skill =>
                    skill.Id == "mage.fireball" && skill.Level == 2));
                Assert.That(SkillSystem.UpgradeSkill(mage, "mage.fireball"), Is.True);

                PlayerAdventureStateStore.Save(testSlot, state);
                var restored = PlayerAdventureStateStore.Load(testSlot);
                var restoredMage = restored.Roster.Single(character => character.Id == mage.Id);
                Assert.That(restoredMage.LearnedSkills.Single(skill => skill.SkillId == "mage.fireball").Level,
                    Is.EqualTo(2));

                gameObject = new GameObject("PureRunPersistedBinderTestUnit");
                var unit = gameObject.AddComponent<TilemapUnit>();
                var result = PureRunAbilityBinder.Bind(
                    restoredMage,
                    unit,
                    path => GameAssetManager.Instance.Load<AbilityConfig>(path));

                Assert.That(result.MissingSkillIds, Is.Empty);
                Assert.That(result.LoadedPaths, Is.EquivalentTo(new[]
                {
                    PureRunAbilityCatalog.MagicBaseAttackPath,
                    "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/Fireball_Lv2_Ability.asset"
                }));
                Assert.That(result.LoadedPaths, Does.Not.Contain(
                    "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/Fireball_Lv1_Ability.asset"));
            }
            finally
            {
                if (gameObject != null)
                    Object.DestroyImmediate(gameObject);

                if (hadPreviousSave)
                    PlayerAdventureStateStore.Save(testSlot, previousState);
                else
                    PlayerAdventureStateStore.Delete(testSlot);
            }
        }

        [UnityTest]
        public IEnumerator FireballPublishedLevels_HaveSingleTargetThenCrossSplashBehavior()
        {
            yield return ExecuteFireballLevel(
                "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/Fireball_Lv1_Ability.asset",
                18f,
                20f);
            yield return ExecuteFireballLevel(
                "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/Fireball_Lv2_Ability.asset",
                16f,
                18f);
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
        }

        [Test]
        public void LightningGraph_IsInstantAndDoesNotUseProjectile()
        {
            var graph = GameAssetManager.Instance.Load<SkillGraphAsset>(
                "Assets/Tactics/Battle/Abilities/SkillGraphs/Lightning_Graph.asset");
            Assert.That(graph, Is.Not.Null);
            Assert.That(graph.Nodes.OfType<ProjectileLaunchNodeRecord>(), Is.Empty);
            Assert.That(graph.Nodes.OfType<MageSkillNodeRecord>().Single().SkillKind,
                Is.EqualTo(MageSkillKind.Lightning));
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
                Task<SkillGraphRuntimeTestResult> task = ability.ExecuteOrderedForTestAsync(
                    new IUnit[] { target, target, target }, world.GridController);

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
                "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/FearCurse_Lv2_Graph_Ability.asset");
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
                {
                    Assert.That(enemies[i].BuffComponent.HasBuff(BuffEffectType.Fear), Is.True,
                        $"Cross target {i} should be feared.");
                    Assert.That(enemies[i].BuffComponent.CanAct, Is.True,
                        "Fear preserves attacks and skills after forced movement.");
                }
                Assert.That(enemies[5].BuffComponent.HasBuff(BuffEffectType.Fear), Is.False,
                    "Diagonal target must not be affected.");
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
                world.CreateSquareCell("PoisonDrop", 2, 0);
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
            finally
            {
                AmazonBattleState.For(world.GridController).Clear();
                world.Dispose();
            }
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
        public IEnumerator RecoverSpear_RecallsExistingDroppedSpear()
        {
            var config = GameAssetManager.Instance.Load<SkillGraphAbilityConfig>(
                "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/RecoverSpear_Graph_Ability.asset");
            var world = new SkillGraphTestWorld();
            try
            {
                var casterCell = world.CreateSquareCell("RecoverCaster", 0, 0);
                var spearCell = world.CreateSquareCell("RecoverSpear", 1, 0);
                var caster = world.CreateUnit("RecoverCasterUnit", 0, casterCell);
                caster.Mana = 20f;
                world.SetTurnContext(world.PlayerOne, new[] { caster });
                var state = AmazonBattleState.For(world.GridController);
                Assert.That(state.DropSpear(caster, spearCell), Is.True);
                var task = new SkillGraphAbilityImpl(caster, config)
                    .ExecuteForTestAsync(spearCell, world.GridController);
                yield return new WaitUntil(() => task.IsCompleted);
                Assert.That(task.Result.ExecutionState, Is.EqualTo(SkillGraphExecutionState.Completed));
                Assert.That(state.IsSpearHeld(caster), Is.True);
            }
            finally
            {
                AmazonBattleState.For(world.GridController).Clear();
                world.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator SummonAndDecoy_CreateTheirDistinctRuntimeEntities()
        {
            var configPaths = new[]
            {
                "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/SummonFireDemon_Graph_Ability.asset",
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
                    if (configPath.Contains("Decoy"))
                    {
                        var decoy = AmazonBattleState.For(world.GridController).GetDecoy(caster);
                        Assert.That(decoy, Is.Not.Null);
                        Assert.That(decoy.CurrentCell, Is.SameAs(casterCell));
                        Assert.That(caster.CurrentCell, Is.SameAs(adjacent));
                    }
                    else
                    {
                        Assert.That(caster.SummonedUnit, Is.Not.Null);
                        Assert.That(caster.SummonedUnit.CurrentCell.GetDistance(casterCell), Is.EqualTo(1));
                    }
                }
                finally
                {
                    AmazonBattleState.For(world.GridController).Clear();
                    world.Dispose();
                }
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

        private static IEnumerator ExecuteFireballLevel(
            string configPath,
            float expectedPrimaryHealth,
            float expectedAdjacentEnemyHealth)
        {
            var config = GameAssetManager.Instance.Load<SkillGraphAbilityConfig>(configPath);
            Assert.That(config, Is.Not.Null, configPath);

            var world = new SkillGraphTestWorld();
            try
            {
                var casterCell = world.CreateSquareCell("FireballCaster", 0, 0);
                world.CreateSquareCell("FireballLine", 1, 0);
                var primaryCell = world.CreateSquareCell("FireballPrimary", 2, 0);
                var adjacentCell = world.CreateSquareCell("FireballAdjacent", 2, 1);
                var friendlyCell = world.CreateSquareCell("FireballFriendly", 3, 0);
                var diagonalCell = world.CreateSquareCell("FireballDiagonal", 3, 1);

                var caster = world.CreateUnit("FireballCasterUnit", 0, casterCell);
                var primary = world.CreateUnit("FireballPrimaryUnit", 1, primaryCell);
                var adjacentEnemy = world.CreateUnit("FireballAdjacentUnit", 1, adjacentCell);
                var friendly = world.CreateUnit("FireballFriendlyUnit", 0, friendlyCell);
                var diagonalEnemy = world.CreateUnit("FireballDiagonalUnit", 1, diagonalCell);

                caster.Mana = 20f;
                caster.MaxMana = 20f;
                caster.Luck = 0;
                foreach (var target in new[] { primary, adjacentEnemy, friendly, diagonalEnemy })
                {
                    target.Health = 20f;
                    target.MaxHealth = 20f;
                    target.DefenceFactor = 0;
                }

                world.SetTurnContext(world.PlayerOne, new[] { caster, friendly });
                world.SetTurnContext(world.PlayerTwo, new[] { primary, adjacentEnemy, diagonalEnemy });

                var ability = new SkillGraphAbilityImpl(caster, config);
                var task = ability.ExecuteForTestAsync(primaryCell, world.GridController);
                yield return new WaitUntil(() => task.IsCompleted);

                Assert.That(task.IsFaulted, Is.False);
                Assert.That(task.Result.ExecutionState, Is.EqualTo(SkillGraphExecutionState.Completed), task.Result.LastError);
                Assert.That(primary.Health, Is.EqualTo(expectedPrimaryHealth), configPath);
                Assert.That(adjacentEnemy.Health, Is.EqualTo(expectedAdjacentEnemyHealth), configPath);
                Assert.That(friendly.Health, Is.EqualTo(20f), "Friendly fire is not allowed.");
                Assert.That(diagonalEnemy.Health, Is.EqualTo(20f), "Cross splash must not hit diagonal cells.");
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
            MageSkillNodeRecord mageSummonNode = null;
            NecromancerSkillNodeRecord necromancerSummonNode = null;
            foreach (var node in graph.Nodes)
            {
                if (node is SummonUnitNodeRecord candidate)
                {
                    summonNode = candidate;
                    break;
                }
                if (node is MageSkillNodeRecord mageCandidate &&
                    mageCandidate.SkillKind == MageSkillKind.SummonFireDemon)
                {
                    mageSummonNode = mageCandidate;
                }
                if (node is NecromancerSkillNodeRecord necromancerCandidate
                    && necromancerCandidate.SkillKind is NecromancerSkillKind.SummonSkeleton
                        or NecromancerSkillKind.SummonSkeletonMage)
                {
                    necromancerSummonNode = necromancerCandidate;
                }
            }

            if (mageSummonNode != null)
            {
                Assert.That(expected, Is.True, "Fire Demons accept standard healing.");
                return;
            }

            if (necromancerSummonNode != null)
            {
                Assert.That(expected, Is.False, "Reanimated summons reject standard HP recovery.");
                return;
            }

            Assert.That(summonNode, Is.Not.Null, $"Missing summon node: {graphPath}");
            Assert.That(summonNode.CanReceiveHealing, Is.EqualTo(expected), graphPath);
        }
    }
}
