using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Tactics.AssetPipeline;
using Tactics.Common.Battle;
using Tactics.Common.AI.MonsterAI;
using Tactics.Common.Cells;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Skills.Graph.Testing;
using Tactics.Common.Testing.Gameplay;
using Tactics.Common.Units;
using Tactics.Common.Units.Abilities;
using Tactics.Common.Units.Buffs;
using Tactics.Common.Utilities;
using Tactics.Roster;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tactics.Tests.PlayMode
{
    public sealed class AmazonSkillLevelTests
    {
        private const string ConfigRoot = "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/";

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            CombatComponent.SetCombatTechniqueRollForTests(null);
            var task = TestGameAssetHelper.EnsureInitialized();
            yield return new WaitUntil(() => task.IsCompleted);
            Assert.That(task.Result, Is.Not.Null);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            CombatComponent.SetCombatTechniqueRollForTests(null);
            TestGameAssetHelper.Cleanup();
            yield return null;
        }

        [Test]
        public void PublishedAmazonLevels_LoadRealGraphsAndPickupUtility()
        {
            var expected = new Dictionary<string, int>
            {
                ["amazon.thrust"] = 3,
                ["amazon.poison_spear"] = 3,
                ["amazon.combat_techniques"] = 3,
                ["amazon.multi_stab"] = 2,
                ["amazon.recover_spear"] = 2,
                ["amazon.decoy"] = 2
            };
            foreach (var pair in expected)
            {
                Assert.That(PureRunAbilityCatalog.TryGet(pair.Key, out var definition), Is.True, pair.Key);
                Assert.That(Enumerable.Range(1, pair.Value).All(definition.IsLevelImplemented), Is.True, pair.Key);
                if (definition.SkillType == SkillType.Passive)
                    continue;
                foreach (int level in Enumerable.Range(1, pair.Value))
                {
                    Assert.That(PureRunAbilityCatalog.TryResolveAbilityPath(pair.Key, level, out string path, out int resolved), Is.True);
                    Assert.That(resolved, Is.EqualTo(level));
                    var config = GameAssetManager.Instance.Load<SkillGraphAbilityConfig>(path);
                    Assert.That(config?.SkillGraph?.Nodes.OfType<AmazonSkillNodeRecord>().Single(), Is.Not.Null, path);
                }
            }

            Assert.That(PureRunAbilityCatalog.TryResolveAbilityPath(
                PureRunAbilityCatalog.PickupSpearSkillId, 1, out string pickupPath, out _), Is.True);
            Assert.That(GameAssetManager.Instance.Load<SkillGraphAbilityConfig>(pickupPath), Is.Not.Null);
        }

        [Test]
        public async Task GameplayRunner_ExecutesAmazonPoisonSpearPlan()
        {
            string path = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", "Tests", "gameplay-specs", "amazon",
                "amazon-poison-spear-level-two.plan.json"));
            Assert.That(File.Exists(path), Is.True, path);
            var plan = ExecutableScenarioPlanLoader.FromFile(path);
            var result = await new GameplayRuntimeRunner().ExecuteAsync(plan);
            Assert.That(result.Passed, Is.True, string.Join("\n", result.Diagnostics));
        }

        [UnityTest]
        public IEnumerator ThrustLevels_ExpandLengthAndConsumeUncappedMovementBonusOnSuccess()
        {
            var world = CreateRectangleWorld(5, 1, out var amazon);
            try
            {
                var first = world.CreateUnit("First", 1, Cell(world, 1, 0));
                var second = world.CreateUnit("Second", 1, Cell(world, 2, 0));
                var third = world.CreateUnit("Third", 1, Cell(world, 3, 0));
                Prepare(first, second, third);
                world.SetTurnContext(world.PlayerTwo, new[] { first, second, third });

                yield return Execute(world, amazon, first.CurrentCell, "Thrust_Graph_Ability.asset");
                Assert.That(first.Health, Is.EqualTo(34f));
                Assert.That(second.Health, Is.EqualTo(34f));
                Assert.That(third.Health, Is.EqualTo(40f));

                first.Health = second.Health = third.Health = 40f;
                var state = AmazonBattleState.For(world.GridController);
                _ = state.GetActiveMovement(amazon);
                amazon.InvokeUnitMoved(new UnitMovedEventArgs(amazon, amazon.CurrentCell, amazon.CurrentCell,
                    new[] { Cell(world, 1, 0), Cell(world, 2, 0), Cell(world, 3, 0), Cell(world, 4, 0) }));
                yield return Execute(world, amazon, first.CurrentCell, "Thrust_Lv3_Graph_Ability.asset");
                Assert.That(first.Health, Is.EqualTo(30f));
                Assert.That(second.Health, Is.EqualTo(30f));
                Assert.That(third.Health, Is.EqualTo(30f));
                Assert.That(state.GetActiveMovement(amazon), Is.Zero);
            }
            finally
            {
                world.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator PoisonSpearAndRecovery_ShareUniqueBlockingSpearAndApplyLevelAreas()
        {
            var world = CreateRectangleWorld(7, 3, out var amazon, casterY: 1);
            try
            {
                var adjacent = world.CreateUnit("Adjacent", 1, Cell(world, 0, 2));
                var target = world.CreateUnit("Target", 1, Cell(world, 3, 1));
                var cross = world.CreateUnit("Cross", 1, Cell(world, 3, 2));
                var diagonal = world.CreateUnit("Diagonal", 1, Cell(world, 4, 2));
                Prepare(adjacent, target, cross, diagonal);
                world.SetTurnContext(world.PlayerTwo, new[] { adjacent, target, cross, diagonal });

                yield return Execute(world, amazon, target.CurrentCell, "PoisonSpear_Lv2_Graph_Ability.asset");
                var state = AmazonBattleState.For(world.GridController);
                Assert.That(target.Health, Is.EqualTo(30f));
                Assert.That(Has(target, BuffEffectType.Poison), Is.True);
                Assert.That(Has(cross, BuffEffectType.Poison), Is.True);
                Assert.That(Has(diagonal, BuffEffectType.Poison), Is.False);
                Assert.That(state.IsSpearHeld(amazon), Is.False);
                Assert.That(state.GetSpearCell(amazon).IsTaken, Is.True);

                var thrust = Ability("Thrust_Graph_Ability.asset", amazon, world);
                Assert.That(thrust.GetAvailability(world.GridController).State,
                    Is.EqualTo(AbilityAvailabilityState.DisabledClickable));
                Assert.That(thrust.GetAvailability(world.GridController).Reason, Is.EqualTo("需要先回收长矛"));

                var spearCell = state.GetSpearCell(amazon);
                yield return Execute(world, amazon, spearCell, "RecoverSpear_Lv2_Graph_Ability.asset");
                Assert.That(state.IsSpearHeld(amazon), Is.True);
                Assert.That(adjacent.Health, Is.EqualTo(34f));
            }
            finally
            {
                AmazonBattleState.For(world.GridController).Clear();
                world.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator PickupSpear_RequiresEightDirectionAdjacencyAndCostsNoMana()
        {
            var world = CreateRectangleWorld(3, 2, out var amazon);
            try
            {
                var state = AmazonBattleState.For(world.GridController);
                Assert.That(state.DropSpear(amazon, Cell(world, 1, 1)), Is.True);
                float mana = amazon.Mana;
                yield return Execute(world, amazon, amazon.CurrentCell, "PickupSpear_Graph_Ability.asset");
                Assert.That(state.IsSpearHeld(amazon), Is.True);
                Assert.That(amazon.Mana, Is.EqualTo(mana));

                Assert.That(state.DropSpear(amazon, Cell(world, 2, 1)), Is.True);
                var pickup = Ability("PickupSpear_Graph_Ability.asset", amazon, world);
                Assert.That(pickup.GetAvailability(world.GridController).Reason, Is.EqualTo("需要移动到长矛相邻格"));
            }
            finally
            {
                AmazonBattleState.For(world.GridController).Clear();
                world.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator MultiStab_UsesOrderedDuplicatePreservingTargets()
        {
            var world = CreateRectangleWorld(4, 3, out var amazon, casterY: 1);
            try
            {
                amazon.Facing = FacingDirection.East;
                var near = world.CreateUnit("Near", 1, Cell(world, 1, 1));
                var flank = world.CreateUnit("Flank", 1, Cell(world, 2, 2));
                Prepare(near, flank);
                world.SetTurnContext(world.PlayerTwo, new[] { near, flank });
                var ability = Ability("MultiStab_Graph_Ability.asset", amazon, world);
                Task<SkillGraphRuntimeTestResult> task = ability.ExecuteOrderedForTestAsync(
                    new IUnit[] { near, flank, near }, world.GridController);
                yield return new WaitUntil(() => task.IsCompleted);
                Assert.That(task.Result.ExecutionState, Is.EqualTo(SkillGraphExecutionState.Completed), task.Result.LastError);
                Assert.That(near.Health, Is.EqualTo(32f));
                Assert.That(flank.Health, Is.EqualTo(36f));
                Assert.That(amazon.Facing, Is.EqualTo(FacingDirection.East));
            }
            finally
            {
                world.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator Decoy_MovesWithoutMovementCost_CleansesAtLevelTwo_AndExpiresOnFourthTurnStart()
        {
            var world = CreateRectangleWorld(3, 2, out var amazon);
            try
            {
                var poison = GameAssetManager.Instance.Load<BuffConfig>("Assets/Tactics/ScriptableObjects/Buffs/Poison.asset");
                amazon.AddBuff(new Buff(poison, amazon, 3));
                float movement = amazon.MovementPoints;
                var facing = amazon.Facing;
                var origin = amazon.CurrentCell;
                yield return Execute(world, amazon, Cell(world, 1, 0), "Decoy_Lv2_Graph_Ability.asset");

                var state = AmazonBattleState.For(world.GridController);
                var decoy = state.GetDecoy(amazon);
                Assert.That(amazon.CurrentCell, Is.EqualTo(Cell(world, 1, 0)));
                Assert.That(amazon.MovementPoints, Is.EqualTo(movement));
                Assert.That(amazon.Facing, Is.EqualTo(facing));
                Assert.That(Has(amazon, BuffEffectType.Poison), Is.False);
                Assert.That(decoy.CurrentCell, Is.EqualTo(origin));
                Assert.That(decoy.MaxHealth, Is.EqualTo(Mathf.Floor(amazon.MaxHealth * 0.5f)));
                Assert.That(AmazonBattleState.IsDecoy(decoy), Is.True);
                decoy.AddBuff(new Buff(poison, amazon, 3));
                Assert.That(decoy.GetActiveBuffs(), Is.Empty);

                state.OnOwnerTurnStart(amazon);
                state.OnOwnerTurnStart(amazon);
                Assert.That(state.GetDecoy(amazon), Is.Not.Null);
                state.OnOwnerTurnStart(amazon);
                Assert.That(state.GetDecoy(amazon), Is.Null);
            }
            finally
            {
                AmazonBattleState.For(world.GridController).Clear();
                world.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator DroppedSpear_DoesNotBlockProjectileLineOfSight()
        {
            var world = CreateRectangleWorld(3, 1, out var amazon);
            try
            {
                var enemy = world.CreateUnit("Enemy", 1, Cell(world, 2, 0));
                Prepare(enemy);
                world.SetTurnContext(world.PlayerTwo, new[] { enemy });
                Assert.That(AmazonBattleState.For(world.GridController).DropSpear(amazon, Cell(world, 1, 0)), Is.True);

                yield return Execute(world, amazon, enemy.CurrentCell, "BoneSpear_Graph_Ability.asset");
                Assert.That(enemy.Health, Is.EqualTo(33f));
            }
            finally
            {
                AmazonBattleState.For(world.GridController).Clear();
                world.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator Decoy_WhenReachable_RestrictsEnemyAiCandidatesToDecoy()
        {
            var world = CreateRectangleWorld(5, 1, out var amazon);
            try
            {
                var enemy = world.CreateUnit("Enemy", 1, Cell(world, 4, 0));
                Prepare(enemy);
                world.SetTurnContext(world.PlayerTwo, new[] { enemy });
                yield return Execute(world, amazon, Cell(world, 1, 0), "Decoy_Graph_Ability.asset");
                var decoy = AmazonBattleState.For(world.GridController).GetDecoy(amazon);
                var brain = GameAssetManager.Instance.Load<AiBrainAsset>("Assets/Tactics/AI/BasicMeleeBrain.asset");

                var context = AiContextBuilder.Build(enemy, world.GridController, brain);
                var candidates = IntentGenerator.Generate(context);
                Assert.That(candidates, Is.Not.Empty);
                Assert.That(candidates.All(candidate => ReferenceEquals(candidate.Target, decoy)), Is.True);
            }
            finally
            {
                AmazonBattleState.For(world.GridController).Clear();
                world.Dispose();
            }
        }

        [Test]
        public void CombatTechniques_UseUnifiedDodgeFollowUpAndCriticalLevelRules()
        {
            var world = CreateRectangleWorld(2, 1, out var amazon);
            try
            {
                var attacker = world.CreateUnit("Attacker", 1, Cell(world, 1, 0));
                Prepare(attacker);
                CombatComponent.EnableCombatTechniques(amazon, 1);
                CombatComponent.SetCombatTechniqueRollForTests(0.2d);
                var result = CombatComponent.ApplyDamage(attacker, amazon, 5f, false,
                    DamageCategory.Magic, ElementType.Fire, true, false, true);
                Assert.That(result.WasDodged, Is.True);

                CombatComponent.SetCombatTechniqueRollForTests(0.5d);
                result = CombatComponent.ApplyDamage(attacker, amazon, 5f, false,
                    DamageCategory.Physical, ElementType.None, true, false, true,
                    accuracyPenalty: 0.40f);
                Assert.That(result.WasDodged, Is.True,
                    "Accuracy penalty and Combat Techniques must share one combined dodge roll.");

                CombatComponent.SetCombatTechniqueRollForTests(0.8d);
                result = CombatComponent.ApplyDamage(attacker, amazon, 5f, false,
                    DamageCategory.Physical, ElementType.None, true, false, true,
                    accuracyPenalty: 0.40f);
                Assert.That(result.WasHit, Is.True);

                CombatComponent.EnableCombatTechniques(amazon, 2);
                CombatComponent.SetCombatTechniqueRollForTests(0.2d);
                Assert.That(CombatComponent.RollCombatTechniqueFollowUp(amazon), Is.True);
                CombatComponent.EnableCombatTechniques(amazon, 3);
                amazon.Luck = 5;
                Assert.That(CombatComponent.GetClampedCritChance(amazon), Is.EqualTo(0.30f).Within(0.001f));
            }
            finally
            {
                world.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator CombatTechniquesLevelTwo_TriggersOneNonRecursiveBasicFollowUp()
        {
            var world = CreateRectangleWorld(2, 1, out var amazon);
            try
            {
                var target = world.CreateUnit("Target", 1, Cell(world, 1, 0));
                Prepare(target);
                world.SetTurnContext(world.PlayerTwo, new[] { target });
                CombatComponent.EnableCombatTechniques(amazon, 2);
                CombatComponent.SetCombatTechniqueRollForTests(0d);

                var ability = Ability("MeleeAttack_Graph_Ability.asset", amazon, world);
                var task = ability.ExecuteForTestAsync(target.CurrentCell, world.GridController);
                yield return new WaitUntil(() => task.IsCompleted);

                Assert.That(task.Result.ExecutionState, Is.EqualTo(SkillGraphExecutionState.Completed));
                Assert.That(task.Result.ExecutionEvents.Count(entry => entry.EventType == "CombatTechniqueFollowUp"),
                    Is.EqualTo(1));
                Assert.That(target.Health, Is.LessThan(40f));
            }
            finally
            {
                world.Dispose();
            }
        }

        private static SkillGraphTestWorld CreateRectangleWorld(
            int width, int height, out Unit amazon, int casterY = 0)
        {
            var world = new SkillGraphTestWorld();
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                world.CreateSquareCell($"Cell{x}_{y}", x, y);
            amazon = world.CreateUnit("Amazon", 0, Cell(world, 0, casterY));
            amazon.ApplyLearnedSkillLevels(new[]
            {
                new CharacterDefinition.LearnedSkill { SkillId = "amazon.thrust", Level = 3, SkillType = SkillType.Active },
                new CharacterDefinition.LearnedSkill { SkillId = "amazon.poison_spear", Level = 3, SkillType = SkillType.Active },
                new CharacterDefinition.LearnedSkill { SkillId = "amazon.combat_techniques", Level = 3, SkillType = SkillType.Passive }
            });
            Prepare(amazon);
            world.SetTurnContext(world.PlayerOne, new[] { amazon });
            return world;
        }

        private static void Prepare(params Unit[] units)
        {
            foreach (var unit in units)
            {
                unit.MaxHealth = 40f;
                unit.Health = 40f;
                unit.MaxMana = 100f;
                unit.Mana = 100f;
                unit.DefenceFactor = 0;
                unit.Luck = -100;
                unit.DodgeRate = 0f;
            }
        }

        private static bool Has(IUnit unit, BuffEffectType effect) =>
            unit.GetActiveBuffs().Any(buff => buff.Config.EffectType == effect);

        private static ICell Cell(SkillGraphTestWorld world, int x, int y) =>
            world.CellManager.GetCellAt(new Vector2IntImpl(x, y));

        private static SkillGraphAbilityImpl Ability(
            string configFile, Unit owner, SkillGraphTestWorld world)
        {
            var config = GameAssetManager.Instance.Load<SkillGraphAbilityConfig>($"{ConfigRoot}{configFile}");
            Assert.That(config, Is.Not.Null, configFile);
            var ability = new SkillGraphAbilityImpl(owner, config);
            ability.Initialize(world.GridController);
            return ability;
        }

        private static IEnumerator Execute(
            SkillGraphTestWorld world, Unit caster, ICell target, string configFile)
        {
            var ability = Ability(configFile, caster, world);
            Task<SkillGraphRuntimeTestResult> task = ability.ExecuteForTestAsync(target, world.GridController);
            yield return new WaitUntil(() => task.IsCompleted);
            Assert.That(task.IsFaulted, Is.False, task.Exception?.ToString());
            Assert.That(task.Result.ExecutionState, Is.EqualTo(SkillGraphExecutionState.Completed), task.Result.LastError);
        }
    }
}
