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

            var config = node.treasureConfig;

            // 1. 随机金币奖励（从配置读取范围，默认 2-5）
            int goldMin = config?.goldMin ?? 2;
            int goldMax = config?.goldMax ?? 5;
            int goldAmount = Random.Range(goldMin, goldMax + 1); // +1 因为 Random.Range(int,int) 上限为 exclusive
            int actualGold = RunGoldManager.Instance.AddGold(goldAmount);
            TLog.Info($"[TreasureNodeHandler] 获得 {actualGold} 金币");

            // 2. 从配置的 buffEntries 按权重随机选择
            string buffResultMessage = null;
            if (config?.buffEntries != null && config.buffEntries.Count > 0)
            {
                var selectedEntry = WeightedRandom(config.buffEntries, e => e.weight);
                if (selectedEntry?.buffConfig != null)
                {
                    var buffConfig = selectedEntry.buffConfig;
                    var state = PlayerAdventureStateStore.LoadRepairAndSave();
                    if (state?.Roster != null && state.ActivePartyCharacterIds.Count > 0)
                    {
                        var activeCharacters = state.Roster
                            .Where(c => state.ActivePartyCharacterIds.Contains(c.Id))
                            .ToList();
                        if (activeCharacters.Count > 0)
                        {
                            var target = activeCharacters[Random.Range(0, activeCharacters.Count)];
                            target.AddPendingBuff(buffConfig);
                            TLog.Info($"[TreasureNodeHandler] 角色 {target.DisplayName} 获得待生效 Buff: {buffConfig.BuffName}");
                            buffResultMessage = $"{target.DisplayName} 获得 Buff：{buffConfig.BuffName}";
                            PlayerAdventureStateStore.Save(state);
                        }
                    }
                }
            }

            // 3. 从配置的 equipmentEntries 按权重随机选择
            bool hasEquipment = false;
            string equipmentName = null;
            if (config?.equipmentEntries != null && config.equipmentEntries.Count > 0)
            {
                var selectedEntry = WeightedRandom(config.equipmentEntries, e => e.weight);
                if (selectedEntry != null &&
                    RoguelikeRewardHelper.TryAddEquipmentToInventory(selectedEntry.equipmentId, out string displayName))
                {
                    hasEquipment = true;
                    equipmentName = displayName;
                }
            }

            // 4. 通过 UIManager 显示 UXML 奖励面板
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

            // 设置金币数量
            var goldLabel = root.Q<Label>("GoldAmountLabel");
            if (goldLabel != null)
                goldLabel.text = $"+{actualGold} 金币";

            // 设置装备（如果有）
            var equipmentRow = root.Q<VisualElement>("EquipmentRow");
            if (equipmentRow != null)
            {
                if (hasEquipment)
                {
                    equipmentRow.style.display = DisplayStyle.Flex;
                    var equipLabel = root.Q<Label>("EquipmentNameLabel");
                    if (equipLabel != null)
                        equipLabel.text = equipmentName;
                }
                else
                {
                    equipmentRow.style.display = DisplayStyle.None;
                }
            }

            // 绑定关闭按钮
            var closeBtn = root.Q<Button>("ConfirmButton");
            if (closeBtn != null)
                closeBtn.RegisterCallback<ClickEvent>(_ => ClosePanel());

            var buffLabel = root.Q<Label>("BuffResultLabel");
            if (buffLabel != null)
            {
                buffLabel.text = string.IsNullOrEmpty(buffResultMessage) ? string.Empty : buffResultMessage;
                buffLabel.style.display = string.IsNullOrEmpty(buffResultMessage) ? DisplayStyle.None : DisplayStyle.Flex;
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
