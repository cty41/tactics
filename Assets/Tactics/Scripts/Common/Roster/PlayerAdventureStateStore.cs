using System.Collections.Generic;
using Tactics.Runtime.Utilities;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using Tactics.AssetPipeline;
using Tactics.Common.Units.Classes;
using Tactics.Common.Battle;
using Tactics.Consumables;
using Tactics.Roguelike;

namespace Tactics.Roster
{
    public static class PlayerAdventureStateStore
    {
        public const string PlayerPrefsKey = "Tactics_PlayerAdventureState";
        public const int SlotCount = 3;
        public const int DefaultSlotIndex = 0;
        public const int CurrentVersion = 5;

        private const string ActiveSlotPrefsKey = "Tactics_PlayerAdventureState_ActiveSlot";
        private const string TestPartyJsonPath = "Assets/Tactics/GameData/TestParty.json";

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        };

        /// <summary>Prefab mapping read from TestParty.json.</summary>
        public static List<PrefabMapping> TestPrefabMappings { get; private set; } = new List<PrefabMapping>();

        private static void EnsureTestPrefabMappingsLoaded()
        {
            if (TestPrefabMappings.Count > 0)
                return;

            var mgr = GameAssetManager.Instance;
            string json = null;

            if (mgr != null)
            {
                var textAsset = mgr.Load<TextAsset>(TestPartyJsonPath);
                if (textAsset != null)
                {
                    json = textAsset.text;
                    mgr.Release(TestPartyJsonPath);
                }
            }
#if UNITY_EDITOR
            if (json == null && File.Exists(TestPartyJsonPath))
                json = File.ReadAllText(TestPartyJsonPath);
#endif

            if (json != null)
            {
                try
                {
                    var config = JsonConvert.DeserializeObject<TestPartyConfig>(json, JsonSettings);
                    if (config?.PrefabMappings != null)
                        TestPrefabMappings = config.PrefabMappings;
                }
                catch (System.Exception ex)
                {
                    TLog.Warning($"[PlayerAdventureStateStore] Failed to load prefab mappings: {ex.Message}");
                }
            }
        }

        public static void EnsureDefaultProfile()
        {
            MigrateLegacySaveIfNeeded();
            if (HasSave(GetActiveSlotIndex()))
                return;
            Save(CreateDefaultState());
        }

        public static PlayerAdventureState Load()
        {
            if (PureRunSessionStore.TryLoadState(out var pureRunState))
                return pureRunState;
            return Load(GetActiveSlotIndex());
        }

        public static PlayerAdventureState Load(int slotIndex)
        {
            MigrateLegacySaveIfNeeded();
            var key = GetSlotPrefsKey(slotIndex);
            if (!PlayerPrefs.HasKey(key))
                return CreateDefaultState();
            try
            {
                string json = PlayerPrefs.GetString(key);
                var state = JsonConvert.DeserializeObject<PlayerAdventureState>(json, JsonSettings);
                if (state == null || !IsStateValid(state))
                {
                    TLog.Warning("[PlayerAdventureStateStore] Saved state is invalid or corrupted. Clearing old save and reloading defaults.");
                    PlayerPrefs.DeleteKey(key);
                    PlayerPrefs.Save();
                    return CreateDefaultState();
                }

                if (state.Roster != null)
                {
                    foreach (var character in state.Roster)
                        character?.HydratePendingBuffs();
                }

                return state;
            }
            catch
            {
                PlayerPrefs.DeleteKey(PlayerPrefsKey);
                PlayerPrefs.Save();
                return CreateDefaultState();
            }
        }

        private static bool IsStateValid(PlayerAdventureState state)
        {
            if (state.Roster == null || state.Roster.Count == 0)
                return false;
            if (state.ActivePartyCharacterIds == null || state.ActivePartyCharacterIds.Count == 0)
                return false;

            var distinctRoles = new HashSet<RoleType>();
            foreach (var character in state.Roster)
            {
                if (character == null)
                    return false;
                distinctRoles.Add(character.RoleType);
            }

            // Reject saves where all characters have the same default RoleType (Barbarian)
            // This indicates the save was created before RoleType was properly serialized.
            return distinctRoles.Count >= 2;
        }

        public static void Save(PlayerAdventureState state)
        {
            if (state?.IsPureRun == true)
            {
                PureRunSessionStore.SaveState(state);
                return;
            }
            Save(GetActiveSlotIndex(), state);
        }

