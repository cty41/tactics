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

        [Test]
        public void ThrustLevels_ExposeOnlyCardinalTargetsAtTheirConfiguredRanges()
        {
            var world = new SkillGraphTestWorld();
            try
            {
                for (int x = 0; x <= 6; x++)
                for (int y = 0; y <= 6; y++)
                    world.CreateSquareCell($"Cell{x}_{y}", x, y);

                var amazon = world.CreateUnit("Amazon", 0, Cell(world, 3, 3));
                Prepare(amazon);
                var enemies = new List<Unit>();
                foreach (var coordinate in new[]
                         {
                             (1, 3), (2, 3), (4, 3), (5, 3), (3, 1), (3, 2), (3, 4), (3, 5),
                             (2, 2), (4, 2), (2, 4), (4, 4)
                         })
                {
                    var enemy = world.CreateUnit($"Enemy{coordinate.Item1}_{coordinate.Item2}", 1,
                        Cell(world, coordinate.Item1, coordinate.Item2));
                    Prepare(enemy);
                    enemies.Add(enemy);
                }

                world.SetTurnContext(world.PlayerOne, new[] { amazon });
                world.SetTurnContext(world.PlayerTwo, enemies);
                AssertThrustTargets("Thrust_Graph_Ability.asset", amazon, world, 8);
                AssertThrustTargets("Thrust_Lv2_Graph_Ability.asset", amazon, world, 12);
                AssertThrustTargets("Thrust_Lv3_Graph_Ability.asset", amazon, world, 12);
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

                foreach (string configFile in new[]
                         {
                             "MeleeAttack_Graph_Ability.asset",
                             "Thrust_Graph_Ability.asset",
                             "MultiStab_Graph_Ability.asset",
                             "PoisonSpear_Graph_Ability.asset"
                         })
                {
                    var heldSpearAttack = Ability(configFile, amazon, world);
                    Assert.That(heldSpearAttack.GetAvailability(world.GridController).State,
                        Is.EqualTo(AbilityAvailabilityState.DisabledClickable), configFile);
                    Assert.That(heldSpearAttack.GetAvailability(world.GridController).Reason,
                        Is.EqualTo("需要先回收长矛"), configFile);
                }

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
        public IEnumerator PickupSpear_RecoveryActionExecutesImmediatelyAgainstAmazonCell()
        {
            var world = CreateRectangleWorld(3, 2, out var amazon);
            try
            {
                var state = AmazonBattleState.For(world.GridController);
                Assert.That(state.DropSpear(amazon, Cell(world, 1, 1)), Is.True);
                var pickup = Ability("PickupSpear_Graph_Ability.asset", amazon, world);
                Assert.That(pickup.TargetMode, Is.EqualTo(SkillTargetMode.RecoveryAction));

                var task = pickup.ExecuteRecoveryActionAsync(world.GridController);
                yield return new WaitUntil(() => task.IsCompleted);

                Assert.That(task.Result.ExecutionState, Is.EqualTo(SkillGraphExecutionState.Completed));
                Assert.That(state.IsSpearHeld(amazon), Is.True);
            }
            finally
            {
                AmazonBattleState.For(world.GridController).Clear();
                world.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator PickupSpear_RemainsOwnedByAmazonAcrossOtherUnitTurns()
        {
            var world = CreateRectangleWorld(4, 3, out var amazon, casterY: 1);
            try
            {
                var necromancer = world.CreateUnit("Necromancer", 0, Cell(world, 0, 2));
                var enemy = world.CreateUnit("Enemy", 1, Cell(world, 3, 1));
                Prepare(necromancer, enemy);
                var state = AmazonBattleState.For(world.GridController);
                Assert.That(state.DropSpear(amazon, Cell(world, 2, 1)), Is.True);

                world.SetTurnContext(world.PlayerTwo, new[] { enemy });
                enemy.OnTurnStart(world.GridController);
                enemy.OnTurnEnd(world.GridController);
                world.SetTurnContext(world.PlayerOne, new[] { necromancer });
                necromancer.OnTurnStart(world.GridController);
                necromancer.OnTurnEnd(world.GridController);
                world.SetTurnContext(world.PlayerOne, new[] { amazon });
                amazon.CurrentCell = Cell(world, 1, 1);

                var pickup = Ability("PickupSpear_Graph_Ability.asset", amazon, world);
                Assert.That(pickup.GetAvailability(world.GridController).CanExecute, Is.True);
                Assert.That(state.GetSpearCell(amazon), Is.EqualTo(Cell(world, 2, 1)));
                Assert.That(state.GetSpearCell(necromancer), Is.Null);

                float mana = amazon.Mana;
                yield return Execute(world, amazon, amazon.CurrentCell, "PickupSpear_Graph_Ability.asset");
                Assert.That(state.IsSpearHeld(amazon), Is.True);
                Assert.That(amazon.Mana, Is.EqualTo(mana));
            }
            finally
            {
                AmazonBattleState.For(world.GridController).Clear();
                world.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator DroppedSpear_AllowsMovementAndShowsPickupGuidance()
        {
            var world = CreateRectangleWorld(5, 5, out var amazon, casterY: 1);
            try
            {
                amazon.MaxMovementPoints = 1f;
                amazon.MovementPoints = 1f;
                var blockedPickupCell = Cell(world, 3, 3);
                var blocker = world.CreateUnit("BlockedPickup", 1, blockedPickupCell);
                Prepare(blocker);

                var state = AmazonBattleState.For(world.GridController);
                var spearCell = Cell(world, 2, 2);
                Assert.That(state.DropSpear(amazon, spearCell), Is.True);

                var move = Ability("Move_Graph_Ability.asset", amazon, world);
                Assert.That(move.GetAvailability(world.GridController).CanExecute, Is.True);
                move.OnAbilitySelected(world.GridController);
                move.Display(world.GridController);

                var cellManager = world.CellManager;
                CollectionAssert.AreEquivalent(
                    new[] { spearCell },
                    cellManager.GetGuidanceCells(CellGuidanceType.SpearLocation));
                Assert.That(cellManager.GetGuidanceCells(CellGuidanceType.SpearPickup).Count, Is.EqualTo(7));
                CollectionAssert.Contains(
                    cellManager.GetGuidanceCells(CellGuidanceType.SpearPickup),
                    Cell(world, 3, 2));
                CollectionAssert.DoesNotContain(
                    cellManager.GetGuidanceCells(CellGuidanceType.SpearPickup),
                    blockedPickupCell);
                Assert.That(cellManager.ReachableCells.Intersect(
                    cellManager.GetGuidanceCells(CellGuidanceType.SpearPickup)), Is.Empty);

                var targetQuery = new AbilityTargetQuery(
                    amazon,
                    amazon.CurrentCell,
                    world.GridController,
                    world.UnitManager.GetUnits());
                CollectionAssert.DoesNotContain(
                    move.QueryTargets(targetQuery).Options.Select(option => option.TargetPoint).ToList(),
                    Cell(world, 3, 2));

                move.CleanUp(world.GridController);
                Assert.That(cellManager.GetGuidanceCells(CellGuidanceType.SpearLocation), Is.Empty);
                Assert.That(cellManager.GetGuidanceCells(CellGuidanceType.SpearPickup), Is.Empty);

                var moveTask = move.ExecuteForTestAsync(Cell(world, 1, 1), world.GridController);
                yield return new WaitUntil(() => moveTask.IsCompleted);
                Assert.That(moveTask.IsFaulted, Is.False, moveTask.Exception?.ToString());
                Assert.That(moveTask.Result.ExecutionState, Is.EqualTo(SkillGraphExecutionState.Completed));
                Assert.That(amazon.CurrentCell, Is.SameAs(Cell(world, 1, 1)));
                Assert.That(state.IsSpearHeld(amazon), Is.False);

                var pickup = Ability("PickupSpear_Graph_Ability.asset", amazon, world);
                Assert.That(pickup.GetAvailability(world.GridController).CanExecute, Is.True);
                float mana = amazon.Mana;
                var pickupTask = pickup.ExecuteForTestAsync(amazon.CurrentCell, world.GridController);
                yield return new WaitUntil(() => pickupTask.IsCompleted);
                Assert.That(pickupTask.IsFaulted, Is.False, pickupTask.Exception?.ToString());
                Assert.That(pickupTask.Result.ExecutionState, Is.EqualTo(SkillGraphExecutionState.Completed));
                Assert.That(state.IsSpearHeld(amazon), Is.True);
                Assert.That(amazon.Mana, Is.EqualTo(mana));
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

        private static void AssertThrustTargets(
            string configFile, Unit amazon, SkillGraphTestWorld world, int expectedCount)
        {
            var config = GameAssetManager.Instance.Load<SkillGraphAbilityConfig>($"{ConfigRoot}{configFile}");
            Assert.That(config, Is.Not.Null, configFile);
            int selectionRange = config.SkillGraph?.Nodes
                .OfType<SelectPrimaryTargetNodeRecord>()
                .Select(node => node.MaxRange)
                .FirstOrDefault() ?? 0;
            var ability = new SkillGraphAbilityImpl(amazon, config);
            ability.Initialize(world.GridController);
            ability.OnAbilitySelected(world.GridController);
            var query = new AbilityTargetQuery(amazon, amazon.CurrentCell, world.GridController,
                world.UnitManager.GetUnits());
            var targets = ability.QueryTargets(query).Options
                .Select(option => option.TargetPoint)
                .ToList();

            Assert.That(targets, NUnit.Framework.Has.Count.EqualTo(expectedCount),
                $"{configFile}: config range={config.TargetRange}, selection range={selectionRange}, " +
                $"targets={string.Join(",", targets.Select(cell => cell.GridCoordinates))}");
            CollectionAssert.DoesNotContain(targets, Cell(world, 2, 2), configFile);
            CollectionAssert.DoesNotContain(targets, Cell(world, 4, 2), configFile);
            CollectionAssert.DoesNotContain(targets, Cell(world, 2, 4), configFile);
            CollectionAssert.DoesNotContain(targets, Cell(world, 4, 4), configFile);
            Assert.That(targets.All(cell => (cell.GridCoordinates.x == 3) ^ (cell.GridCoordinates.y == 3)),
                Is.True, configFile);
            ability.CleanUp(world.GridController);
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
