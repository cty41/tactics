using System.Collections;
using System.Linq;
using Tactics.AssetPipeline;
using Newtonsoft.Json;
using Tactics.Common.Controllers.GameResolvers;
using Tactics.Common.Players;
using Tactics.Common.Controllers;
using UnityEngine;

namespace Tactics.Roguelike
{
    public class RoguelikeBattleReturn : MonoBehaviour
    {
        private static readonly JsonSerializerSettings MapJsonSettings = new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        };

        [SerializeField] private float _returnDelaySeconds = 1.5f;
        [Tooltip("Used when PlayerPrefs key is missing (e.g. opened Test1 directly from editor).")]
        [SerializeField] private string _defaultMapSceneName = "Home";

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

            var stored = PlayerPrefs.GetString(Tactics.UI.RoguelikeMapUIController.RoguelikeReturnScenePrefsKey, _defaultMapSceneName);
            if (string.IsNullOrWhiteSpace(stored))
                stored = _defaultMapSceneName;
            SceneProjectPathHelper.TryLoadSceneViaAssetManager(stored);
        }

        private static void ApplyRoguelikePathAfterBattle(GameResult result)
        {
            string pending = PlayerPrefs.GetString(Tactics.UI.RoguelikeMapUIController.RoguelikePendingNodePrefsKey, "");
            if (string.IsNullOrEmpty(pending))
                return;

            bool humanWon = result.Winners != null &&
                             result.Winners.Any(p => p != null && p.PlayerType == PlayerType.HumanPlayer);

            if (!humanWon)
            {
                PlayerPrefs.DeleteKey(Tactics.UI.RoguelikeMapUIController.RoguelikePendingNodePrefsKey);
                PlayerPrefs.Save();
                return;
            }

            if (!PlayerPrefs.HasKey(Tactics.UI.RoguelikeMapUIController.MapPlayerPrefsKey))
            {
                PlayerPrefs.DeleteKey(Tactics.UI.RoguelikeMapUIController.RoguelikePendingNodePrefsKey);
                PlayerPrefs.Save();
                return;
            }

            string[] parts = pending.Split(',');
            if (parts.Length != 2 || !int.TryParse(parts[0], out int x) || !int.TryParse(parts[1], out int y))
            {
                PlayerPrefs.DeleteKey(Tactics.UI.RoguelikeMapUIController.RoguelikePendingNodePrefsKey);
                PlayerPrefs.Save();
                return;
            }

            string mapJson = PlayerPrefs.GetString(Tactics.UI.RoguelikeMapUIController.MapPlayerPrefsKey);
            global::Tactics.RoguelikeMap.RoguelikeMap map = JsonConvert.DeserializeObject<global::Tactics.RoguelikeMap.RoguelikeMap>(mapJson, MapJsonSettings);
            if (map?.path == null)
            {
                PlayerPrefs.DeleteKey(Tactics.UI.RoguelikeMapUIController.RoguelikePendingNodePrefsKey);
                PlayerPrefs.Save();
                return;
            }

            var point = new Vector2Int(x, y);
            if (!map.path.Any(p => p.Equals(point)))
                map.path.Add(point);

            string newJson = JsonConvert.SerializeObject(map, Formatting.Indented, MapJsonSettings);
            PlayerPrefs.SetString(Tactics.UI.RoguelikeMapUIController.MapPlayerPrefsKey, newJson);
            PlayerPrefs.DeleteKey(Tactics.UI.RoguelikeMapUIController.RoguelikePendingNodePrefsKey);
            PlayerPrefs.Save();
        }
    }
}