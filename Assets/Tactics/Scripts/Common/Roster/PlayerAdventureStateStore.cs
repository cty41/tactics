using System.Collections.Generic;
using Tactics.Runtime.Utilities;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using Tactics.AssetPipeline;
using Tactics.Common.Units.Classes;
using Tactics.Common.Battle;

namespace Tactics.Roster
{
    public static class PlayerAdventureStateStore
    {
        public const string PlayerPrefsKey = "Tactics_PlayerAdventureState";
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
            if (PlayerPrefs.HasKey(PlayerPrefsKey))
                return;
            Save(CreateDefaultState());
        }

        public static PlayerAdventureState Load()
        {
            if (!PlayerPrefs.HasKey(PlayerPrefsKey))
                return CreateDefaultState();
            try
            {
                string json = PlayerPrefs.GetString(PlayerPrefsKey);
                var state = JsonConvert.DeserializeObject<PlayerAdventureState>(json, JsonSettings);
                if (state == null || !IsStateValid(state))
                {
                    TLog.Warning("[PlayerAdventureStateStore] Saved state is invalid or corrupted. Clearing old save and reloading defaults.");
                    PlayerPrefs.DeleteKey(PlayerPrefsKey);
                    PlayerPrefs.Save();
                    return CreateDefaultState();
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
            if (state == null)
                return;
            string json = JsonConvert.SerializeObject(state, Formatting.Indented, JsonSettings);
            PlayerPrefs.SetString(PlayerPrefsKey, json);
            PlayerPrefs.Save();
        }

        /// <summary>Guarantee roster has at least 3 characters and active party lists 3 valid ids (mutates and saves if repaired).</summary>
        public static PlayerAdventureState LoadRepairAndSave()
        {
            var state = Load();
            TryRepair(state, out bool changed);

            // Ensure each character has at least one default learned skill
            if (state.Roster != null)
            {
                foreach (var character in state.Roster)
                {
                    if (character == null) continue;
                    if (character.LearnedSkills == null)
                        character.LearnedSkills = new List<CharacterDefinition.LearnedSkill>();
                    
                    if (character.LearnedSkills.Count == 0)
                    {
                        string defaultSkillId = character.RoleType switch
                        {
                            RoleType.Barbarian => "barb_slash_1",
                            RoleType.Mage => "mage_fireball_1",
                            RoleType.Hunter => "hunter_shot_1",
                            RoleType.Healer => "heal_heal_1",
                            RoleType.Rogue => "rogue_backstab_1",
                            _ => null
                        };
                        
                        if (!string.IsNullOrEmpty(defaultSkillId))
                        {
                            character.LearnedSkills.Add(new CharacterDefinition.LearnedSkill
                            {
                                SkillId = defaultSkillId,
                                SkillType = SkillType.Active,
                                Level = 1
                            });
                            changed = true;
                            TLog.Info($"[PlayerAdventureStateStore] Added default skill '{defaultSkillId}' to {character.DisplayName}");
                        }
                    }
                }
            }

            if (changed)
                Save(state);
            return state;
        }

        private static void TryRepair(PlayerAdventureState state, out bool changed)
        {
            changed = false;
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
                var mapping = TestPrefabMappings.FirstOrDefault(m => m.RoleType == character.RoleType);
                var expectedPath = mapping?.PrefabPath ?? character.RoleType.ToString();
                if (character.PrefabPath == expectedPath)
                    continue;

                character.PrefabPath = expectedPath;
                changed = true;
            }
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
                return new PlayerAdventureState { Version = 2, Gold = 0, Roster = new List<CharacterDefinition>(), ActivePartyCharacterIds = new List<string>() };
            }

            try
            {
                var config = JsonConvert.DeserializeObject<TestPartyConfig>(json, JsonSettings);
                if (config == null)
                {
                    TLog.Error("[PlayerAdventureStateStore] Failed to deserialize TestParty.json");
                    return new PlayerAdventureState { Version = 2, Gold = 0, Roster = new List<CharacterDefinition>(), ActivePartyCharacterIds = new List<string>() };
                }
                TestPrefabMappings = config.PrefabMappings ?? new List<PrefabMapping>();
                return new PlayerAdventureState
                {
                    Version = 2,
                    Gold = 0,
                    Roster = config.Roster ?? new List<CharacterDefinition>(),
                    ActivePartyCharacterIds = config.ActivePartyCharacterIds ?? new List<string>()
                };
            }
            catch (System.Exception ex)
            {
                TLog.Error($"[PlayerAdventureStateStore] Failed to parse TestParty.json: {ex.Message}");
                return new PlayerAdventureState { Version = 2, Gold = 0, Roster = new List<CharacterDefinition>(), ActivePartyCharacterIds = new List<string>() };
            }
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
}