        public static void Save(int slotIndex, PlayerAdventureState state)
        {
            if (state == null)
                return;
            slotIndex = NormalizeSlotIndex(slotIndex);
            string json = JsonConvert.SerializeObject(state, Formatting.Indented, JsonSettings);
            PlayerPrefs.SetString(GetSlotPrefsKey(slotIndex), json);
            SetActiveSlotIndex(slotIndex);
            PlayerPrefs.Save();
        }

        /// <summary>Guarantee roster has at least 3 characters and active party lists 3 valid ids (mutates and saves if repaired).</summary>
        public static PlayerAdventureState LoadRepairAndSave()
        {
            if (PureRunSessionStore.TryLoadState(out var pureRunState))
                return pureRunState;
            return LoadRepairAndSave(GetActiveSlotIndex());
        }

        public static PlayerAdventureState LoadRepairAndSave(int slotIndex)
        {
            slotIndex = NormalizeSlotIndex(slotIndex);
            var state = Load(slotIndex);
            bool changed = RepairInPlace(state);

            if (changed)
                Save(slotIndex, state);
            return state;
        }

        public static bool HasSave(int slotIndex)
        {
            MigrateLegacySaveIfNeeded();
            return PlayerPrefs.HasKey(GetSlotPrefsKey(slotIndex));
        }

        public static void Delete(int slotIndex)
        {
            PlayerPrefs.DeleteKey(GetSlotPrefsKey(slotIndex));
            PlayerPrefs.Save();
        }

        public static PlayerAdventureState CreateNew(int slotIndex)
        {
            var state = CreateDefaultState();
            Save(slotIndex, state);
            return state;
        }

        /// <summary>
        /// Creates the fixed first-demo party in stable Mage, Necromancer, Amazon order.
        /// Every base attribute starts at five and the seed selects one basic branch per role.
        /// </summary>
        public static PlayerAdventureState CreatePureRunState(int runSeed)
        {
            EnsureTestPrefabMappingsLoaded();

            var roster = new List<CharacterDefinition>
            {
                CreatePureRunCharacter("pure_run_mage", "Mage", RoleType.Mage, "Mage", runSeed, 0),
                CreatePureRunCharacter("pure_run_necromancer", "Necromancer", RoleType.Necromancer, "Necromancer", runSeed, 1),
                CreatePureRunCharacter("pure_run_amazon", "Amazon", RoleType.Amazon, "Hunter", runSeed, 2)
            };

            return new PlayerAdventureState
            {
                Version = CurrentVersion,
                IsPureRun = true,
                RunSeed = runSeed,
                Gold = 0,
                Roster = roster,
                ActivePartyCharacterIds = roster.Select(character => character.Id).ToList(),
                Inventory = new List<string>(),
                ConsumableInstances = new List<ConsumableInstance>()
            };
        }

        public static int GetActiveSlotIndex()
        {
            return NormalizeSlotIndex(PlayerPrefs.GetInt(ActiveSlotPrefsKey, DefaultSlotIndex));
        }

        public static void SetActiveSlotIndex(int slotIndex)
        {
            PlayerPrefs.SetInt(ActiveSlotPrefsKey, NormalizeSlotIndex(slotIndex));
            PlayerPrefs.Save();
        }

        public static SaveSlotSummary GetSlotSummary(int slotIndex)
        {
            slotIndex = NormalizeSlotIndex(slotIndex);
            if (!HasSave(slotIndex))
                return SaveSlotSummary.Empty(slotIndex);

            try
            {
                var state = Load(slotIndex);
                return SaveSlotSummary.FromState(slotIndex, state);
            }
            catch (System.Exception ex)
            {
                TLog.Warning($"[PlayerAdventureStateStore] Failed to summarize slot {slotIndex + 1}: {ex.Message}");
                return SaveSlotSummary.Corrupted(slotIndex);
            }
        }

        private static string GetSlotPrefsKey(int slotIndex)
        {
            return $"{PlayerPrefsKey}_Slot{NormalizeSlotIndex(slotIndex) + 1}";
        }

        private static int NormalizeSlotIndex(int slotIndex)
        {
            return Mathf.Clamp(slotIndex, 0, SlotCount - 1);
        }

