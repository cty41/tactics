using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Tactics.AssetPipeline;
using Tactics.Common.Units.Buffs;
using Tactics.Common.Testing.Gameplay;
using Tactics.Roguelike;
using Tactics.RoguelikeMap;
using Tactics.RoguelikeMap.Events;
using Tactics.RoguelikeMap.Interaction;
using Tactics.Roster;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tactics.Tests.PlayMode
{
    public class PureRunNodeTransactionTests
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
        }

        [Test]
        public void AttributeCheck_UsesFiveAsBaselineAndClamps()
        {
            Assert.That(AttributeCheckSystem.CalculateSuccessRate(60, 5), Is.EqualTo(60));
            Assert.That(AttributeCheckSystem.CalculateSuccessRate(60, 10), Is.EqualTo(85));
            Assert.That(AttributeCheckSystem.CalculateSuccessRate(5, 0), Is.EqualTo(5));
            Assert.That(AttributeCheckSystem.CalculateSuccessRate(95, 20), Is.EqualTo(95));
        }

        [Test]
        public void PureRunMap_AssignsStableDistinctMysteryEventsAndPreservesExistingAssignment()
        {
            var config = ScriptableObject.CreateInstance<RoguelikeMapConfig>();
            try
            {
                var first = RoguelikeMapGenerator.GetPureRunMap(config, 7301);
                var second = RoguelikeMapGenerator.GetPureRunMap(config, 7301);
                var firstIds = first.nodes.Where(node => node.nodeType == RoguelikeNodeType.Mystery)
                    .OrderBy(node => node.LayerIndex).Select(node => node.eventId).ToList();
                var secondIds = second.nodes.Where(node => node.nodeType == RoguelikeNodeType.Mystery)
                    .OrderBy(node => node.LayerIndex).Select(node => node.eventId).ToList();

                Assert.That(firstIds, Is.EqualTo(secondIds));
                Assert.That(firstIds.Distinct().Count(), Is.EqualTo(firstIds.Count));
                Assert.That(firstIds, Is.EqualTo(new[] { "cursed_chest_001", "lost_villager_001" }));

                var layerFour = first.GetNode("layer_04_event");
                layerFour.eventId = "fallen_altar_001";
                Assert.That(RoguelikeMapGenerator.EnsurePureRunMysteryEvents(first), Is.False);
                Assert.That(layerFour.eventId, Is.EqualTo("fallen_altar_001"));
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [UnityTest]
        public IEnumerator DarkForestRuntimeConfig_LoadsAssignedEventsWhenSerializedReferencesAreNull()
        {
            const string configPath = "Assets/Tactics/RoguelikeMap/MapConfigs/DarkForestPrototypeConfig.asset";
            var assetTask = TestGameAssetHelper.EnsureInitialized();
            yield return new WaitUntil(() => assetTask.IsCompleted);
            Assert.That(assetTask.Result, Is.Not.Null);

            var sourceConfig = assetTask.Result.Load<RoguelikeMapConfig>(configPath);
            Assert.That(sourceConfig, Is.Not.Null, "The real DarkForest config must load through the runtime asset pipeline.");
            var runtimeConfig = Object.Instantiate(sourceConfig);
            var eventManager = EventManager.Instance;
            eventManager.ClearEvents();
            try
            {
                runtimeConfig.eventFiles = new List<TextAsset> { null, null, null };

                eventManager.LoadRegionEvents("DarkForest", runtimeConfig);

                Assert.That(eventManager.GetEventCount("DarkForest"), Is.GreaterThan(0));
                Assert.That(eventManager.GetEvent("cursed_chest_001"), Is.Not.Null);
            }
            finally
            {
                eventManager.ClearEvents();
                assetTask.Result.Release(configPath);
                Object.Destroy(runtimeConfig);
                TestGameAssetHelper.Cleanup();
            }
        }

        [Test]
        public void RestTransaction_AppliesOnceAcrossReloadAndConsumesOnlyOnCommit()
        {
            var config = ScriptableObject.CreateInstance<RoguelikeMapConfig>();
            try
            {
                var map = RoguelikeMapGenerator.GetPureRunMap(config, 91);
                var state = PlayerAdventureStateStore.CreatePureRunState(91);
                var mage = state.Roster.Single(character => character.Id == "pure_run_mage");
                mage.CurrentHp = 1;
                mage.CurrentMp = 1;
                PureRunSessionStore.StartNew(state, map);

                var node = map.GetNode("layer_04_rest");
                var reward = RewardResult.Empty();
                reward.HealPercent = 0.3f;
                reward.ManaHealPercent = 0.3f;
                RoguelikeNodeTransactionService.MarkResolved(node, map, "rested");
                Assert.That(RoguelikeNodeTransactionService.TryApplyOnce(
                    state,
                    node.Transaction.TransactionKey,
                    reward), Is.True);
                Assert.That(RoguelikeNodeTransactionService.TryApplyOnce(
                    state,
                    node.Transaction.TransactionKey,
                    reward), Is.False);
                Assert.That(mage.CurrentHp, Is.EqualTo(8));
                Assert.That(mage.CurrentMp, Is.EqualTo(6));
                Assert.That(node.IsConsumed, Is.False);

                Assert.That(PureRunSessionStore.TryLoad(out var reloadedState, out var reloadedMap), Is.True);
                var reloadedNode = reloadedMap.GetNode(node.nodeId);
                Assert.That(reloadedNode.Transaction.Phase, Is.EqualTo(RoguelikeNodeTransactionPhase.Resolved));
                Assert.That(reloadedState.AppliedNodeTransactionKeys.Count(key => key == node.Transaction.TransactionKey), Is.EqualTo(1));

                RoguelikeNodeTransactionService.Commit(reloadedNode, reloadedMap, true);
                Assert.That(PureRunSessionStore.TryLoad(out _, out var committedMap), Is.True);
                Assert.That(committedMap.GetNode(node.nodeId).Transaction.Phase, Is.EqualTo(RoguelikeNodeTransactionPhase.Committed));
                Assert.That(committedMap.GetNode(node.nodeId).IsConsumed, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void FormalEvents_HaveStableOptionIdsAndOnlyDefinedRewardReferences()
        {
            string[] paths =
            {
                "Assets/Tactics/GameData/Events/DarkForest/cursed_chest_001.json",
                "Assets/Tactics/GameData/Events/DarkForest/fallen_altar_001.json",
                "Assets/Tactics/GameData/Events/DarkForest/lost_villager_001.json"
            };

            foreach (string path in paths)
            {
                var evt = RoguelikeEvent.FromJson(File.ReadAllText(path));
                Assert.That(evt, Is.Not.Null, path);
                Assert.That(evt.options.All(option => !string.IsNullOrWhiteSpace(option.stableOptionId)), Is.True, path);
                Assert.That(evt.options.Select(option => option.stableOptionId).Distinct().Count(), Is.EqualTo(evt.options.Count), path);

                var referencedIds = evt.options
                    .SelectMany(option => new[] { option.success?.itemId, option.failure?.itemId })
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .ToList();
                Assert.That(referencedIds, Does.Not.Contain("holy_symbol"), path);
                foreach (string id in referencedIds.Where(id => id.StartsWith("Assets/")))
                    Assert.That(File.Exists(id), Is.True, $"Missing event reward asset: {id}");
            }
        }

        [UnityTest]
        public IEnumerator EventBuffAssets_LoadWithExactThreeTurnModifiers()
        {
            var task = TestGameAssetHelper.EnsureInitialized();
            yield return new WaitUntil(() => task.IsCompleted);
            Assert.That(task.Result, Is.Not.Null);

            const string damageUpPath = "Assets/Tactics/ScriptableObjects/Buffs/EventDamageTakenUp.asset";
            const string reductionPath = "Assets/Tactics/ScriptableObjects/Buffs/EventDamageReduction.asset";
            var damageUp = task.Result.Load<BuffConfig>(damageUpPath);
            var reduction = task.Result.Load<BuffConfig>(reductionPath);

            Assert.That(damageUp, Is.Not.Null);
            Assert.That(damageUp.DefaultDuration, Is.EqualTo(3));
            Assert.That(damageUp.EffectType, Is.EqualTo(BuffEffectType.CurseDamageAmplifier));
            Assert.That(damageUp.Polarity, Is.EqualTo(BuffPolarity.Harmful));
            Assert.That(reduction, Is.Not.Null);
            Assert.That(reduction.DefaultDuration, Is.EqualTo(3));
            Assert.That(reduction.DamageReductionPercent, Is.EqualTo(0.2f).Within(0.001f));
            Assert.That(reduction.Polarity, Is.EqualTo(BuffPolarity.Beneficial));

            task.Result.Release(damageUpPath);
            task.Result.Release(reductionPath);
            TestGameAssetHelper.Cleanup();
        }
    }
}
