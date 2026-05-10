using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using Tactics.Common.Controllers.GameResolvers;
using Tactics.Common.Battle;
using Tactics.Common.Players;
using Tactics.Flow.Battle;

namespace Tactics.Roguelike
{
    /// <summary>
    /// Roguelike-specific battle return handler.
    /// Consumes BattleController.BattleEnded to update map path and coordinate return flow.
    /// </summary>
    public sealed class RoguelikeBattleReturnHandler
    {
        private static readonly RoguelikeBattleReturnHandler _instance = new RoguelikeBattleReturnHandler();
        public static RoguelikeBattleReturnHandler Instance => _instance;

        private static readonly JsonSerializerSettings MapJsonSettings = new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        };

        private RoguelikeBattleReturnHandler() { }

        /// <summary>
        /// Register with a BattleController instance.
        /// Call this when BattleController becomes available (e.g. from BattleController.Awake).
        /// </summary>
        public void RegisterController(BattleController controller)
        {
            if (controller == null) return;
            controller.BattleEnded += OnBattleEnded;
        }

        /// <summary>
        /// Unregister from a BattleController instance.
        /// </summary>
        public void UnregisterController(BattleController controller)
        {
            if (controller == null) return;
            controller.BattleEnded -= OnBattleEnded;
        }

        private void OnBattleEnded(GameResult result)
        {
            ApplyRoguelikePathAfterBattle(result);

            bool humanWon = result.Winners != null &&
                            result.Winners.Any(p => p != null && p.PlayerType == PlayerType.HumanPlayer);

            if (humanWon)
            {
                var allUnits = BattleController.Instance?.GetUnits();
                int totalRounds = BattleController.Instance?.CurrentRound ?? 0;

                BattleSettlementCoordinator.Instance.StartSettlement(
                    result,
                    totalRounds,
                    allUnits,
                    () =>
                    {
                        _ = BattleFlowCoordinator.Instance.EndBattleAsync(result);
                    }
                );
            }
            else
            {
                _ = BattleFlowCoordinator.Instance.EndBattleAsync(result);
            }
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