        private static void MigrateLegacySaveIfNeeded()
        {
            var slotOneKey = GetSlotPrefsKey(DefaultSlotIndex);
            if (!PlayerPrefs.HasKey(PlayerPrefsKey) || PlayerPrefs.HasKey(slotOneKey))
                return;

            PlayerPrefs.SetString(slotOneKey, PlayerPrefs.GetString(PlayerPrefsKey));
            PlayerPrefs.SetInt(ActiveSlotPrefsKey, DefaultSlotIndex);
            PlayerPrefs.Save();
        }

        private static void TryRepair(PlayerAdventureState state, out bool changed)
        {
            changed = MigrateToCurrentVersion(state);

            if (state.Inventory == null)
            {
                state.Inventory = new List<string>();
                changed = true;
            }

            if (state.ConsumableInstances == null)
            {
                state.ConsumableInstances = new List<ConsumableInstance>();
                changed = true;
            }

            for (int i = state.ConsumableInstances.Count - 1; i >= 0; i--)
            {
                var instance = state.ConsumableInstances[i];
                var definition = instance == null ? null : ConsumableDatabase.GetById(instance.DefinitionId);
                if (instance == null || definition == null || instance.RemainingCharges <= 0)
                {
                    state.ConsumableInstances.RemoveAt(i);
                    changed = true;
                    continue;
                }

                int maxCharges = System.Math.Max(1, definition.MaxCharges);
                if (string.IsNullOrWhiteSpace(instance.InstanceId))
                {
                    instance.InstanceId = System.Guid.NewGuid().ToString("N");
                    changed = true;
                }
                if (instance.MaxCharges != maxCharges)
                {
                    instance.MaxCharges = maxCharges;
                    changed = true;
                }
                if (instance.RemainingCharges > maxCharges)
                {
                    instance.RemainingCharges = maxCharges;
                    changed = true;
                }
            }
            if (state.Roster == null)
            {
                state.Roster = new List<CharacterDefinition>();
                changed = true;
            }

            while (state.Roster.Count < 3)
            {
                int n = state.Roster.Count;
                state.Roster.Add(CharacterDefinition.CreateDefault(
                    $"roster_fill_{n}",
                    $"Recruit {n + 1}"));
                changed = true;
            }

            if (state.ActivePartyCharacterIds == null)
            {
                state.ActivePartyCharacterIds = new List<string>();
                changed = true;
            }

            while (state.ActivePartyCharacterIds.Count < 3)
            {
                int idx = state.ActivePartyCharacterIds.Count;
                state.ActivePartyCharacterIds.Add(state.Roster[idx].Id);
                changed = true;
            }

            for (int i = 0; i < state.ActivePartyCharacterIds.Count; i++)
            {
                string id = state.ActivePartyCharacterIds[i];
                if (string.IsNullOrEmpty(id) || state.Roster.All(c => c.Id != id))
                {
                    state.ActivePartyCharacterIds[i] = state.Roster[i % state.Roster.Count].Id;
                    changed = true;
                }
            }

            if (state.ActivePartyCharacterIds.Count > 3)
            {
                state.ActivePartyCharacterIds = state.ActivePartyCharacterIds.Take(3).ToList();
                changed = true;
            }

            EnsureTestPrefabMappingsLoaded();

            foreach (var character in state.Roster)
            {
                if (character.CurrentHp <= 0 && !character.IsDead)
                {
                    character.CurrentHp = character.MaxHp;
                    changed = true;
                }

                if (!character.CurrentMp.HasValue)
                {
                    character.CurrentMp = character.MaxMp;
                    changed = true;
                }

                var mapping = TestPrefabMappings.FirstOrDefault(m => m.RoleType == character.RoleType);
                string pureRunFallback = state.IsPureRun && character.RoleType == RoleType.Amazon
                    ? "Hunter"
                    : character.RoleType.ToString();
                var expectedPath = mapping?.PrefabPath ?? pureRunFallback;
                if (character.PrefabPath == expectedPath)
                    continue;

                character.PrefabPath = expectedPath;
                changed = true;
            }

            if (CharacterLoadoutService.RepairLoadouts(state))
                changed = true;
        }

