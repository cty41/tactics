using System.Threading.Tasks;
using Tactics.Flow.Battle;
using Tactics.Roguelike;
using Tactics.RoguelikeMap.Economy;
using Tactics.RoguelikeMap.Events;
using Tactics.RoguelikeMap.UI;
using Tactics.Runtime.Utilities;
using Tactics.UI;
using UnityEngine;

namespace Tactics.RoguelikeMap.Interaction
{
    /// <summary>
    /// 节点交互管理器
    /// 负责处理不同节点类型的交互逻辑
    /// </summary>
    public class NodeInteractionManager : MonoBehaviour
    {
        public static NodeInteractionManager Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        /// <summary>
        /// 处理节点交互
        /// </summary>
        public void HandleNodeInteraction(RoguelikeMapNode node)
        {
            if (node == null)
            {
                TLog.Warning("[NodeInteractionManager] 节点为空");
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
                    HandleMysteryNode(node);
                    break;
                case RoguelikeNodeType.Treasure:
                    HandleTreasureNode(node);
                    break;
                case RoguelikeNodeType.Store:
                    HandleStoreNode(node);
                    break;
                case RoguelikeNodeType.RestSite:
                    HandleRestSiteNode(node);
                    break;
                default:
                    TLog.Warning($"[NodeInteractionManager] 未知节点类型: {node.nodeType}");
                    break;
            }
        }

        /// <summary>
        /// 处理战斗节点
        /// </summary>
        private async void HandleBattleNode(RoguelikeMapNode node)
        {
            TLog.Info($"[NodeInteractionManager] 进入战斗: {node.blueprintName}");

            // 保存当前节点ID用于战后处理
            PlayerPrefs.SetString(RoguelikeMapUIController.RoguelikePendingNodePrefsKey, node.nodeId);
            PlayerPrefs.SetString(RoguelikeMapUIController.RoguelikeReturnScenePrefsKey, "Home");
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
        private void HandleMysteryNode(RoguelikeMapNode node)
        {
            TLog.Info($"[NodeInteractionManager] 触发事件: {node.blueprintName}");

            // 获取地图配置
            var mapConfig = RoguelikeMapUIController.Instance?.mapConfig;
            if (mapConfig == null)
            {
                TLog.Warning("[NodeInteractionManager] 地图配置为空");
                return;
            }

            // 加载事件
            var eventManager = EventManager.Instance;
            eventManager.LoadRegionEvents("DarkForest", mapConfig);

            var evt = eventManager.GetRandomEvent("DarkForest");
            if (evt == null)
            {
                TLog.Warning("[NodeInteractionManager] 没有可用事件");
                return;
            }

            // 显示事件UI
            if (EventUIController.Instance != null)
            {
                EventUIController.Instance.ShowEvent(evt, (success) =>
                {
                    TLog.Info($"[NodeInteractionManager] 事件完成，结果: {success}");
                    // TODO: 处理事件结果
                });
            }
            else
            {
                TLog.Warning("[NodeInteractionManager] EventUIController未初始化");
            }
        }

        /// <summary>
        /// 处理宝藏节点
        /// </summary>
        private void HandleTreasureNode(RoguelikeMapNode node)
        {
            TLog.Info($"[NodeInteractionManager] 打开宝藏: {node.blueprintName}");

            // 委托给 TreasureNodeHandler 处理（金币奖励 + UXML 面板）
            var handler = GetComponentInChildren<TreasureNodeHandler>();
            if (handler != null)
            {
                handler.HandleTreasureNode(node);
            }
            else
            {
                TLog.Warning("[NodeInteractionManager] TreasureNodeHandler 未找到，使用回退逻辑");
                // 回退：直接增加金币
                int goldAmount = Random.Range(2, 6);
                RunGoldManager.Instance.AddGold(goldAmount);
                TLog.Info($"[NodeInteractionManager] 获得 {goldAmount} 金币");
            }
        }

        /// <summary>
        /// 处理商店节点
        /// </summary>
        private void HandleStoreNode(RoguelikeMapNode node)
        {
            TLog.Info($"[NodeInteractionManager] 进入商店: {node.blueprintName}");

            if (StoreNodeHandler.Instance != null)
            {
                StoreNodeHandler.Instance.ShowShop(node);
            }
            else
            {
                TLog.Warning("[NodeInteractionManager] StoreNodeHandler未初始化");
                ShowRewardPopup("商店功能开发中...");
            }
        }

        /// <summary>
        /// 处理休息站节点
        /// </summary>
        private void HandleRestSiteNode(RoguelikeMapNode node)
        {
            TLog.Info($"[NodeInteractionManager] 进入休息站: {node.blueprintName}");

            // 委托给 RestSiteNodeHandler 处理
            if (RestSiteNodeHandler.Instance != null)
            {
                RestSiteNodeHandler.Instance.HandleRestSiteNode(node);
            }
            else
            {
                TLog.Warning("[NodeInteractionManager] RestSiteNodeHandler 未初始化");
            }
        }

        /// <summary>
        /// 显示奖励提示
        /// </summary>
        private void ShowRewardPopup(string message)
        {
            // TODO: 实现奖励提示UI
            TLog.Info($"[NodeInteractionManager] 奖励提示: {message}");
        }
    }
}
