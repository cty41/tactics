using Tactics.RoguelikeMap.Economy;
using Tactics.Runtime.Utilities;
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
        [Header("UI Settings")]
        [SerializeField] private VisualTreeAsset treasurePanelTemplate;

        private UIDocument _uiDocument;
        private VisualElement _currentPanel;

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
        }

        /// <summary>
        /// 处理宝藏节点交互
        /// </summary>
        /// <param name="node">宝藏节点数据</param>
        public void HandleTreasureNode(RoguelikeMapNode node)
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

            // 3. 显示 UXML 奖励面板
            ShowTreasurePanel(actualGold, equipmentName);
        }

        /// <summary>
        /// 显示宝藏奖励面板
        /// </summary>
        private void ShowTreasurePanel(int goldAmount, string equipmentName)
        {
            // 清除现有面板
            ClearExistingPanel();

            if (treasurePanelTemplate == null)
            {
                TLog.Error("[TreasureNodeHandler] treasurePanelTemplate 未设置");
                return;
            }

            var root = _uiDocument?.rootVisualElement;
            if (root == null)
            {
                TLog.Error("[TreasureNodeHandler] UIDocument rootVisualElement 为空");
                return;
            }

            // 实例化 UXML 模板
            var instance = treasurePanelTemplate.Instantiate();
            root.Add(instance);
            _currentPanel = instance;

            // 设置金币数量
            var goldLabel = instance.Q<Label>("GoldAmountLabel");
            if (goldLabel != null)
                goldLabel.text = $"+{goldAmount} 金币";

            // 设置装备（如果有）
            var equipmentRow = instance.Q<VisualElement>("EquipmentRow");
            var equipmentLabel = instance.Q<Label>("EquipmentLabel");
            if (equipmentName != null)
            {
                if (equipmentRow != null)
                    equipmentRow.style.display = DisplayStyle.Flex;
                if (equipmentLabel != null)
                    equipmentLabel.text = equipmentName;
            }
            else
            {
                if (equipmentRow != null)
                    equipmentRow.style.display = DisplayStyle.None;
            }

            // 注册确认按钮
            var confirmButton = instance.Q<Button>("ConfirmButton");
            confirmButton?.RegisterCallback<ClickEvent>(OnConfirmClicked);
        }

        /// <summary>
        /// 确认按钮点击回调
        /// </summary>
        private void OnConfirmClicked(ClickEvent evt)
        {
            ClearExistingPanel();
            TLog.Info("[TreasureNodeHandler] 宝藏面板已关闭");
        }

        /// <summary>
        /// 清除现有面板
        /// </summary>
        private void ClearExistingPanel()
        {
            if (_currentPanel != null)
            {
                var confirmButton = _currentPanel.Q<Button>("ConfirmButton");
                confirmButton?.UnregisterCallback<ClickEvent>(OnConfirmClicked);

                _currentPanel.RemoveFromHierarchy();
                _currentPanel = null;
            }
        }

        private void OnDestroy()
        {
            ClearExistingPanel();
        }
    }
}