        private static bool MigrateToCurrentVersion(PlayerAdventureState state)
        {
            if (state == null || state.Version >= CurrentVersion)
                return false;

            state.Inventory ??= new List<string>();
            state.ConsumableInstances ??= new List<ConsumableInstance>();
            state.Roster ??= new List<CharacterDefinition>();

            var migratedInstances = new List<ConsumableInstance>();
            foreach (var instance in state.ConsumableInstances)
            {
                if (instance == null || instance.RemainingCharges <= 0)
                    continue;

                switch (instance.DefinitionId)
                {
                    case "field_ration":
                        migratedInstances.Add(CreateMigratedInstance("life_potion", instance.InstanceId));
                        break;
                    case "catnip_tonic":
                        migratedInstances.Add(CreateMigratedInstance("mana_potion", instance.InstanceId));
                        break;
                    case "bandage_roll":
                        for (int chargeIndex = 0; chargeIndex < instance.RemainingCharges; chargeIndex++)
                        {
                            string instanceId = chargeIndex == 0
                                ? instance.InstanceId
                                : System.Guid.NewGuid().ToString("N");
                            migratedInstances.Add(CreateMigratedInstance(
                                "cleansing_potion",
                                instanceId));
                        }
                        break;
                    default:
                        migratedInstances.Add(instance);
                        break;
                }
            }

            state.ConsumableInstances = migratedInstances;
            foreach (var character in state.Roster)
            {
                if (character != null)
                    character.CarriedConsumableInstanceId = null;
            }

            state.Version = CurrentVersion;
            CharacterLoadoutService.AutoUnloadDeadCharacters(state);
            return true;
        }

        private static ConsumableInstance CreateMigratedInstance(
            string definitionId,
            string instanceId)
        {
            return new ConsumableInstance
            {
                InstanceId = string.IsNullOrWhiteSpace(instanceId)
                    ? System.Guid.NewGuid().ToString("N")
                    : instanceId,
                DefinitionId = definitionId,
                RemainingCharges = 1,
                MaxCharges = 1
            };
        }

        internal static bool RepairInPlace(PlayerAdventureState state)
        {
            if (state == null)
                return false;

            TryRepair(state, out bool changed);

            // Legacy profiles may predate learned-skill persistence. Pure Run characters
            // already receive their branch skill and therefore do not enter this fallback.
            if (state.Roster == null)
                return changed;

            foreach (var character in state.Roster)
            {
                if (character == null)
                    continue;

                character.HydratePendingBuffs();
                character.LearnedSkills ??= new List<CharacterDefinition.LearnedSkill>();
                if (character.LearnedSkills.Count > 0)
                    continue;

                string defaultSkillId = character.RoleType switch
                {
                    RoleType.Barbarian => "barb_slash_1",
                    RoleType.Mage => "mage_fireball_1",
                    RoleType.Hunter => "hunter_shot_1",
                    RoleType.Healer => "heal_heal_1",
                    RoleType.Rogue => "rogue_backstab_1",
                    _ => null
                };

                if (string.IsNullOrEmpty(defaultSkillId))
                    continue;

                character.LearnedSkills.Add(new CharacterDefinition.LearnedSkill
                {
                    SkillId = defaultSkillId,
                    SkillType = SkillType.Active,
                    Level = 1
                });
                changed = true;
                TLog.Info($"[PlayerAdventureStateStore] Added default skill '{defaultSkillId}' to {character.DisplayName}");
            }

            return changed;
        }

        private static PlayerAdventureState CreateDefaultState()
        {
            string json = null;
            var mgr = GameAssetManager.Instance;

            if (mgr != null)
            {
                var textAsset = mgr.Load<TextAsset>(TestPartyJsonPath);
                if (textAsset != null)
                {
                    json = textAsset.text;
                    mgr.Release(TestPartyJsonPath);
                }
            }
#if UNITY_EDITOR
            else if (File.Exists(TestPartyJsonPath))
            {
                json = File.ReadAllText(TestPartyJsonPath);
            }
#endif

            if (json == null)
            {
                TLog.Error($"[PlayerAdventureStateStore] TestParty.json not found at {TestPartyJsonPath}");
                return new PlayerAdventureState { Version = CurrentVersion, Gold = 0, Roster = new List<CharacterDefinition>(), ActivePartyCharacterIds = new List<string>() };
            }

            try
            {
                var config = JsonConvert.DeserializeObject<TestPartyConfig>(json, JsonSettings);
                if (config == null)
                {
                    TLog.Error("[PlayerAdventureStateStore] Failed to deserialize TestParty.json");
                    return new PlayerAdventureState { Version = CurrentVersion, Gold = 0, Roster = new List<CharacterDefinition>(), ActivePartyCharacterIds = new List<string>() };
                }
                TestPrefabMappings = config.PrefabMappings ?? new List<PrefabMapping>();
                return new PlayerAdventureState
                {
                    Version = CurrentVersion,
                    Gold = 0,
                    Roster = config.Roster ?? new List<CharacterDefinition>(),
                    ActivePartyCharacterIds = config.ActivePartyCharacterIds ?? new List<string>()
                };
            }
            catch (System.Exception ex)
            {
                TLog.Error($"[PlayerAdventureStateStore] Failed to parse TestParty.json: {ex.Message}");
                return new PlayerAdventureState { Version = CurrentVersion, Gold = 0, Roster = new List<CharacterDefinition>(), ActivePartyCharacterIds = new List<string>() };
            }
        }

