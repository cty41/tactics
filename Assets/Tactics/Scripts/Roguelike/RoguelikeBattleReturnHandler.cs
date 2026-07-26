using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UIElements;
using Tactics.Common.Controllers.GameResolvers;
using Tactics.Common.Battle;
using Tactics.Common.Players;
using Tactics.Flow.Battle;
using Tactics.Roster;
using Tactics.RoguelikeMap;
using Tactics.RoguelikeMap.UI;

using Tactics.Common.Units;
using Tactics.Runtime.Utilities;
using Tactics.UI;

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
        private static GameResult _pendingRoguelikeReturnResult;
        private static bool _pendingRunTerminal;
        private static PureRunEndReason _pendingRunEndReason;

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

        private async void OnBattleEnded(GameResult result)
        {
            bool humanWon = result.Winners != null &&
                            result.Winners.Any(p => p != null && p.PlayerType == PlayerType.HumanPlayer);
            bool isRoguelikeBattle = HasRoguelikeBattleContext();

            if (humanWon)
            {
                var allUnits = BattleController.Instance?.GetUnits();
                int totalRounds = BattleController.Instance?.CurrentRound ?? 1;

                // Show recovery before settlement so players can see the actual persistent gains.
                var battleUi = Object.FindFirstObjectByType<BattleUIController>();
                if (battleUi != null)
                    await battleUi.ShowPostBattleRecoveryAsync(allUnits);
                else if (allUnits != null)
                    ApplyPostBattleRegeneration(allUnits);

                // 加载玩家状态供结算流程使用
                var state = PlayerAdventureStateStore.LoadRepairAndSave();

                SyncPartyStateFromBattleUnits(allUnits, state);

                // 注册 BattleSettlementFlow 来管理UI流程（必须在 StartSettlement 之前）
                BattleSettlementFlow.Instance.Subscribe(BattleSettlementCoordinator.Instance, state);
                if (isRoguelikeBattle)
                {
                    _pendingRoguelikeReturnResult = result;
                    _pendingRunTerminal = IsBossBattle();
                    _pendingRunEndReason = PureRunEndReason.BossVictory;
                    BattleSettlementFlow.Instance.OnFlowFinished -= OnRoguelikeSettlementFlowFinished;
                    BattleSettlementFlow.Instance.OnFlowFinished += OnRoguelikeSettlementFlowFinished;
                }

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

                        if (isRoguelikeBattle)
                        {
                            // 延迟提交地图路径（结算完成后再前进路径）
                            bool committedInMemory = RoguelikeMapRuntimeState.TryCommitPendingBattleVictory();
                            ApplyRoguelikePathAfterBattle(result);
                            if (!committedInMemory)
                                RoguelikeMapRuntimeState.MarkResumeMapOnHome();

                            // 清除事件进行中标记
                            RoguelikeEventReentryManager.ClearEventInProgress();

                            TLog.Info("[RoguelikeBattleReturnHandler] Settlement data committed. Waiting for BattleSettlementFlow to finish before leaving battle.");
                        }
                        else
                        {
                            TLog.Info("[RoguelikeBattleReturnHandler] Settlement complete in standalone battle context. Staying in current scene.");
                        }
                    }
                );
            }
            else
            {
                if (isRoguelikeBattle)
                {
                    // 失败态也经过统一结算流程
                    var allUnits = BattleController.Instance?.GetUnits();
                    int totalRounds = BattleController.Instance?.CurrentRound ?? 1;
                    var state = PlayerAdventureStateStore.LoadRepairAndSave();

                    SyncPartyStateFromBattleUnits(allUnits, state);

                    // 注册 BattleSettlementFlow 来管理UI流程
                    BattleSettlementFlow.Instance.Subscribe(BattleSettlementCoordinator.Instance, state);
                    _pendingRoguelikeReturnResult = result;
                    _pendingRunTerminal = true;
                    _pendingRunEndReason = PureRunEndReason.Defeat;
                    BattleSettlementFlow.Instance.OnFlowFinished -= OnRoguelikeSettlementFlowFinished;
                    BattleSettlementFlow.Instance.OnFlowFinished += OnRoguelikeSettlementFlowFinished;

                    BattleSettlementCoordinator.Instance.StartSettlement(
                        result,
                        totalRounds,
                        allUnits,
                        state,
                        () =>
                        {
                            // 失败态结算完成回调
                            if (state != null)
                                PlayerAdventureStateStore.Save(state);

                            RoguelikeMapRuntimeState.ClearPendingBattle();
                            RoguelikeEventReentryManager.ClearEventInProgress();
                            TLog.Info("[RoguelikeBattleReturnHandler] Defeat settlement complete. Waiting for BattleSettlementFlow to finish.");
                        }
                    );
                }
                else
                {
                    TLog.Info("[RoguelikeBattleReturnHandler] Defeat in standalone battle context. Staying in current scene.");
                }
            }
        }

        /// <summary>
        /// 战后恢复：只对存活的人类单位恢复 HP 和 MP。
        /// 已倒下单位按永久死亡口径保留 0 HP / 0 MP / downed 状态，不在此处复活。
        /// </summary>
        private static void ApplyPostBattleRegeneration(IEnumerable<IUnit> allUnits)
        {
            foreach (var unit in allUnits)
            {
                if (unit.PlayerNumber != 0)
                    continue;

                if (unit.IsDowned || unit.Health <= 0f)
                {
                    unit.Health = 0f;
                    unit.Mana = 0f;
                    continue;
                }

                float hpRegen = unit.Constitution * 2;
                float mpRegen = unit.Charisma;

                unit.Health = Mathf.Min(unit.MaxHealth, unit.Health + hpRegen);
                unit.Mana = Mathf.Min(unit.MaxMana, unit.Mana + mpRegen);

                string unitName = unit is INamedUnit named ? named.UnitName : $"Unit_{unit.UnitID}";
                TLog.Info($"[PostBattleRegen] {unitName}: HP +{hpRegen}, MP +{mpRegen}");
            }
        }

        private static void SyncPartyStateFromBattleUnits(IEnumerable<IUnit> allUnits, PlayerAdventureState state)
        {
            if (allUnits == null || state?.Roster == null)
                return;

            foreach (var unit in allUnits)
            {
                if (unit.PlayerNumber != 0)
                    continue;

                var mono = unit as MonoBehaviour;
                if (mono == null)
                    continue;

                var link = mono.GetComponent<RosterCharacterLink>();
                if (link == null)
                    continue;

                var def = state.Roster.FirstOrDefault(c => c.Id == link.CharacterId);
                if (def == null)
                    continue;

                def.CurrentHp = Mathf.RoundToInt(unit.Health);
                def.CurrentMp = Mathf.RoundToInt(unit.Mana);
                def.IsDead = unit.IsDowned;
            }

            // Death is the single transition point that returns all carried loadout
            // entries to the shared backpack. The settlement callback persists the state.
            CharacterLoadoutService.AutoUnloadDeadCharacters(state);
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

            map.RecordNodeCompletion(pending, true);

            string newJson = JsonConvert.SerializeObject(map, Formatting.Indented, MapJsonSettings);
            PlayerPrefs.SetString(Tactics.UI.RoguelikeMapUIController.MapPlayerPrefsKey, newJson);
            PlayerPrefs.DeleteKey(Tactics.UI.RoguelikeMapUIController.RoguelikePendingNodePrefsKey);
            PlayerPrefs.Save();
        }

        private static bool HasRoguelikeBattleContext()
        {
            if (RoguelikeMapRuntimeState.HasActiveRun || !string.IsNullOrEmpty(RoguelikeMapRuntimeState.PendingBattleNodeId))
                return true;

            if (PlayerPrefs.HasKey(RoguelikeMapUIController.RoguelikePendingNodePrefsKey))
                return true;

            return RoguelikeEventReentryManager.IsEventInProgress(out string eventType, out _)
                && string.Equals(eventType, "Battle", System.StringComparison.Ordinal);
        }

        /// <summary>
        /// Leaves normal victories through the map return flow and shows a run summary only
        /// for defeat or boss victory.
        /// </summary>
        private static async void OnRoguelikeSettlementFlowFinished()
        {
            BattleSettlementFlow.Instance.OnFlowFinished -= OnRoguelikeSettlementFlowFinished;

            var result = _pendingRoguelikeReturnResult;
            _pendingRoguelikeReturnResult = default;
            bool isTerminal = _pendingRunTerminal;
            PureRunEndReason endReason = _pendingRunEndReason;
            _pendingRunTerminal = false;

            if (!isTerminal)
            {
                TLog.Info("[RoguelikeBattleReturnHandler] Settlement finished. Returning to the active run map.");
                await BattleFlowCoordinator.Instance.EndBattleAsync(result);
                return;
            }

            TLog.Info($"[RoguelikeBattleReturnHandler] Settlement finished. Showing terminal run summary: {endReason}.");

            // Freeze the terminal snapshot before clearing the active session. The UI
            // owns the snapshot until the player closes the summary.
            var summary = PureRunSessionStore.Finish(endReason);

            // 显示 RunEndSummaryUIController
            await UIManager.Instance.ShowAsync(UIManager.UIId.RunEndSummary);
            var controller = FindController<RunEndSummaryUIController>(UIManager.UIId.RunEndSummary);
            if (controller != null)
            {
                controller.ShowSummary(summary, () =>
                {
                    TLog.Info("[RoguelikeBattleReturnHandler] RunEndSummary closed. Leaving battle scene now.");
                    UIManager.Instance.Hide(UIManager.UIId.RunEndSummary);
                    PureRunSessionStore.ConsumeCompletedSummary();
                    ClearTerminalRunUiAndMapState();
                    _ = BattleFlowCoordinator.Instance.EndBattleAsync(result);
                });
            }
            else
            {
                TLog.Warning("[RoguelikeBattleReturnHandler] RunEndSummaryUIController not found. Leaving battle scene directly.");
                PureRunSessionStore.ConsumeCompletedSummary();
                ClearTerminalRunUiAndMapState();
                _ = BattleFlowCoordinator.Instance.EndBattleAsync(result);
            }
        }

        private static void ClearTerminalRunUiAndMapState()
        {
            RoguelikeMapRuntimeState.ClearAll();
            RoguelikeEventReentryManager.ClearEventInProgress();
            EncounterRuntimeState.ClearPendingEncounter();
            PlayerPrefs.DeleteKey(RoguelikeMapUIController.RoguelikePendingNodePrefsKey);
            PlayerPrefs.DeleteKey(RoguelikeMapUIController.RoguelikeReturnScenePrefsKey);
            PlayerPrefs.Save();
            UIManager.Instance.Destroy(UIManager.UIId.RoguelikeMap);
        }

        /// <summary>
        /// 判断当前战斗是否是 Boss 战
        /// </summary>
        private static bool IsBossBattle()
        {
            // 检查 PendingBattleNodeId 对应的节点类型
            if (!string.IsNullOrEmpty(RoguelikeMapRuntimeState.PendingBattleNodeId) &&
                RoguelikeMapRuntimeState.CurrentMap != null)
            {
                var node = RoguelikeMapRuntimeState.CurrentMap.GetNode(RoguelikeMapRuntimeState.PendingBattleNodeId);
                if (node != null && node.nodeType == RoguelikeNodeType.Boss)
                {
                    return true;
                }
            }

            // 检查 PlayerPrefs 中的 Boss 标记
            if (PlayerPrefs.HasKey("RoguelikeBossBattle"))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 查找指定 UIId 对应的 UIController
        /// </summary>
        private static T FindController<T>(UIManager.UIId uiId) where T : UIControllerBase
        {
            var uiDoc = GetUiDocument(uiId);
            if (uiDoc != null)
            {
                var controller = uiDoc.GetComponent<T>();
                if (controller != null)
                    return controller;
            }

            var controllers = UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            if (controllers.Length > 0)
                return controllers[0];

            return null;
        }

        /// <summary>
        /// 获取指定 UIId 对应的 UIDocument
        /// </summary>
        private static UIDocument GetUiDocument(UIManager.UIId uiId)
        {
            string uiName = uiId.ToString();
            var uiDocs = UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            foreach (var doc in uiDocs)
            {
                if (doc.gameObject.name.Contains(uiName))
                    return doc;
            }
            return null;
        }
    }
}
