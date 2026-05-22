using Tactics.RoguelikeMap.Economy;
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

        /// <summary>
        /// 处理宝藏节点交互
        /// </summary>
        public async void HandleTreasureNode(RoguelikeMapNode node)
        {
            if (node == null)
            {
                TLog.Warning("[TreasureNodeHandler] 节点为空");
                return;
            }

            TLog.Info($"[TreasureNodeHandler] 打开宝藏: {node.blueprintName}");

            // 1. 随机金币奖励：2-5 金币
            int goldAmount = Random.Range(2, 6);
            int actualGold = RunGoldManager.Instance.AddGold(goldAmount);
            TLog.Info($"[TreasureNodeHandler] 获得 {actualGold} 金币");

            // 2. 20% 概率获得占位装备
            bool hasEquipment = Random.value < 0.2f;
            string equipmentName = hasEquipment ? "[铁剑] — TODO: 对接装备系统" : null;

            // 3. 通过 UIManager 显示 UXML 奖励面板
            await UIManager.Instance.ShowAsync(UIManager.UIId.TreasurePanel);
            var root = UIManager.Instance.GetRootElement(UIManager.UIId.TreasurePanel);
            if (root == null)
            {
                TLog.Error("[TreasureNodeHandler] 无法获取 TreasurePanel 根元素");
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
            var closeBtn = root.Q<Button>("CloseButton");
            if (closeBtn != null)
                closeBtn.RegisterCallback<ClickEvent>(_ => ClosePanel());
        }

        private void ClosePanel()
        {
            UIManager.Instance.Hide(UIManager.UIId.TreasurePanel);
            _currentPanel = null;
        }
    }
}
