using System.Collections.Generic;
using Tactics.RoguelikeMap.Economy;
using Tactics.Runtime.Utilities;
using Tactics.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tactics.RoguelikeMap.Interaction
{
    /// <summary>
    /// 商店节点处理器
    /// 使用 UXML 构建 FTL 风格商店界面，玩家可浏览并购买商品
    /// </summary>
    public class StoreNodeHandler : MonoBehaviour
    {
        public static StoreNodeHandler Instance { get; private set; }

        private VisualElement _overlay;
        private Label _titleLabel;
        private Label _goldLabel;
        private VisualElement _goodsContainer;
        private Button _closeButton;

        private ShopManager _shopManager;
        private List<ShopGood> _currentGoods;
        private RoguelikeMapNode _currentNode;
        private System.Action _onClose;

        /// <summary>
        /// 当前 RoguelikeMap 实例，由调用方设置，用于持久化购买记录
        /// </summary>
        public global::Tactics.RoguelikeMap.RoguelikeMap CurrentMap { get; set; }

        private void Awake()
        {
            Instance = this;
            _shopManager = new ShopManager();
        }

        /// <summary>
        /// 显示商店界面
        /// </summary>
        public async void ShowShop(RoguelikeMapNode node, System.Action onClose = null)
        {
            if (node == null)
            {
                TLog.Warning("[StoreNodeHandler] 节点为空");
                return;
            }

            _currentNode = node;
            _onClose = onClose;

            // 通过 UIManager 显示 UI
            await UIManager.Instance.ShowAsync(UIManager.UIId.ShopPanel);
            var root = UIManager.Instance.GetRootElement(UIManager.UIId.ShopPanel);
            if (root == null)
            {
                TLog.Error("[StoreNodeHandler] 无法获取 ShopPanel 根元素");
                return;
            }

            // 缓存元素引用
            _overlay = root.Q<VisualElement>("ShopOverlay");
            _titleLabel = root.Q<Label>("ShopTitle");
            _goldLabel = root.Q<Label>("GoldDisplay");
            _goodsContainer = root.Q<VisualElement>("GoodsContainer");
            _closeButton = root.Q<Button>("CloseButton");

            // 绑定关闭按钮
            if (_closeButton != null)
                _closeButton.RegisterCallback<ClickEvent>(_ => CloseShop());

            // 生成商品
            int goodCount = Random.Range(2, 4); // 2-3 件
            _currentGoods = _shopManager.GenerateGoods(goodCount);

            // 显示商品列表
            DisplayGoods();

            // 更新金币显示
            UpdateGoldDisplay();

            TLog.Info($"[StoreNodeHandler] 显示商店，{_currentGoods.Count} 件商品");
        }

        private void DisplayGoods()
        {
            if (_goodsContainer == null) return;
            _goodsContainer.Clear();

            foreach (var good in _currentGoods)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.marginBottom = 4;

                var nameLabel = new Label(good.Name);
                nameLabel.style.flexGrow = 1;
                row.Add(nameLabel);

                var priceLabel = new Label($"{good.Price} 金");
                priceLabel.style.marginLeft = 8;
                row.Add(priceLabel);

                var buyBtn = new Button(() => BuyGood(good)) { text = "购买" };

                if (CurrentMap != null && CurrentMap.IsStoreGoodPurchased(_currentNode.nodeId, good.Name))
                {
                    buyBtn.text = "已售出";
                    buyBtn.SetEnabled(false);
                }
                else
                {
                    buyBtn.SetEnabled(RunGoldManager.Instance.HasEnoughGold(good.Price));
                }

                row.Add(buyBtn);

                _goodsContainer.Add(row);
            }
        }

        private void BuyGood(ShopGood good)
        {
            if (!RunGoldManager.Instance.HasEnoughGold(good.Price))
            {
                TLog.Info("[StoreNodeHandler] 金币不足");
                return;
            }

            RunGoldManager.Instance.SpendGold(good.Price);
            CurrentMap?.AddStorePurchase(_currentNode.nodeId, good.Name);
            _currentGoods.Remove(good);
            DisplayGoods();
            UpdateGoldDisplay();
            TLog.Info($"[StoreNodeHandler] 购买了 {good.Name}，花费 {good.Price} 金币");
        }

        private void UpdateGoldDisplay()
        {
            if (_goldLabel != null)
                _goldLabel.text = $"金币: {RunGoldManager.Instance.CurrentGold}";
        }

        private void CloseShop()
        {
            UIManager.Instance.Hide(UIManager.UIId.ShopPanel);
            _overlay = null;
            _onClose?.Invoke();
            TLog.Info("[StoreNodeHandler] 商店关闭");
        }
    }
}
