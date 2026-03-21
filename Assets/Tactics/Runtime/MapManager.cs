using System.Linq;
using Tactics.Roster;
using UnityEngine;
using Newtonsoft.Json;

namespace Map
{
    public class MapManager : MonoBehaviour
    {
        public const string MapPlayerPrefsKey = "Map";

        public MapConfig config;
        public MapView view;

        public Map CurrentMap { get; private set; }

        private void Start()
        {
            ClearStaleRoguelikePendingNode();
            PlayerAdventureStateStore.EnsureDefaultProfile();

            if (PlayerPrefs.HasKey(MapPlayerPrefsKey))
            {
                string mapJson = PlayerPrefs.GetString(MapPlayerPrefsKey);
                Map map = JsonConvert.DeserializeObject<Map>(mapJson);
                // using this instead of .Contains()
                if (map.path.Any(p => p.Equals(map.GetBossNode().point)))
                {
                    // payer has already reached the boss, generate a new map
                    GenerateNewMap();
                }
                else
                {
                    CurrentMap = map;
                    // player has not reached the boss yet, load the current map
                    view.ShowMap(map);
                }
            }
            else
            {
                GenerateNewMap();
            }
        }

        public void GenerateNewMap()
        {
            Map map = MapGenerator.GetMap(config);
            CurrentMap = map;
            Debug.Log(map.ToJson());
            view.ShowMap(map);
        }

        public void SaveMap()
        {
            if (CurrentMap == null) return;

            string json = JsonConvert.SerializeObject(CurrentMap, Formatting.Indented,
                new JsonSerializerSettings {ReferenceLoopHandling = ReferenceLoopHandling.Ignore});
            PlayerPrefs.SetString(MapPlayerPrefsKey, json);
            PlayerPrefs.Save();
        }

        /// <summary>Clears battle pending node left from a crash/force-quit mid-fight (never left when returning normally).</summary>
        private static void ClearStaleRoguelikePendingNode()
        {
            if (!PlayerPrefs.HasKey(MapPlayerTracker.RoguelikePendingNodePrefsKey))
                return;

            PlayerPrefs.DeleteKey(MapPlayerTracker.RoguelikePendingNodePrefsKey);
            PlayerPrefs.Save();
        }

        private void OnApplicationQuit()
        {
            SaveMap();
        }
    }
}
