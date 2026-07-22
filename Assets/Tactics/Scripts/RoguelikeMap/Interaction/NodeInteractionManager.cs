using System.Collections.Generic;
using System.Threading.Tasks;
using Tactics.Flow.Battle;
using Tactics.Common.Battle;
using Tactics.Roguelike;
using Tactics.RoguelikeMap.Economy;
using Tactics.RoguelikeMap.Events;
using Tactics.RoguelikeMap.UI;
using Tactics.Roster;
using Tactics.Runtime.Utilities;
using Tactics.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tactics.RoguelikeMap.Interaction
{
    /// <summary>
    /// 节点交互管理器
    /// 负责处理不同节点类型的交互逻辑
    /// </summary>
    public class NodeInteractionManager : MonoBehaviour
    {
        public static NodeInteractionManager Instance { get; private set; }

        /// <summary>
        /// 当前 RoguelikeMap 实例，由 RoguelikeMapUIController 设置
        /// </summary>
        public global::Tactics.RoguelikeMap.RoguelikeMap CurrentMap { get; set; }

        private void Awake()
        {
            Instance = this;
            EnsureHandler<TreasureNodeHandler>();
            EnsureHandler<StoreNodeHandler>();
            EnsureHandler<RestSiteNodeHandler>();
        }

        private void EnsureHandler<T>() where T : MonoBehaviour
        {
            if (GetComponent<T>() == null)
                gameObject.AddComponent<T>();

        }

        /// <summary>
        /// 处理节点交互
        /// </summary>
        public void HandleNodeInteraction(RoguelikeMapNode node, System.Action onCompleted = null)
        {
            if (node == null)
            {
                TLog.Warning("[NodeInteractionManager] 节点为空");
                return;
            }

            // 消耗性节点已消耗则跳过
            if (node.IsConsumed && IsConsumableNodeType(node.nodeType))
            {
                TLog.Info($"[NodeInteractionManager] 节点 {node.nodeId} 已被消耗，跳过事件");
                return;
            }

            TLog.Info($"[NodeInteractionManager] 处理节点交互: {node.nodeType} - {node.blueprintName}");

            switch (node.nodeType)
            {
                case RoguelikeNodeType.MinorEnemy:
                case RoguelikeNodeType.EliteEnemy:
                case RoguelikeNodeType.Boss:
                    HandleBattleNode(node);
                    break;
                case RoguelikeNodeType.Mystery:
                    HandleMysteryNode(node, onCompleted);
                    break;
                case RoguelikeNodeType.Treasure:
                    HandleTreasureNode(node, onCompleted);
                    node.IsConsumed = true;
                    break;
                case RoguelikeNodeType.Store:
                    HandleStoreNode(node, onCompleted);
                    break;
                case RoguelikeNodeType.RestSite:
                    HandleRestSiteNode(node, onCompleted);
                    break;
                default:
                    TLog.Warning($"[NodeInteractionManager] 未知节点类型: {node.nodeType}");
                    break;
            }
        }

        /// <summary>
        /// 判断是否为消耗性节点类型
        /// </summary>
        private bool IsConsumableNodeType(RoguelikeNodeType type)
        {
            return type == RoguelikeNodeType.Mystery
                || type == RoguelikeNodeType.Treasure
                || type == RoguelikeNodeType.RestSite;
        }

        /// <summary>
        /// 处理战斗节点
        /// </summary>
        private async void HandleBattleNode(RoguelikeMapNode node)
        {
            TLog.Info($"[NodeInteractionManager] 进入战斗: {node.blueprintName}");

            RoguelikeMapRuntimeState.BeginBattleFromNode(CurrentMap, node.nodeId, "Home");

            // 保存当前节点ID用于战后处理
            PlayerPrefs.SetString(RoguelikeMapUIController.RoguelikePendingNodePrefsKey, node.nodeId);
            PlayerPrefs.SetString(RoguelikeMapUIController.RoguelikeReturnScenePrefsKey, "Home");
            EncounterRuntimeState.SetPendingEncounterPath(
                string.IsNullOrWhiteSpace(node.encounterConfigPath)
                    ? EncounterConfigLoader.GetDefaultEncounterPath(node.nodeType)
                    : node.encounterConfigPath);
            PlayerPrefs.Save();

            // 标记事件进行中（支持断线重连恢复）
            RoguelikeEventReentryManager.MarkEventInProgress("Battle", node.nodeId);

            // 获取战斗场景名
            string battleSceneName = RoguelikeMapUIController.Instance?.BattleSceneName ?? "Test1";

            // 触发战斗场景加载
            await BattleFlowCoordinator.Instance.StartBattleAsync(battleSceneName);
        }

        /// <summary>
        /// 处理神秘节点（事件）
        /// </summary>
        private async void HandleMysteryNode(RoguelikeMapNode node, System.Action onCompleted = null)
        {
            TLog.Info($"[NodeInteractionManager] 触发事件: {node.blueprintName}");
            RoguelikeNodeTransactionService.Begin(node, CurrentMap);

            // 获取地图配置
            var mapConfig = RoguelikeMapUIController.Instance?.mapConfig;
            TLog.Info($"[NodeInteractionManager] mapConfig = {mapConfig != null}");
            if (mapConfig == null)
            {
                TLog.Warning("[NodeInteractionManager] 地图配置为空");
                onCompleted?.Invoke();
                return;
            }

            var eventManager = EventManager.Instance;
            if (eventManager == null)
            {
                TLog.Warning("[NodeInteractionManager] EventManager 未初始化");
                onCompleted?.Invoke();
                return;
            }

            const string regionName = "DarkForest";
            EnsureRegionEventsLoaded(eventManager, regionName, mapConfig);

            RoguelikeEvent evt = null;
            if (!string.IsNullOrWhiteSpace(node.eventId))
                evt = eventManager.GetEvent(node.eventId);

            if (evt == null)
            {
                int eventSeed = Roguelike.RoguelikeMapRuntimeState.DeriveSeed(
                    Roguelike.RoguelikeMapRuntimeState.RunSeed,
                    $"map-event:{node.nodeId}");
                evt = eventManager.GetDeterministicEvent(regionName, eventSeed);
                if (evt != null)
                {
                    node.eventId = evt.eventId;
                    Roguelike.PureRunSessionStore.SaveMap(Roguelike.RoguelikeMapRuntimeState.CurrentMap);
                }
            }
            if (evt == null)
            {
                TLog.Warning("[NodeInteractionManager] 没有可用事件");
                onCompleted?.Invoke();
                return;
            }

            var state = PlayerAdventureStateStore.LoadRepairAndSave();
            var effectContext = RoguelikeRewardHelper.CreateActivePartyContext(state);

            // 显示事件UI（通过 UIManager 初始化 EventUIController）
            await UIManager.Instance.ShowAsync(UIManager.UIId.EventPanel);
            if (EventUIController.Instance != null)
            {
                EventUIController.Instance.ShowEvent(evt, effectContext, node, CurrentMap, committed =>
                {
                    TLog.Info($"[NodeInteractionManager] 事件界面关闭，已提交: {committed}");
                    if (!committed)
                        return;

                    onCompleted?.Invoke();
                    if (state?.Roster != null && state.Roster.Count > 0 &&
                        state.Roster.TrueForAll(character => character == null || character.IsDead))
                    {
                        ShowEventDefeatSummary(state);
                    }
                });
            }
            else
            {
                TLog.Warning("[NodeInteractionManager] EventUIController未初始化");
                onCompleted?.Invoke();
            }
        }

        /// <summary>
        /// 处理宝藏节点
        /// </summary>
        private void HandleTreasureNode(RoguelikeMapNode node, System.Action onCompleted = null)
        {
            TLog.Info($"[NodeInteractionManager] 打开宝藏: {node.blueprintName}");

            // 委托给 TreasureNodeHandler 处理（金币奖励 + UXML 面板）
            var handler = GetComponentInChildren<TreasureNodeHandler>();
            TLog.Info($"[NodeInteractionManager] TreasureNodeHandler = {handler != null}");
            if (handler != null)
            {
                handler.HandleTreasureNode(node, onCompleted);
            }
            else
            {
                TLog.Warning("[NodeInteractionManager] TreasureNodeHandler 未找到，使用回退逻辑");
                int goldAmount = Random.Range(2, 6);
                ApplyRewardResult(RewardResult.Gold(goldAmount));
                onCompleted?.Invoke();
            }
        }

        /// <summary>
        /// 处理商店节点
        /// </summary>
        private void HandleStoreNode(RoguelikeMapNode node, System.Action onCompleted = null)
        {
            TLog.Info($"[NodeInteractionManager] 进入商店: {node.blueprintName}");
            RoguelikeNodeTransactionService.Begin(node, CurrentMap);
            TLog.Info($"[NodeInteractionManager] StoreNodeHandler.Instance = {StoreNodeHandler.Instance != null}");

            if (StoreNodeHandler.Instance != null)
            {
                StoreNodeHandler.Instance.CurrentMap = CurrentMap;
                StoreNodeHandler.Instance.ShowShop(node, onCompleted);
            }
            else
            {
                TLog.Warning("[NodeInteractionManager] StoreNodeHandler未初始化");
                ShowRewardPopup("商店功能开发中...");
                onCompleted?.Invoke();
            }
        }

        /// <summary>
        /// 处理休息站节点
        /// </summary>
        private void HandleRestSiteNode(RoguelikeMapNode node, System.Action onCompleted = null)
        {
            TLog.Info($"[NodeInteractionManager] 进入休息站: {node.blueprintName}");
            RoguelikeNodeTransactionService.Begin(node, CurrentMap);
            TLog.Info($"[NodeInteractionManager] RestSiteNodeHandler.Instance = {RestSiteNodeHandler.Instance != null}");

            // 委托给 RestSiteNodeHandler 处理
            if (RestSiteNodeHandler.Instance != null)
            {
                RestSiteNodeHandler.Instance.HandleRestSiteNode(node, onCompleted);
            }
            else
            {
                TLog.Warning("[NodeInteractionManager] RestSiteNodeHandler 未初始化");
                onCompleted?.Invoke();
            }
        }

        private static void EnsureRegionEventsLoaded(EventManager eventManager, string regionName, RoguelikeMapConfig mapConfig)
        {
            if (eventManager.IsRegionLoaded(regionName))
                return;

            eventManager.LoadRegionEvents(regionName, mapConfig);
        }

        private async void ShowEventDefeatSummary(PlayerAdventureState state)
        {
            var summary = new RunSummary();
            summary.SetRunOutcome(RunOutcome.Defeat);
            summary.AddGold(state?.Gold ?? 0);
            int visitedCount = CurrentMap?.visitedNodes?.Count ?? 0;
            for (int i = 0; i < visitedCount; i++)
                summary.IncrementNodesVisited();

            await UIManager.Instance.ShowAsync(UIManager.UIId.RunEndSummary);
            var controller = UnityEngine.Object.FindFirstObjectByType<RunEndSummaryUIController>();
            if (controller == null)
            {
                TLog.Warning("[NodeInteractionManager] 事件全灭后无法显示 RunEndSummary");
                return;
            }

            controller.ShowSummary(summary, () =>
            {
                UIManager.Instance.Hide(UIManager.UIId.RunEndSummary);
                PureRunSessionStore.Finish(PureRunEndReason.Defeat);
            });
        }

        /// <summary>
        /// 显示奖励提示
        /// </summary>
        private void ShowRewardPopup(string message)
        {
            // TODO: 实现奖励提示UI
            TLog.Info($"[NodeInteractionManager] 奖励提示: {message}");
        }

        /// <summary>
        /// 统一应用节点结果到冒险状态并保存。
        /// </summary>
        public PlayerAdventureState ApplyRewardResult(RewardResult rewardResult, PlayerAdventureState state = null)
        {
            if (rewardResult == null)
                return state;

            state ??= PlayerAdventureStateStore.LoadRepairAndSave();
            if (state == null)
            {
                TLog.Warning("[NodeInteractionManager] 无法应用节点结果：玩家状态为空");
                return null;
            }

            rewardResult.ApplyToState(state);
            PlayerAdventureStateStore.Save(state);
            TLog.Info($"[NodeInteractionManager] 已统一应用节点结果: {rewardResult.GetDisplayText()}");
            return state;
        }

        /// <summary>
        /// 显示效果结果弹窗（通用）
        /// 在 TLog 中记录日志，并显示一个简单的运行时弹窗，玩家点击确认后关闭。
        /// </summary>
        /// <param name="title">弹窗标题</param>
        /// <param name="message">效果描述文本</param>
        /// <param name="onClose">关闭后的回调（可选）</param>
        public void ShowEffectResult(string title, string message, System.Action onClose = null)
        {
            TLog.Info($"[EffectResult] {title}: {message}");

            var uiDoc = UnityEngine.Object.FindFirstObjectByType<UIDocument>();
            var root = uiDoc?.rootVisualElement;
            if (root == null)
            {
                TLog.Warning("[NodeInteractionManager] 无法显示效果结果：没有活动的 UIDocument");
                onClose?.Invoke();
                return;
            }

            // 半透明遮罩
            var overlay = new VisualElement();
            overlay.style.position = Position.Absolute;
            overlay.style.left = 0;
            overlay.style.right = 0;
            overlay.style.top = 0;
            overlay.style.bottom = 0;
            overlay.style.alignItems = Align.Center;
            overlay.style.justifyContent = Justify.Center;
            overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);

            // 弹窗容器
            var box = new VisualElement();
            box.style.backgroundColor = new Color(0.12f, 0.12f, 0.14f, 0.97f);
            box.style.borderTopLeftRadius = 10;
            box.style.borderTopRightRadius = 10;
            box.style.borderBottomLeftRadius = 10;
            box.style.borderBottomRightRadius = 10;
            box.style.paddingTop = 24;
            box.style.paddingBottom = 24;
            box.style.paddingLeft = 32;
            box.style.paddingRight = 32;
            box.style.minWidth = 220;
            box.style.maxWidth = 360;
            box.style.alignItems = Align.Center;

            // 标题
            var titleLabel = new Label(title);
            titleLabel.style.fontSize = 18;
            titleLabel.style.color = new Color(1f, 0.85f, 0.4f);
            titleLabel.style.marginBottom = 12;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            box.Add(titleLabel);

            // 消息文本
            var messageLabel = new Label(message);
            messageLabel.style.fontSize = 14;
            messageLabel.style.color = new Color(0.9f, 0.9f, 0.9f);
            messageLabel.style.whiteSpace = WhiteSpace.Normal;
            messageLabel.style.marginBottom = 18;
            messageLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            box.Add(messageLabel);

            // 确认按钮
            var confirmBtn = new Button(() =>
            {
                if (overlay.parent != null)
                    overlay.parent.Remove(overlay);
                onClose?.Invoke();
            });
            confirmBtn.text = "确认";
            confirmBtn.style.width = 100;
            confirmBtn.style.height = 32;
            confirmBtn.style.fontSize = 14;
            confirmBtn.style.color = Color.white;
            confirmBtn.style.backgroundColor = new Color(0.25f, 0.55f, 0.25f, 1f);
            confirmBtn.style.borderTopLeftRadius = 6;
            confirmBtn.style.borderTopRightRadius = 6;
            confirmBtn.style.borderBottomLeftRadius = 6;
            confirmBtn.style.borderBottomRightRadius = 6;
            box.Add(confirmBtn);

            overlay.Add(box);
            root.Add(overlay);
        }
    }
}
