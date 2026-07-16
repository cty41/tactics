using Newtonsoft.Json;
using Tactics.RoguelikeMap;
using Tactics.Roster;
using Tactics.Runtime.Utilities;
using UnityEngine;

namespace Tactics.Roguelike
{
    public enum PureRunEndReason
    {
        Defeat,
        BossVictory
    }

    /// <summary>
    /// Persists the single global Pure Run session as an adventure-state/map pair.
    /// </summary>
    /// <remarks>
    /// A session is valid only when both payloads exist and the map layout version is current.
    /// Slot saves are intentionally outside this boundary and remain available to legacy flows.
    /// </remarks>
    public static class PureRunSessionStore
    {
        public const string StatePrefsKey = "Tactics_PureRun_State";
        public const string MapPrefsKey = "Tactics_PureRun_Map";
        public const string PendingNodePrefsKey = "Tactics_PureRun_PendingNode";
        public const string ReturnScenePrefsKey = "Tactics_PureRun_ReturnScene";

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        };

        public static bool HasState => PlayerPrefs.HasKey(StatePrefsKey);
        public static bool HasMap => PlayerPrefs.HasKey(MapPrefsKey);
        public static bool HasActiveRun => HasState && HasMap;

        public static void StartNew(PlayerAdventureState state, global::Tactics.RoguelikeMap.RoguelikeMap map)
        {
            Clear();
            SaveSession(state, map);
            RoguelikeMapRuntimeState.AttachMap(map, map?.currentNodeId);
        }

        public static bool TryLoad(
            out PlayerAdventureState state,
            out global::Tactics.RoguelikeMap.RoguelikeMap map)
        {
            state = null;
            map = null;

            if (!HasState || !HasMap)
            {
                if (HasState || HasMap)
                {
                    TLog.Warning("[PureRunSessionStore] Found an incomplete session pair; clearing it.");
                    Clear();
                }
                return false;
            }

            try
            {
                state = JsonConvert.DeserializeObject<PlayerAdventureState>(
                    PlayerPrefs.GetString(StatePrefsKey), JsonSettings);
                map = JsonConvert.DeserializeObject<global::Tactics.RoguelikeMap.RoguelikeMap>(
                    PlayerPrefs.GetString(MapPrefsKey), JsonSettings);

                if (state == null || !state.IsPureRun || map == null ||
                    map.layoutVersion != RoguelikeMapGenerator.PureRunLayoutVersion)
                {
                    TLog.Warning("[PureRunSessionStore] Session payload is invalid or uses an obsolete map layout.");
                    Clear();
                    state = null;
                    map = null;
                    return false;
                }

                bool repaired = PlayerAdventureStateStore.RepairInPlace(state);
                if (repaired)
                    SaveState(state);

                RoguelikeMapRuntimeState.AttachMap(map, map.currentNodeId);
                return true;
            }
            catch (System.Exception ex)
            {
                TLog.Warning($"[PureRunSessionStore] Failed to load session: {ex.Message}");
                Clear();
                state = null;
                map = null;
                return false;
            }
        }

        public static bool TryLoadState(out PlayerAdventureState state)
        {
            state = null;
            if (!HasState)
                return false;

            try
            {
                state = JsonConvert.DeserializeObject<PlayerAdventureState>(
                    PlayerPrefs.GetString(StatePrefsKey), JsonSettings);
                if (state == null || !state.IsPureRun)
                {
                    PlayerPrefs.DeleteKey(StatePrefsKey);
                    PlayerPrefs.Save();
                    state = null;
                    return false;
                }

                bool repaired = PlayerAdventureStateStore.RepairInPlace(state);
                if (repaired)
                    SaveState(state);
                return true;
            }
            catch (System.Exception ex)
            {
                TLog.Warning($"[PureRunSessionStore] Failed to load adventure state: {ex.Message}");
                PlayerPrefs.DeleteKey(StatePrefsKey);
                PlayerPrefs.Save();
                return false;
            }
        }

        public static void SaveSession(
            PlayerAdventureState state,
            global::Tactics.RoguelikeMap.RoguelikeMap map)
        {
            if (state == null || !state.IsPureRun || map == null)
                return;

            PlayerPrefs.SetString(StatePrefsKey, JsonConvert.SerializeObject(state, Formatting.Indented, JsonSettings));
            PlayerPrefs.SetString(MapPrefsKey, JsonConvert.SerializeObject(map, Formatting.Indented, JsonSettings));
            PlayerPrefs.Save();
        }

        public static void SaveState(PlayerAdventureState state)
        {
            if (state == null || !state.IsPureRun)
                return;

            PlayerPrefs.SetString(StatePrefsKey, JsonConvert.SerializeObject(state, Formatting.Indented, JsonSettings));
            PlayerPrefs.Save();
        }

        public static void SaveMap(global::Tactics.RoguelikeMap.RoguelikeMap map)
        {
            if (map == null)
                return;

            PlayerPrefs.SetString(MapPrefsKey, JsonConvert.SerializeObject(map, Formatting.Indented, JsonSettings));
            PlayerPrefs.Save();
        }

        public static void Finish(PureRunEndReason reason)
        {
            TLog.Info($"[PureRunSessionStore] Finishing run: {reason}.");
            Clear();
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(StatePrefsKey);
            PlayerPrefs.DeleteKey(MapPrefsKey);
            PlayerPrefs.DeleteKey(PendingNodePrefsKey);
            PlayerPrefs.DeleteKey(ReturnScenePrefsKey);
            PlayerPrefs.DeleteKey("RoguelikeMap");
            PlayerPrefs.DeleteKey("RoguelikePendingNode");
            PlayerPrefs.DeleteKey("RoguelikeReturnScene");
            PlayerPrefs.DeleteKey("RoguelikeBossBattle");
            PlayerPrefs.Save();

            RoguelikeMapRuntimeState.ClearAll();
            RoguelikeEventReentryManager.ClearEventInProgress();
        }
    }
}
