using System.Collections.Generic;
using System.Linq;
using Tactics.AssetPipeline;
using Tactics.Common.Units.Buffs;
using Tactics.RoguelikeMap.Economy;
using Tactics.Roster;
using Tactics.Runtime.Utilities;
using Tactics.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tactics.RoguelikeMap.Interaction
{
    /// <summary>
    /// 宝藏节点处理器
    /// 当玩家点击宝藏节点时，显示 UXML 奖励面板（金币 + 概率装备）
    /// </summary>
    public class TreasureNodeHandler : MonoBehaviour
    {
        private VisualElement _currentPanel;
        private System.Action _onClose;

        /// <summary>
        /// 处理宝藏节点交互
        /// </summary>
        public async void HandleTreasureNode(RoguelikeMapNode node, System.Action onClose = null)
        {
            if (node == null)
            {
                TLog.Warning("[TreasureNodeHandler] 节点为空");
                onClose?.Invoke();
                return;
            }

            TLog.Info($"[TreasureNodeHandler] 打开宝藏: {node.blueprintName}");
            _onClose = onClose;

            // 1. 计算奖励结果
            var rewardResult = CalculateTreasureReward(node);

            // 2. 应用奖励到游戏状态
            ApplyReward(rewardResult);

            // 3. 通过 UIManager 显示 UXML 奖励面板
            await UIManager.Instance.ShowAsync(UIManager.UIId.TreasurePanel);
            var root = UIManager.Instance.GetRootElement(UIManager.UIId.TreasurePanel);
            if (root == null)
            {
                TLog.Error("[TreasureNodeHandler] 无法获取 TreasurePanel 根元素");
                _onClose?.Invoke();
                _onClose = null;
                return;
            }

            _currentPanel = root;

            // 4. 使用 RewardResult 的展示文本更新 UI
            DisplayRewardResult(root, rewardResult);

            // 绑定关闭按钮
            var closeBtn = root.Q<Button>("ConfirmButton");
            if (closeBtn != null)
                closeBtn.RegisterCallback<ClickEvent>(_ => ClosePanel());
        }

        /// <summary>
        /// 计算宝藏节点的奖励结果
        /// </summary>
        private RewardResult CalculateTreasureReward(RoguelikeMapNode node)
        {
            var config = node.treasureConfig;
            var result = new RewardResult();

            // 随机金币奖励
            int goldMin = config?.goldMin ?? 2;
            int goldMax = config?.goldMax ?? 5;
            result.GoldAmount = Random.Range(goldMin, goldMax + 1);

            // 从配置的 buffEntries 按权重随机选择
            if (config?.buffEntries != null && config.buffEntries.Count > 0)
            {
                var selectedEntry = WeightedRandom(config.buffEntries, e => e.weight);
                if (selectedEntry?.buffConfig != null)
                {
                    result.Buffs.Add(selectedEntry.buffConfig);
                }
            }

            // 从配置的 equipmentEntries 按权重随机选择
            if (config?.equipmentEntries != null && config.equipmentEntries.Count > 0)
            {
                var selectedEntry = WeightedRandom(config.equipmentEntries, e => e.weight);
                if (selectedEntry != null)
                {
                    result.EquipmentIds.Add(selectedEntry.equipmentId);
                }
            }

            // 标记事件完成
            result.EventsCompleted = 1;

            return result;
        }

        /// <summary>
        /// 应用奖励到游戏状态
        /// </summary>
        private void ApplyReward(RewardResult rewardResult)
        {
            var state = PlayerAdventureStateStore.LoadRepairAndSave();

            int beforeGold = state?.Gold ?? 0;
            NodeInteractionManager.Instance?.ApplyRewardResult(rewardResult, state);

            int actualGold = (state?.Gold ?? beforeGold) - beforeGold;
            if (actualGold > 0)
                TLog.Info($"[TreasureNodeHandler] 获得 {actualGold} 金币");
        }

        /// <summary>
        /// 使用 RewardResult 的展示文本更新 UI
        /// </summary>
        private void DisplayRewardResult(VisualElement root, RewardResult rewardResult)
        {
            // 设置金币数量
            var goldLabel = root.Q<Label>("GoldAmountLabel");
            if (goldLabel != null)
                goldLabel.text = $"+{rewardResult.GoldAmount} 金币";

            // 设置装备（如果有）
            var equipmentRow = root.Q<VisualElement>("EquipmentRow");
            if (equipmentRow != null)
            {
                if (rewardResult.EquipmentIds.Count > 0)
                {
                    equipmentRow.style.display = DisplayStyle.Flex;
                    var equipLabel = root.Q<Label>("EquipmentNameLabel");
                    if (equipLabel != null)
                    {
                        // 使用第一个装备ID获取显示名称
                        var equipId = rewardResult.EquipmentIds[0];
                        var def = Tactics.Equipment.EquipmentDatabase.GetById(equipId);
                        equipLabel.text = def?.DisplayName ?? equipId;
                    }
                }
                else
                {
                    equipmentRow.style.display = DisplayStyle.None;
                }
            }

            // 设置 Buff 结果
            var buffLabel = root.Q<Label>("BuffResultLabel");
            if (buffLabel != null)
            {
                if (rewardResult.Buffs.Count > 0)
                {
                    buffLabel.text = $"获得 Buff: {string.Join(", ", rewardResult.Buffs.ConvertAll(b => b.BuffName))}";
                    buffLabel.style.display = DisplayStyle.Flex;
                }
                else
                {
                    buffLabel.style.display = DisplayStyle.None;
                }
            }
        }

        /// <summary>
        /// 按权重从列表中随机选择一个元素
        /// </summary>
        private T WeightedRandom<T>(IList<T> entries, System.Func<T, float> weightSelector)
        {
            if (entries == null || entries.Count == 0)
                return default;

            float totalWeight = 0f;
            foreach (var entry in entries)
                totalWeight += weightSelector(entry);

            if (totalWeight <= 0f)
                return entries[Random.Range(0, entries.Count)];

            float randomValue = Random.Range(0f, totalWeight);
            float accumulated = 0f;
            foreach (var entry in entries)
            {
                accumulated += weightSelector(entry);
                if (randomValue < accumulated)
                    return entry;
            }

            return entries[entries.Count - 1];
        }

        private void ClosePanel()
        {
            UIManager.Instance.Hide(UIManager.UIId.TreasurePanel);
            _currentPanel = null;
            var callback = _onClose;
            _onClose = null;
            callback?.Invoke();
        }
    }
}
