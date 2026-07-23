using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Tactics.Common.AI.MonsterAI;
using Tactics.Common.Battle;
using Tactics.Common.Controllers;
using Tactics.Common.Controllers.GameResolvers;
using Tactics.Common.Players;
using Tactics.Common.Skills.Graph.Testing;
using Tactics.Common.Testing.Gameplay;
using Tactics.Common.Units;
using Tactics.Common.Units.Buffs;
using Tactics.Common.Units.Abilities;
using Tactics.Roguelike;
using Tactics.RoguelikeMap;
using Tactics.RoguelikeMap.Interaction;
using Tactics.Roster;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tactics.Tests.PlayMode
{
    public class PureRunEncounterAndSummaryTests
    {
        [SetUp]
        public void SetUp()
        {
            PureRunSessionStore.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            PureRunSessionStore.Clear();
            TestGameAssetHelper.Cleanup();
        }

        [Test]
        public void EncounterRecipes_PreserveMultipliersBlockersAndDistinctBrains()
        {
            var elite = EncounterResolver.Resolve("E1", 10);
            var special = EncounterResolver.Resolve("Special", 10);

            Assert.That(elite.HealthMultiplier, Is.EqualTo(1.3f));
            Assert.That(elite.OutputMultiplier, Is.EqualTo(1.15f));
            Assert.That(elite.Layout.BlockedCells.Select(cell => $"{cell.X},{cell.Y}"), Contains.Item("15,27"));
            Assert.That(special.HealthMultiplier, Is.EqualTo(1.8f));
            Assert.That(special.OutputMultiplier, Is.EqualTo(1.25f));
            Assert.That(EncounterCatalog.Monsters.Values.Select(monster => monster.AiBrainAssetPath).Distinct().Count(), Is.EqualTo(6));
            Assert.That(EncounterCatalog.Monsters[EncounterCatalog.RangedId].MinimumStartingMana, Is.EqualTo(15));
        }

        [Test]
        public void RuntimeModifiers_ScaleHealthOutputAndStartingMana()
        {
            using var world = new SkillGraphTestWorld();
            var attacker = world.CreateUnit("EncounterAttacker", 1);
            var target = world.CreateUnit("Target", 0);
            var modifiers = attacker.gameObject.AddComponent<EncounterUnitRuntimeModifiers>();
            modifiers.Configure("elite", 1.3f, 1.25f, 15);

            float baseMaxHealth = attacker.MaxHealth;
            modifiers.ApplyAfterUnitInitialization(attacker);
            Assert.That(attacker.MaxHealth, Is.EqualTo(Mathf.CeilToInt(baseMaxHealth * 1.3f)));
            Assert.That(attacker.Health, Is.EqualTo(attacker.MaxHealth));
            Assert.That(attacker.Mana, Is.GreaterThanOrEqualTo(15));

            target.Health = 100f;
            target.MaxHealth = 100f;
            var resolution = CombatComponent.ApplyDamage(
                attacker,
                target,
                8f,
                false,
                DamageCategory.Magic,
                ElementType.None,
                false,
                false,
                false,
                bypassDefense: true);
            Assert.That(resolution.DamageApplied, Is.EqualTo(10f).Within(0.001f));
            Assert.That(target.Health, Is.EqualTo(90f).Within(0.001f));
        }

        [Test]
        public void BattleRewards_PlayerDefeatReturnsZero()
        {
            var human = new TestPlayer(0, PlayerType.HumanPlayer);
            var ai = new TestPlayer(1, PlayerType.AutomatedPlayer);
            var defeat = BattleRewardSystem.CalculateBattleRewards(
                new GameResult(ai, new[] { human }),
                2,
                System.Array.Empty<IUnit>());

            Assert.That(defeat.TotalGold, Is.Zero);
            Assert.That(defeat.ExperiencePerCharacter, Is.Empty);
            Assert.That(defeat.ItemIds, Is.Empty);
            Assert.That(defeat.EnemiesDefeated, Is.Zero);
        }

        [Test]
        public void Summary_IsIdempotentTracksGrossAcquisitionAndSurvivesInventoryChanges()
        {
            var state = PlayerAdventureStateStore.CreatePureRunState(1203);
            var mapConfig = ScriptableObject.CreateInstance<RoguelikeMapConfig>();
            var map = RoguelikeMapGenerator.GetPureRunMap(mapConfig, 1203);
            PureRunSessionStore.StartNew(state, map);

            var acquisition = RewardResult.Empty();
            acquisition.GoldAmount = 20;
            acquisition.ItemIds.Add("life_potion");
            Assert.That(RoguelikeNodeTransactionService.TryApplyOnce(state, "test:reward", acquisition), Is.True);
            Assert.That(RoguelikeNodeTransactionService.TryApplyOnce(state, "test:reward", acquisition), Is.False);

            var purchase = RewardResult.GoldCostResult(5);
            Assert.That(RoguelikeNodeTransactionService.TryApplyOnce(state, "test:purchase", purchase), Is.True);
            state.ConsumableInstances.Clear();
            Assert.That(PureRunSummaryRecorder.RecordNodeCompletion(state, "event-node", RoguelikeNodeType.Mystery), Is.True);
            Assert.That(PureRunSummaryRecorder.RecordNodeCompletion(state, "event-node", RoguelikeNodeType.Mystery), Is.False);
            PlayerAdventureStateStore.Save(state);

            Assert.That(state.Gold, Is.EqualTo(15));
            Assert.That(state.CurrentRunSummary.totalGold, Is.EqualTo(20));
            Assert.That(state.CurrentRunSummary.acquiredItems, Is.EqualTo(new[] { "life_potion" }));
            Assert.That(state.CurrentRunSummary.nodesVisited, Is.EqualTo(1));
            Assert.That(state.CurrentRunSummary.eventsCompleted, Is.EqualTo(1));

            var snapshot = PureRunSessionStore.Finish(PureRunEndReason.Defeat);
            Assert.That(PureRunSessionStore.HasActiveRun, Is.False);
            Assert.That(snapshot.totalGold, Is.EqualTo(20));
            Assert.That(snapshot.acquiredItems, Does.Contain("life_potion"));
            Assert.That(snapshot.GetRunOutcome(), Is.EqualTo(RunOutcome.Defeat));
            Assert.That(PureRunSessionStore.TryLoadCompletedSummary(out var reloaded), Is.True);
            Assert.That(reloaded.totalGold, Is.EqualTo(20));

            PureRunSessionStore.ConsumeCompletedSummary();
            Assert.That(PureRunSessionStore.TryLoadCompletedSummary(out _), Is.False);
            Object.DestroyImmediate(mapConfig);
        }

        [UnityTest]
        public IEnumerator EncounterBrains_LoadWithDistinctProfilesAndValidPatterns()
        {
            var task = TestGameAssetHelper.EnsureInitialized();
            yield return new WaitUntil(() => task.IsCompleted);
            Assert.That(task.Result, Is.Not.Null);

            string[] names = { "Charger", "Ranged", "AOE", "Support", "EliteCharger", "ElitePoisonCaster" };
            var profiles = new HashSet<AIProfile>();
            foreach (string name in names)
            {
                string path = $"Assets/Tactics/AI/Encounters/{name}Brain.asset";
                var brain = task.Result.Load<AiBrainAsset>(path);
                Assert.That(brain, Is.Not.Null, path);
                Assert.That(brain.IsValid(), Is.True, path);
                Assert.That(brain.Profile, Is.Not.Null, path);
                profiles.Add(brain.Profile);
                task.Result.Release(path);
            }

            Assert.That(profiles.Count, Is.EqualTo(6));
            var eliteCharger = task.Result.Load<AiBrainAsset>("Assets/Tactics/AI/Encounters/EliteChargerBrain.asset");
            var eliteCaster = task.Result.Load<AiBrainAsset>("Assets/Tactics/AI/Encounters/ElitePoisonCasterBrain.asset");
            Assert.That(eliteCharger.PatternSteps.Select(step => step.AbilityName), Is.EqualTo(new[] { "Charge Strike Lv1", "Melee Attack" }));
            Assert.That(eliteCaster.PatternSteps.Select(step => step.AbilityName), Is.EqualTo(new[] { "Area Blast Lv1", "Melee Attack" }));
            task.Result.Release("Assets/Tactics/AI/Encounters/EliteChargerBrain.asset");
            task.Result.Release("Assets/Tactics/AI/Encounters/ElitePoisonCasterBrain.asset");
        }

        [UnityTest]
        public IEnumerator EncounterAi_RejectsFriendlyFireCentersAndRepeatedCurseTargets()
        {
            var task = TestGameAssetHelper.EnsureInitialized();
            yield return new WaitUntil(() => task.IsCompleted);
            Assert.That(task.Result, Is.Not.Null);

            var curseConfig = task.Result.Load<AbilityConfig>(
                "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/Curse_Graph_Ability.asset");
            var areaConfig = task.Result.Load<AbilityConfig>(
                "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/AreaBlast_Lv1_Ability.asset");
            var supportBrain = task.Result.Load<AiBrainAsset>("Assets/Tactics/AI/Encounters/SupportBrain.asset");
            var aoeBrain = task.Result.Load<AiBrainAsset>("Assets/Tactics/AI/Encounters/AOEBrain.asset");

            using var world = new SkillGraphTestWorld();
            for (int x = 0; x < 4; x++)
            for (int y = 0; y < 3; y++)
                world.CreateSquareCell($"Cell_{x}_{y}", x, y);

            var support = world.CreateUnit("Support", 1, world.CellManager.GetCellAt(new Tactics.Common.Utilities.Vector2IntImpl(0, 0)));
            var cursed = world.CreateUnit("Cursed", 0, world.CellManager.GetCellAt(new Tactics.Common.Utilities.Vector2IntImpl(1, 0)));
            var fresh = world.CreateUnit("Fresh", 0, world.CellManager.GetCellAt(new Tactics.Common.Utilities.Vector2IntImpl(2, 0)));
            support.ApplyAbilityConfigs(new[] { curseConfig });
            support.Initialize(world.GridController);
            support.MaxMana = support.Mana = 100;
            world.SetTurnContext(world.PlayerTwo, new[] { support });

            var curseGraph = ((SkillGraphAbilityConfig)curseConfig).SkillGraph;
            var harmful = curseGraph.Nodes.OfType<Tactics.Common.Skills.Graph.NecromancerSkillNodeRecord>()
                .SelectMany(node => new[] { node.AmplifyDamageBuff, node.FearBuff })
                .First(config => config != null && config.Polarity == BuffPolarity.Harmful);
            cursed.BuffComponent.AddBuff(new Buff(harmful, support, harmful.DefaultDuration));

            var supportCandidates = IntentGenerator.Generate(AiContextBuilder.Build(support, world.GridController, supportBrain));
            var curseCandidates = supportCandidates
                .Where(candidate => candidate.Ability?.HasTag(AbilityAiTags.Debuff) == true)
                .ToList();
            Assert.That(curseCandidates, Is.Not.Empty);
            Assert.That(curseCandidates.Any(candidate => ReferenceEquals(candidate.Target, fresh)), Is.True);
            Assert.That(curseCandidates.Any(candidate => ReferenceEquals(candidate.Target, cursed)), Is.False);

            var aoe = world.CreateUnit("AOE", 1, world.CellManager.GetCellAt(new Tactics.Common.Utilities.Vector2IntImpl(0, 2)));
            var ally = world.CreateUnit("AOEAlly", 1, world.CellManager.GetCellAt(new Tactics.Common.Utilities.Vector2IntImpl(1, 1)));
            aoe.ApplyAbilityConfigs(new[] { areaConfig });
            aoe.Initialize(world.GridController);
            aoe.MaxMana = aoe.Mana = 100;
            world.SetTurnContext(world.PlayerTwo, new[] { aoe });
            var aoeCandidates = IntentGenerator.Generate(AiContextBuilder.Build(aoe, world.GridController, aoeBrain))
                .Where(candidate => candidate.Ability?.HasTag(AbilityAiTags.Aoe) == true)
                .ToList();
            Assert.That(aoeCandidates, Is.Not.Empty);
            Assert.That(aoeCandidates.All(candidate => candidate.EstimatedFriendlyFireDamage <= 0f), Is.True);

            task.Result.Release("Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/Curse_Graph_Ability.asset");
            task.Result.Release("Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/AreaBlast_Lv1_Ability.asset");
            task.Result.Release("Assets/Tactics/AI/Encounters/SupportBrain.asset");
            task.Result.Release("Assets/Tactics/AI/Encounters/AOEBrain.asset");
        }

        [UnityTest]
        public IEnumerator EncounterAi_RetreatsOnlyWhileAnEnemyIsWithinThreatDistance()
        {
            var task = TestGameAssetHelper.EnsureInitialized();
            yield return new WaitUntil(() => task.IsCompleted);
            Assert.That(task.Result, Is.Not.Null);

            const string brainPath = "Assets/Tactics/AI/Encounters/RangedBrain.asset";
            var brain = task.Result.Load<AiBrainAsset>(brainPath);
            Assert.That(brain, Is.Not.Null);

            using var world = new SkillGraphTestWorld();
            for (int x = 0; x < 24; x++)
                world.CreateSquareCell($"Cell_{x}_0", x, 0);

            var ranged = world.CreateUnit(
                "Ranged",
                1,
                world.CellManager.GetCellAt(new Tactics.Common.Utilities.Vector2IntImpl(0, 0)));
            var enemy = world.CreateUnit(
                "Enemy",
                0,
                world.CellManager.GetCellAt(new Tactics.Common.Utilities.Vector2IntImpl(3, 0)));
            ranged.MaxHealth = 10;
            ranged.Health = 1;
            ranged.MaxMovementPoints = 5;
            ranged.AttackRange = 3;
            enemy.AttackRange = 3;

            var threatenedCandidates = IntentGenerator.Generate(
                AiContextBuilder.Build(ranged, world.GridController, brain));
            Assert.That(
                threatenedCandidates.Any(candidate => candidate.IntentType == IntentType.Retreat),
                Is.True);

            enemy.CurrentCell = world.CellManager.GetCellAt(
                new Tactics.Common.Utilities.Vector2IntImpl(23, 0));
            var safeCandidates = IntentGenerator.Generate(
                AiContextBuilder.Build(ranged, world.GridController, brain));
            Assert.That(
                safeCandidates.Any(candidate => candidate.IntentType == IntentType.Retreat),
                Is.False);

            task.Result.Release(brainPath);
        }

        private sealed class TestPlayer : IPlayer
        {
            public TestPlayer(int number, PlayerType type)
            {
                PlayerNumber = number;
                PlayerType = type;
            }

            public int PlayerNumber { get; set; }
            public PlayerType PlayerType { get; set; }
            public void Initialize(GridController gridController) { }
            public void Play(GridController gridController) { }
        }
    }
}
