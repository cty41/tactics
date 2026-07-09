using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Tactics.AssetPipeline;
using Tactics.Common.Testing.Gameplay;
using Tactics.Common.Units.Buffs;
using Tactics.Roster;
using Tactics.Common.Units.Classes;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tactics.Tests.PlayMode
{
    public class PendingBuffPersistenceTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var initTask = TestGameAssetHelper.EnsureInitialized();
            yield return new WaitUntil(() => initTask.IsCompleted);
            Assume.That(initTask.Result, Is.Not.Null, "GameAssetManager should be initialized.");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            TestGameAssetHelper.Cleanup();
            yield return null;
        }

        [UnityTest]
        public IEnumerator PendingBuffSnapshot_Reload_RetainsRuntimeIcon()
        {
            const string buffPath = "Assets/Tactics/Battle/Buffs/Frozen.asset";

            var buffConfig = GameAssetManager.Instance.Load<BuffConfig>(buffPath);
            Assert.IsNotNull(buffConfig, "BuffConfig should load from asset path.");
            Assert.IsNotNull(buffConfig.Icon, "Source BuffConfig should have icon.");
            buffConfig.RuntimeSourceAssetPath = buffPath;

            var state = new PlayerAdventureState
            {
                Version = 2,
                Gold = 0,
                Roster = new List<CharacterDefinition>
                {
                    CharacterDefinition.CreateDefault("warrior", "Warrior", roleType: RoleType.Barbarian),
                    CharacterDefinition.CreateDefault("mage", "Mage", intelligenceBonus: 2, roleType: RoleType.Mage),
                    CharacterDefinition.CreateDefault("hunter", "Hunter", agilityBonus: 2, roleType: RoleType.Hunter)
                },
                ActivePartyCharacterIds = new List<string> { "warrior", "mage", "hunter" }
            };

            state.Roster[0].AddPendingBuff(buffConfig);
            PlayerAdventureStateStore.Save(state);

            var reloaded = PlayerAdventureStateStore.LoadRepairAndSave();
            var warrior = reloaded.Roster[0];

            Assert.That(warrior.PendingBuffSnapshots.Count, Is.EqualTo(1), "Pending buff snapshot should persist across save/load.");
            Assert.That(warrior.PendingBuffSnapshots[0].BuffAssetPath, Is.EqualTo(buffPath), "Snapshot should retain buff asset path.");
            Assert.That(warrior.PendingBuffs.Count, Is.EqualTo(1), "Hydration should rebuild runtime PendingBuffs.");
            Assert.IsNotNull(warrior.PendingBuffs[0].Icon, "Hydrated runtime BuffConfig should retain icon for battle UI consumption.");

            yield return null;
        }
    }
}
