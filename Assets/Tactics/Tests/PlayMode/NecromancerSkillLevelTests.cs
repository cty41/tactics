using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Tactics.AssetPipeline;
using Tactics.Common.Battle;
using Tactics.Common.Cells;
using Tactics.Common.Interactables;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Skills.Graph.Testing;
using Tactics.Common.Testing.Gameplay;
using Tactics.Common.Units;
using Tactics.Common.Units.Abilities;
using Tactics.Common.Units.Buffs;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tactics.Tests.PlayMode
{
    public sealed class NecromancerSkillLevelTests
    {
        private const string ConfigRoot = "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/";

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
        public void PublishedNecromancerLevels_LoadRealGraphs()
        {
            var expected = new Dictionary<string, int>
            {
                ["necromancer.summon_skeleton"] = 3,
                ["necromancer.amplify_damage"] = 3,
                ["necromancer.bone_spear"] = 3,
                ["necromancer.skeleton_mage"] = 2,
                ["necromancer.fear_curse"] = 2,
                ["necromancer.bone_shield"] = 2
            };

            foreach (var pair in expected)
            {
                Assert.That(PureRunAbilityCatalog.TryGet(pair.Key, out var definition), Is.True, pair.Key);
                for (int level = 1; level <= pair.Value; level++)
                {
                    Assert.That(definition.AbilityConfigPaths.TryGetValue(level, out string path), Is.True);
                    var config = GameAssetManager.Instance.Load<SkillGraphAbilityConfig>(path);
                    Assert.That(config, Is.Not.Null, path);
                    Assert.That(SkillGraphValidation.Validate(config.SkillGraph, out var errors, out _), Is.True,
                        string.Join("; ", errors.Select(error => error.Message)));
                    if (pair.Key == "necromancer.skeleton_mage")
                    {
                        var summon = config.SkillGraph.Nodes.OfType<NecromancerSkillNodeRecord>().Single();
                        Assert.That(summon.SummonAttack.ManaCost, Is.Zero);
                        Assert.That(summon.SummonAttack.IsBasicAbility, Is.True);
                        Assert.That(summon.SummonBrain, Is.Not.Null);
                        Assert.That(summon.SummonBrain.PreferredMinimumRange, Is.EqualTo(2));
                        Assert.That(summon.SummonBrain.PreferredMaximumRange, Is.EqualTo(3));
                        Assert.That(summon.SummonAttack is SkillGraphAbilityConfig mageAttack
                            && mageAttack.SkillGraph.Nodes.OfType<MageSkillNodeRecord>()
                                .Any(node => node.SkillKind == MageSkillKind.Fireball && node.Level == level), Is.True);
                    }
                }
            }
        }

        [Test]
        public async Task GameplayRunner_ExecutesNecromancerLevelPlan()
        {
            string path = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", "Tests", "gameplay-specs", "necromancer",
                "necromancer-skill-levels.plan.json"));
            Assert.That(File.Exists(path), Is.True, path);
            var plan = ExecutableScenarioPlanLoader.FromFile(path);
            var result = await new GameplayRuntimeRunner().ExecuteAsync(plan);
            Assert.That(result.Passed, Is.True, string.Join("\n", result.Diagnostics));
        }

        [UnityTest]
        public IEnumerator SummonSkeleton_ConsumesOnlySelectedCorpse_AndReplacesOldestAtLevelCap()
        {
            var world = CreateLineWorld(5, out var caster);
            var corpseObjects = new List<GameObject>();
            try
            {
                var corpses = new List<Corpse>();
                for (int x = 1; x <= 3; x++)
                {
                    var go = new GameObject($"Corpse{x}");
                    corpseObjects.Add(go);
                    var corpse = go.AddComponent<Corpse>();
                    corpse.CurrentCell = Cell(world, x, 0);
                    corpse.CurrentCell.AddInteractable(corpse);
                    corpses.Add(corpse);
                }

                yield return Execute(world, caster, corpses[1].CurrentCell, "SummonSkeleton_Lv2_Graph_Ability.asset");
                Assert.That(corpses[0].IsDestroyed, Is.False, "Non-selected corpse must remain.");
                Assert.That(corpses[1].IsDestroyed, Is.True);
                var first = SummonRegistry.For(world.GridController).GetOrdered(caster, "Skeleton").Single();
                Assert.That(first.MaxHealth, Is.EqualTo(10f));
                Assert.That(first.CanReceiveHealing, Is.False);
                first.Health = 5f;
                first.ModifyHealth(5f, caster);
                Assert.That(first.Health, Is.EqualTo(5f), "Standard HP recovery resolves to zero.");

                yield return Execute(world, caster, corpses[0].CurrentCell, "SummonSkeleton_Lv2_Graph_Ability.asset");
                yield return Execute(world, caster, corpses[2].CurrentCell, "SummonSkeleton_Lv2_Graph_Ability.asset");
                var active = SummonRegistry.For(world.GridController).GetOrdered(caster, "Skeleton");
                Assert.That(active.Count, Is.EqualTo(2));
                Assert.That(active.Contains(first), Is.False, "Third successful summon replaces the oldest.");
            }
            finally
            {
                SummonRegistry.For(world.GridController).Clear(true);
                foreach (var corpseObject in corpseObjects)
                    if (corpseObject != null) Object.DestroyImmediate(corpseObject);
                world.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator AmplifyDamageLevels_UseSingleCrossAndSquareAreas()
        {
            var world = CreateAreaWorld(out var caster, out var center, out var cross, out var diagonal);
            try
            {
                yield return Execute(world, caster, center.CurrentCell, "Curse_Graph_Ability.asset");
                Assert.That(Has(center, BuffEffectType.CurseDamageAmplifier), Is.True);
                Assert.That(Has(cross, BuffEffectType.CurseDamageAmplifier), Is.False);
                center.RemoveBuff(center.GetActiveBuffs().Single());

                yield return Execute(world, caster, center.CurrentCell, "Curse_Lv2_Graph_Ability.asset");
                Assert.That(Has(center, BuffEffectType.CurseDamageAmplifier), Is.True);
                Assert.That(Has(cross, BuffEffectType.CurseDamageAmplifier), Is.True);
                Assert.That(Has(diagonal, BuffEffectType.CurseDamageAmplifier), Is.False);

                yield return Execute(world, caster, center.CurrentCell, "Curse_Lv3_Graph_Ability.asset");
                Assert.That(Has(diagonal, BuffEffectType.CurseDamageAmplifier), Is.True);
                Assert.That(center.GetActiveBuffs().Single().RemainingTurns, Is.EqualTo(5));
            }
            finally
            {
                world.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator BoneSpearLevelThree_PiercesEnemiesButIgnoresAllies()
        {
            var world = CreateLineWorld(5, out var caster);
            try
            {
                var ally = world.CreateUnit("Ally", 0, Cell(world, 1, 0));
                var first = world.CreateUnit("Enemy1", 1, Cell(world, 2, 0));
                var second = world.CreateUnit("Enemy2", 1, Cell(world, 3, 0));
                Prepare(ally, first, second);
                world.SetTurnContext(world.PlayerTwo, new[] { first, second });

                yield return Execute(world, caster, Cell(world, 4, 0), "BoneSpear_Lv3_Graph_Ability.asset");
                Assert.That(ally.Health, Is.EqualTo(20f));
                Assert.That(first.Health, Is.EqualTo(13f));
                Assert.That(second.Health, Is.EqualTo(13f));
            }
            finally
            {
                world.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator BoneSpearLevelOne_StopsAtFirstEnemyOnSelectedLine()
        {
            var world = CreateLineWorld(5, out var caster);
            try
            {
                var first = world.CreateUnit("NearEnemy", 1, Cell(world, 2, 0));
                var selected = world.CreateUnit("SelectedEnemy", 1, Cell(world, 4, 0));
                Prepare(first, selected);
                world.SetTurnContext(world.PlayerTwo, new[] { first, selected });

                yield return Execute(world, caster, selected.CurrentCell, "BoneSpear_Graph_Ability.asset");
                Assert.That(first.Health, Is.EqualTo(13f));
                Assert.That(selected.Health, Is.EqualTo(20f));
            }
            finally
            {
                world.Dispose();
            }
        }

        [Test]
        public void BoneSpearAllLevels_RejectNonStraightEndpointsBeforeExecution()
        {
            var world = new SkillGraphTestWorld();
            try
            {
                for (int x = 0; x <= 3; x++)
                for (int y = 0; y <= 3; y++)
                    world.CreateSquareCell($"Cell{x}_{y}", x, y);
                var caster = world.CreateUnit("Necromancer", 0, Cell(world, 0, 0));
                var straightEnemy = world.CreateUnit("StraightEnemy", 1, Cell(world, 0, 3));
                var offLineEnemy = world.CreateUnit("OffLineEnemy", 1, Cell(world, 1, 3));
                var diagonalEnemy = world.CreateUnit("DiagonalEnemy", 1, Cell(world, 2, 2));
                Prepare(caster, straightEnemy, offLineEnemy, diagonalEnemy);
                world.SetTurnContext(world.PlayerOne, new[] { caster });
                world.SetTurnContext(world.PlayerTwo, new[] { straightEnemy, offLineEnemy, diagonalEnemy });

                foreach (string configFile in new[]
                         {
                             "BoneSpear_Graph_Ability.asset",
                             "BoneSpear_Lv2_Graph_Ability.asset",
                             "BoneSpear_Lv3_Graph_Ability.asset"
                         })
                {
                    var ability = Ability(configFile, caster);
                    ability.OnAbilitySelected(world.GridController);
                    var query = new AbilityTargetQuery(caster, caster.CurrentCell, world.GridController,
                        world.UnitManager.GetUnits());
                    var targets = ability.QueryTargets(query).Options
                        .Select(option => option.TargetPoint)
                        .ToList();
                    CollectionAssert.Contains(targets, straightEnemy.CurrentCell, configFile);
                    CollectionAssert.DoesNotContain(targets, offLineEnemy.CurrentCell, configFile);
                    CollectionAssert.DoesNotContain(targets, diagonalEnemy.CurrentCell, configFile);
                    ability.CleanUp(world.GridController);
                }
            }
            finally
            {
                world.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator Fear_ForcesFarthestReachableMove_ButLeavesTargetAbleToAct()
        {
            var world = CreateLineWorld(5, out var caster);
            try
            {
                var target = world.CreateUnit("Target", 1, Cell(world, 1, 0));
                Prepare(target);
                target.Speed = 2f;
                world.SetTurnContext(world.PlayerTwo, new[] { target });

                yield return Execute(world, caster, target.CurrentCell, "FearCurse_Graph_Ability.asset");
                target.OnTurnStart(world.GridController);

                Assert.That(target.CurrentCell.GridCoordinates.x, Is.EqualTo(3));
                Assert.That(target.MovementPoints, Is.Zero);
                Assert.That(target.CanAct, Is.True, "Fear consumes movement, not the whole action.");
                target.OnTurnEnd(world.GridController);
                Assert.That(Has(target, BuffEffectType.Fear), Is.False);
            }
            finally
            {
                world.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator BoneShieldLevels_ResetValue_AndExpandDamageCoverage()
        {
            var world = CreateLineWorld(2, out var caster);
            try
            {
                var attacker = world.CreateUnit("Attacker", 1, Cell(world, 1, 0));
                Prepare(attacker);
                caster.Charisma = 5;
                caster.Health = 20f;
                caster.MaxHealth = 20f;

                yield return Execute(world, caster, caster.CurrentCell, "BoneShield_Graph_Ability.asset");
                CombatComponent.ApplyDamage(attacker, caster, 6f, false, DamageCategory.Physical, ElementType.None,
                    false, false, false);
                CombatComponent.ApplyDamage(attacker, caster, 6f, false, DamageCategory.Magic, ElementType.Fire,
                    false, false, false);
                Assert.That(caster.Health, Is.EqualTo(14f));
                Assert.That(CombatComponent.GetDamageShield(caster), Is.EqualTo(4f));

                yield return Execute(world, caster, caster.CurrentCell, "BoneShield_Lv2_Graph_Ability.asset");
                Assert.That(CombatComponent.GetDamageShield(caster), Is.EqualTo(10f), "Recast resets instead of stacking.");
                CombatComponent.ApplyDamage(attacker, caster, 6f, false, DamageCategory.Magic, ElementType.Fire,
                    false, false, false);
                Assert.That(caster.Health, Is.EqualTo(14f));
                Assert.That(CombatComponent.GetDamageShield(caster), Is.EqualTo(4f));
            }
            finally
            {
                world.Dispose();
            }
        }

        private static SkillGraphTestWorld CreateLineWorld(int count, out Unit caster)
        {
            var world = new SkillGraphTestWorld();
            for (int x = 0; x < count; x++)
                world.CreateSquareCell($"Cell{x}", x, 0);
            caster = world.CreateUnit("Necromancer", 0, Cell(world, 0, 0));
            Prepare(caster);
            world.SetTurnContext(world.PlayerOne, new[] { caster });
            return world;
        }

        private static SkillGraphTestWorld CreateAreaWorld(
            out Unit caster, out Unit center, out Unit cross, out Unit diagonal)
        {
            var world = new SkillGraphTestWorld();
            for (int x = 0; x <= 4; x++)
            for (int y = 0; y <= 3; y++)
                world.CreateSquareCell($"Cell{x}_{y}", x, y);
            caster = world.CreateUnit("Necromancer", 0, Cell(world, 0, 1));
            center = world.CreateUnit("Center", 1, Cell(world, 3, 1));
            cross = world.CreateUnit("Cross", 1, Cell(world, 3, 2));
            diagonal = world.CreateUnit("Diagonal", 1, Cell(world, 4, 2));
            Prepare(caster, center, cross, diagonal);
            world.SetTurnContext(world.PlayerOne, new[] { caster });
            world.SetTurnContext(world.PlayerTwo, new[] { center, cross, diagonal });
            return world;
        }

        private static void Prepare(params Unit[] units)
        {
            foreach (var unit in units)
            {
                unit.MaxHealth = 20f;
                unit.Health = 20f;
                unit.MaxMana = 100f;
                unit.Mana = 100f;
                unit.DefenceFactor = 0;
                unit.Luck = 0;
            }
        }

        private static ICell Cell(SkillGraphTestWorld world, int x, int y) =>
            world.CellManager.GetCellAt(new Tactics.Common.Utilities.Vector2IntImpl(x, y));

        private static bool Has(IUnit unit, BuffEffectType type) =>
            unit.GetActiveBuffs().Any(buff => buff.Config.EffectType == type);

        private static IEnumerator Execute(
            SkillGraphTestWorld world, Unit caster, ICell target, string configFile)
        {
            var config = GameAssetManager.Instance.Load<SkillGraphAbilityConfig>($"{ConfigRoot}{configFile}");
            Assert.That(config, Is.Not.Null, configFile);
            Task<SkillGraphRuntimeTestResult> task =
                new SkillGraphAbilityImpl(caster, config).ExecuteForTestAsync(target, world.GridController);
            yield return new WaitUntil(() => task.IsCompleted);
            Assert.That(task.IsFaulted, Is.False, task.Exception?.ToString());
            Assert.That(task.Result.ExecutionState, Is.EqualTo(SkillGraphExecutionState.Completed), task.Result.LastError);
        }

        private static SkillGraphAbilityImpl Ability(string configFile, Unit caster)
        {
            var config = GameAssetManager.Instance.Load<SkillGraphAbilityConfig>($"{ConfigRoot}{configFile}");
            Assert.That(config, Is.Not.Null, configFile);
            return new SkillGraphAbilityImpl(caster, config);
        }
    }
}
