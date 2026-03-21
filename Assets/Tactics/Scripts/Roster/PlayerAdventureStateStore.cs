using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Tactics.Roster
{
    public static class PlayerAdventureStateStore
    {
        public const string PlayerPrefsKey = "Tactics_PlayerAdventureState";

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        };

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
                return state ?? CreateDefaultState();
            }
            catch
            {
                return CreateDefaultState();
            }
        }

        public static void Save(PlayerAdventureState state)
        {
            if (state == null)
                return;
            string json = JsonConvert.SerializeObject(state, Formatting.Indented, JsonSettings);
            PlayerPrefs.SetString(PlayerPrefsKey, json);
            PlayerPrefs.Save();
        }

        /// <summary>Guarantee roster has at least 2 characters and active party lists 2 valid ids (mutates and saves if repaired).</summary>
        public static PlayerAdventureState LoadRepairAndSave()
        {
            var state = Load();
            TryRepair(state, out bool changed);
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

            while (state.Roster.Count < 2)
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

            while (state.ActivePartyCharacterIds.Count < 2)
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

            if (state.ActivePartyCharacterIds.Count > 2)
            {
                state.ActivePartyCharacterIds = state.ActivePartyCharacterIds.Take(2).ToList();
                changed = true;
            }
        }

        private static PlayerAdventureState CreateDefaultState()
        {
            var a = CharacterDefinition.CreateDefault("hero_a", "Hero A", strengthBonus: 1, intelligenceBonus: 0);
            var b = CharacterDefinition.CreateDefault("hero_b", "Hero B", strengthBonus: 0, intelligenceBonus: 1);
            return new PlayerAdventureState
            {
                Version = 1,
                Roster = new List<CharacterDefinition> { a, b },
                ActivePartyCharacterIds = new List<string> { a.Id, b.Id }
            };
        }
    }
}
