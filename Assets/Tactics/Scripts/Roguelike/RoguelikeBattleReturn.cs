using System.Collections;
using System.Linq;
using Map;
using Tactics.AssetPipeline;
using Newtonsoft.Json;
using Tactics.Tbsf.Common.Controllers.GameResolvers;
using Tactics.Tbsf.Common.Players;
using Tactics.Tbsf.Unity.Controllers;
using UnityEngine;

namespace Tactics.Roguelike
{
    /// <summary>
    /// After a tactics battle ends, loads the roguelike map scene stored in PlayerPrefs (see MapPlayerTracker).
    /// Attach to the same GameObject as <see cref="UnityGridController"/> (e.g. GridController in Test1).
    /// </summary>
    public class RoguelikeBattleReturn : MonoBehaviour
    {
        /// <summary>Must match MapPlayerTracker.RoguelikeReturnScenePrefsKey in Assets/Scripts.</summary>
        public const string ReturnScenePlayerPrefsKey = "RoguelikeReturnScene";

        private static readonly JsonSerializerSettings MapJsonSettings = new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        };

        [SerializeField] private float _returnDelaySeconds = 1.5f;
        [Tooltip("Used when PlayerPrefs key is missing (e.g. opened Test1 directly from editor).")]
        [SerializeField] private string _defaultMapSceneName = "SampleScene";

        private UnityGridController _grid;

        private void Awake()
        {
            _grid = GetComponent<UnityGridController>();
            if (_grid != null)
                _grid.GameEnded += OnGameEnded;
        }

        private void OnDestroy()
        {
            if (_grid != null)
                _grid.GameEnded -= OnGameEnded;
        }

        private void OnGameEnded(GameResult result)
        {
            StartCoroutine(CoReturnToMap(result));
        }

        private IEnumerator CoReturnToMap(GameResult result)
        {
            if (_returnDelaySeconds > 0f)
                yield return new WaitForSeconds(_returnDelaySeconds);

            ApplyRoguelikePathAfterBattle(result);

            var stored = PlayerPrefs.GetString(ReturnScenePlayerPrefsKey, _defaultMapSceneName);
            if (string.IsNullOrWhiteSpace(stored))
                stored = _defaultMapSceneName;
            SceneProjectPathHelper.TryLoadSceneViaAssetManager(stored);
        }

        private static void ApplyRoguelikePathAfterBattle(GameResult result)
        {
            string pending = PlayerPrefs.GetString(MapPlayerTracker.RoguelikePendingNodePrefsKey, "");
            if (string.IsNullOrEmpty(pending))
                return;

            bool humanWon = result.Winners != null &&
                             result.Winners.Any(p => p != null && p.PlayerType == PlayerType.HumanPlayer);

            if (!humanWon)
            {
                PlayerPrefs.DeleteKey(MapPlayerTracker.RoguelikePendingNodePrefsKey);
                PlayerPrefs.Save();
                return;
            }

            if (!PlayerPrefs.HasKey(MapManager.MapPlayerPrefsKey))
            {
                PlayerPrefs.DeleteKey(MapPlayerTracker.RoguelikePendingNodePrefsKey);
                PlayerPrefs.Save();
                return;
            }

            string[] parts = pending.Split(',');
            if (parts.Length != 2 || !int.TryParse(parts[0], out int x) || !int.TryParse(parts[1], out int y))
            {
                PlayerPrefs.DeleteKey(MapPlayerTracker.RoguelikePendingNodePrefsKey);
                PlayerPrefs.Save();
                return;
            }

            string mapJson = PlayerPrefs.GetString(MapManager.MapPlayerPrefsKey);
            global::Map.Map map = JsonConvert.DeserializeObject<global::Map.Map>(mapJson, MapJsonSettings);
            if (map?.path == null)
            {
                PlayerPrefs.DeleteKey(MapPlayerTracker.RoguelikePendingNodePrefsKey);
                PlayerPrefs.Save();
                return;
            }

            var point = new Vector2Int(x, y);
            if (!map.path.Any(p => p.Equals(point)))
                map.path.Add(point);

            string newJson = JsonConvert.SerializeObject(map, Formatting.Indented, MapJsonSettings);
            PlayerPrefs.SetString(MapManager.MapPlayerPrefsKey, newJson);
            PlayerPrefs.DeleteKey(MapPlayerTracker.RoguelikePendingNodePrefsKey);
            PlayerPrefs.Save();
        }
    }
}