        private static CharacterDefinition CreatePureRunCharacter(
            string id,
            string displayName,
            RoleType roleType,
            string fallbackPrefabPath,
            int runSeed,
            int partyIndex)
        {
            var character = CharacterDefinition.CreateDefault(id, displayName, roleType: roleType);
            var mapping = TestPrefabMappings.FirstOrDefault(entry => entry.RoleType == roleType);
            character.PrefabPath = mapping?.PrefabPath ?? fallbackPrefabPath;

            string[] branchSkillIds = roleType switch
            {
                RoleType.Mage => new[] { "mage.fireball", "mage.ice_bolt", "mage.lightning" },
                RoleType.Necromancer => new[]
                {
                    "necromancer.summon_skeleton",
                    "necromancer.amplify_damage",
                    "necromancer.bone_spear"
                },
                RoleType.Amazon => new[] { "amazon.thrust", "amazon.poison_spear", "amazon.combat_techniques" },
                _ => System.Array.Empty<string>()
            };

            if (branchSkillIds.Length == 0)
                return character;

            int branchSeed = Tactics.Roguelike.RoguelikeMapRuntimeState.DeriveSeed(
                runSeed,
                $"starting-branch-{roleType}",
                partyIndex);
            int branchIndex = (int)((uint)branchSeed % (uint)branchSkillIds.Length);
            character.StartingBranchSkillId = branchSkillIds[branchIndex];

            if (FirstSliceSkillCatalog.TryGet(character.StartingBranchSkillId, out var skill))
            {
                character.LearnedSkills.Add(new CharacterDefinition.LearnedSkill
                {
                    SkillId = skill.Id,
                    SkillType = skill.SkillType,
                    Level = 1
                });
            }

            return character;
        }
    }

    public class TestPartyConfig
    {
        public List<CharacterDefinition> Roster { get; set; }
        public List<string> ActivePartyCharacterIds { get; set; }
        public List<PrefabMapping> PrefabMappings { get; set; }
    }

    public class PrefabMapping
    {
        public RoleType RoleType { get; set; }
        public string PrefabPath { get; set; }
    }

    public readonly struct SaveSlotSummary
    {
        public int SlotIndex { get; }
        public bool HasSave { get; }
        public bool IsCorrupted { get; }
        public int Gold { get; }
        public int RosterCount { get; }
        public int ActivePartyCount { get; }

        private SaveSlotSummary(int slotIndex, bool hasSave, bool isCorrupted, int gold, int rosterCount, int activePartyCount)
        {
            SlotIndex = slotIndex;
            HasSave = hasSave;
            IsCorrupted = isCorrupted;
            Gold = gold;
            RosterCount = rosterCount;
            ActivePartyCount = activePartyCount;
        }

        public static SaveSlotSummary Empty(int slotIndex)
        {
            return new SaveSlotSummary(slotIndex, false, false, 0, 0, 0);
        }

        public static SaveSlotSummary Corrupted(int slotIndex)
        {
            return new SaveSlotSummary(slotIndex, true, true, 0, 0, 0);
        }

        public static SaveSlotSummary FromState(int slotIndex, PlayerAdventureState state)
        {
            return new SaveSlotSummary(
                slotIndex,
                true,
                false,
                state?.Gold ?? 0,
                state?.Roster?.Count ?? 0,
                state?.ActivePartyCharacterIds?.Count ?? 0);
        }
    }
}
