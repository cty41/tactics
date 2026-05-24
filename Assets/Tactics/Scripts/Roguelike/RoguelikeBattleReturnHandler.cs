using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using Tactics.Common.Controllers.GameResolvers;
using Tactics.Common.Battle;
using Tactics.Common.Players;
using Tactics.Flow.Battle;
using Tactics.Roster;

using Tactics.Common.Units;
using Tactics.Runtime.Utilities;

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
            bool humanWon = result.Winners != null &&
                            result.Winners.Any(p => p != null && p.PlayerType == PlayerType.HumanPlayer);

            if (humanWon)
            {
                var allUnits = BattleController.Instance?.GetUnits();
                int totalRounds = BattleController.Instance?.CurrentRound ?? 1;

                // 战后恢复 - 人类单位
                if (allUnits != null)
                    ApplyPostBattleRegeneration(allUnits);

                // 加载玩家状态供结算流程使用
                var state = PlayerAdventureStateStore.LoadRepairAndSave();

                // 同步 HP：从战斗层 Unit.Health 保存到地图层 CharacterDefinition.CurrentHp
                if (allUnits != null && state?.Roster != null)
                {
                    foreach (var unit in allUnits)
                    {
                        if (unit.PlayerNumber != 0) continue;
                        var mono = unit as MonoBehaviour;
                        if (mono == null) continue;
                        var link = mono.GetComponent<RosterCharacterLink>();
                        if (link == null) continue;
                        var def = state.Roster.FirstOrDefault(c => c.Id == link.CharacterId);
                        if (def == null) continue;
                        def.CurrentHp = Mathf.RoundToInt(unit.Health);
                    }
                }

                // 注册 BattleSettlementFlow 来管理UI流程（必须在 StartSettlement 之前）
                BattleSettlementFlow.Instance.Subscribe(BattleSettlementCoordinator.Instance, state);

                BattleSettlementCoordinator.Instance.StartSettlement(
                    result,
                    totalRounds,
                    allUnits,
                    state,
                    () =>
                    {
                        // 保存状态
                        if (state != null)
                            PlayerAdventureStateStore.Save(state);
            
                        // 延迟提交地图路径（结算完成后再前进路径）
                        ApplyRoguelikePathAfterBattle(result);
            
                        // 清除事件进行中标记
                        RoguelikeEventReentryManager.ClearEventInProgress();
            
                        TLog.Info("[RoguelikeBattleReturnHandler] Settlement complete. Path committed, markers cleared.");
                        // TODO: 恢复场景切换
                        // _ = BattleFlowCoordinator.Instance.EndBattleAsync(result);
                    }
                );
            }
            else
            {
                _ = BattleFlowCoordinator.Instance.EndBattleAsync(result);
            }
        }

        /// <summary>
        /// 战后恢复：对人类单位恢复 HP 和 MP，并清除昏迷状态。
        /// </summary>
        private static void ApplyPostBattleRegeneration(IEnumerable<IUnit> allUnits)
        {
            foreach (var unit in allUnits)
            {
                if (unit.PlayerNumber != 0)
                    continue;

                float hpRegen = unit.Constitution * 2;
                float mpRegen = unit.Charisma;

                unit.Health = Mathf.Min(unit.MaxHealth, unit.Health + hpRegen);
                unit.Mana = Mathf.Min(unit.MaxMana, unit.Mana + mpRegen);

                if (unit.IsDowned)
                    unit.IsDowned = false;

                string unitName = unit is INamedUnit named ? named.UnitName : $"Unit_{unit.UnitID}";
                TLog.Info($"[PostBattleRegen] {unitName}: HP +{hpRegen}, MP +{mpRegen}");
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

            string mapJson = PlayerPrefs.GetString(Tactics.UI.RoguelikeMapUIController.MapPlayerPrefsKey);
            global::Tactics.RoguelikeMap.RoguelikeMap map = JsonConvert.DeserializeObject<global::Tactics.RoguelikeMap.RoguelikeMap>(mapJson, MapJsonSettings);
            if (map?.visitedNodes == null)
            {
                PlayerPrefs.DeleteKey(Tactics.UI.RoguelikeMapUIController.RoguelikePendingNodePrefsKey);
                PlayerPrefs.Save();
                return;
            }

            // pending is now a nodeId string
            map.visitedNodes.Add(pending);

            string newJson = JsonConvert.SerializeObject(map, Formatting.Indented, MapJsonSettings);
            PlayerPrefs.SetString(Tactics.UI.RoguelikeMapUIController.MapPlayerPrefsKey, newJson);
            PlayerPrefs.DeleteKey(Tactics.UI.RoguelikeMapUIController.RoguelikePendingNodePrefsKey);
            PlayerPrefs.Save();
        }
    }
}
